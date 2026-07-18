using System.Linq.Expressions;
using FluentValidation;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Products;

namespace ToyStore.Application.Products;

internal static class PreOrderProductValidationRules
{
    private static readonly TimeZoneInfo BangkokTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok");

    internal static void AddTemporalRules<T>(
        AbstractValidator<T> validator,
        TimeProvider timeProvider,
        Expression<Func<T, DateOnly>> closeDateExpression,
        Func<T, DateOnly> closeDate,
        Func<T, int> estimatedArrivalMonth,
        Func<T, int> estimatedArrivalYear)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(timeProvider);

        validator.RuleFor(closeDateExpression)
            .NotEmpty().WithMessage("กรุณาเลือกวันปิดรอบ")
            .Must(date => date == default || BangkokCloseAtUtc(date) > timeProvider.GetUtcNow())
            .WithMessage("วันปิดรอบต้องเป็นวันในอนาคต (ปิด 23:59 เวลาไทย)");
        validator.RuleFor(command => command).Custom((command, context) =>
        {
            var close = closeDate(command);
            var month = estimatedArrivalMonth(command);
            var year = estimatedArrivalYear(command);
            if (close == default || month is < 1 or > 12 || year is < 1 or > 9999)
            {
                return;
            }

            if (year < close.Year || (year == close.Year && month < close.Month))
            {
                context.AddFailure(
                    "EstimatedArrivalMonth",
                    "เดือนที่สินค้าคาดว่าจะมาถึงต้องไม่ก่อนเดือนปิดรอบ");
            }
        });
    }

    internal static bool TryMapProductRule(
        ProductRule rule,
        out Error error,
        out FieldValidationFailure? failure)
    {
        failure = rule switch
        {
            ProductRule.PreOrderCloseMustBeFuture => new(
                "CloseDate",
                "วันปิดรอบต้องเป็นวันในอนาคต (ปิด 23:59 เวลาไทย)"),
            ProductRule.EstimatedArrivalBeforeClose => new(
                "EstimatedArrivalMonth",
                "เดือนที่สินค้าคาดว่าจะมาถึงต้องไม่ก่อนเดือนปิดรอบ"),
            _ => null,
        };
        if (failure is not null)
        {
            error = ProductErrors.InvalidInput;
            return true;
        }

        return InStockProductMutationSupport.TryMapProductRule(rule, out error);
    }

    private static DateTimeOffset BangkokCloseAtUtc(DateOnly closeDate)
    {
        var localClose = DateTime.SpecifyKind(
            closeDate.ToDateTime(new TimeOnly(23, 59, 59)),
            DateTimeKind.Unspecified);
        return new DateTimeOffset(
            TimeZoneInfo.ConvertTimeToUtc(localClose, BangkokTimeZone),
            TimeSpan.Zero);
    }
}
