namespace ToyStore.IntegrationTests.Infrastructure;

public sealed class TestDatabaseSafetyTests
{
    [Theory]
    [InlineData("Host=localhost;Database=toystore", false)]
    [InlineData("Host=localhost;Database=postgres", false)]
    [InlineData("Host=localhost;Database=template1", false)]
    [InlineData("Host=localhost;Database=toystore_integration_test", true)]
    [InlineData("Host=localhost;Database=catalog_test", true)]
    [InlineData("Host=localhost;Database=toystore_testing", false)]
    public void ResetGuardOnlyAcceptsExplicitTestDatabase(
        string connectionString,
        bool expected)
    {
        var actual = PostgreSqlFixture.IsSafeTestDatabase(connectionString);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-connection-string")]
    [InlineData("Host=localhost")]
    public void ResetGuardRejectsMissingOrMalformedDatabase(string connectionString)
    {
        Assert.False(PostgreSqlFixture.IsSafeTestDatabase(connectionString));
    }
}
