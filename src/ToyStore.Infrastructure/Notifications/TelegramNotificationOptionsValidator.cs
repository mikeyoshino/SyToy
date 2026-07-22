using Microsoft.Extensions.Options;

namespace ToyStore.Infrastructure.Notifications;

internal sealed class TelegramNotificationOptionsValidator
    : IValidateOptions<TelegramNotificationOptions>
{
    public ValidateOptionsResult Validate(string? name, TelegramNotificationOptions options)
    {
        if (!options.Enabled) return ValidateOptionsResult.Success;
        if (string.IsNullOrWhiteSpace(options.BotToken))
            return ValidateOptionsResult.Fail("Telegram:BotToken is required when Telegram notifications are enabled.");
        if (options.BotToken.Count(character => character == ':') != 1
            || options.BotToken.Any(character =>
                !char.IsAsciiLetterOrDigit(character)
                && character is not ':' and not '_' and not '-'))
            return ValidateOptionsResult.Fail("Telegram:BotToken has an invalid format.");
        if (string.IsNullOrWhiteSpace(options.ChatId))
            return ValidateOptionsResult.Fail("Telegram:ChatId is required when Telegram notifications are enabled.");
        if (!Uri.TryCreate(options.AdminBaseUrl, UriKind.Absolute, out var adminBaseUri)
            || adminBaseUri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(adminBaseUri.UserInfo))
            return ValidateOptionsResult.Fail("Telegram:AdminBaseUrl must be an absolute HTTPS URL without user info.");
        return ValidateOptionsResult.Success;
    }
}
