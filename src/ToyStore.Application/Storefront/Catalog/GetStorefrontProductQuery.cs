using FluentValidation;
using MediatR;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Storefront.Catalog;

public sealed record GetStorefrontProductQuery(string Slug) : IRequest<Result<StorefrontProductDetail>>;

public static class StorefrontCatalogErrors
{
    public static readonly Error ProductNotFound = new(
        "Storefront.ProductNotFound", "ไม่พบสินค้านี้หรือสินค้าไม่ได้เปิดขายแล้ว", ErrorType.NotFound);
}

public sealed class GetStorefrontProductValidator : AbstractValidator<GetStorefrontProductQuery>
{
    public GetStorefrontProductValidator()
    {
        RuleFor(query => query.Slug).NotEmpty().WithMessage("ต้องระบุสินค้า")
            .Must(slug => !string.IsNullOrWhiteSpace(slug) && ToyStore.Domain.Catalog.CatalogSlug.IsValid(slug.Trim().ToLowerInvariant()))
            .WithMessage("ส่วน URL ของสินค้าไม่ถูกต้อง");
    }
}

public sealed class GetStorefrontProductHandler(IStorefrontCatalogReader reader, TimeProvider timeProvider)
    : IRequestHandler<GetStorefrontProductQuery, Result<StorefrontProductDetail>>
{
    public async Task<Result<StorefrontProductDetail>> Handle(GetStorefrontProductQuery request, CancellationToken cancellationToken)
    {
        var product = await reader.FindBySlugAsync(
            request.Slug.Trim().ToLowerInvariant(), timeProvider.GetUtcNow().ToUniversalTime(), cancellationToken);
        return product is null
            ? Result<StorefrontProductDetail>.Failure(StorefrontCatalogErrors.ProductNotFound)
            : Result<StorefrontProductDetail>.Success(product);
    }
}
