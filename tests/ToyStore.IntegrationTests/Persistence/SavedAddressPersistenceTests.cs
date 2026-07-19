using Microsoft.Extensions.DependencyInjection;
using ToyStore.Application.Addresses.SavedAddresses;
using ToyStore.Domain.Addresses;
using ToyStore.Domain.Checkouts;
using ToyStore.Infrastructure.Identity;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class SavedAddressPersistenceTests(PostgreSqlFixture postgreSql)
{
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task StoreEnforcesFiveAddressLimitAndMaintainsExactlyOneDefault()
    {
        await using var factory = await StartAndResetAsync();
        var customerId = await SeedCustomerAsync(factory);
        var store = factory.Services.GetRequiredService<ISavedAddressStore>();

        for (var index = 1; index <= 5; index++)
        {
            var result = await store.CreateAsync(Create(customerId, index, makeDefault: index == 3),
                TestContext.Current.CancellationToken);
            Assert.True(result.IsSuccess);
        }
        var rejected = await store.CreateAsync(Create(customerId, 6, false),
            TestContext.Current.CancellationToken);
        Assert.True(rejected.IsFailure);
        Assert.Equal(SavedAddressErrors.LimitReached, rejected.Error);

        var addresses = await store.ListAsync(customerId, TestContext.Current.CancellationToken);
        Assert.Equal(5, addresses.Count);
        Assert.Equal("ที่อยู่ 3", Assert.Single(addresses, address => address.IsDefault).Label);

        var newDefault = addresses.Single(address => address.Label == "ที่อยู่ 5");
        Assert.True((await store.SetDefaultAsync(customerId, newDefault.Id, Now.AddHours(1),
            TestContext.Current.CancellationToken)).IsSuccess);
        addresses = await store.ListAsync(customerId, TestContext.Current.CancellationToken);
        Assert.Equal(newDefault.Id, Assert.Single(addresses, address => address.IsDefault).Id);
    }

    [Fact]
    public async Task DeleteIsOwnershipScopedAndDeletingDefaultPromotesReplacement()
    {
        await using var factory = await StartAndResetAsync();
        var ownerId = await SeedCustomerAsync(factory);
        var otherId = await SeedCustomerAsync(factory);
        var store = factory.Services.GetRequiredService<ISavedAddressStore>();
        var first = await store.CreateAsync(Create(ownerId, 1, false), TestContext.Current.CancellationToken);
        var second = await store.CreateAsync(Create(ownerId, 2, false), TestContext.Current.CancellationToken);

        var forbidden = await store.DeleteAsync(otherId, first.Value.Id, Now.AddMinutes(10),
            TestContext.Current.CancellationToken);
        Assert.True(forbidden.IsFailure);
        Assert.Equal(SavedAddressErrors.NotFound, forbidden.Error);

        Assert.True((await store.DeleteAsync(ownerId, first.Value.Id, Now.AddMinutes(11),
            TestContext.Current.CancellationToken)).IsSuccess);
        var remaining = Assert.Single(await store.ListAsync(ownerId, TestContext.Current.CancellationToken));
        Assert.Equal(second.Value.Id, remaining.Id);
        Assert.True(remaining.IsDefault);
    }

    private async Task<ToyStoreWebApplicationFactory> StartAndResetAsync()
    {
        var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        _ = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        return factory;
    }

    private static async Task<string> SeedCustomerAsync(ToyStoreWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var id = Guid.NewGuid().ToString("N");
        var email = $"{id}@example.test";
        db.Users.Add(new ApplicationUser { Id = id, UserName = email,
            NormalizedUserName = email.ToUpperInvariant(), Email = email,
            NormalizedEmail = email.ToUpperInvariant(), SecurityStamp = Guid.NewGuid().ToString("N") });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return id;
    }

    private static SavedAddress Create(string customerId, int index, bool makeDefault) =>
        SavedAddress.Create(Guid.NewGuid(), customerId, $"ที่อยู่ {index}",
            ShippingAddressSnapshot.Create("สมชาย ใจดี", "0812345678", $"{index} ถนนสุขุมวิท",
                "คลองตัน", "คลองเตย", "กรุงเทพมหานคร", "10110"),
            1, 2, 3, makeDefault, Now.AddMinutes(index));
}
