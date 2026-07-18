using ToyStore.Application.Products.CreatePreOrderProduct;
using ToyStore.Application.Products.UpdateDraftPreOrderProduct;

namespace ToyStore.UnitTests.Application.Products;

public sealed class PreOrderProductCommandContractTests
{
    [Fact]
    public async Task ValidatorsReturnThaiFailuresForConditionalPreOrderFields()
    {
        var create = InvalidCreate();
        var update = new UpdateDraftPreOrderProductCommand(
            Guid.Empty, 0, create.DisplayName, create.EnglishName, create.Description,
            create.ProductCategoryId, create.BrandId, create.UniverseId, create.CharacterIds,
            create.FullPrice, create.DepositAmount, create.CloseDate,
            create.EstimatedArrivalMonth, create.EstimatedArrivalYear,
            create.TotalCapacity, create.MaxPerCustomer, create.BalancePaymentDays,
            create.Images);

        var createFailures = (await new CreatePreOrderProductValidator(new FixedTimeProvider()).ValidateAsync(
            create, TestContext.Current.CancellationToken)).Errors;
        var updateFailures = (await new UpdateDraftPreOrderProductValidator(new FixedTimeProvider()).ValidateAsync(
            update, TestContext.Current.CancellationToken)).Errors;

        Assert.Contains(createFailures, x => x.PropertyName == nameof(create.FullPrice)
            && x.ErrorMessage == "ราคาเต็มต้องมากกว่า 0 บาท");
        Assert.Contains(createFailures, x => x.PropertyName == nameof(create.CloseDate)
            && x.ErrorMessage == "กรุณาเลือกวันปิดรอบ");
        Assert.Contains(createFailures, x => x.PropertyName == nameof(create.TotalCapacity)
            && x.ErrorMessage == "จำนวนรับพรีออเดอร์ต้องมากกว่า 0");
        Assert.Contains(updateFailures, x => x.PropertyName == nameof(update.Id)
            && x.ErrorMessage == "รหัสสินค้าไม่ถูกต้อง");
        Assert.Contains(updateFailures, x => x.PropertyName == nameof(update.ExpectedVersion)
            && x.ErrorMessage == "เวอร์ชันข้อมูลสินค้าไม่ถูกต้อง");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidatorsRejectPassedBangkokCloseAndEtaBeforeCloseWithThaiFieldErrors(
        bool update)
    {
        var valid = ValidCreate() with
        {
            CloseDate = new DateOnly(2026, 7, 17),
            EstimatedArrivalMonth = 6,
            EstimatedArrivalYear = 2026,
        };
        var failures = update
            ? (await new UpdateDraftPreOrderProductValidator(new FixedTimeProvider()).ValidateAsync(
                ToUpdate(valid), TestContext.Current.CancellationToken)).Errors
            : (await new CreatePreOrderProductValidator(new FixedTimeProvider()).ValidateAsync(
                valid, TestContext.Current.CancellationToken)).Errors;

        Assert.Contains(failures, x => x.PropertyName == nameof(valid.CloseDate)
            && x.ErrorMessage == "วันปิดรอบต้องเป็นวันในอนาคต (ปิด 23:59 เวลาไทย)");
        Assert.Contains(failures, x => x.PropertyName == nameof(valid.EstimatedArrivalMonth)
            && x.ErrorMessage == "เดือนที่สินค้าคาดว่าจะมาถึงต้องไม่ก่อนเดือนปิดรอบ");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidatorsAcceptFutureBangkokCloseAndSameEtaMonth(bool update)
    {
        var valid = ValidCreate();
        var failures = update
            ? (await new UpdateDraftPreOrderProductValidator(new FixedTimeProvider()).ValidateAsync(
                ToUpdate(valid), TestContext.Current.CancellationToken)).Errors
            : (await new CreatePreOrderProductValidator(new FixedTimeProvider()).ValidateAsync(
                valid, TestContext.Current.CancellationToken)).Errors;

        Assert.DoesNotContain(failures, x => x.PropertyName == nameof(valid.CloseDate));
        Assert.DoesNotContain(failures, x => x.PropertyName == nameof(valid.EstimatedArrivalMonth));
    }

    private static CreatePreOrderProductCommand InvalidCreate() => new(
        "", "", "", Guid.Empty, Guid.Empty, Guid.Empty, [],
        0, 0, default, 0, 0, 0, 0, 0, []);

    private static CreatePreOrderProductCommand ValidCreate() => new(
        "สินค้า", "Product", "รายละเอียด", ToyStore.Domain.Catalog.CatalogSeedIds.ArtToyCategory,
        Guid.NewGuid(), Guid.NewGuid(), [], 1000, 200,
        new DateOnly(2026, 8, 1), 8, 2026, 10, 2, 7, []);

    private static UpdateDraftPreOrderProductCommand ToUpdate(CreatePreOrderProductCommand command) => new(
        Guid.NewGuid(), 1, command.DisplayName, command.EnglishName, command.Description,
        command.ProductCategoryId, command.BrandId, command.UniverseId, command.CharacterIds,
        command.FullPrice, command.DepositAmount, command.CloseDate,
        command.EstimatedArrivalMonth, command.EstimatedArrivalYear,
        command.TotalCapacity, command.MaxPerCustomer, command.BalancePaymentDays, command.Images);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 7, 17, 17, 0, 0, TimeSpan.Zero);
    }
}
