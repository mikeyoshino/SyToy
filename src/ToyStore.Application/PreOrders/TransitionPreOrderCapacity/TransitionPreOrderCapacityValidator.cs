using FluentValidation;
using ToyStore.Domain.PreOrders;

namespace ToyStore.Application.PreOrders.TransitionPreOrderCapacity;

public sealed class TransitionPreOrderCapacityValidator
    : AbstractValidator<TransitionPreOrderCapacityCommand>
{
    public TransitionPreOrderCapacityValidator()
    {
        RuleFor(x => x.CapacityId).NotEmpty().WithMessage("รหัสรอบพรีออเดอร์ไม่ถูกต้อง");
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("รหัสสินค้าไม่ถูกต้อง");
        RuleFor(x => x.ReservationId).NotEmpty().WithMessage("รหัสการจองไม่ถูกต้อง");
        RuleFor(x => x.OperationId).NotEmpty().WithMessage("รหัสการทำรายการไม่ถูกต้อง");
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage("เวอร์ชันข้อมูลไม่ถูกต้อง");
        RuleFor(x => x.Action).IsInEnum().WithMessage("ประเภทการทำรายการไม่ถูกต้อง");
        RuleFor(x => x.Reason).Must(value => !string.IsNullOrWhiteSpace(value)).WithMessage("กรุณาระบุเหตุผล")
            .Must(value => value is null || value.Trim().Length <= PreOrderCapacityLimits.ReasonLength).WithMessage($"เหตุผลต้องไม่เกิน {PreOrderCapacityLimits.ReasonLength} ตัวอักษร");
        RuleFor(x => x.Reference).Must(value => !string.IsNullOrWhiteSpace(value)).WithMessage("กรุณาระบุข้อมูลอ้างอิง")
            .Must(value => value is null || value.Trim().Length <= PreOrderCapacityLimits.ReferenceLength).WithMessage($"ข้อมูลอ้างอิงต้องไม่เกิน {PreOrderCapacityLimits.ReferenceLength} ตัวอักษร");
    }
}
