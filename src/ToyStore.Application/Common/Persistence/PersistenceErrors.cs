using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Common.Persistence;

public static class PersistenceErrors
{
    public static readonly Error CommitOutcomeUnknown = new(
        "Persistence.CommitOutcomeUnknown",
        "ยังยืนยันผลการบันทึกไม่ได้ กรุณารีเฟรชข้อมูลก่อนลองอีกครั้ง",
        ErrorType.Failure);
}
