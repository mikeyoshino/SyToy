using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ToyStore.Application;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Application.Products;
using ToyStore.Application.Products.CreateInStockProduct;
using ToyStore.Application.Products.UpdateDraftInStockProduct;
using ToyStore.Domain.Catalog;

namespace ToyStore.UnitTests.Application.Products;

public sealed class InStockProductCommandContractTests
{
    [Fact]
    public async Task CreateValidatorReturnsThaiFailuresForEveryAuthoritativeField()
    {
        var validator = new CreateInStockProductValidator();
        var repeatedCharacter = Guid.NewGuid();
        var command = new CreateInStockProductCommand(
            " ",
            "ภาษาไทย",
            " ",
            Guid.NewGuid(),
            Guid.Empty,
            Guid.Empty,
            [Guid.Empty, repeatedCharacter, repeatedCharacter],
            0,
            -1,
            [new RetainedProductMediaSlot(Guid.Empty)]);

        var result = await validator.ValidateAsync(
            command,
            TestContext.Current.CancellationToken);

        AssertFailure(result.Errors, nameof(command.DisplayName), "กรุณากรอกชื่อสินค้า");
        AssertFailure(
            result.Errors,
            nameof(command.EnglishName),
            "ชื่อภาษาอังกฤษต้องสร้างส่วน URL ได้ด้วยตัวอักษรอังกฤษหรือตัวเลข");
        AssertFailure(result.Errors, nameof(command.Description), "กรุณากรอกคำอธิบายสินค้า");
        AssertFailure(result.Errors, nameof(command.ProductCategoryId), "กรุณาเลือกหมวดหมู่ Art Toy หรือ Gundam");
        AssertFailure(result.Errors, nameof(command.BrandId), "กรุณาเลือกแบรนด์");
        AssertFailure(result.Errors, nameof(command.UniverseId), "กรุณาเลือกจักรวาล");
        Assert.Contains(result.Errors, failure =>
            failure.PropertyName == nameof(command.CharacterIds)
            && failure.ErrorMessage.Contains("ตัวละคร", StringComparison.Ordinal));
        AssertFailure(result.Errors, nameof(command.Price), "ราคาสินค้าต้องมากกว่า 0 บาท");
        AssertFailure(result.Errors, nameof(command.InitialStock), "สต็อกเริ่มต้นต้องไม่ติดลบ");
        AssertFailure(result.Errors, nameof(command.Images), "การสร้างสินค้าใหม่รับได้เฉพาะรูปภาพที่อัปโหลดใหม่");
    }

    [Fact]
    public async Task UpdateValidatorRequiresIdentityVersionAndRejectsInvalidCombinedMediaPlan()
    {
        var validator = new UpdateDraftInStockProductValidator();
        var retainedId = Guid.NewGuid();
        var upload = Upload();
        ProductMediaPlanSlot[] images =
        [
            new RetainedProductMediaSlot(retainedId),
            new RetainedProductMediaSlot(retainedId),
            new UploadProductMediaSlot(upload),
            new UploadProductMediaSlot(upload),
            new UploadProductMediaSlot(Upload()),
            new UploadProductMediaSlot(Upload()),
            new UploadProductMediaSlot(Upload()),
            new UploadProductMediaSlot(Upload()),
            new UploadProductMediaSlot(Upload()),
        ];

        var result = await validator.ValidateAsync(
            new UpdateDraftInStockProductCommand(
                Guid.Empty,
                0,
                "สินค้า",
                "Product",
                "รายละเอียด",
                CatalogSeedIds.ArtToyCategory,
                Guid.NewGuid(),
                Guid.NewGuid(),
                [],
                100,
                images),
            TestContext.Current.CancellationToken);

        AssertFailure(result.Errors, nameof(UpdateDraftInStockProductCommand.Id), "รหัสสินค้าไม่ถูกต้อง");
        AssertFailure(result.Errors, nameof(UpdateDraftInStockProductCommand.ExpectedVersion), "เวอร์ชันข้อมูลสินค้าไม่ถูกต้อง");
        Assert.Contains(result.Errors, failure =>
            failure.PropertyName == nameof(UpdateDraftInStockProductCommand.Images)
            && failure.ErrorMessage.Contains("ไม่เกิน 8", StringComparison.Ordinal));
        Assert.Contains(result.Errors, failure =>
            failure.PropertyName == nameof(UpdateDraftInStockProductCommand.Images)
            && failure.ErrorMessage.Contains("ซ้ำ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false, false, "Authorization.Unauthorized")]
    [InlineData(true, false, "Authorization.Forbidden")]
    public async Task AuthorizationStopsBeforeValidationAndHandlerSideEffects(
        bool authenticated,
        bool authorized,
        string expectedCode)
    {
        var command = ValidCreate();
        var validator = new CountingValidator<CreateInStockProductCommand>();
        var validation = new ValidationBehavior<
            CreateInStockProductCommand,
            Result<ProductMutationResult>>([validator]);
        var authorization = new AuthorizationBehavior<
            CreateInStockProductCommand,
            Result<ProductMutationResult>>(
                new StubAuthorization(authenticated, authorized));
        var handlerCalled = false;

        var result = await authorization.Handle(
            command,
            cancellationToken => validation.Handle(
                command,
                _ =>
                {
                    handlerCalled = true;
                    return Task.FromResult(Result<ProductMutationResult>.Failure(ProductErrors.InvalidInput));
                },
                cancellationToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Equal(0, validator.CallCount);
        Assert.False(handlerCalled);
        Assert.Null(command.AuthorizedActorId);
    }

    [Fact]
    public void CommandsRequireProductPolicyAndMapOnlyProductPersistenceFailures()
    {
        var create = ValidCreate();
        var update = new UpdateDraftInStockProductCommand(
            Guid.NewGuid(),
            1,
            create.DisplayName,
            create.EnglishName,
            create.Description,
            create.ProductCategoryId,
            create.BrandId,
            create.UniverseId,
            create.CharacterIds,
            create.Price,
            []);

        Assert.Equal(PolicyNames.CanManageProducts, create.RequiredPolicy);
        Assert.Equal(PolicyNames.CanManageProducts, update.RequiredPolicy);
        var request = Assert.IsAssignableFrom<
            IPersistenceFailureResultRequest<Result<ProductMutationResult>>>(create);
        Assert.Equal(
            ProductErrors.DuplicateDisplayName,
            request.MapPersistenceFailure(new PersistenceFailure(
                PersistenceFailureTarget.Product,
                PersistenceFailureKind.DuplicateDisplayName)));
        Assert.Equal(
            ProductErrors.DuplicateEnglishName,
            request.MapPersistenceFailure(new PersistenceFailure(
                PersistenceFailureTarget.Product,
                PersistenceFailureKind.DuplicateEnglishName)));
        Assert.Equal(
            ProductErrors.StaleVersion,
            request.MapPersistenceFailure(new PersistenceFailure(
                PersistenceFailureTarget.Request,
                PersistenceFailureKind.ConcurrencyConflict)));
        Assert.Null(request.MapPersistenceFailure(new PersistenceFailure(
            PersistenceFailureTarget.Brand,
            PersistenceFailureKind.DuplicateDisplayName)));
    }

    [Fact]
    public void AddApplicationRegistersProductCoordinatorAndBothHandlers()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ProductMediaMutationCoordinator)
            && descriptor.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IValidator<CreateInStockProductCommand>));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IValidator<UpdateDraftInStockProductCommand>));
    }

    private static CreateInStockProductCommand ValidCreate() => new(
        "สินค้า",
        "Product",
        "รายละเอียด",
        CatalogSeedIds.GundamCategory,
        Guid.NewGuid(),
        Guid.NewGuid(),
        [],
        100,
        0,
        []);

    private static MediaUpload Upload() =>
        new(new MemoryStream([0xff, 0xd8, 0xff]), "image/jpeg");

    private static void AssertFailure(
        IEnumerable<FluentValidation.Results.ValidationFailure> failures,
        string propertyName,
        string message) => Assert.Contains(failures, failure =>
            failure.PropertyName == propertyName && failure.ErrorMessage == message);

    private sealed class CountingValidator<T> : AbstractValidator<T>
    {
        public int CallCount { get; private set; }

        public override Task<FluentValidation.Results.ValidationResult> ValidateAsync(
            ValidationContext<T> context,
            CancellationToken cancellation = default)
        {
            CallCount++;
            return base.ValidateAsync(context, cancellation);
        }
    }

    private sealed class StubAuthorization(bool authenticated, bool authorized)
        : ICurrentUserAuthorization
    {
        public Task<CurrentUserAuthorizationResult> AuthorizeAsync(
            string policy,
            CancellationToken cancellationToken) => Task.FromResult(
                new CurrentUserAuthorizationResult(
                    authenticated,
                    authorized,
                    authorized ? "admin-1" : null));
    }
}
