using FluentValidation;

namespace ToyStore.Application.Accounts.ChangePassword;

public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(command => command.CurrentPassword)
            .NotEmpty().WithMessage("กรุณากรอกรหัสผ่านปัจจุบัน");

        RuleFor(command => command.NewPassword)
            .NotEmpty().WithMessage("กรุณากรอกรหัสผ่านใหม่")
            .Length(8, 100).WithMessage("รหัสผ่านใหม่ต้องมีความยาว 8–100 ตัวอักษร")
            .Matches("[A-Z]").WithMessage("รหัสผ่านใหม่ต้องมีตัวอักษรภาษาอังกฤษพิมพ์ใหญ่อย่างน้อย 1 ตัว")
            .Matches("[a-z]").WithMessage("รหัสผ่านใหม่ต้องมีตัวอักษรภาษาอังกฤษพิมพ์เล็กอย่างน้อย 1 ตัว")
            .Matches("[0-9]").WithMessage("รหัสผ่านใหม่ต้องมีตัวเลขอย่างน้อย 1 ตัว");

        RuleFor(command => command.ConfirmPassword)
            .Equal(command => command.NewPassword)
            .WithMessage("รหัสผ่านและการยืนยันรหัสผ่านไม่ตรงกัน");
    }
}
