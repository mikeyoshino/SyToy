namespace ToyStore.UnitTests.Web;

public sealed class SavedAddressCheckoutSourceContractTests
{
    [Fact]
    public void BothCheckoutFlowsSelectSavedAddressesAndConfirmBeforePayment()
    {
        var root = FindRepositoryRoot();
        foreach (var file in new[] { "Checkout.razor", "PreOrderCheckout.razor" })
        {
            var source = File.ReadAllText(Path.Combine(root, "src", "ToyStore.Web",
                "Components", "Pages", file));
            Assert.Contains("<SavedAddressBook", source, StringComparison.Ordinal);
            Assert.Contains("OpenConfirmationAsync", source, StringComparison.Ordinal);
            Assert.Contains("<StoreDialog", source, StringComparison.Ordinal);
            Assert.Contains("จัดส่งไปที่", source, StringComparison.Ordinal);
            Assert.Contains("ConfirmCheckoutAsync", source, StringComparison.Ordinal);
            var saveIndex = source.IndexOf("SaveRequestedAddressAsync", StringComparison.Ordinal);
            var beginCheckoutIndex = source.IndexOf("new Begin", saveIndex, StringComparison.Ordinal);
            Assert.True(saveIndex >= 0 && beginCheckoutIndex > saveIndex,
                $"{file} must save an opted-in new address before beginning checkout.");
        }
    }

    [Fact]
    public void SharedAddressBookExposesFiveAddressLimitAndOwnershipCommands()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "ToyStore.Web", "Components",
            "Checkout", "SavedAddressBook.razor"));
        Assert.Contains("สูงสุด 5 รายการ", source, StringComparison.Ordinal);
        Assert.Contains("ListSavedAddressesQuery", source, StringComparison.Ordinal);
        Assert.Contains("CreateSavedAddressCommand", source, StringComparison.Ordinal);
        Assert.Contains("DeleteSavedAddressCommand", source, StringComparison.Ordinal);
        Assert.Contains("SetDefaultSavedAddressCommand", source, StringComparison.Ordinal);
        Assert.Contains("เพิ่มที่อยู่ใหม่", source, StringComparison.Ordinal);
        Assert.Contains("บันทึกและใช้เป็นที่อยู่เริ่มต้น", source, StringComparison.Ordinal);
        Assert.Contains("SaveRequestedAddressAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("บันทึกที่อยู่นี้", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ToyStore.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
