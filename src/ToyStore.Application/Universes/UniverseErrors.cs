using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Universes;

public static class UniverseErrors
{
    public static readonly Error DuplicateDisplayName = new(
        "Universe.DuplicateDisplayName",
        "ชื่อจักรวาลนี้มีอยู่แล้ว",
        ErrorType.Conflict);

    public static readonly Error DuplicateEnglishName = new(
        "Universe.DuplicateEnglishName",
        "ชื่อภาษาอังกฤษของจักรวาลนี้มีอยู่แล้ว",
        ErrorType.Conflict);

    public static readonly Error NotFound = new(
        "Universe.NotFound",
        "ไม่พบจักรวาลที่ต้องการ",
        ErrorType.NotFound);

    public static readonly Error Archived = new(
        "Universe.Archived",
        "จักรวาลนี้ถูกเก็บถาวรแล้ว",
        ErrorType.Conflict);

    public static readonly Error MissingMedia = new(
        "Universe.MissingMedia",
        "กรุณาเลือกโลโก้จักรวาล",
        ErrorType.Validation);

    public static readonly Error StaleVersion = new(
        "Universe.StaleVersion",
        "ข้อมูลจักรวาลมีการเปลี่ยนแปลง กรุณารีเฟรชแล้วลองอีกครั้ง",
        ErrorType.Conflict);
}
