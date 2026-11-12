namespace MandiDost.Api.Entities;

/// <summary>A conversation between a buyer and a seller, optionally about one listing.</summary>
public class Chat
{
    public int Id { get; set; }
    public int BuyerId { get; set; }
    public int SellerId { get; set; }
    public int? ListingId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastMessageAtUtc { get; set; }

    public User Buyer { get; set; } = null!;
    public User Seller { get; set; } = null!;
    public Listing? Listing { get; set; }
    public ICollection<Message> Messages { get; set; } = [];
}

public class Message
{
    public long Id { get; set; }
    public int ChatId { get; set; }
    public int SenderId { get; set; }
    public MessageType Type { get; set; } = MessageType.Text;
    public string? Text { get; set; }
    public string? MediaUrl { get; set; }
    public int? DurationSeconds { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }

    public Chat Chat { get; set; } = null!;
    public User Sender { get; set; } = null!;
}
