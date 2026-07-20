using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Products;

public static class ProductErrors
{
    public static readonly Error DuplicateDisplayName = new(
        "Product.DuplicateDisplayName",
        "ชื่อสินค้านี้มีอยู่แล้ว",
        ErrorType.Conflict);

    public static readonly Error DuplicateEnglishName = new(
        "Product.DuplicateEnglishName",
        "ชื่อภาษาอังกฤษของสินค้านี้มีอยู่แล้ว",
        ErrorType.Conflict);

    public static readonly Error NotFound = new(
        "Product.NotFound",
        "ไม่พบสินค้าที่ต้องการ",
        ErrorType.NotFound);

    public static readonly Error StaleVersion = new(
        "Product.StaleVersion",
        "ข้อมูลสินค้ามีการเปลี่ยนแปลง กรุณารีเฟรชแล้วลองอีกครั้ง",
        ErrorType.Conflict);

    public static readonly Error EditableInStockRequired = new(
        "Product.EditableInStockRequired",
        "แก้ไขได้เฉพาะสินค้า In-stock ที่เป็นฉบับร่างหรือเผยแพร่แล้ว",
        ErrorType.Conflict);

    public static readonly Error EditablePreOrderRequired = new(
        "Product.EditablePreOrderRequired",
        "แก้ไขได้เฉพาะสินค้าพรีออเดอร์ที่เป็นฉบับร่างหรือเผยแพร่แล้ว",
        ErrorType.Conflict);

    public static readonly Error PublishedPreOrderCapacityLocked = new(
        "Product.PublishedPreOrderCapacityLocked",
        "หลังเผยแพร่แล้วไม่สามารถเปลี่ยนวันปิดรอบได้",
        ErrorType.Conflict);

    public static readonly Error PreOrderCapacityUnavailable = new(
        "Product.PreOrderCapacityUnavailable",
        "ข้อมูลจำนวนพรีออเดอร์ไม่สมบูรณ์ กรุณาติดต่อผู้ดูแลระบบ",
        ErrorType.Failure);

    public static readonly Error PreOrderCapacityBelowAllocated = new(
        "Product.PreOrderCapacityBelowAllocated",
        "จำนวนรับพรีออเดอร์ใหม่ต้องไม่น้อยกว่าจำนวนที่ถูกจอง ชำระ หรือปิดสิทธิ์ไปแล้ว",
        ErrorType.Conflict);

    public static readonly Error CategoryUnavailable = new(
        "Product.CategoryUnavailable",
        "หมวดหมู่สินค้าต้องเป็น Art Toy หรือ Gundam",
        ErrorType.Conflict);

    public static readonly Error BrandUnavailable = new(
        "Product.BrandUnavailable",
        "ไม่พบแบรนด์ที่พร้อมใช้งาน หรือแบรนด์ถูกเก็บถาวรแล้ว",
        ErrorType.Conflict);

    public static readonly Error UniverseUnavailable = new(
        "Product.UniverseUnavailable",
        "ไม่พบจักรวาลที่พร้อมใช้งาน หรือจักรวาลถูกเก็บถาวรแล้ว",
        ErrorType.Conflict);

    public static readonly Error CharactersUnavailable = new(
        "Product.CharactersUnavailable",
        "ตัวละครบางรายการไม่พบหรือไม่ได้อยู่ในจักรวาลที่เลือก",
        ErrorType.Conflict);

    public static readonly Error DuplicateCharacters = new(
        "Product.DuplicateCharacters",
        "กรุณาเลือกตัวละครแต่ละรายการเพียงครั้งเดียว",
        ErrorType.Validation);

    public static readonly Error InvalidMediaPlan = new(
        "Product.InvalidMediaPlan",
        "รายการรูปภาพสินค้าไม่ถูกต้อง กรุณาเลือกและจัดลำดับใหม่",
        ErrorType.Validation);

    public static readonly Error InvalidInput = new(
        "Product.InvalidInput",
        "ข้อมูลสินค้าไม่ถูกต้อง กรุณาตรวจสอบแล้วลองอีกครั้ง",
        ErrorType.Validation);

    public static readonly Error InStockLifecycleRequired = new(
        "Product.InStockLifecycleRequired",
        "คำสั่งนี้ใช้ได้เฉพาะสินค้า In-stock",
        ErrorType.Conflict);

    public static readonly Error PublishDraftRequired = new(
        "Product.PublishDraftRequired",
        "เผยแพร่ได้เฉพาะสินค้าที่ยังเป็นฉบับร่าง",
        ErrorType.Conflict);

    public static readonly Error ArchivePublishedRequired = new(
        "Product.ArchivePublishedRequired",
        "เก็บถาวรได้เฉพาะสินค้าที่เผยแพร่แล้ว",
        ErrorType.Conflict);

    public static readonly Error PublishRequiresImage = new(
        "Product.PublishRequiresImage",
        "กรุณาเพิ่มรูปภาพสินค้าอย่างน้อย 1 รูปก่อนเผยแพร่",
        ErrorType.Validation);

    public static readonly Error PublishBrandUnavailable = new(
        "Product.PublishBrandUnavailable",
        "แบรนด์ต้องเปิดใช้งานและมีรูปภาพก่อนเผยแพร่สินค้า",
        ErrorType.Conflict);

    public static readonly Error PublishUniverseUnavailable = new(
        "Product.PublishUniverseUnavailable",
        "จักรวาลต้องเปิดใช้งานและมีโลโก้ก่อนเผยแพร่สินค้า",
        ErrorType.Conflict);

    public static readonly Error PreOrderClosePassed = new(
        "Product.PreOrderClosePassed",
        "วันปิดรอบพรีออเดอร์ผ่านไปแล้ว กรุณาแก้ไขก่อนเผยแพร่",
        ErrorType.Validation);
}
