using ToyStore.Application.Cart;
using ToyStore.Application.Cart.AddCartItem;
using ToyStore.Application.Cart.ChangeCartItemQuantity;
using ToyStore.Application.Cart.ClearCart;
using ToyStore.Application.Cart.GetAnonymousCartPreview;
using ToyStore.Application.Cart.GetCart;
using ToyStore.Application.Cart.MergeAnonymousCart;
using ToyStore.Application.Cart.RemoveCartItem;
using ToyStore.Application.Common.Authorization;
using ToyStore.Domain.Carts;

namespace ToyStore.UnitTests.Application.Cart;

public sealed class CartSliceContractTests
{
    [Fact]
    public void AddValidatorRejectsUnstableIdentityQuantityAndVersionWithThaiMessages()
    {
        var result = new AddCartItemValidator().Validate(new AddCartItemCommand(
            Guid.Empty, Guid.Empty, 0, -1));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(AddCartItemCommand.OperationId));
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(AddCartItemCommand.ProductId));
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(AddCartItemCommand.Quantity));
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(AddCartItemCommand.ExpectedVersion));
        Assert.All(result.Errors, failure => Assert.False(string.IsNullOrWhiteSpace(failure.ErrorMessage)));
    }

    [Fact]
    public void AddValidatorAcceptsBoundariesAndRejectsAboveMaximum()
    {
        var validator = new AddCartItemValidator();
        Assert.True(validator.Validate(new AddCartItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), 1, 0)).IsValid);
        Assert.True(validator.Validate(new AddCartItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), CartLimits.MaximumQuantityPerItem, long.MaxValue)).IsValid);
        Assert.False(validator.Validate(new AddCartItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), CartLimits.MaximumQuantityPerItem + 1, 0)).IsValid);
    }

    [Fact]
    public void CustomerCartRequestsRequireCustomerPolicyAndNeverAcceptCustomerId()
    {
        var add = new AddCartItemCommand(Guid.NewGuid(), Guid.NewGuid(), 1, 0);
        var get = new GetCartQuery();

        Assert.Equal(PolicyNames.CanUseCustomerCart, add.RequiredPolicy);
        Assert.Equal(PolicyNames.CanUseCustomerCart, get.RequiredPolicy);
        Assert.DoesNotContain(typeof(AddCartItemCommand).GetProperties(),
            property => property.Name.Contains("Customer", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(GetCartQuery).GetProperties(),
            property => property.Name.Contains("Customer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TypedThaiErrorsSeparateUnavailableStaleOwnershipAndRetryConflict()
    {
        Assert.Equal("Cart.ProductUnavailable", CartErrors.ProductUnavailable.Code);
        Assert.Equal("Cart.StaleVersion", CartErrors.StaleVersion.Code);
        Assert.Equal("Cart.OwnershipMismatch", CartErrors.OwnershipMismatch.Code);
        Assert.Equal("Cart.OperationConflict", CartErrors.OperationConflict.Code);
        Assert.All(new[]
        {
            CartErrors.ProductUnavailable,
            CartErrors.StaleVersion,
            CartErrors.OwnershipMismatch,
            CartErrors.OperationConflict,
        }, error => Assert.Contains(error.Message, character => character >= '\u0E00' && character <= '\u0E7F'));
    }

    [Fact]
    public void EveryMutationHasAuthoritativeThaiFluentValidation()
    {
        var change = new ChangeCartItemQuantityValidator().Validate(
            new ChangeCartItemQuantityCommand(Guid.Empty, Guid.Empty, 0, 0));
        var remove = new RemoveCartItemValidator().Validate(
            new RemoveCartItemCommand(Guid.Empty, Guid.Empty, 0));
        var clear = new ClearCartValidator().Validate(new ClearCartCommand(Guid.Empty, 0));
        var merge = new MergeAnonymousCartValidator().Validate(
            new MergeAnonymousCartCommand(Guid.Empty, [new(Guid.Empty, 0)]));

        foreach (var result in new[] { change, remove, clear, merge })
        {
            Assert.False(result.IsValid);
            Assert.All(result.Errors, failure =>
                Assert.Contains(failure.ErrorMessage,
                    character => character >= '\u0E00' && character <= '\u0E7F'));
        }
    }

    [Fact]
    public void AnonymousPreviewTreatsBrowserIdentityAndQuantityAsValidatedInput()
    {
        var result = new GetAnonymousCartPreviewValidator().Validate(
            new GetAnonymousCartPreviewQuery([new(Guid.Empty, 0)]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName.EndsWith("ProductId", StringComparison.Ordinal));
        Assert.Contains(result.Errors, failure => failure.PropertyName.EndsWith("Quantity", StringComparison.Ordinal));
        Assert.All(result.Errors, failure =>
            Assert.Contains(failure.ErrorMessage,
                character => character >= '\u0E00' && character <= '\u0E7F'));
    }
}
