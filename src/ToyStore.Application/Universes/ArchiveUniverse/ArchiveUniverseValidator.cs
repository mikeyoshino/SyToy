using FluentValidation;

namespace ToyStore.Application.Universes.ArchiveUniverse;

public sealed class ArchiveUniverseValidator : AbstractValidator<ArchiveUniverseCommand>
{
    public ArchiveUniverseValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty()
            .WithMessage("รหัสจักรวาลไม่ถูกต้อง");
        RuleFor(command => command.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("เวอร์ชันข้อมูลจักรวาลไม่ถูกต้อง");
    }
}
