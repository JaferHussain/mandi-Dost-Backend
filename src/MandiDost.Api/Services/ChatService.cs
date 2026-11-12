using MandiDost.Api.Common;
using MandiDost.Api.Data;
using MandiDost.Api.DTOs;
using MandiDost.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace MandiDost.Api.Services;

public interface IChatService
{
    Task<ChatSummaryDto> StartAsync(int userId, StartChatInput input, CancellationToken ct = default);
    Task<IReadOnlyList<ChatSummaryDto>> ListAsync(int userId, CancellationToken ct = default);
    Task<ChatDetailDto> GetAsync(int userId, int chatId, long? afterId, long? beforeId, CancellationToken ct = default);
    Task<MessageDto> SendTextAsync(int userId, int chatId, string text, CancellationToken ct = default);
    Task<MessageDto> SendVoiceAsync(int userId, int chatId, IFormFile file, int durationSeconds, CancellationToken ct = default);
    Task<int> UnreadCountAsync(int userId, CancellationToken ct = default);
}

/// <summary>
/// Two-person conversations, optionally about one listing. The app polls for new messages
/// (GET with afterId) while a chat is open – works on any hosting, no websockets needed.
/// </summary>
public class ChatService(AppDbContext db, IMediaStorage media, TimeProvider clock) : IChatService
{
    public const long MaxVoiceBytes = 2 * 1024 * 1024;
    public const int MaxVoiceSeconds = 180;
    private const int PageSize = 50;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<ChatSummaryDto> StartAsync(int userId, StartChatInput input, CancellationToken ct = default)
    {
        int otherId;
        int? listingId = null;
        if (input.ListingId is { } lid)
        {
            var listing = await db.Listings.Where(l => l.Id == lid && l.Status != ListingStatus.Removed)
                .Select(l => new { l.SellerId }).FirstOrDefaultAsync(ct)
                ?? throw new NotFoundException(ErrorCodes.ListingNotFound, "Listing not found.");
            otherId = listing.SellerId;
            listingId = lid;
        }
        else if (input.UserId is { } uid)
        {
            if (!await db.Users.AnyAsync(u => u.Id == uid && u.IsActive, ct))
                throw new NotFoundException(ErrorCodes.UserNotFound, "User not found.");
            otherId = uid;
        }
        else throw new BadRequestException(ErrorCodes.Validation, "Give a listing or a user to talk to.");

        if (otherId == userId) throw new BadRequestException(ErrorCodes.ChatWithSelf, "You can't chat with yourself.");

        // One conversation per pair of people per listing (either direction).
        var chat = await db.Chats.FirstOrDefaultAsync(c =>
            c.ListingId == listingId &&
            ((c.BuyerId == userId && c.SellerId == otherId) || (c.BuyerId == otherId && c.SellerId == userId)), ct);
        if (chat is null)
        {
            var now = Now;
            chat = new Chat { BuyerId = userId, SellerId = otherId, ListingId = listingId, CreatedAtUtc = now, LastMessageAtUtc = now };
            db.Chats.Add(chat);
            await db.SaveChangesAsync(ct);
        }
        return (await SummariesAsync(userId, db.Chats.Where(c => c.Id == chat.Id), ct)).Single();
    }

    public async Task<IReadOnlyList<ChatSummaryDto>> ListAsync(int userId, CancellationToken ct = default) =>
        await SummariesAsync(userId,
            db.Chats.Where(c => (c.BuyerId == userId || c.SellerId == userId) && c.Messages.Any())
                .OrderByDescending(c => c.LastMessageAtUtc).Take(100), ct);

    public async Task<ChatDetailDto> GetAsync(int userId, int chatId, long? afterId, long? beforeId, CancellationToken ct = default)
    {
        await LoadMineAsync(userId, chatId, ct);

        var q = db.Messages.AsNoTracking().Where(m => m.ChatId == chatId);
        List<Message> messages;
        bool hasOlder;
        if (afterId is { } after)
        {
            // Polling: only what's new since the last message the app has
            messages = await q.Where(m => m.Id > after).OrderBy(m => m.Id).Take(PageSize).ToListAsync(ct);
            hasOlder = false;
        }
        else
        {
            if (beforeId is { } before) q = q.Where(m => m.Id < before);
            var newestFirst = await q.OrderByDescending(m => m.Id).Take(PageSize + 1).ToListAsync(ct);
            hasOlder = newestFirst.Count > PageSize;
            messages = newestFirst.Take(PageSize).OrderBy(m => m.Id).ToList();
        }

        // Opening the chat marks the other person's messages as read.
        await db.Messages.Where(m => m.ChatId == chatId && m.SenderId != userId && m.ReadAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.ReadAtUtc, Now), ct);

        var summary = (await SummariesAsync(userId, db.Chats.Where(c => c.Id == chatId), ct)).Single();
        return new ChatDetailDto(summary, messages.Select(m => ToDto(m, userId)).ToList(), hasOlder);
    }

    public async Task<MessageDto> SendTextAsync(int userId, int chatId, string text, CancellationToken ct = default)
    {
        var chat = await LoadMineAsync(userId, chatId, ct);
        var msg = new Message { ChatId = chatId, SenderId = userId, Type = MessageType.Text, Text = text.Trim(), CreatedAtUtc = Now };
        db.Messages.Add(msg);
        chat.LastMessageAtUtc = msg.CreatedAtUtc;
        await db.SaveChangesAsync(ct);
        return ToDto(msg, userId);
    }

    public async Task<MessageDto> SendVoiceAsync(int userId, int chatId, IFormFile file, int durationSeconds, CancellationToken ct = default)
    {
        var chat = await LoadMineAsync(userId, chatId, ct);
        if (file.Length == 0 || file.Length > MaxVoiceBytes)
            throw new BadRequestException(ErrorCodes.InvalidAudio, "Voice message must be smaller than 2 MB.");
        if (durationSeconds is < 1 or > MaxVoiceSeconds)
            throw new BadRequestException(ErrorCodes.InvalidAudio, "Voice message must be 1 second to 3 minutes.");

        await using var stream = file.OpenReadStream();
        var head = new byte[12];
        var read = await stream.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, ct);
        var ext = AudioSniffer.Detect(head.AsSpan(0, read))
            ?? throw new BadRequestException(ErrorCodes.InvalidAudio, "Unsupported voice message format.");
        stream.Position = 0;

        // Random file name: the URL can't be guessed by people outside the chat.
        var path = $"voice/{chatId}/{Guid.NewGuid():N}{ext}";
        await media.SaveAsync(path, stream, ct);

        var msg = new Message
        {
            ChatId = chatId, SenderId = userId, Type = MessageType.Voice, MediaUrl = path,
            DurationSeconds = durationSeconds, CreatedAtUtc = Now,
        };
        db.Messages.Add(msg);
        chat.LastMessageAtUtc = msg.CreatedAtUtc;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            media.Delete(path);
            throw;
        }
        return ToDto(msg, userId);
    }

    public Task<int> UnreadCountAsync(int userId, CancellationToken ct = default) =>
        db.Messages.CountAsync(m => (m.Chat.BuyerId == userId || m.Chat.SellerId == userId) && m.SenderId != userId && m.ReadAtUtc == null, ct);

    // ---- Helpers ----

    private async Task<Chat> LoadMineAsync(int userId, int chatId, CancellationToken ct)
    {
        var chat = await db.Chats.FirstOrDefaultAsync(c => c.Id == chatId, ct);
        // Same answer for "doesn't exist" and "not yours", so chat ids can't be probed.
        if (chat is null || (chat.BuyerId != userId && chat.SellerId != userId))
            throw new NotFoundException(ErrorCodes.ChatNotFound, "Chat not found.");
        return chat;
    }

    private static MessageDto ToDto(Message m, int userId) => new(
        m.Id, m.SenderId, m.SenderId == userId, m.Type, m.Text,
        m.MediaUrl is null ? null : LocalMediaStorage.UrlFor(m.MediaUrl), m.DurationSeconds, m.CreatedAtUtc, m.ReadAtUtc);

    private async Task<List<ChatSummaryDto>> SummariesAsync(int userId, IQueryable<Chat> chats, CancellationToken ct)
    {
        var rows = await chats.AsNoTracking().Select(c => new
        {
            c.Id, c.ListingId, c.LastMessageAtUtc,
            Other = c.BuyerId == userId ? c.Seller : c.Buyer,
            ListingTitle = c.Listing != null ? c.Listing.Title : null,
            Thumb = c.Listing != null ? c.Listing.Media.OrderBy(m => m.SortOrder).Select(m => m.Path).FirstOrDefault() : null,
            Last = c.Messages.OrderByDescending(m => m.Id).Select(m => new { m.Type, m.Text, m.SenderId }).FirstOrDefault(),
            Unread = c.Messages.Count(m => m.SenderId != userId && m.ReadAtUtc == null),
        }).ToListAsync(ct);

        var otherIds = rows.Select(r => r.Other.Id).ToList();
        var businesses = await db.BusinessProfiles.AsNoTracking().Where(b => otherIds.Contains(b.UserId))
            .ToDictionaryAsync(b => b.UserId, b => b.BusinessName, ct);

        return rows.Select(r => new ChatSummaryDto(
            r.Id,
            new ChatPartyDto(r.Other.Id, r.Other.FullName, r.Other.PhoneVerified, businesses.GetValueOrDefault(r.Other.Id)),
            r.ListingId, r.ListingTitle, r.Thumb is null ? null : LocalMediaStorage.UrlFor(r.Thumb),
            r.Last?.Type == MessageType.Text ? Truncate(r.Last.Text) : null,
            r.Last?.Type, r.Last?.SenderId == userId, r.LastMessageAtUtc, r.Unread)).ToList();
    }

    private static string? Truncate(string? s) => s is null ? null : s.Length <= 80 ? s : s[..80] + "…";
}

/// <summary>Recognises the audio formats phones record (m4a/aac/3gp, webm, ogg, mp3) from the first bytes.</summary>
public static class AudioSniffer
{
    public static string? Detect(ReadOnlySpan<byte> h)
    {
        if (h.Length >= 12 && h[4..8].SequenceEqual("ftyp"u8))
            return h[8..11].SequenceEqual("3gp"u8) ? ".3gp" : ".m4a";            // MP4 container (m4a, 3gp)
        if (h.Length >= 4 && h[0] == 0x1A && h[1] == 0x45 && h[2] == 0xDF && h[3] == 0xA3) return ".webm";
        if (h.Length >= 4 && h[..4].SequenceEqual("OggS"u8)) return ".ogg";
        if (h.Length >= 3 && h[..3].SequenceEqual("ID3"u8)) return ".mp3";
        if (h.Length >= 2 && h[0] == 0xFF && (h[1] & 0xF6) == 0xF0) return ".aac"; // ADTS AAC
        if (h.Length >= 2 && h[0] == 0xFF && (h[1] & 0xE0) == 0xE0) return ".mp3"; // MPEG audio frame
        return null;
    }
}
