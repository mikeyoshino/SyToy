using System.Net.Http.Json;
using System.Text.Json;

namespace ToyStore.Infrastructure.Notifications;

internal sealed class TelegramBotClient : IDisposable
{
    private readonly HttpClient httpClient;

    public TelegramBotClient()
        : this(new HttpClient
        {
            BaseAddress = new Uri("https://api.telegram.org"),
            Timeout = TimeSpan.FromSeconds(10),
        })
    {
    }

    internal TelegramBotClient(HttpClient httpClient) => this.httpClient = httpClient;

    public async Task<TelegramSendResult> SendMessageAsync(
        string botToken,
        string chatId,
        string text,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"/bot{botToken}/sendMessage",
            new { chat_id = chatId, text, disable_web_page_preview = true },
            cancellationToken);

        try
        {
            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
            var root = json.RootElement;
            var ok = response.IsSuccessStatusCode
                && root.TryGetProperty("ok", out var okElement)
                && okElement.ValueKind == JsonValueKind.True;
            if (ok
                && root.TryGetProperty("result", out var result)
                && result.TryGetProperty("message_id", out var messageId))
                return new(true, $"telegram-message:{messageId.GetInt64()}");

            var errorCode = root.TryGetProperty("error_code", out var code)
                ? code.GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture)
                : ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return new(false, $"telegram-error:{errorCode}");
        }
        catch (JsonException)
        {
            return new(false, $"telegram-invalid-response:{(int)response.StatusCode}");
        }
    }

    public void Dispose() => httpClient.Dispose();
}

internal sealed record TelegramSendResult(bool IsSuccess, string SafeProviderResponse);
