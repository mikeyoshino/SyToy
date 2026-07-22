using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Reports.GetSalesReport;

public sealed record GetSalesReportQuery(
    DateOnly? From = null,
    DateOnly? To = null)
    : AuthorizedResultRequest<Result<SalesReportView>>
{
    public override string RequiredPolicy => PolicyNames.CanManageOrders;

    public override Result<SalesReportView> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<SalesReportView>.Failure(requestError, validationFailures);
}
