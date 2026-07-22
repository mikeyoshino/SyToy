using System.Net;
using System.Text;
using System.Text.Json;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure.Notifications;

namespace ToyStore.UnitTests.Infrastructure.Notifications;

public sealed class TelegramNotificationTests
{
    [Fact]
    public async Task BotClientPostsOnlyExpectedSendMessagePayload()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK,
            "{\"ok\":true,\"result\":{\"message_id\":42}}");
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.telegram.org"),
        };
        using var client = new TelegramBotClient(httpClient);

        var result = await client.SendMessageAsync(
            "123456:secret_token", "-100123", "ข้อความทดสอบ",
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("telegram-message:42", result.SafeProviderResponse);
        Assert.Equal("/bot123456:secret_token/sendMessage", handler.RequestUri?.AbsolutePath);
        Assert.Contains("\"chat_id\":\"-100123\"", handler.Body, StringComparison.Ordinal);
        using var payload = JsonDocument.Parse(handler.Body);
        Assert.Equal("ข้อความทดสอบ", payload.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public void MessageContainsSafeOrderFactsAndAdminLinkOnly()
    {
        var message = TelegramOrderPlacedNotificationDispatcher.BuildMessage(
            "SY-20260722-ABC", SaleType.PreOrder, 1250m, "https://sytoys.shop");

        Assert.Contains("SY-20260722-ABC", message, StringComparison.Ordinal);
        Assert.Contains("พรีออเดอร์", message, StringComparison.Ordinal);
        Assert.Contains("฿1,250.00 THB", message, StringComparison.Ordinal);
        Assert.Contains("https://sytoys.shop/admin/orders/SY-20260722-ABC", message,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ที่อยู่", message, StringComparison.Ordinal);
        Assert.DoesNotContain("โทร", message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnabledOptionsRequireSafeCompleteConfiguration()
    {
        var validator = new TelegramNotificationOptionsValidator();

        Assert.True(validator.Validate(null, new TelegramNotificationOptions()).Succeeded);
        Assert.True(validator.Validate(null, new TelegramNotificationOptions
        {
            Enabled = true,
            BotToken = "123456:secret_token",
            ChatId = "-100123",
            AdminBaseUrl = "https://sytoys.shop",
        }).Succeeded);
        Assert.False(validator.Validate(null, new TelegramNotificationOptions
        {
            Enabled = true,
            BotToken = "bad/token",
            ChatId = "-100123",
            AdminBaseUrl = "https://sytoys.shop",
        }).Succeeded);
    }

    private sealed class RecordingHandler(HttpStatusCode statusCode, string responseBody)
        : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }
}
