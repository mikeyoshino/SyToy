using FluentValidation;

namespace ToyStore.Application.Accounts.RegisterCustomer;

public sealed class RegisterCustomerValidator : AbstractValidator<RegisterCustomerCommand>
{
    public RegisterCustomerValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("กรุณากรอกอีเมล")
            .EmailAddress().WithMessage("รูปแบบอีเมลไม่ถูกต้อง");

        RuleFor(command => command.Password)
            .NotEmpty().WithMessage("กรุณากรอกรหัสผ่าน")
            .Length(8, 100).WithMessage("รหัสผ่านต้องมีความยาว 8–100 ตัวอักษร")
            .Matches("[A-Z]").WithMessage("รหัสผ่านต้องมีตัวอักษรภาษาอังกฤษพิมพ์ใหญ่อย่างน้อย 1 ตัว")
            .Matches("[a-z]").WithMessage("รหัสผ่านต้องมีตัวอักษรภาษาอังกฤษพิมพ์เล็กอย่างน้อย 1 ตัว")
            .Matches("[0-9]").WithMessage("รหัสผ่านต้องมีตัวเลขอย่างน้อย 1 ตัว");

        RuleFor(command => command.ConfirmPassword)
            .Equal(command => command.Password)
            .WithMessage("รหัสผ่านและการยืนยันรหัสผ่านไม่ตรงกัน");
    }
}
