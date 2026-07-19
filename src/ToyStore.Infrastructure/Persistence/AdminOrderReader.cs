using Microsoft.EntityFrameworkCore;
using ToyStore.Application.Orders;
using ToyStore.Domain.Orders;

namespace ToyStore.Infrastructure.Persistence;

internal sealed class AdminOrderReader(
    IDbContextFactory<ApplicationDbContext> contextFactory) : IAdminOrderReader
{
    public async Task<AdminOrderPage> ListAsync(
        AdminOrderReadRequest request,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = from order in db.Orders.AsNoTracking()
                    join customer in db.Users.AsNoTracking()
                        on order.CustomerId equals customer.Id
                    select new { Order = order, CustomerEmail = customer.Email! };

        if (request.SaleType is not null)
        {
            var saleType = MapSaleType(request.SaleType.Value);
            query = query.Where(row => row.Order.SaleType == saleType);
        }
        if (request.PaymentStatus is not null)
        {
            var paymentStatus = MapPaymentStatus(request.PaymentStatus.Value);
            query = query.Where(row => row.Order.PaymentStatus == paymentStatus);
        }
        if (request.FulfillmentStatus is not null)
        {
            var fulfillmentStatus = MapFulfillmentStatus(request.FulfillmentStatus.Value);
            query = query.Where(row => row.Order.FulfillmentStatus == fulfillmentStatus);
        }
        if (request.CreatedFromUtc is not null)
            query = query.Where(row => row.Order.CreatedAtUtc >= request.CreatedFromUtc);
        if (request.CreatedBeforeUtc is not null)
            query = query.Where(row => row.Order.CreatedAtUtc < request.CreatedBeforeUtc);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{EscapeLikePattern(request.Search)}%";
            query = query.Where(row =>
                EF.Functions.ILike(row.Order.Number, pattern, "\\")
                || EF.Functions.ILike(row.CustomerEmail, pattern, "\\")
                || EF.Functions.ILike(row.Order.Address.RecipientName, pattern, "\\")
                || row.Order.Items.Any(item =>
                    EF.Functions.ILike(item.DisplayName, pattern, "\\")
                    || EF.Functions.ILike(item.EnglishName, pattern, "\\"))
                || db.Shipments.Any(shipment => shipment.OrderId == row.Order.Id
                    && EF.Functions.ILike(shipment.TrackingNumber, pattern, "\\")));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)request.PageSize));
        var effectivePage = Math.Min(request.Page, totalPages);
        var rows = await query
            .OrderByDescending(row => row.Order.CreatedAtUtc)
            .ThenByDescending(row => row.Order.Number)
            .Skip((effectivePage - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(row => new
            {
                row.Order.Number,
                row.CustomerEmail,
                row.Order.Address.RecipientName,
                row.Order.SaleType,
                row.Order.PaymentStatus,
                row.Order.FulfillmentStatus,
                ItemCount = row.Order.Items.Sum(item => item.Quantity),
                row.Order.TotalPaid,
                row.Order.CreatedAtUtc,
            })
            .ToArrayAsync(cancellationToken);

        var items = rows.Select(row => new AdminOrderListItem(
            row.Number,
            row.CustomerEmail,
            row.RecipientName,
            (AdminOrderSaleType)row.SaleType,
            (AdminOrderPaymentStatus)row.PaymentStatus,
            (AdminOrderFulfillmentStatus)row.FulfillmentStatus,
            row.ItemCount,
            row.TotalPaid,
            row.CreatedAtUtc)).ToArray();

        return new AdminOrderPage(items, effectivePage, request.PageSize, totalCount);
    }

    public async Task<AdminOrderDetailView?> GetAsync(
        string orderNumber,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var order = await db.Orders.AsNoTracking()
            .Include(current => current.Items)
            .SingleOrDefaultAsync(current => current.Number == orderNumber, cancellationToken);
        if (order is null) return null;

        var customerEmail = await db.Users.AsNoTracking()
            .Where(customer => customer.Id == order.CustomerId)
            .Select(customer => customer.Email!)
            .SingleAsync(cancellationToken);
        var paymentRows = await db.Payments.AsNoTracking()
            .Where(payment => payment.OrderId == order.Id)
            .OrderBy(payment => payment.PaidAtUtc)
            .Select(payment => new
            {
                payment.Purpose,
                payment.Amount,
                payment.Currency,
                payment.ProviderPaymentReference,
                payment.PaidAtUtc,
            })
            .ToArrayAsync(cancellationToken);
        var payments = paymentRows.Select(payment => new AdminOrderPaymentView(
            (AdminOrderPaymentPurpose)payment.Purpose,
            payment.Amount,
            payment.Currency,
            payment.ProviderPaymentReference,
            payment.PaidAtUtc)).ToArray();
        var shipment = await db.Shipments.AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == order.Id, cancellationToken);
        var audit = await db.OrderAuditEvents.AsNoTracking().Where(x => x.OrderId == order.Id)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Select(x => new AdminOrderAuditView(x.Action, x.ActorId, x.Detail, x.OccurredAtUtc))
            .ToArrayAsync(cancellationToken);

        return new AdminOrderDetailView(
            order.Number,
            customerEmail,
            (AdminOrderSaleType)order.SaleType,
            (AdminOrderPaymentStatus)order.PaymentStatus,
            (AdminOrderFulfillmentStatus)order.FulfillmentStatus,
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
            payments,
            order.Version,
            shipment is null ? null : new CustomerShipmentView(shipment.Carrier.ToString(), shipment.TrackingNumber,
                shipment.TrackingUrl, shipment.ShippedAtUtc),
            audit);
    }

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

    private static ToyStore.Domain.Products.SaleType MapSaleType(AdminOrderSaleType value) =>
        (ToyStore.Domain.Products.SaleType)value;

    private static PaymentStatus MapPaymentStatus(AdminOrderPaymentStatus value) =>
        (PaymentStatus)value;

    private static FulfillmentStatus MapFulfillmentStatus(AdminOrderFulfillmentStatus value) =>
        (FulfillmentStatus)value;

    private static string EscapeLikePattern(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);
}
