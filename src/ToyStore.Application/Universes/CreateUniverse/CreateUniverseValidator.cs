using FluentValidation;

namespace ToyStore.Application.Universes.CreateUniverse;

public sealed class CreateUniverseValidator : AbstractValidator<CreateUniverseCommand>
{
    public CreateUniverseValidator()
    {
        RuleFor(command => command.DisplayName)
            .Custom((value, context) => UniverseMutationValidation.ValidateName(
                value,
                context,
                "กรุณากรอกชื่อจักรวาล",
                "ชื่อจักรวาลต้องไม่เกิน 200 ตัวอักษร",
                "ชื่อจักรวาลต้องไม่เกิน 200 ตัวอักษรหลังปรับรูปแบบ"));
        RuleFor(command => command.EnglishName)
            .Custom(UniverseMutationValidation.ValidateEnglishName);
        RuleFor(command => command.Logo)
            .NotNull()
            .WithMessage("กรุณาเลือกโลโก้จักรวาล");
    }
}
