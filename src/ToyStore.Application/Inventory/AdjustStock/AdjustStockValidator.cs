using FluentValidation;
using ToyStore.Domain.Inventory;

namespace ToyStore.Application.Inventory.AdjustStock;

public sealed class AdjustStockValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockValidator()
    {
        RuleFor(command => command.InventoryItemId)
            .NotEmpty().WithMessage("รหัสสต็อกสินค้าไม่ถูกต้อง");
        RuleFor(command => command.ProductId)
            .NotEmpty().WithMessage("รหัสสินค้าไม่ถูกต้อง");
        RuleFor(command => command.OperationId)
            .NotEmpty().WithMessage("รหัสการทำรายการไม่ถูกต้อง");
        RuleFor(command => command.ExpectedVersion)
            .GreaterThan(0).WithMessage("เวอร์ชันข้อมูลสต็อกไม่ถูกต้อง");
        RuleFor(command => command.QuantityDelta)
            .NotEqual(0).WithMessage("จำนวนที่ปรับต้องไม่เป็น 0");
        RuleFor(command => command.Reason)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("กรุณากรอกเหตุผล")
            .Must(value => value is null || value.Trim().Length <= InventoryLimits.ReasonLength)
            .WithMessage($"เหตุผลต้องไม่เกิน {InventoryLimits.ReasonLength} ตัวอักษร");
        RuleFor(command => command.Reference)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("กรุณากรอกข้อมูลอ้างอิง")
            .Must(value => value is null || value.Trim().Length <= InventoryLimits.ReferenceLength)
            .WithMessage($"ข้อมูลอ้างอิงต้องไม่เกิน {InventoryLimits.ReferenceLength} ตัวอักษร");
    }
}
