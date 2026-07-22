using FluentValidation;

namespace ToyStore.Application.Reports.GetSalesReport;

public sealed class GetSalesReportValidator : AbstractValidator<GetSalesReportQuery>
{
    public const int MaximumPeriodDays = 366;

    public GetSalesReportValidator()
    {
        RuleFor(query => query)
            .Must(query => query.From.HasValue == query.To.HasValue)
            .WithName(nameof(GetSalesReportQuery.From))
            .WithMessage("กรุณาระบุวันที่เริ่มต้นและวันที่สิ้นสุดให้ครบ");
        RuleFor(query => query)
            .Must(query => query.From is null || query.To is null || query.From <= query.To)
            .WithName(nameof(GetSalesReportQuery.To))
            .WithMessage("วันที่สิ้นสุดต้องไม่ก่อนวันที่เริ่มต้น");
        RuleFor(query => query)
            .Must(query => query.From is null || query.To is null
                || query.To.Value.DayNumber - query.From.Value.DayNumber + 1 <= MaximumPeriodDays)
            .WithName(nameof(GetSalesReportQuery.To))
            .WithMessage($"ช่วงรายงานต้องไม่เกิน {MaximumPeriodDays} วัน");
    }
}
