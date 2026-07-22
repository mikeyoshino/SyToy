namespace ToyStore.Infrastructure.Notifications;

public sealed class TelegramNotificationOptions
{
    public const string SectionName = "Telegram";

    public bool Enabled { get; set; }
    public string BotToken { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
    public string AdminBaseUrl { get; set; } = "https://sytoys.shop";
}
