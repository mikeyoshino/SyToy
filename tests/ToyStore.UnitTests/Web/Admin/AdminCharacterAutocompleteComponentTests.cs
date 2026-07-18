using System.Linq.Expressions;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ToyStore.Application.Characters;
using ToyStore.Web.Components.Admin.Primitives;
using ToyStore.Web.Components.Forms;

namespace ToyStore.UnitTests.Web.Admin;

public sealed class AdminCharacterAutocompleteComponentTests
{
    [Fact]
    public async Task EmptyUniverseDisablesTheThaiFieldWithoutSendingAndKeepsControlledContract()
    {
        var model = new FormModel();
        Expression<Func<IReadOnlyList<CharacterOption>>> expression = () => model.Characters;
        var adapter = AdapterThatMustNotSend();

        var html = await RenderAsync(
            adapter,
            new EditContext(model),
            new()
            {
                [nameof(AdminCharacterAutocomplete.UniverseId)] = Guid.Empty,
                [nameof(AdminCharacterAutocomplete.Values)] = model.Characters,
                [nameof(AdminCharacterAutocomplete.ValuesChanged)] =
                    EventCallback.Factory.Create<IReadOnlyList<CharacterOption>>(
                        this,
                        values => model.Characters = values),
                [nameof(AdminCharacterAutocomplete.ValuesExpression)] = expression,
            });

        var input = Regex.Match(
            html,
            "<input[^>]*class=\"[^\"]*store-autocomplete__input[^\"]*\"[^>]*>").Value;
        Assert.NotEmpty(input);
        Assert.Contains("disabled", input, StringComparison.Ordinal);
        Assert.Contains("ตัวละคร", html, StringComparison.Ordinal);
        Assert.Contains("เลือกจักรวาลก่อน", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ActiveUniverseRendersAuthoritativeSelectedCharacterAndThaiCopy()
    {
        var universeId = Guid.NewGuid();
        var selected = new CharacterOption(Guid.NewGuid(), universeId, "Iron Man");
        var model = new FormModel { Characters = [selected] };
        Expression<Func<IReadOnlyList<CharacterOption>>> expression = () => model.Characters;

        var html = await RenderAsync(
            AdapterThatMustNotSend(),
            new EditContext(model),
            new()
            {
                [nameof(AdminCharacterAutocomplete.UniverseId)] = universeId,
                [nameof(AdminCharacterAutocomplete.Values)] = model.Characters,
                [nameof(AdminCharacterAutocomplete.ValuesChanged)] =
                    EventCallback.Factory.Create<IReadOnlyList<CharacterOption>>(
                        this,
                        values => model.Characters = values),
                [nameof(AdminCharacterAutocomplete.ValuesExpression)] = expression,
            });

        Assert.Contains("Iron Man", html, StringComparison.Ordinal);
        Assert.Contains("ค้นหาตัวละคร", html, StringComparison.Ordinal);
        Assert.Contains("นำ Iron Man ออกจากตัวละคร", html, StringComparison.Ordinal);
        Assert.DoesNotContain("เลือกจักรวาลก่อน", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<select", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FakeCallbacksOverrideInjectedAdapterWithoutSending()
    {
        var universeId = Guid.NewGuid();
        var model = new FormModel();
        Expression<Func<IReadOnlyList<CharacterOption>>> expression = () => model.Characters;

        var html = await RenderAsync(
            AdapterThatMustNotSend(),
            new EditContext(model),
            new()
            {
                [nameof(AdminCharacterAutocomplete.UniverseId)] = universeId,
                [nameof(AdminCharacterAutocomplete.Values)] = model.Characters,
                [nameof(AdminCharacterAutocomplete.ValuesChanged)] =
                    EventCallback.Factory.Create<IReadOnlyList<CharacterOption>>(
                        this,
                        values => model.Characters = values),
                [nameof(AdminCharacterAutocomplete.ValuesExpression)] = expression,
                [nameof(AdminCharacterAutocomplete.SearchOverride)] =
                    new Func<Guid, string, CancellationToken,
                        Task<AutocompleteSearchResult<CharacterOption>>>(
                            (_, _, _) => Task.FromResult(
                                AutocompleteSearchResults.Success<CharacterOption>(
                                    [],
                                    offerInlineCreate: true))),
                [nameof(AdminCharacterAutocomplete.InlineCreateOverride)] =
                    new Func<Guid, string, CancellationToken,
                        Task<AutocompleteCreateResult<CharacterOption>>>(
                            (_, _, _) => Task.FromResult(
                                AutocompleteCreateResults.Success(
                                    new CharacterOption(Guid.NewGuid(), universeId, "Fake")))),
            });

        Assert.Contains("store-autocomplete__input", html, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceKeepsWrapperThinReusableAndFreeOfRoutesDomainAndCharacterManagementUi()
    {
        var wrapper = Source("Components/Admin/Shared/AdminCharacterAutocomplete.razor");
        var adapter = Source("Components/Admin/Shared/AdminCharacterAutocompleteAdapter.cs");
        var program = Source("Program.cs");
        var sources = wrapper + adapter;
        var allAdminRazor = Directory
            .EnumerateFiles(
                Path.Combine(RepositoryRoot(), "src", "ToyStore.Web", "Components", "Admin"),
                "*.razor",
                SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();

        Assert.Contains("<StoreAutocompleteMultiSelect", wrapper, StringComparison.Ordinal);
        Assert.Contains("Owner=\"@UniverseId\"", wrapper, StringComparison.Ordinal);
        Assert.Contains("Values=\"@Values\"", wrapper, StringComparison.Ordinal);
        Assert.Contains("ValuesChanged=\"@ValuesChanged\"", wrapper, StringComparison.Ordinal);
        Assert.Contains("Search=\"@EffectiveSearch\"", wrapper, StringComparison.Ordinal);
        Assert.Contains("InlineCreate=\"@EffectiveInlineCreate\"", wrapper, StringComparison.Ordinal);
        Assert.Contains("SearchOverride ?? Adapter.SearchAsync", wrapper, StringComparison.Ordinal);
        Assert.Contains("InlineCreateOverride ?? Adapter.CreateAsync", wrapper, StringComparison.Ordinal);
        Assert.Contains("ISender", adapter, StringComparison.Ordinal);
        Assert.Contains("AdminRequestExecutor", adapter, StringComparison.Ordinal);
        Assert.Contains("HasExactMatch", adapter, StringComparison.Ordinal);
        Assert.Contains(
            "AddScoped<AdminCharacterAutocompleteAdapter>()",
            program,
            StringComparison.Ordinal);
        Assert.DoesNotContain("@page", wrapper, StringComparison.Ordinal);
        Assert.DoesNotContain("ToyStore.Domain", sources, StringComparison.Ordinal);
        Assert.DoesNotContain("CatalogNameNormalizer", sources, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", sources, StringComparison.Ordinal);
        Assert.DoesNotContain("AdminDataTable", sources, StringComparison.Ordinal);
        Assert.All(allAdminRazor, source =>
        {
            Assert.DoesNotContain("@page \"/admin/characters", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("href=\"/admin/characters", source, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static AdminCharacterAutocompleteAdapter AdapterThatMustNotSend() => new(
        (_, _) => throw new InvalidOperationException("Rendering must not search."),
        (_, _) => throw new InvalidOperationException("Rendering must not create."),
        new AdminRequestExecutor(new NullLogger<AdminRequestExecutor>()));

    private static async Task<string> RenderAsync(
        AdminCharacterAutocompleteAdapter adapter,
        EditContext editContext,
        Dictionary<string, object?> parameters)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton(adapter);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            provider,
            provider.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            RenderFragment child = builder =>
            {
                builder.OpenComponent<AdminCharacterAutocomplete>(0);
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

    private sealed class NullLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }

    private sealed class FormModel
    {
        public IReadOnlyList<CharacterOption> Characters { get; set; } = [];
    }
}
