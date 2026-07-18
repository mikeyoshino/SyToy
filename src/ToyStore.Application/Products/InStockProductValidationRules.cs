using FluentValidation;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Products;

namespace ToyStore.Application.Products;

internal static class InStockProductValidationRules
{
    internal static void ValidateDisplayName<T>(string? value, ValidationContext<T> context) =>
        ValidateName(
            value,
            context,
            "กรุณากรอกชื่อสินค้า",
            "ชื่อสินค้าต้องไม่เกิน 200 ตัวอักษร",
            "ชื่อสินค้าต้องไม่เกิน 200 ตัวอักษรหลังปรับรูปแบบ",
            requireSlug: false);

    internal static void ValidateEnglishName<T>(string? value, ValidationContext<T> context) =>
        ValidateName(
            value,
            context,
            "กรุณากรอกชื่อภาษาอังกฤษ",
            "ชื่อภาษาอังกฤษต้องไม่เกิน 200 ตัวอักษร",
            "ชื่อภาษาอังกฤษต้องไม่เกิน 200 ตัวอักษรหลังปรับรูปแบบ",
            requireSlug: true);

    internal static void ValidateCharacters<T>(
        IReadOnlyList<Guid>? characterIds,
        ValidationContext<T> context)
    {
        if (characterIds is null)
        {
            context.AddFailure("รายการตัวละครไม่ถูกต้อง");
            return;
        }

        if (characterIds.Any(id => id == Guid.Empty))
        {
            context.AddFailure("รหัสตัวละครไม่ถูกต้อง");
        }

        if (characterIds.Distinct().Count() != characterIds.Count)
        {
            context.AddFailure("กรุณาเลือกตัวละครแต่ละรายการเพียงครั้งเดียว");
        }
    }

    internal static void ValidateMedia<T>(
        IReadOnlyList<ProductMediaPlanSlot>? slots,
        ValidationContext<T> context,
        bool allowRetained)
    {
        if (slots is null)
        {
            context.AddFailure("รายการรูปภาพสินค้าไม่ถูกต้อง");
            return;
        }

        if (slots.Count > Product.MaximumImageCount)
        {
            context.AddFailure("รูปภาพสินค้าต้องไม่เกิน 8 รูป");
        }

        if (slots.Any(slot => slot is null))
        {
            context.AddFailure("รายการรูปภาพสินค้าไม่ถูกต้อง");
            return;
        }

        var retained = slots.OfType<RetainedProductMediaSlot>().ToArray();
        var uploads = slots.OfType<UploadProductMediaSlot>().ToArray();
        if (retained.Length + uploads.Length != slots.Count)
        {
            context.AddFailure("รายการรูปภาพสินค้ามีชนิดที่ไม่รองรับ");
        }

        if (!allowRetained && retained.Length > 0)
        {
            context.AddFailure("การสร้างสินค้าใหม่รับได้เฉพาะรูปภาพที่อัปโหลดใหม่");
        }

        if (retained.Any(slot => slot.ProductImageId == Guid.Empty))
        {
            context.AddFailure("รหัสรูปภาพเดิมไม่ถูกต้อง");
        }

        if (retained.Select(slot => slot.ProductImageId).Distinct().Count() != retained.Length)
        {
            context.AddFailure("ห้ามเลือกรูปภาพเดิมซ้ำ");
        }

        if (uploads.Any(slot => slot.Upload is null))
        {
            context.AddFailure("กรุณาเลือกไฟล์รูปภาพให้ครบ");
        }

        if (uploads.Select(slot => slot.Upload)
            .Distinct(ReferenceEqualityComparer.Instance).Count() != uploads.Length)
        {
            context.AddFailure("ห้ามใช้ไฟล์อัปโหลดรายการเดียวกันซ้ำ");
        }
    }

    internal static bool IsAllowedCategory(Guid categoryId) =>
        categoryId == CatalogSeedIds.ArtToyCategory
        || categoryId == CatalogSeedIds.GundamCategory;

    private static void ValidateName<T>(
        string? value,
        ValidationContext<T> context,
        string required,
        string persistedTooLong,
        string normalizedTooLong,
        bool requireSlug)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            context.AddFailure(required);
            return;
        }

        if (value.Trim().Length > CatalogReferenceLimits.NameLength)
        {
            context.AddFailure(persistedTooLong);
        }

        if (CatalogNameNormalizer.Normalize(value).Length > CatalogReferenceLimits.NameLength)
        {
            context.AddFailure(normalizedTooLong);
        }

        if (!requireSlug)
        {
            return;
        }

        try
        {
            _ = CatalogSlugGenerator.GenerateBase(value);
        }
        catch (CatalogReferenceRuleException exception)
            when (exception.Rule == CatalogReferenceRule.SlugCannotBeGenerated)
        {
            context.AddFailure(
                "ชื่อภาษาอังกฤษต้องสร้างส่วน URL ได้ด้วยตัวอักษรอังกฤษหรือตัวเลข");
        }
    }
}
