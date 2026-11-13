using MandiDost.Api.Common;
using MandiDost.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace MandiDost.Api.Services.Voice;

public interface IVoiceService
{
    Task<VoiceParseResult> ParseAsync(string text, CancellationToken ct = default);
    Task<string> TranscribeAsync(IFormFile file, string language, CancellationToken ct = default);
    bool SpeechRecognitionAvailable { get; }
}

public class VoiceService(AppDbContext db, ISpeechToText speech, IMemoryCache cache) : IVoiceService
{
    public const int MaxAudioBytes = 2 * 1024 * 1024;
    private static readonly HashSet<string> Languages = ["ur", "pa"];

    public bool SpeechRecognitionAvailable => speech.IsConfigured;

    public async Task<VoiceParseResult> ParseAsync(string text, CancellationToken ct = default)
    {
        // Categories and cities change rarely, so keep them for a few minutes instead of asking the database each time.
        var refs = await cache.GetOrCreateAsync("voice-reference", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            var cats = await db.Categories.AsNoTracking().OrderBy(c => c.SortOrder).Select(c => new CategoryRef(c.Id, c.Slug, c.Section)).ToListAsync(ct);
            var locs = await db.Locations.AsNoTracking().Select(l => new LocationRef(l.Id, l.NameUr)).ToListAsync(ct);
            return (Categories: cats, Locations: locs);
        });
        return UrduVoiceParser.Parse(text, refs!.Categories, refs.Locations);
    }

    public async Task<string> TranscribeAsync(IFormFile file, string language, CancellationToken ct = default)
    {
        if (!speech.IsConfigured)
            throw new AppException(StatusCodes.Status501NotImplemented, ErrorCodes.VoiceNotConfigured, "Voice recognition is not set up on this server.");
        if (file.Length is 0 or > MaxAudioBytes)
            throw new BadRequestException(ErrorCodes.InvalidAudio, "The recording is empty or too long. Keep it under about a minute.");

        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream((int)file.Length);
        await stream.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        // Judge the file by its first bytes, not by the name or type the phone claims.
        var ext = AudioSniffer.Detect(bytes.AsSpan(0, Math.Min(bytes.Length, 16)))
            ?? throw new BadRequestException(ErrorCodes.InvalidAudio, "This is not a voice recording the server can read.");

        var lang = Languages.Contains(language) ? language : "ur";
        return await speech.TranscribeAsync(bytes, "voice" + ext, ContentTypeFor(ext), lang, ct);
    }

    private static string ContentTypeFor(string ext) => ext switch
    {
        ".m4a" => "audio/mp4",
        ".3gp" => "audio/3gpp",
        ".webm" => "audio/webm",
        ".ogg" => "audio/ogg",
        ".aac" => "audio/aac",
        _ => "audio/mpeg",
    };
}
