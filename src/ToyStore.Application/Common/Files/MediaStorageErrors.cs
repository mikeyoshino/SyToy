using ToyStore.Application.Common.Models;
using ToyStore.Domain.Products;

namespace ToyStore.Application.Common.Files;

public static class MediaStorageErrors
{
    public static readonly Error EmptyBatch = new(
        "Media.EmptyBatch",
        "กรุณาเลือกรูปภาพอย่างน้อย 1 รูป",
        ErrorType.Validation);

    public static readonly Error TooManyFiles = new(
        "Media.TooManyFiles",
        $"เลือกภาพได้ไม่เกิน {Product.MaximumImageCount} รูปต่อครั้ง",
        ErrorType.Validation);

    public static readonly Error UnsupportedContentType = new(
        "Media.UnsupportedContentType",
        "รองรับเฉพาะไฟล์ JPEG, PNG และ WebP",
        ErrorType.Validation);

    public static readonly Error InvalidSignature = new(
        "Media.InvalidSignature",
        "เนื้อหาไฟล์รูปภาพไม่ถูกต้องหรือไฟล์ไม่สมบูรณ์",
        ErrorType.Validation);

    public static readonly Error ContentTypeMismatch = new(
        "Media.ContentTypeMismatch",
        "ชนิดไฟล์ที่ระบุไม่ตรงกับเนื้อหาไฟล์",
        ErrorType.Validation);

    public static readonly Error TooLarge = new(
        "Media.TooLarge",
        "รูปภาพแต่ละไฟล์ต้องมีขนาดไม่เกิน 5 MiB",
        ErrorType.Validation);
}
