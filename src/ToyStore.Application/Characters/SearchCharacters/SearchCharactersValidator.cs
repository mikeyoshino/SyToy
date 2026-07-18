using FluentValidation;
using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Characters.SearchCharacters;

public sealed class SearchCharactersValidator : AbstractValidator<SearchCharactersQuery>
{
    public SearchCharactersValidator()
    {
        RuleFor(query => query.UniverseId)
            .NotEmpty()
            .WithMessage("กรุณาเลือกจักรวาล");

        RuleFor(query => query.Term)
            .Must(HaveValidNormalizedLength)
            .WithMessage("คำค้นหาต้องไม่เกิน 200 ตัวอักษรหลังจัดรูปแบบ");

        RuleFor(query => query.Limit)
            .InclusiveBetween(1, 20)
            .WithMessage("จำนวนผลลัพธ์ต้องอยู่ระหว่าง 1–20 รายการ");
    }

    private static bool HaveValidNormalizedLength(string? term) =>
        string.IsNullOrWhiteSpace(term)
        || CatalogNameNormalizer.Normalize(term).Length <= CatalogReferenceLimits.NameLength;
}
