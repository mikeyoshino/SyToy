using System.Reflection;
using ToyStore.Application.Addresses;
using ToyStore.Application.Addresses.SavedAddresses;
using ToyStore.Application.Addresses.SavedAddresses.CreateSavedAddress;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Addresses;

namespace ToyStore.UnitTests.Application.Addresses;

public sealed class CreateSavedAddressTests
{
    [Fact]
    public async Task HandlerRejectsMismatchedRelationshipIdsBeforePersistence()
    {
        var store = new RecordingStore();
        var handler = new CreateSavedAddressHandler(store, new Catalog(),
            new FixedTimeProvider());
        var command = Command(districtId: 999);
        Authorize(command);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(SavedAddressErrors.AddressInvalid, result.Error);
        Assert.Null(store.Created);
    }

    [Fact]
    public async Task HandlerPersistsValidatedOwnedAddressWithRelationshipIds()
    {
        var store = new RecordingStore();
        var handler = new CreateSavedAddressHandler(store, new Catalog(),
            new FixedTimeProvider());
        var command = Command(districtId: 20);
        Authorize(command);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.NotNull(store.Created);
        Assert.Equal("customer-1", store.Created.CustomerId);
        Assert.Equal((10, 20, 30), (store.Created.ProvinceId,
            store.Created.DistrictId, store.Created.SubDistrictId));
    }

    private static CreateSavedAddressCommand Command(int districtId) => new(
        "บ้าน", "สมชาย ใจดี", "0812345678", "99 ถนนสุขุมวิท",
        10, districtId, 30, "กรุงเทพมหานคร", "คลองเตย", "คลองตัน", "10110", true);

    private static void Authorize(CreateSavedAddressCommand command)
    {
        var property = typeof(AuthorizedResultRequest<Result<SavedAddressView>>)
            .GetProperty(nameof(AuthorizedResultRequest<Result<SavedAddressView>>.AuthorizedActorId));
        property!.GetSetMethod(nonPublic: true)!.Invoke(command, ["customer-1"]);
    }

    private sealed class Catalog : IThaiAddressCatalog
    {
        public IReadOnlyList<ThaiProvince> Provinces => [new(10, "กรุงเทพมหานคร")];
        public IReadOnlyList<ThaiDistrict> GetDistricts(int provinceId) =>
            provinceId == 10 ? [new(20, "คลองเตย", 10)] : [];
        public IReadOnlyList<ThaiSubDistrict> GetSubDistricts(int districtId) =>
            districtId == 20 ? [new(30, "คลองตัน", "10110", 20)] : [];
        public bool IsValid(string province, string district, string subDistrict, string postalCode) =>
            province == "กรุงเทพมหานคร" && district == "คลองเตย"
            && subDistrict == "คลองตัน" && postalCode == "10110";
    }

    private sealed class RecordingStore : ISavedAddressStore
    {
        public SavedAddress? Created { get; private set; }
        public Task<IReadOnlyList<SavedAddressView>> ListAsync(string customerId,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<SavedAddressView>>([]);
        public Task<Result<SavedAddressView>> CreateAsync(SavedAddress address,
            CancellationToken cancellationToken)
        {
            Created = address;
            return Task.FromResult(Result<SavedAddressView>.Success(new(address.Id, address.Label,
                address.Address.RecipientName, address.Address.PhoneNumber, address.Address.AddressLine,
                address.ProvinceId, address.DistrictId, address.SubDistrictId, address.Address.Province,
                address.Address.District, address.Address.SubDistrict, address.Address.PostalCode,
                address.IsDefault)));
        }
        public Task<Result> DeleteAsync(string customerId, Guid addressId, DateTimeOffset nowUtc,
            CancellationToken cancellationToken) => Task.FromResult(Result.Success());
        public Task<Result> SetDefaultAsync(string customerId, Guid addressId, DateTimeOffset nowUtc,
            CancellationToken cancellationToken) => Task.FromResult(Result.Success());
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 7, 19, 9, 0, 0, TimeSpan.Zero);
    }
}
