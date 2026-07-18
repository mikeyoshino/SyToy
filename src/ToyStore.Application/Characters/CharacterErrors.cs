using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Characters;

public static class CharacterErrors
{
    public static readonly Error DuplicateName = new(
        "Character.DuplicateName",
        "ชื่อตัวละครนี้มีอยู่แล้วในจักรวาลที่เลือก",
        ErrorType.Conflict);

    public static readonly Error UniverseUnavailable = new(
        "Character.UniverseUnavailable",
        "ไม่พบจักรวาลที่เลือกหรือจักรวาลนี้ถูกเก็บถาวรแล้ว",
        ErrorType.Conflict);
}
