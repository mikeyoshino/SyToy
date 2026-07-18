using ToyStore.Domain.Products;

namespace ToyStore.IntegrationTests.Persistence;

internal static class ProductTestMutations
{
    public static void AddImage(
        this Product product,
        Guid imageId,
        string storageKey,
        string publicRelativeUrl,
        string altText,
        DateTimeOffset changedAtUtc,
        string actor)
    {
        var images = CurrentImages(product).Append(
            new ProductImageDefinition(imageId, storageKey, publicRelativeUrl, altText));
        Update(product, images.ToArray(), CurrentCharacters(product), changedAtUtc, actor);
    }

    public static void RemoveImage(
        this Product product,
        Guid imageId,
        DateTimeOffset changedAtUtc,
        string actor)
    {
        var images = CurrentImages(product).Where(image => image.Id != imageId).ToArray();
        Update(product, images, CurrentCharacters(product), changedAtUtc, actor);
    }

    public static void ReorderImages(
        this Product product,
        IReadOnlyCollection<Guid> imageIds,
        DateTimeOffset changedAtUtc,
        string actor)
    {
        var imagesById = CurrentImages(product).ToDictionary(image => image.Id);
        var images = imageIds.Select(imageId => imagesById[imageId]).ToArray();
        Update(product, images, CurrentCharacters(product), changedAtUtc, actor);
    }

    public static void AddCharacter(
        this Product product,
        Guid characterId,
        DateTimeOffset changedAtUtc,
        string actor)
    {
        var characters = CurrentCharacters(product).Append(characterId).ToArray();
        Update(product, CurrentImages(product), characters, changedAtUtc, actor);
    }

    public static void RemoveCharacter(
        this Product product,
        Guid characterId,
        DateTimeOffset changedAtUtc,
        string actor)
    {
        var characters = CurrentCharacters(product)
            .Where(current => current != characterId)
            .ToArray();
        Update(product, CurrentImages(product), characters, changedAtUtc, actor);
    }

    private static ProductImageDefinition[] CurrentImages(Product product) => product.Images
        .OrderBy(image => image.SortOrder)
        .Select(image => new ProductImageDefinition(
            image.Id,
            image.StorageKey,
            image.PublicRelativeUrl,
            image.AltText))
        .ToArray();

    private static Guid[] CurrentCharacters(Product product) => product.Characters
        .Select(link => link.CharacterId)
        .ToArray();

    private static void Update(
        Product product,
        IReadOnlyCollection<ProductImageDefinition> images,
        IReadOnlyCollection<Guid> characters,
        DateTimeOffset changedAtUtc,
        string actor) => product.UpdateDraftInStock(
            product.DisplayName,
            product.EnglishName,
            product.Description,
            product.Slug,
            product.ProductCategoryId,
            product.BrandId,
            product.UniverseId,
            product.InStockOffer!,
            images,
            characters,
            product.Version,
            changedAtUtc,
            actor);
}
