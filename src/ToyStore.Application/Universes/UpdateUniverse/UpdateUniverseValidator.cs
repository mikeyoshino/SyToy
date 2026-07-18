using FluentValidation;

namespace ToyStore.Application.Universes.UpdateUniverse;

public sealed class UpdateUniverseValidator : AbstractValidator<UpdateUniverseCommand>
{
    public UpdateUniverseValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty()
            .WithMessage("รหัสจักรวาลไม่ถูกต้อง");
        RuleFor(command => command.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("เวอร์ชันข้อมูลจักรวาลไม่ถูกต้อง");
        RuleFor(command => command.DisplayName)
            .Custom((value, context) => UniverseMutationValidation.ValidateName(
                value,
                context,
                "กรุณากรอกชื่อจักรวาล",
                "ชื่อจักรวาลต้องไม่เกิน 200 ตัวอักษร",
                "ชื่อจักรวาลต้องไม่เกิน 200 ตัวอักษรหลังปรับรูปแบบ"));
        RuleFor(command => command.EnglishName)
            .Custom(UniverseMutationValidation.ValidateEnglishName);
    }
}
