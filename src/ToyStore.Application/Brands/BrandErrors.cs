using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Brands;

public static class BrandErrors
{
    public static readonly Error DuplicateDisplayName = new(
        "Brand.DuplicateDisplayName",
        "ชื่อแบรนด์นี้มีอยู่แล้ว",
        ErrorType.Conflict);

    public static readonly Error DuplicateEnglishName = new(
        "Brand.DuplicateEnglishName",
        "ชื่อภาษาอังกฤษของแบรนด์นี้มีอยู่แล้ว",
        ErrorType.Conflict);

    public static readonly Error NotFound = new(
        "Brand.NotFound",
        "ไม่พบแบรนด์ที่ต้องการ",
        ErrorType.NotFound);

    public static readonly Error Archived = new(
        "Brand.Archived",
        "แบรนด์นี้ถูกเก็บถาวรแล้ว",
        ErrorType.Conflict);

    public static readonly Error MissingMedia = new(
        "Brand.MissingMedia",
        "กรุณาเลือกรูปภาพแบรนด์",
        ErrorType.Validation);

    public static readonly Error StaleVersion = new(
        "Brand.StaleVersion",
        "ข้อมูลแบรนด์มีการเปลี่ยนแปลง กรุณารีเฟรชแล้วลองอีกครั้ง",
        ErrorType.Conflict);
}
