using System.Linq.Expressions;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ToyStore.Web.Components.Forms;

namespace ToyStore.UnitTests.Web;

public sealed class StoreAutocompleteMultiSelectRenderingTests
{
    [Fact]
    public async Task FieldRendersControlledChipsAndValidComboboxRelationshipsWithExternalError()
    {
        var universeId = Guid.NewGuid();
        var selected = new Option(Guid.NewGuid(), "Iron Man");
        var model = new FormModel { Characters = [selected] };
        Expression<Func<IReadOnlyList<Option>>> expression = () => model.Characters;
        var editContext = new EditContext(model);
        var messages = new ValidationMessageStore(editContext);
        messages.Add(FieldIdentifier.Create(expression), "กรุณาตรวจสอบตัวละครที่เลือก");

        var html = await RenderFieldAsync<StoreAutocompleteMultiSelect<Guid, Option, Guid>>(
            editContext,
            new()
            {
                [nameof(StoreAutocompleteMultiSelect<Guid, Option, Guid>.Owner)] = universeId,
                [nameof(StoreAutocompleteMultiSelect<Guid, Option, Guid>.Values)] = model.Characters,
                [nameof(StoreAutocompleteMultiSelect<Guid, Option, Guid>.ValuesChanged)] =
                    EventCallback.Factory.Create<IReadOnlyList<Option>>(this, values => model.Characters = values),
                [nameof(StoreAutocompleteMultiSelect<Guid, Option, Guid>.ValuesExpression)] = expression,
                [nameof(StoreAutocompleteMultiSelect<Guid, Option, Guid>.OptionKey)] =
                    (Func<Option, Guid>)(option => option.Id),
                [nameof(StoreAutocompleteMultiSelect<Guid, Option, Guid>.OptionLabel)] =
                    (Func<Option, string>)(option => option.Name),
                [nameof(StoreAutocompleteMultiSelect<Guid, Option, Guid>.OptionId)] =
                    (Func<Option, string>)(option => option.Id.ToString("N")),
                [nameof(StoreAutocompleteMultiSelect<Guid, Option, Guid>.Search)] =
                    (Func<Guid, string, CancellationToken, Task<AutocompleteSearchResult<Option>>>)
                    ((_, _, _) => Task.FromResult(AutocompleteSearchResults.Success<Option>([], false))),
                [nameof(StoreAutocompleteMultiSelect<Guid, Option, Guid>.Copy)] = ThaiCopy(),
                [nameof(StoreAutocompleteMultiSelect<Guid, Option, Guid>.HelpText)] =
                    "ค้นหาและเลือกได้หลายตัวละคร",
            });

        var input = Regex.Match(html, "<input[^>]*class=\"[^\"]*store-autocomplete__input[^\"]*\"[^>]*>").Value;
        Assert.NotEmpty(input);
        Assert.Contains("role=\"combobox\"", input, StringComparison.Ordinal);
        Assert.Contains("aria-autocomplete=\"list\"", input, StringComparison.Ordinal);
        Assert.Contains("aria-expanded=\"false\"", input, StringComparison.Ordinal);
        Assert.Contains("aria-invalid=\"true\"", input, StringComparison.Ordinal);
        Assert.Matches("aria-controls=\"(?<list>[^\"]+)\"", input);
        Assert.Matches("aria-describedby=\"[^\"]+-help [^\"]+-error\"", input);
        Assert.Contains("Iron Man", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"นำ Iron Man ออกจากตัวละคร\"", html, StringComparison.Ordinal);
        Assert.Contains("กรุณาตรวจสอบตัวละครที่เลือก", html, StringComparison.Ordinal);
        Assert.Contains("role=\"status\"", html, StringComparison.Ordinal);
        Assert.Contains("role=\"alert\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExternalValidationStateChangeRerendersComboboxAriaInvalid()
    {
        var model = new FormModel();
        Expression<Func<IReadOnlyList<Option>>> expression = () => model.Characters;
        var editContext = new EditContext(model);
        var messages = new ValidationMessageStore(editContext);
        var parameters = CharacterFieldParameters(model, expression);
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            provider,
            provider.GetRequiredService<ILoggerFactory>());
        var output = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            RenderFragment child = builder =>
            {
                builder.OpenComponent<StoreAutocompleteMultiSelect<Guid, Option, Guid>>(0);
                builder.AddMultipleAttributes(1, parameters.Select(pair =>
                    new KeyValuePair<string, object>(pair.Key, pair.Value!)));
                builder.CloseComponent();
            };
            return await renderer.RenderComponentAsync<CascadingValue<EditContext>>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(CascadingValue<EditContext>.Value)] = editContext,
                    [nameof(CascadingValue<EditContext>.IsFixed)] = true,
                    [nameof(CascadingValue<EditContext>.ChildContent)] = child,
                }));
        });

        var initial = await renderer.Dispatcher.InvokeAsync(output.ToHtmlString);
        Assert.Contains("aria-invalid=\"false\"", initial, StringComparison.Ordinal);
        await renderer.Dispatcher.InvokeAsync(() =>
        {
            messages.Add(FieldIdentifier.Create(expression), "กรุณาตรวจสอบตัวละครที่เลือก");
            editContext.NotifyValidationStateChanged();
        });
        await output.QuiescenceTask;

        var updated = await renderer.Dispatcher.InvokeAsync(output.ToHtmlString);
        Assert.Contains("aria-invalid=\"true\"", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceLocksListboxPseudoOptionImeAndJsFreeFocusModel()
    {
        var source = Source("Components/Forms/StoreAutocompleteMultiSelect.razor");
        var css = Source("Components/Forms/StoreAutocompleteMultiSelect.razor.css");
        var support = string.Join('\n', new[]
        {
            Source("Components/Forms/AutocompleteSearchResult.cs"),
            Source("Components/Forms/AutocompleteMultiSelectCopy.cs"),
        });

        Assert.Contains("role=\"listbox\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-multiselectable=\"true\"", source, StringComparison.Ordinal);
        Assert.Contains("role=\"option\"", source, StringComparison.Ordinal);
        Assert.Contains("State.CanOfferInlineCreate && InlineCreate is not null", source, StringComparison.Ordinal);
        Assert.Contains("aria-selected=\"false\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-disabled", source, StringComparison.Ordinal);
        Assert.Contains("@oncompositionstart", source, StringComparison.Ordinal);
        Assert.Contains("@oncompositionend", source, StringComparison.Ordinal);
        Assert.Contains("OnValidationStateChanged", source, StringComparison.Ordinal);
        Assert.Contains("SetInlineCreateId", source, StringComparison.Ordinal);
        Assert.Contains("SetSearchStatus", source, StringComparison.Ordinal);
        Assert.Contains("@onmousedown:preventDefault", source, StringComparison.Ordinal);
        Assert.Contains("form=\"@DetachedFormId\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("<select", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IJSRuntime", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Character", support, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("min-height: 2.75rem", css, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion: reduce", css, StringComparison.Ordinal);
        Assert.Contains("flex-wrap: wrap", css, StringComparison.Ordinal);
    }

    private static AutocompleteMultiSelectCopy ThaiCopy() => new(
        label: "ตัวละคร",
        placeholder: "ค้นหาตัวละคร",
        loading: "กำลังค้นหาตัวละคร",
        empty: "ไม่พบตัวละคร",
        results: count => $"พบตัวละคร {count} รายการ",
        createLabel: term => $"เพิ่มตัวละคร {term}",
        removeLabel: label => $"นำ {label} ออกจากตัวละคร",
        selected: label => $"เลือก {label} แล้ว",
        removed: label => $"นำ {label} ออกแล้ว");

    private Dictionary<string, object?> CharacterFieldParameters(
        FormModel model,
        Expression<Func<IReadOnlyList<Option>>> expression) =>
        new()
        {
            [nameof(StoreAutocompleteMultiSelect<Guid, Option, Guid>.Owner)] = Guid.NewGuid(),
            [nameof(StoreAutocompleteMultiSelect<Guid, Option, Guid>.Values)] = model.Characters,
            [nameof(StoreAutocompleteMultiSelect<Guid, Option, Guid>.ValuesChanged)] =
                EventCallback.Factory.Create<IReadOnlyList<Option>>(
                    this,
                    values => model.Characters = values),
            [nameof(StoreAutocompleteMultiSelect<Guid, Option, Guid>.ValuesExpression)] = expression,
            [nameof(StoreAutocompleteMultiSelect<Guid, Option, Guid>.OptionKey)] =
                (Func<Option, Guid>)(option => option.Id),
            [nameof(StoreAutocompleteMultiSelect<Guid, Option, Guid>.OptionLabel)] =
                (Func<Option, string>)(option => option.Name),
            [nameof(StoreAutocompleteMultiSelect<Guid, Option, Guid>.OptionId)] =
                (Func<Option, string>)(option => option.Id.ToString("N")),
            [nameof(StoreAutocompleteMultiSelect<Guid, Option, Guid>.Search)] =
                (Func<Guid, string, CancellationToken, Task<AutocompleteSearchResult<Option>>>)
                ((_, _, _) => Task.FromResult(AutocompleteSearchResults.Success<Option>([], false))),
            [nameof(StoreAutocompleteMultiSelect<Guid, Option, Guid>.Copy)] = ThaiCopy(),
        };

    private static async Task<string> RenderFieldAsync<TComponent>(
        EditContext editContext,
        Dictionary<string, object?> parameters)
        where TComponent : IComponent
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            RenderFragment child = builder =>
            {
                builder.OpenComponent<TComponent>(0);
                builder.AddMultipleAttributes(1, parameters.Select(pair =>
                    new KeyValuePair<string, object>(pair.Key, pair.Value!)));
                builder.CloseComponent();
            };
            var output = await renderer.RenderComponentAsync<CascadingValue<EditContext>>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(CascadingValue<EditContext>.Value)] = editContext,
                    [nameof(CascadingValue<EditContext>.IsFixed)] = true,
                    [nameof(CascadingValue<EditContext>.ChildContent)] = child,
                }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }

    private static string Source(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "ToyStore.Web", relativePath));

    private static string RepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory);
             current is not null;
             current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "ToyStore.sln")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate ToyStore.sln.");
    }

    private sealed record Option(Guid Id, string Name);

    private sealed class FormModel
    {
        public IReadOnlyList<Option> Characters { get; set; } = [];
    }
}
