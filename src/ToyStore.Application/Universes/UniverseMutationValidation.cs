using FluentValidation;
using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Universes;

internal static class UniverseMutationValidation
{
    public static bool ValidateName<T>(
        string? value,
        ValidationContext<T> context,
        string requiredMessage,
        string persistedLengthMessage,
        string normalizedLengthMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            context.AddFailure(requiredMessage);
            return false;
        }

        var valid = true;
        if (value.Trim().Length > CatalogReferenceLimits.NameLength)
        {
            context.AddFailure(persistedLengthMessage);
            valid = false;
        }

        if (CatalogNameNormalizer.Normalize(value).Length > CatalogReferenceLimits.NameLength)
        {
            context.AddFailure(normalizedLengthMessage);
            valid = false;
        }

        return valid;
    }

    public static void ValidateEnglishName<T>(string? value, ValidationContext<T> context)
    {
        if (!ValidateName(
                value,
                context,
                "กรุณากรอกชื่อภาษาอังกฤษ",
                "ชื่อภาษาอังกฤษต้องไม่เกิน 200 ตัวอักษร",
                "ชื่อภาษาอังกฤษต้องไม่เกิน 200 ตัวอักษรหลังปรับรูปแบบ"))
        {
            return;
        }

        try
        {
            _ = CatalogSlugGenerator.GenerateBase(value!);
        }
        catch (CatalogReferenceRuleException exception)
            when (exception.Rule == CatalogReferenceRule.SlugCannotBeGenerated)
        {
            context.AddFailure(
                "ชื่อภาษาอังกฤษต้องสร้างส่วน URL ได้ด้วยตัวอักษรอังกฤษหรือตัวเลข");
        }
    }
}
