using System.Text.RegularExpressions;

namespace ToyStore.UnitTests.Web;

public sealed class SharedFormDesignContractTests
{
    [Fact]
    public void SharedFormAndFeedbackSourcesExist()
    {
        var webRoot = GetWebRoot();

        foreach (var relativePath in new[]
        {
            "Components/Forms/StoreTextField.razor",
            "Components/Forms/StoreNumberField.razor",
            "Components/Forms/StoreSelectField.razor",
            "Components/Forms/SelectOption.cs",
            "Components/Forms/FormValidationStore.cs",
            "Components/Feedback/StoreAlert.razor",
            "Components/Feedback/StoreToast.razor",
            "Components/Feedback/StoreDialog.razor",
            "Components/Feedback/StoreDialog.razor.js",
            "Components/Feedback/StoreDrawer.razor",
            "Components/Feedback/StoreSkeleton.razor",
            "wwwroot/css/forms.css",
            "wwwroot/css/feedback.css",
        })
        {
            Assert.True(File.Exists(Path.Combine(webRoot, relativePath)), $"Missing shared UI source: {relativePath}");
        }
    }

    [Fact]
    public void FormStylesLoadAfterFoundationAndBeforeScopedStylesWithoutBootstrap()
    {
        var app = File.ReadAllText(Path.Combine(GetWebRoot(), "Components", "App.razor"));
        var siteIndex = app.IndexOf("css/site.css", StringComparison.Ordinal);
        var formsIndex = app.IndexOf("css/forms.css", StringComparison.Ordinal);
        var feedbackIndex = app.IndexOf("css/feedback.css", StringComparison.Ordinal);
        var scopedIndex = app.IndexOf("ToyStore.Web.styles.css", StringComparison.Ordinal);

        Assert.DoesNotContain("bootstrap", app, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            siteIndex >= 0 && siteIndex < formsIndex && formsIndex < feedbackIndex && feedbackIndex < scopedIndex,
            "Expected site.css, forms.css, feedback.css, then scoped component styles.");
    }

    [Fact]
    public void SelectUsesOneCrossBrowserCustomAppearanceContract()
    {
        var css = File.ReadAllText(Path.Combine(GetWebRoot(), "wwwroot", "css", "forms.css"));
        var source = File.ReadAllText(Path.Combine(GetWebRoot(), "Components", "Forms", "StoreSelectField.razor"));

        Assert.Contains("aria-haspopup=\"listbox\"", source, StringComparison.Ordinal);
        Assert.Contains("role=\"listbox\"", source, StringComparison.Ordinal);
        Assert.Contains("role=\"option\"", source, StringComparison.Ordinal);
        Assert.DoesNotMatch("(?i)<select(?:\\s|>)", source);
        Assert.Matches(@"(?s)\.store-select__trigger\s*\{[^}]*padding-inline-end:", css);
        Assert.Matches(@"(?s)\.store-select::after\s*\{[^}]*pointer-events:\s*none", css);
        Assert.Matches(@"(?s)\.store-field__control:focus-visible\s*\{[^}]*var\(--color-focus\)", css);
        Assert.Matches(@"(?s)\.store-field__control(?:\.invalid|\[aria-invalid=[""']true[""']\])", css);
        Assert.Matches(@"(?s)\.store-field__control:disabled", css);
        Assert.DoesNotContain("appearance:", css, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FieldSourcesExposeTheSharedBindingAndAccessibilityApi()
    {
        var formsRoot = Path.Combine(GetWebRoot(), "Components", "Forms");

        foreach (var fileName in new[] { "StoreTextField.razor", "StoreNumberField.razor", "StoreSelectField.razor" })
        {
            var source = File.ReadAllText(Path.Combine(formsRoot, fileName));
            foreach (var apiName in new[]
            {
                "Label", "HelpText", "Required", "Disabled", "Value", "ValueChanged",
                "ValueExpression", "AdditionalAttributes",
            })
            {
                Assert.Contains(apiName, source, StringComparison.Ordinal);
            }
        }

        var number = File.ReadAllText(Path.Combine(formsRoot, "StoreNumberField.razor"));
        foreach (var apiName in new[] { "Min", "Max", "Step", "InputMode" })
        {
            Assert.Contains(apiName, number, StringComparison.Ordinal);
        }

        var select = File.ReadAllText(Path.Combine(formsRoot, "StoreSelectField.razor"));
        Assert.Contains("@typeparam TValue", select, StringComparison.Ordinal);
        Assert.Contains("Options", select, StringComparison.Ordinal);
    }

    [Fact]
    public void RepeatedAccountTextFieldsUseSharedControlsAndNoBootstrapClassTokensRemain()
    {
        var componentRoot = Path.Combine(GetWebRoot(), "Components");
        var accountRoot = Path.Combine(componentRoot, "Account");

        foreach (var relativePath in new[]
        {
            "Pages/Register.razor",
            "Pages/Login.razor",
            "Pages/Manage/ChangePassword.razor",
        })
        {
            var source = File.ReadAllText(Path.Combine(accountRoot, relativePath));
            Assert.Contains("<StoreTextField", source, StringComparison.Ordinal);
            Assert.DoesNotContain("<InputText", source, StringComparison.Ordinal);
        }

        var bootstrapTokens = new Regex(
            @"^(?:row|container(?:-fluid)?|col(?:-[a-z]+)?(?:-\d+)?|form-(?:control|floating|check|check-input|check-label|select)|btn(?:-[a-z0-9]+)*|w-100|m[trblxy]?-[0-9]+|p[trblxy]?-[0-9]+|d-(?:flex|grid|block|none)|flex-(?:column|row)|gap-[0-9]+|text-(?:danger|secondary)|alert(?:-[a-z]+)?|nav(?:-pills|-item|-link)?)$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        foreach (var path in Directory.GetFiles(componentRoot, "*.razor", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(path);
            foreach (Match classAttribute in Regex.Matches(source, "class=\\\"(?<value>[^\\\"]*)\\\""))
            {
                foreach (var token in classAttribute.Groups["value"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    Assert.False(bootstrapTokens.IsMatch(token), $"Bootstrap class token '{token}' remains in {path}.");
                }
            }
        }
    }

    [Fact]
    public void NativeDialogInteropSynchronizesCloseAndReturnsFocusWithoutDependencies()
    {
        var feedbackRoot = Path.Combine(GetWebRoot(), "Components", "Feedback");
        var dialog = File.ReadAllText(Path.Combine(feedbackRoot, "StoreDialog.razor"));
        var script = File.ReadAllText(Path.Combine(feedbackRoot, "StoreDialog.razor.js"));

        Assert.Contains("IAsyncDisposable", dialog, StringComparison.Ordinal);
        Assert.Contains("HandleNativeClosedAsync", dialog, StringComparison.Ordinal);
        Assert.Contains("DotNetObjectReference", dialog, StringComparison.Ordinal);
        Assert.Contains("JSDisconnectedException", dialog, StringComparison.Ordinal);
        Assert.Contains("addEventListener(\"close\"", script, StringComparison.Ordinal);
        Assert.Contains("document.activeElement", script, StringComparison.Ordinal);
        Assert.Contains("showModal()", script, StringComparison.Ordinal);
        Assert.Contains("dialog.close()", script, StringComparison.Ordinal);
        Assert.Contains("returnFocusElement.focus()", script, StringComparison.Ordinal);
        Assert.DoesNotContain("bootstrap", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DesignSystemExamplesStayPresentationOnly()
    {
        var page = File.ReadAllText(
            Path.Combine(GetWebRoot(), "Components", "Pages", "DesignSystem.razor"));

        Assert.Contains("@page \"/design-system\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("@inject", page, StringComparison.Ordinal);
        Assert.DoesNotContain("ISender", page, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", page, StringComparison.Ordinal);
        Assert.DoesNotContain("ToyStore.Domain", page, StringComparison.Ordinal);
        Assert.DoesNotContain("ToyStore.Infrastructure", page, StringComparison.Ordinal);
    }

    private static string GetWebRoot() => Path.Combine(FindRepositoryRoot(), "src", "ToyStore.Web");

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null && !File.Exists(Path.Combine(current.FullName, "ToyStore.sln")))
        {
            current = current.Parent;
        }

        return current?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
