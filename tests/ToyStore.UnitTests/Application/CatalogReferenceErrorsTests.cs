using ToyStore.Application.Brands;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Application.Universes;

namespace ToyStore.UnitTests.Application;

public sealed class CatalogReferenceErrorsTests
{
    [Fact]
    public void BrandErrorsHaveStableDistinctThaiContracts()
    {
        AssertError(BrandErrors.DuplicateDisplayName, "Brand.DuplicateDisplayName", "ชื่อแบรนด์นี้มีอยู่แล้ว", ErrorType.Conflict);
        AssertError(BrandErrors.DuplicateEnglishName, "Brand.DuplicateEnglishName", "ชื่อภาษาอังกฤษของแบรนด์นี้มีอยู่แล้ว", ErrorType.Conflict);
        AssertError(BrandErrors.NotFound, "Brand.NotFound", "ไม่พบแบรนด์ที่ต้องการ", ErrorType.NotFound);
        AssertError(BrandErrors.Archived, "Brand.Archived", "แบรนด์นี้ถูกเก็บถาวรแล้ว", ErrorType.Conflict);
        AssertError(BrandErrors.MissingMedia, "Brand.MissingMedia", "กรุณาเลือกรูปภาพแบรนด์", ErrorType.Validation);
        AssertError(BrandErrors.StaleVersion, "Brand.StaleVersion", "ข้อมูลแบรนด์มีการเปลี่ยนแปลง กรุณารีเฟรชแล้วลองอีกครั้ง", ErrorType.Conflict);
    }

    [Fact]
    public void UniverseErrorsHaveStableDistinctThaiContracts()
    {
        AssertError(UniverseErrors.DuplicateDisplayName, "Universe.DuplicateDisplayName", "ชื่อจักรวาลนี้มีอยู่แล้ว", ErrorType.Conflict);
        AssertError(UniverseErrors.DuplicateEnglishName, "Universe.DuplicateEnglishName", "ชื่อภาษาอังกฤษของจักรวาลนี้มีอยู่แล้ว", ErrorType.Conflict);
        AssertError(UniverseErrors.NotFound, "Universe.NotFound", "ไม่พบจักรวาลที่ต้องการ", ErrorType.NotFound);
        AssertError(UniverseErrors.Archived, "Universe.Archived", "จักรวาลนี้ถูกเก็บถาวรแล้ว", ErrorType.Conflict);
        AssertError(UniverseErrors.MissingMedia, "Universe.MissingMedia", "กรุณาเลือกโลโก้จักรวาล", ErrorType.Validation);
        AssertError(UniverseErrors.StaleVersion, "Universe.StaleVersion", "ข้อมูลจักรวาลมีการเปลี่ยนแปลง กรุณารีเฟรชแล้วลองอีกครั้ง", ErrorType.Conflict);
    }

    [Fact]
    public void CommitOutcomeUnknownRemainsACommonSafePersistenceFailure()
    {
        AssertError(
            PersistenceErrors.CommitOutcomeUnknown,
            "Persistence.CommitOutcomeUnknown",
            "ยังยืนยันผลการบันทึกไม่ได้ กรุณารีเฟรชข้อมูลก่อนลองอีกครั้ง",
            ErrorType.Failure);
    }

    private static void AssertError(
        Error error,
        string code,
        string message,
        ErrorType type)
    {
        Assert.Equal(code, error.Code);
        Assert.Equal(message, error.Message);
        Assert.Equal(type, error.Type);
    }
}
