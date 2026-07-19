using FluentValidation;
using ToyStore.Domain.Orders;

namespace ToyStore.Application.Orders.CreateShipment;

public sealed class CreateShipmentValidator : AbstractValidator<CreateShipmentCommand>
{
    public CreateShipmentValidator()
    {
        RuleFor(x => x.OrderNumber).NotEmpty().WithMessage("กรุณาระบุเลขคำสั่งซื้อ").MaximumLength(40).WithMessage("เลขคำสั่งซื้อต้องไม่เกิน 40 ตัวอักษร");
        RuleFor(x => x.Carrier).Must(Enum.IsDefined).WithMessage("บริษัทขนส่งไม่ถูกต้อง");
        RuleFor(x => x.TrackingNumber).NotEmpty().WithMessage("กรุณากรอกเลข Tracking").MaximumLength(100).WithMessage("เลข Tracking ต้องไม่เกิน 100 ตัวอักษร");
        RuleFor(x => x.ExpectedOrderVersion).GreaterThan(0).WithMessage("เวอร์ชันคำสั่งซื้อไม่ถูกต้อง");
        RuleFor(x => x.OperationId).NotEqual(Guid.Empty).WithMessage("รหัสการดำเนินการไม่ถูกต้อง");
        RuleFor(x => x).Custom((command, context) =>
        {
            var rule = Shipment.Validate((ShippingCarrier)command.Carrier,
                command.TrackingNumber, command.OtherTrackingUrl, out _);
            var message = rule switch
            {
                ShipmentRule.TrackingInvalid => "รูปแบบเลข Tracking ไม่ถูกต้องสำหรับบริษัทขนส่งที่เลือก",
                ShipmentRule.OtherUrlRequired => "กรุณากรอกลิงก์ติดตามสำหรับบริษัทขนส่งอื่น",
                ShipmentRule.OtherUrlInvalid => "ลิงก์ติดตามต้องเป็น HTTPS ที่ถูกต้อง",
                ShipmentRule.OtherUrlNotAllowed => "ลิงก์กำหนดเองใช้ได้เฉพาะบริษัทขนส่งอื่น",
                _ => null,
            };
            if (message is not null) context.AddFailure(nameof(command.TrackingNumber), message);
        });
    }
}
