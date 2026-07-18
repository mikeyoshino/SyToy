using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Accounts;

public static class AccountErrors
{
    public static readonly Error InvalidCredentials = new(
        "accounts.invalid_credentials",
        "อีเมลหรือรหัสผ่านไม่ถูกต้อง",
        ErrorType.Unauthorized);

    public static readonly Error LockedOut = new(
        "accounts.locked_out",
        "บัญชีถูกล็อกชั่วคราว กรุณาลองใหม่ภายหลัง",
        ErrorType.Forbidden);

    public static readonly Error EmailAlreadyUsed = new(
        "accounts.email_already_used",
        "อีเมลนี้ถูกใช้งานแล้ว",
        ErrorType.Conflict);

    public static readonly Error PasswordChangeFailed = new(
        "accounts.password_change_failed",
        "ไม่สามารถเปลี่ยนรหัสผ่านได้ กรุณาตรวจสอบรหัสผ่านปัจจุบัน",
        ErrorType.Validation);

    public static readonly Error NotAuthenticated = new(
        "accounts.not_authenticated",
        "กรุณาเข้าสู่ระบบก่อนดำเนินการ",
        ErrorType.Unauthorized);

    public static readonly Error RegistrationFailed = new(
        "accounts.registration_failed",
        "ไม่สามารถสร้างบัญชีได้ กรุณาตรวจสอบข้อมูลแล้วลองใหม่",
        ErrorType.Validation);

    public static readonly Error AdminAlreadyExists = new(
        "accounts.admin_already_exists",
        "ระบบมีบัญชีผู้ดูแลแล้ว",
        ErrorType.Conflict);

    public static readonly Error AdminBootstrapFailed = new(
        "accounts.admin_bootstrap_failed",
        "ไม่สามารถสร้างบัญชีผู้ดูแลคนแรกได้",
        ErrorType.Failure);
}
