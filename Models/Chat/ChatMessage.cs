namespace PortfolioChat.Models.Chat;

public interface IChatMessage
{
    public string MessageKey { get; set; }
    public string UserKey { get; set; }
    public string Content { get; set; }
    public long Timestamp { get; set; }
}