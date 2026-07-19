using Microsoft.EntityFrameworkCore;
using ToyStore.Application.Orders;
using ToyStore.Domain.Orders;

namespace ToyStore.Infrastructure.Persistence;

internal sealed class CustomerOrderReader(
    IDbContextFactory<ApplicationDbContext> contextFactory) : ICustomerOrderReader
{
    public async Task<CustomerOrderPage> ListAsync(
        string customerId,
        int page,
        int pageSize,
        string? searchTerm,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var ownedOrders = db.Orders.AsNoTracking()
            .Where(order => order.CustomerId == customerId);
        var normalizedSearch = searchTerm?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var pattern = $"%{EscapeLikePattern(normalizedSearch)}%";
            ownedOrders = ownedOrders.Where(order =>
                EF.Functions.ILike(order.Number, pattern, "\\")
                || order.Items.Any(item =>
                    EF.Functions.ILike(item.DisplayName, pattern, "\\")
                    || EF.Functions.ILike(item.EnglishName, pattern, "\\")));
        }

        var totalCount = await ownedOrders.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        var effectivePage = Math.Min(page, totalPages);
        var orders = await ownedOrders
            .Include(order => order.Items)
            .OrderByDescending(order => order.CreatedAtUtc)
            .ThenByDescending(order => order.Number)
            .Skip((effectivePage - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new CustomerOrderPage(
            orders.Select(ToSummary).ToArray(),
            effectivePage,
            pageSize,
            totalCount);
    }

    public async Task<CustomerOrderDetailView?> GetAsync(
        string customerId,
        string orderNumber,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var order = await db.Orders.AsNoTracking()
            .Include(current => current.Items)
            .SingleOrDefaultAsync(
                current => current.CustomerId == customerId
                    && current.Number == orderNumber,
                cancellationToken);
        if (order is null)
        {
            return null;
        }

        var shipment = await db.Shipments.AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == order.Id, cancellationToken);

        return new CustomerOrderDetailView(
            order.Number,
            order.SaleType,
            order.PaymentStatus,
            order.FulfillmentStatus,
            new CustomerOrderAddressView(
                order.Address.RecipientName,
                order.Address.PhoneNumber,
                order.Address.AddressLine,
                order.Address.SubDistrict,
                order.Address.District,
                order.Address.Province,
                order.Address.PostalCode),
            order.ShippingAmount,
            order.TotalPaid,
            order.CreatedAtUtc,
            order.Items.Select(ToItem).ToArray(),
            shipment is null ? null : new CustomerShipmentView(shipment.Carrier.ToString(), shipment.TrackingNumber,
                shipment.TrackingUrl, shipment.ShippedAtUtc));
    }

    private static CustomerOrderSummaryView ToSummary(Order order) => new(
        order.Number,
        order.SaleType,
        order.PaymentStatus,
        order.FulfillmentStatus,
        order.ShippingAmount,
        order.TotalPaid,
        order.CreatedAtUtc,
        order.Items.Select(ToItem).ToArray());

    private static CustomerOrderItemView ToItem(OrderItem item) => new(
        item.ProductId,
        item.DisplayName,
        item.EnglishName,
        item.ProductSlug,
        item.CategoryName,
        item.BrandName,
        item.UniverseName,
        item.PrimaryImageUrl,
        item.SaleType,
        item.Quantity,
        item.FullPrice,
        item.DepositAmount,
        item.BalanceAmount,
        item.LinePaidAmount,
        item.PreOrderCloseAtUtc,
        item.EstimatedArrivalMonth,
        item.EstimatedArrivalYear,
        item.BalancePaymentDays,
        item.DepositPolicy);

    private static string EscapeLikePattern(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);
}
