using FluentValidation;

namespace ToyStore.Application.Brands.ArchiveBrand;

public sealed class ArchiveBrandValidator : AbstractValidator<ArchiveBrandCommand>
{
    public ArchiveBrandValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty()
            .WithMessage("รหัสแบรนด์ไม่ถูกต้อง");

        RuleFor(command => command.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("เวอร์ชันข้อมูลแบรนด์ไม่ถูกต้อง");
    }
}
