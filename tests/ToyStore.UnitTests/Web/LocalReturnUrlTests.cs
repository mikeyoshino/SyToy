using ToyStore.Web.Components.Account;

namespace ToyStore.UnitTests.Web;

public sealed class LocalReturnUrlTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://evil.example/account")]
    [InlineData("//evil.example/account")]
    [InlineData("\\\\evil.example\\account")]
    [InlineData("/Account\\Login")]
    [InlineData("/%ZZ")]
    public void UnsafeOrMalformedValuesFallBackToHome(string? value)
    {
        Assert.Equal("/", LocalReturnUrl.Normalize(value));
    }

    [Theory]
    [InlineData("/products?page=2", "/products?page=2")]
    [InlineData("Account/Login", "/Account/Login")]
    [InlineData("~/Account/Login", "/Account/Login")]
    public void LocalValuesAreNormalized(string value, string expected)
    {
        Assert.Equal(expected, LocalReturnUrl.Normalize(value));
    }
}
