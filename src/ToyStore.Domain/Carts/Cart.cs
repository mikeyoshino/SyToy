using ToyStore.Domain.Products;

namespace ToyStore.Domain.Carts;

public sealed class Cart
{
    private readonly List<CartItem> _items = [];

    private Cart()
    {
        CustomerId = null!;
        Version = 1;
    }

    private Cart(Guid id, string customerId, DateTimeOffset createdAtUtc)
    {
        Id = id;
        CustomerId = customerId;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        Version = 1;
    }

    public Guid Id { get; private set; }

    public string CustomerId { get; private set; }

    public IReadOnlyList<CartItem> Items => _items.AsReadOnly();

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public long Version { get; private set; }

    public static Cart Create(Guid id, string customerId, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new CartRuleException(CartRule.CartIdentityRequired);
        }

        var preparedCustomerId = PrepareCustomerIdentity(customerId);
        CartRules.EnsureUtc(createdAtUtc);
        return new Cart(id, preparedCustomerId, createdAtUtc);
    }

    public void EnsureOwnedBy(string customerId)
    {
        var preparedCustomerId = PrepareCustomerIdentity(customerId);
        if (!string.Equals(CustomerId, preparedCustomerId, StringComparison.Ordinal))
        {
            throw new CartRuleException(CartRule.CartOwnershipMismatch);
        }
    }

    public void Add(
        Product product,
        int quantity,
        long expectedVersion,
        DateTimeOffset changedAtUtc)
    {
        if (product is null)
        {
            throw new CartRuleException(CartRule.ProductRequired);
        }

        if (product.Status != ProductStatus.Published || product.SaleType != SaleType.InStock)
        {
            throw new CartRuleException(CartRule.ProductMustBePublishedInStock);
        }

        CartRules.EnsureQuantity(quantity);
        var nextVersion = PrepareMutation(expectedVersion, changedAtUtc);
        var item = _items.SingleOrDefault(current => current.ProductId == product.Id);
        if (item is null)
        {
            _items.Add(new CartItem(product.Id, quantity));
        }
        else
        {
            int combined;
            try
            {
                combined = checked(item.Quantity + quantity);
            }
            catch (OverflowException)
            {
                throw new CartRuleException(CartRule.QuantityExceedsLimit);
            }

            CartRules.EnsureQuantity(combined);
            item.SetQuantity(combined);
        }

        CompleteMutation(nextVersion, changedAtUtc);
    }

    public void SetQuantity(
        Guid productId,
        int quantity,
        long expectedVersion,
        DateTimeOffset changedAtUtc)
    {
        CartRules.EnsureProductIdentity(productId);
        CartRules.EnsureQuantity(quantity);
        var nextVersion = PrepareMutation(expectedVersion, changedAtUtc);
        var item = _items.SingleOrDefault(current => current.ProductId == productId)
            ?? throw new CartRuleException(CartRule.CartItemNotFound);
        item.SetQuantity(quantity);
        CompleteMutation(nextVersion, changedAtUtc);
    }

    public void Remove(Guid productId, long expectedVersion, DateTimeOffset changedAtUtc)
    {
        CartRules.EnsureProductIdentity(productId);
        var nextVersion = PrepareMutation(expectedVersion, changedAtUtc);
        var item = _items.SingleOrDefault(current => current.ProductId == productId)
            ?? throw new CartRuleException(CartRule.CartItemNotFound);
        _items.Remove(item);
        CompleteMutation(nextVersion, changedAtUtc);
    }

    public void Clear(long expectedVersion, DateTimeOffset changedAtUtc)
    {
        var nextVersion = PrepareMutation(expectedVersion, changedAtUtc);
        if (_items.Count == 0)
        {
            return;
        }

        _items.Clear();
        CompleteMutation(nextVersion, changedAtUtc);
    }

    private long PrepareMutation(long expectedVersion, DateTimeOffset changedAtUtc)
    {
        if (expectedVersion != Version)
        {
            throw new CartRuleException(CartRule.ConcurrencyVersionMismatch);
        }

        CartRules.EnsureUtc(changedAtUtc);
        if (changedAtUtc < UpdatedAtUtc)
        {
            throw new CartRuleException(CartRule.AuditTimeWentBackwards);
        }

        try
        {
            return checked(Version + 1);
        }
        catch (OverflowException)
        {
            throw new CartRuleException(CartRule.ConcurrencyVersionExhausted);
        }
    }

    private void CompleteMutation(long nextVersion, DateTimeOffset changedAtUtc)
    {
        Version = nextVersion;
        UpdatedAtUtc = changedAtUtc;
    }

    private static string PrepareCustomerIdentity(string customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new CartRuleException(CartRule.CustomerIdentityRequired);
        }

        var prepared = customerId.Trim();
        if (prepared.Length > CartLimits.CustomerIdentityLength)
        {
            throw new CartRuleException(CartRule.CustomerIdentityTooLong);
        }

        return prepared;
    }
}
