namespace ToyStore.Domain.Products;

public sealed record ProductCharacter
{
    private Guid _productId;
    private Guid _characterId;

    private ProductCharacter()
    {
    }

    private ProductCharacter(Guid productId, Guid characterId)
    {
        _productId = productId;
        _characterId = characterId;
    }

    public Guid ProductId => _productId;

    public Guid CharacterId => _characterId;

    internal static ProductCharacter Create(Guid productId, Guid characterId) =>
        new(productId, characterId);
}
