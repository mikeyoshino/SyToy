using Microsoft.EntityFrameworkCore;
using ToyStore.Application.Checkout;

namespace ToyStore.Infrastructure.Persistence;

internal sealed class CheckoutCustomerReader(IDbContextFactory<ApplicationDbContext> contextFactory)
    : ICheckoutCustomerReader
{
    public async Task<string?> GetEmailAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Users.AsNoTracking()
            .Where(user => user.Id == customerId)
            .Select(user => user.Email)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
