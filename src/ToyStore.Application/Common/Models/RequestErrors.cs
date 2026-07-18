namespace ToyStore.Application.Common.Models;

public static class RequestErrors
{
    public static readonly Error ValidationFailed = new(
        "Validation.Failed",
        "ข้อมูลไม่ถูกต้อง กรุณาตรวจสอบอีกครั้ง",
        ErrorType.Validation);

    public static readonly Error Unauthorized = new(
        "Authorization.Unauthorized",
        "กรุณาเข้าสู่ระบบเพื่อดำเนินการต่อ",
        ErrorType.Unauthorized);

    public static readonly Error Forbidden = new(
        "Authorization.Forbidden",
        "คุณไม่มีสิทธิ์ดำเนินการนี้",
        ErrorType.Forbidden);
}
