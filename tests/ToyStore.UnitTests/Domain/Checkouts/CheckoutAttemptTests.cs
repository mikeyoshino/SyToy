using ToyStore.Domain.Checkouts;

namespace ToyStore.UnitTests.Domain.Checkouts;

public sealed class CheckoutAttemptTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 18, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PreOrderSnapshotCalculatesDepositTotalAndCompletesOnlyAfterProviderSession()
    {
        var checkout = Create();

        Assert.Equal(800m, checkout.PaymentAmount);
        Assert.Equal(3000m, checkout.BalanceAmount);
        Assert.Equal(CheckoutAttemptStatus.AwaitingProvider, checkout.Status);
        Assert.Throws<InvalidOperationException>(() => checkout.Complete(Now.AddMinutes(1)));

        checkout.AttachProviderSession("cs_test_1", Now.AddSeconds(1));
        checkout.Complete(Now.AddMinutes(1));

        Assert.Equal(CheckoutAttemptStatus.Completed, checkout.Status);
        Assert.Equal(Now.AddMinutes(1), checkout.CompletedAtUtc);
    }

    [Fact]
    public void ProviderSessionCannotBeReplaced()
    {
        var checkout = Create();
        checkout.AttachProviderSession("cs_test_1", Now.AddSeconds(1));

        Assert.Throws<InvalidOperationException>(() =>
            checkout.AttachProviderSession("cs_test_2", Now.AddSeconds(2)));
    }

    private static CheckoutAttempt Create() => CheckoutAttempt.CreatePreOrder(
        Guid.NewGuid(), "customer-1", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2,
        "สินค้า", "Product", "product", "ArtToy", "Brand", "Universe", "/media/product.webp",
        3400m, 400m, Now.AddDays(10), 12, 2026, 7,
        ShippingAddressSnapshot.Create("ผู้รับ", "0812345678", "1 ถนนสุขุมวิท",
            "คลองเตย", "คลองเตย", "กรุงเทพมหานคร", "10110"),
        Guid.NewGuid().ToString("N"), Now, Now.AddMinutes(32));
}
