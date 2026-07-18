using System.Linq.Expressions;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ToyStore.Web.Components.Forms;

namespace ToyStore.UnitTests.Web;

public sealed class SharedFormRenderingTests
{
    [Fact]
    public async Task TextFieldSupportsStandaloneGetSearchWithoutEditContext()
    {
        var model = new FormModel { Email = "ORDER-001" };
        Expression<Func<string>> expression = () => model.Email;

        var html = await RenderStandaloneAsync<StoreTextField>(new()
        {
            [nameof(StoreTextField.Label)] = "ค้นหาคำสั่งซื้อ",
            [nameof(StoreTextField.Type)] = "search",
            [nameof(StoreTextField.UpdateOnInput)] = true,
            [nameof(StoreTextField.Value)] = model.Email,
            [nameof(StoreTextField.ValueChanged)] = EventCallback.Factory.Create<string>(
                this, value => model.Email = value),
            [nameof(StoreTextField.ValueExpression)] = expression,
            [nameof(StoreTextField.AdditionalAttributes)] = new Dictionary<string, object>
            {
                ["name"] = "q",
            },
        });

        Assert.Contains("name=\"q\"", html, StringComparison.Ordinal);
        Assert.Contains("type=\"search\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"ORDER-001\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TextFieldAssociatesStableLabelHelpAndErrorIdsInsideEditContext()
    {
        var model = new FormModel();
        Expression<Func<string>> expression = () => model.Email;
        var editContext = new EditContext(model);
        var messages = new ValidationMessageStore(editContext);
        messages.Add(editContext.Field(nameof(FormModel.Email)), "รูปแบบอีเมลไม่ถูกต้อง");

        var html = await RenderFieldAsync<StoreTextField>(editContext, new()
        {
            [nameof(StoreTextField.Label)] = "อีเมล",
            [nameof(StoreTextField.HelpText)] = "ใช้สำหรับแจ้งสถานะคำสั่งซื้อ",
            [nameof(StoreTextField.Required)] = true,
            [nameof(StoreTextField.Value)] = model.Email,
            [nameof(StoreTextField.ValueChanged)] = EventCallback.Factory.Create<string>(this, value => model.Email = value),
            [nameof(StoreTextField.ValueExpression)] = expression,
            [nameof(StoreTextField.AdditionalAttributes)] = new Dictionary<string, object>
            {
                ["class"] = "test-class",
                ["data-test"] = "email",
            },
        });

        var inputMatch = Regex.Match(html, "<input[^>]*id=\"(?<id>store-field-[^\"]+)\"");
        Assert.True(inputMatch.Success, "Expected a stable generated field ID.");
        var id = inputMatch.Groups["id"].Value;
        Assert.Matches($"<label[^>]*for=\"{Regex.Escape(id)}\"", html);
        Assert.Contains("aria-required=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-invalid=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains($"aria-describedby=\"{id}-help {id}-error\"", html, StringComparison.Ordinal);
        Assert.Contains($"id=\"{id}-help\"", html, StringComparison.Ordinal);
        Assert.Contains($"id=\"{id}-error\"", html, StringComparison.Ordinal);
        Assert.Contains("store-field__control", html, StringComparison.Ordinal);
        Assert.Contains("test-class", html, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(html, "class=\"[^\"]*test-class", RegexOptions.CultureInvariant));
        Assert.Contains("data-test=\"email\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TextFieldForwardsExplicitIdTypeAutocompletePlaceholderAndDisabled()
    {
        var model = new FormModel();
        Expression<Func<string>> expression = () => model.Password;
        var editContext = new EditContext(model);

        var html = await RenderFieldAsync<StoreTextField>(editContext, new()
        {
            [nameof(StoreTextField.Id)] = "account-password",
            [nameof(StoreTextField.Label)] = "รหัสผ่าน",
            [nameof(StoreTextField.Type)] = "password",
            [nameof(StoreTextField.Autocomplete)] = "current-password",
            [nameof(StoreTextField.Placeholder)] = "กรอกรหัสผ่าน",
            [nameof(StoreTextField.Disabled)] = true,
            [nameof(StoreTextField.Value)] = model.Password,
            [nameof(StoreTextField.ValueChanged)] = EventCallback.Factory.Create<string>(this, value => model.Password = value),
            [nameof(StoreTextField.ValueExpression)] = expression,
        });

        Assert.Contains("id=\"account-password\"", html, StringComparison.Ordinal);
        Assert.Contains("type=\"password\"", html, StringComparison.Ordinal);
        Assert.Contains("autocomplete=\"current-password\"", html, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"กรอกรหัสผ่าน\"", html, StringComparison.Ordinal);
        Assert.Contains("disabled", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NumberFieldForwardsNumericBrowserHints()
    {
        var model = new FormModel();
        Expression<Func<int>> expression = () => model.Quantity;
        var editContext = new EditContext(model);

        var html = await RenderFieldAsync<StoreNumberField<int>>(editContext, new()
        {
            [nameof(StoreNumberField<int>.Label)] = "จำนวน",
            [nameof(StoreNumberField<int>.Value)] = model.Quantity,
            [nameof(StoreNumberField<int>.ValueChanged)] = EventCallback.Factory.Create<int>(this, value => model.Quantity = value),
            [nameof(StoreNumberField<int>.ValueExpression)] = expression,
            [nameof(StoreNumberField<int>.Min)] = "1",
            [nameof(StoreNumberField<int>.Max)] = "10",
            [nameof(StoreNumberField<int>.Step)] = "1",
            [nameof(StoreNumberField<int>.InputMode)] = "numeric",
        });

        Assert.Contains("type=\"number\"", html, StringComparison.Ordinal);
        Assert.Contains("min=\"1\"", html, StringComparison.Ordinal);
        Assert.Contains("max=\"10\"", html, StringComparison.Ordinal);
        Assert.Contains("step=\"1\"", html, StringComparison.Ordinal);
        Assert.Contains("inputmode=\"numeric\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SelectFieldRendersGenericIntegerOptionsWithSelectedSemanticsAndCustomWrapper()
    {
        var model = new FormModel();
        Expression<Func<int>> expression = () => model.Size;
        var editContext = new EditContext(model);

        var html = await RenderFieldAsync<StoreSelectField<int>>(editContext, new()
        {
            [nameof(StoreSelectField<int>.Label)] = "ขนาด",
            [nameof(StoreSelectField<int>.Value)] = model.Size,
            [nameof(StoreSelectField<int>.ValueChanged)] = EventCallback.Factory.Create<int>(this, value => model.Size = value),
            [nameof(StoreSelectField<int>.ValueExpression)] = expression,
            [nameof(StoreSelectField<int>.Options)] = new[]
            {
                new SelectOption<int>(1, "เล็ก"),
                new SelectOption<int>(2, "กลาง"),
                new SelectOption<int>(3, "ใหญ่", Disabled: true),
            },
        });

        Assert.Contains("class=\"store-select\"", html, StringComparison.Ordinal);
        Assert.Contains("<select", html, StringComparison.Ordinal);
        Assert.Contains("<option value=\"1\">เล็ก</option>", html, StringComparison.Ordinal);
        Assert.Matches("<option(?=[^>]*value=\"2\")(?=[^>]*selected)[^>]*>กลาง</option>", html);
        Assert.Matches("<option value=\"3\"[^>]*disabled[^>]*>ใหญ่</option>", html);

        var css = File.ReadAllText(Path.Combine(GetWebRoot(), "wwwroot", "css", "forms.css"));
        Assert.Contains(".store-select::after", css, StringComparison.Ordinal);
        Assert.Contains("pointer-events: none", css, StringComparison.Ordinal);
        Assert.Contains("appearance: none", css, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenericIntegerSelectFormatsAndForwardsOneChangedValue()
    {
        var callbackCount = 0;
        var callbackValue = 0;
#pragma warning disable BL0005 // Direct assignment verifies the component's internal binding seam.
        var field = new StoreSelectField<int>
        {
            Label = "ขนาด",
            ValueChanged = EventCallback.Factory.Create<int>(
                this,
                value =>
                {
                    callbackCount++;
                    callbackValue = value;
                }),
        };
#pragma warning restore BL0005

        Assert.Equal("2", field.FormatOptionValue(2));

        await field.HandleValueChangedAsync(2);

        Assert.Equal(1, callbackCount);
        Assert.Equal(2, callbackValue);
    }

    private static async Task<string> RenderFieldAsync<TComponent>(
        EditContext editContext,
        Dictionary<string, object?> componentParameters)
        where TComponent : IComponent
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));

        await using var serviceProvider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            serviceProvider,
            serviceProvider.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            RenderFragment childContent = builder => RenderComponent<TComponent>(builder, componentParameters);
            var output = await renderer.RenderComponentAsync<CascadingValue<EditContext>>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(CascadingValue<EditContext>.Value)] = editContext,
                    [nameof(CascadingValue<EditContext>.IsFixed)] = true,
                    [nameof(CascadingValue<EditContext>.ChildContent)] = childContent,
                }));

            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }

    private static async Task<string> RenderStandaloneAsync<TComponent>(
        Dictionary<string, object?> componentParameters)
        where TComponent : IComponent
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));

        await using var serviceProvider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            serviceProvider,
            serviceProvider.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<TComponent>(
                ParameterView.FromDictionary(componentParameters));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }

    private static void RenderComponent<TComponent>(
        RenderTreeBuilder builder,
        Dictionary<string, object?> componentParameters)
        where TComponent : IComponent
    {
        builder.OpenComponent<TComponent>(0);
        builder.AddMultipleAttributes(
            1,
            componentParameters.Select(parameter =>
                new KeyValuePair<string, object>(parameter.Key, parameter.Value!)));
        builder.CloseComponent();
    }

    private static string GetWebRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "ToyStore.sln")))
        {
            current = current.Parent;
        }

        return Path.Combine(
            current?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root."),
            "src",
            "ToyStore.Web");
    }

    private sealed class FormModel
    {
        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public int Quantity { get; set; } = 1;

        public int Size { get; set; } = 2;
    }
}
