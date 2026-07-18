using FluentValidation;
using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Brands.UpdateBrand;

public sealed class UpdateBrandValidator : AbstractValidator<UpdateBrandCommand>
{
    public UpdateBrandValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty()
            .WithMessage("รหัสแบรนด์ไม่ถูกต้อง");

        RuleFor(command => command.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("เวอร์ชันข้อมูลแบรนด์ไม่ถูกต้อง");

        RuleFor(command => command.DisplayName)
            .Custom((value, context) => ValidateName(
                value,
                context,
                "กรุณากรอกชื่อแบรนด์",
                "ชื่อแบรนด์ต้องไม่เกิน 200 ตัวอักษร",
                "ชื่อแบรนด์ต้องไม่เกิน 200 ตัวอักษรหลังปรับรูปแบบ"));

        RuleFor(command => command.EnglishName)
            .Custom((value, context) =>
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
                    _ = CatalogSlugGenerator.GenerateBase(value);
                }
                catch (CatalogReferenceRuleException exception)
                    when (exception.Rule == CatalogReferenceRule.SlugCannotBeGenerated)
                {
                    context.AddFailure(
                        "ชื่อภาษาอังกฤษต้องสร้างส่วน URL ได้ด้วยตัวอักษรอังกฤษหรือตัวเลข");
                }
            });
    }

    private static bool ValidateName<T>(
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
}
