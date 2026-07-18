using Microsoft.AspNetCore.Identity;

namespace ToyStore.Infrastructure.Identity;

public sealed class ThaiIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DuplicateEmail(string email) =>
        Create(nameof(DuplicateEmail), "อีเมลนี้ถูกใช้งานแล้ว");

    public override IdentityError DuplicateUserName(string userName) =>
        Create(nameof(DuplicateUserName), "อีเมลนี้ถูกใช้งานแล้ว");

    public override IdentityError InvalidEmail(string? email) =>
        Create(nameof(InvalidEmail), "รูปแบบอีเมลไม่ถูกต้อง");

    public override IdentityError InvalidUserName(string? userName) =>
        Create(nameof(InvalidUserName), "รูปแบบอีเมลไม่ถูกต้อง");

    public override IdentityError PasswordTooShort(int length) =>
        Create(nameof(PasswordTooShort), $"รหัสผ่านต้องมีความยาวอย่างน้อย {length} ตัวอักษร");

    public override IdentityError PasswordRequiresDigit() =>
        Create(nameof(PasswordRequiresDigit), "รหัสผ่านต้องมีตัวเลขอย่างน้อย 1 ตัว");

    public override IdentityError PasswordRequiresLower() =>
        Create(nameof(PasswordRequiresLower), "รหัสผ่านต้องมีตัวอักษรภาษาอังกฤษพิมพ์เล็กอย่างน้อย 1 ตัว");

    public override IdentityError PasswordRequiresUpper() =>
        Create(nameof(PasswordRequiresUpper), "รหัสผ่านต้องมีตัวอักษรภาษาอังกฤษพิมพ์ใหญ่อย่างน้อย 1 ตัว");

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Create(nameof(PasswordRequiresNonAlphanumeric), "รหัสผ่านต้องมีอักขระพิเศษอย่างน้อย 1 ตัว");

    public override IdentityError PasswordMismatch() =>
        Create(nameof(PasswordMismatch), "รหัสผ่านปัจจุบันไม่ถูกต้อง");

    private static IdentityError Create(string code, string description) =>
        new() { Code = code, Description = description };
}
