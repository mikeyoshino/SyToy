using FluentValidation;
using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Characters.CreateCharacter;

public sealed class CreateCharacterValidator : AbstractValidator<CreateCharacterCommand>
{
    public CreateCharacterValidator()
    {
        RuleFor(command => command.UniverseId)
            .NotEmpty()
            .WithMessage("กรุณาเลือกจักรวาล");

        RuleFor(command => command.Name)
            .Custom(ValidateName);
    }

    private static void ValidateName(
        string? value,
        ValidationContext<CreateCharacterCommand> context)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            context.AddFailure("กรุณากรอกชื่อตัวละคร");
            return;
        }

        if (value.Trim().Length > CatalogReferenceLimits.NameLength)
        {
            context.AddFailure("ชื่อตัวละครต้องไม่เกิน 200 ตัวอักษร");
        }

        if (CatalogNameNormalizer.Normalize(value).Length > CatalogReferenceLimits.NameLength)
        {
            context.AddFailure("ชื่อตัวละครต้องไม่เกิน 200 ตัวอักษรหลังปรับรูปแบบ");
        }
    }
}
