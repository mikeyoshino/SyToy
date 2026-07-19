using System.Text.RegularExpressions;

namespace ToyStore.UnitTests.Web;

public sealed class StorefrontDesignContractTests
{
    [Fact]
    public void StorefrontDesignTokensExposeTheDocumentedReusableFamilies()
    {
        var tokensPath = Path.Combine(GetWebRoot(), "wwwroot", "css", "tokens.css");

        Assert.True(File.Exists(tokensPath), $"Missing storefront design tokens: {tokensPath}");

        var css = File.ReadAllText(tokensPath);

        AssertCssCustomProperty(css, "color-bg", "#f8f8f6");
        AssertCssCustomProperty(css, "color-surface", "#fff(?:fff)?");
        AssertCssCustomProperty(css, "color-ink", "#111111");
        AssertCssCustomProperty(css, "color-muted", "#686864");
        AssertCssCustomProperty(css, "color-line", "#dededb");
        AssertCssCustomProperty(css, "color-accent", "#dfff29");
        AssertCssCustomProperty(css, "color-danger", "#b42318");
        AssertCssCustomProperty(css, "color-success", "#16794a");
        AssertCssCustomProperty(css, "color-focus", "#2563eb");

        AssertCssCustomProperty(css, "font-family-base", "\"Noto Sans Thai\", system-ui, -apple-system, sans-serif");
        AssertCssCustomProperty(css, "font-size-display", "4.5rem");
        AssertCssCustomProperty(css, "font-size-display-mobile", "3rem");
        AssertCssCustomProperty(css, "font-size-h1", "3rem");
        AssertCssCustomProperty(css, "font-size-h1-mobile", "2.375rem");
        AssertCssCustomProperty(css, "font-size-h2", "2.25rem");
        AssertCssCustomProperty(css, "font-size-h2-mobile", "1.75rem");
        AssertCssCustomProperty(css, "font-size-h3", "1.375rem");
        AssertCssCustomProperty(css, "font-size-h3-mobile", "1.125rem");
        AssertCssCustomProperty(css, "font-size-body-lg", "1.125rem");
        AssertCssCustomProperty(css, "font-size-body", "1rem");
        AssertCssCustomProperty(css, "font-size-label", ".8125rem");
        AssertCssCustomProperty(css, "font-size-caption", ".75rem");
        AssertCssCustomProperty(css, "font-weight-regular", "400");
        AssertCssCustomProperty(css, "font-weight-medium", "500");
        AssertCssCustomProperty(css, "font-weight-bold", "700");
        AssertCssCustomProperty(css, "font-weight-extrabold", "800");
        AssertCssCustomProperty(css, "font-weight-black", "900");
        AssertCssCustomProperty(css, "line-height-tight", "1.15");
        AssertCssCustomProperty(css, "line-height-heading", "1.25");
        AssertCssCustomProperty(css, "line-height-body", "1.6");

        for (var index = 1; index <= 18; index++)
        {
            var remValue = index / 4m;
            var expected = remValue < 1 ? $"{remValue:.##}".TrimStart('0') : $"{remValue:.##}";
            AssertCssCustomProperty(css, $"space-{index}", $"{Regex.Escape(expected)}rem");
        }

        AssertCssCustomProperty(css, "radius-sm", ".25rem");
        AssertCssCustomProperty(css, "radius-md", ".5rem");
        AssertCssCustomProperty(css, "radius-lg", ".75rem");
        AssertCssCustomProperty(css, "radius-pill", "999rem");
        AssertCssCustomProperty(css, "shadow-sm", "0 1px 2px rgb\\(17 17 17 / 8%\\)");
        AssertCssCustomProperty(css, "shadow-md", "0 8px 24px rgb\\(17 17 17 / 12%\\)");
        AssertCssCustomProperty(css, "shadow-lg", "0 18px 45px rgb\\(17 17 17 / 16%\\)");

        AssertCssCustomProperty(css, "content-max", "112rem");
        AssertCssCustomProperty(css, "gutter-mobile", "1rem");
        AssertCssCustomProperty(css, "gutter-tablet", "1.5rem");
        AssertCssCustomProperty(css, "gutter-desktop", "2rem");
        AssertCssCustomProperty(css, "mobile-nav-height", "4.5rem");
        AssertCssCustomProperty(css, "section-space-min", @"var\(--space-11\)");
        AssertCssCustomProperty(css, "section-space-max", @"var\(--space-18\)");
        AssertCssCustomProperty(css, "z-header", "100");
        AssertCssCustomProperty(css, "z-dropdown", "200");
        AssertCssCustomProperty(css, "z-overlay", "300");
        AssertCssCustomProperty(css, "z-toast", "400");
        AssertCssCustomProperty(css, "duration-fast", "160ms");
        AssertCssCustomProperty(css, "duration-normal", "260ms");
        AssertCssCustomProperty(css, "duration-slow", "600ms");
        AssertCssCustomProperty(css, "ease-standard", "cubic-bezier\\(\\.2,\\s*\\.8,\\s*\\.2,\\s*1\\)");
    }

    [Fact]
    public void StorefrontFoundationProvidesResponsiveAndAccessibleGlobalStyles()
    {
        var sitePath = Path.Combine(GetWebRoot(), "wwwroot", "css", "site.css");

        Assert.True(File.Exists(sitePath), $"Missing storefront foundation stylesheet: {sitePath}");

        var css = File.ReadAllText(sitePath);

        Assert.Matches(@"(?s)\*,\s*\*::before,\s*\*::after\s*\{[^}]*box-sizing:\s*border-box", css);
        Assert.Matches(@"(?s)body\s*\{[^}]*margin:\s*0[^}]*background(?:-color)?:\s*var\(--color-bg\)[^}]*color:\s*var\(--color-ink\)[^}]*font-family:\s*var\(--font-family-base\)[^}]*font-size:\s*var\(--font-size-body\)[^}]*line-height:\s*var\(--line-height-body\)", css);
        Assert.Matches(@"(?s)(?:a|button|input|textarea|select)[^{]*\{[^}]*font:\s*inherit", css);
        Assert.Matches(@"(?s)img[^{]*\{[^}]*display:\s*block[^}]*max-width:\s*100%", css);
        Assert.Matches(@"(?s)h1,\s*h2,\s*h3[^\{]*\{[^}]*margin:\s*0", css);
        Assert.Matches(@"(?s)p\s*\{[^}]*margin:\s*0", css);

        Assert.Matches(@"(?s)\.store-container\s*\{[^}]*width:\s*100%[^}]*max-width:\s*var\(--content-max\)[^}]*padding-inline:\s*var\(--gutter-mobile\)", css);
        Assert.Matches(@"(?s)\.visually-hidden\s*\{[^}]*position:\s*absolute[^}]*clip-path:\s*inset\(50%\)", css);
        Assert.Matches(@"(?s)\.skip-link\s*\{[^}]*position:\s*fixed[^}]*transform:\s*translateY\(-[^)]*\)", css);
        Assert.Matches(@"(?s)\.skip-link:focus(?:-visible)?\s*\{[^}]*transform:\s*translateY\(0\)", css);
        Assert.Matches(@"(?s):focus-visible\s*\{[^}]*outline:[^;}]*var\(--color-focus\)[^}]*box-shadow:[^;}]*var\(--color-surface\)[^;}]*var\(--color-focus\)", css);
        Assert.Matches(@"(?s)\.touch-target\s*\{[^}]*min-width:\s*2\.75rem[^}]*min-height:\s*2\.75rem", css);

        Assert.Matches(@"(?s)@media\s*\(min-width:\s*35rem\)\s*\{.*?\.store-container\s*\{[^}]*padding-inline:\s*var\(--gutter-tablet\)", css);
        Assert.Matches(@"(?s)@media\s*\(min-width:\s*65\.625rem\)\s*\{.*?\.store-container\s*\{[^}]*padding-inline:\s*var\(--gutter-desktop\)", css);
        Assert.Matches(@"(?s)@media\s*\(prefers-reduced-motion:\s*reduce\)\s*\{.*?animation-duration:\s*\.01ms.*?animation-iteration-count:\s*1.*?transition-duration:\s*\.01ms.*?scroll-behavior:\s*auto", css);
        Assert.Matches(@"(?s)@media\s*\(prefers-reduced-motion:\s*reduce\)\s*\{.*?animation-delay:\s*0s\s*!important", css);
        Assert.Matches(@"(?s)@media\s*\(prefers-reduced-motion:\s*reduce\)\s*\{.*?transition-delay:\s*0s\s*!important", css);
    }

    [Fact]
    public void ActiveUnhandledErrorBannerUsesThaiCopyAndActions()
    {
        var layout = File.ReadAllText(
            Path.Combine(GetWebRoot(), "Components", "Layout", "MainLayout.razor"));

        Assert.Contains("id=\"blazor-error-ui\"", layout, StringComparison.Ordinal);
        Assert.Contains("เกิดข้อผิดพลาดที่ไม่คาดคิด", layout, StringComparison.Ordinal);
        Assert.Matches(
            @"(?s)<a[^>]*class=""reload""[^>]*>\s*โหลดหน้าใหม่\s*</a>",
            layout);
        Assert.Matches(
            @"(?s)<button\s+type=""button""\s+class=""dismiss""\s+aria-label=""ปิดข้อความแจ้งเตือน"">",
            layout);
        Assert.DoesNotMatch(@"(?s)<span[^>]*class=""dismiss""[^>]*>", layout);
        Assert.DoesNotContain("An unhandled error", layout, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Reload<", layout, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppLoadsSharedStylesWithoutBootstrapInDeterministicOrder()
    {
        var app = File.ReadAllText(Path.Combine(GetWebRoot(), "Components", "App.razor"));
        var fontsIndex = app.IndexOf("css/fonts.css", StringComparison.Ordinal);
        var tokensIndex = app.IndexOf("css/tokens.css", StringComparison.Ordinal);
        var siteIndex = app.IndexOf("css/site.css", StringComparison.Ordinal);
        var formsIndex = app.IndexOf("css/forms.css", StringComparison.Ordinal);
        var feedbackIndex = app.IndexOf("css/feedback.css", StringComparison.Ordinal);
        var appCssIndex = app.IndexOf("app.css", StringComparison.Ordinal);
        var scopedIndex = app.IndexOf("ToyStore.Web.styles.css", StringComparison.Ordinal);

        Assert.DoesNotContain("bootstrap", app, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            fontsIndex < tokensIndex &&
            tokensIndex < siteIndex &&
            siteIndex < formsIndex &&
            formsIndex < feedbackIndex &&
            feedbackIndex < appCssIndex &&
            siteIndex < appCssIndex &&
            appCssIndex < scopedIndex,
            "Expected stylesheet order: fonts, tokens, site, forms, feedback, app, scoped styles.");
        Assert.DoesNotContain("fonts.googleapis.com", app, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fonts.gstatic.com", app, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BlazorHooksUseStorefrontTokensAndThaiErrorCopyWithoutScaffoldArtifacts()
    {
        var css = File.ReadAllText(Path.Combine(GetWebRoot(), "wwwroot", "app.css"));

        Assert.DoesNotContain("Helvetica", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("base64", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("An error has occurred.", css, StringComparison.Ordinal);
        Assert.DoesNotContain("#26b050", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#e50000", css, StringComparison.OrdinalIgnoreCase);

        Assert.Contains(".valid.modified", css, StringComparison.Ordinal);
        Assert.Contains("var(--color-success)", css, StringComparison.Ordinal);
        Assert.Contains(".invalid", css, StringComparison.Ordinal);
        Assert.Contains(".validation-message", css, StringComparison.Ordinal);
        Assert.Contains("var(--color-danger)", css, StringComparison.Ordinal);
        Assert.Contains(".blazor-error-boundary", css, StringComparison.Ordinal);
        Assert.Matches(@"(?s)\.blazor-error-boundary::after\s*\{[^}]*content:\s*[""']เกิดข้อผิดพลาด กรุณาลองใหม่อีกครั้ง[""']", css);
    }

    [Fact]
    public void NotoSansThaiIsSelfHostedAndPreloadedWithoutRuntimeFontCdn()
    {
        var repositoryRoot = FindRepositoryRoot();
        var webRoot = Path.Combine(repositoryRoot, "src", "ToyStore.Web");
        var fontsRoot = Path.Combine(webRoot, "wwwroot", "fonts");
        var thaiFontPath = Path.Combine(fontsRoot, "noto-sans-thai-thai-wght-normal.woff2");
        var latinFontPath = Path.Combine(fontsRoot, "noto-sans-thai-latin-wght-normal.woff2");
        var licensePath = Path.Combine(fontsRoot, "OFL-Noto-Sans-Thai.txt");
        var fontsCssPath = Path.Combine(webRoot, "wwwroot", "css", "fonts.css");
        var appPath = Path.Combine(webRoot, "Components", "App.razor");

        Assert.True(File.Exists(thaiFontPath), $"Missing Thai font asset: {thaiFontPath}");
        Assert.True(new FileInfo(thaiFontPath).Length > 20_000, "Thai font asset is unexpectedly small.");
        Assert.True(File.Exists(latinFontPath), $"Missing Latin font asset: {latinFontPath}");
        Assert.True(new FileInfo(latinFontPath).Length > 20_000, "Latin font asset is unexpectedly small.");
        Assert.True(File.Exists(licensePath), $"Missing font license: {licensePath}");

        var fontsCss = File.ReadAllText(fontsCssPath);
        var app = File.ReadAllText(appPath);

        Assert.Contains("font-weight: 100 900", fontsCss, StringComparison.Ordinal);
        Assert.Contains("rel=\"preload\"", app, StringComparison.Ordinal);
        Assert.Contains("noto-sans-thai-thai-wght-normal.woff2", app, StringComparison.Ordinal);
        Assert.DoesNotContain("fonts.googleapis.com", app, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fonts.gstatic.com", app, StringComparison.OrdinalIgnoreCase);

        var thaiCssSource = Regex.Match(
            fontsCss,
            """src:\s*url\(["'](?<url>[^"']*noto-sans-thai-thai-wght-normal\.woff2)["']\)""");
        var preloadTag = Regex.Match(app, """<link\s+rel="preload"[^>]*>""");

        Assert.True(thaiCssSource.Success, "Could not find the Thai font source in fonts.css.");
        Assert.True(preloadTag.Success, "Could not find the font preload tag in App.razor.");

        var preloadHref = Regex.Match(preloadTag.Value, "href=\"(?<url>[^\"]+)\"");
        Assert.True(preloadHref.Success, "Could not read the font preload href in App.razor.");

        var origin = new Uri("https://toystore.invalid/");
        var fontsCssRequestUri = new Uri(origin, "css/fonts.css");
        var cssFontRequestPath = new Uri(
            fontsCssRequestUri,
            thaiCssSource.Groups["url"].Value).AbsolutePath;
        var preloadRequestPath = new Uri(origin, preloadHref.Groups["url"].Value).AbsolutePath;

        Assert.Equal(cssFontRequestPath, preloadRequestPath);
    }

    [Fact]
    public void StoreShellSourcesProvideResponsiveSemanticNavigationWithoutScaffoldFiles()
    {
        var webRoot = GetWebRoot();
        var layoutRoot = Path.Combine(webRoot, "Components", "Layout");
        var pagesRoot = Path.Combine(webRoot, "Components", "Pages");
        var layout = File.ReadAllText(Path.Combine(layoutRoot, "MainLayout.razor"));
        var header = File.ReadAllText(Path.Combine(layoutRoot, "StoreHeader.razor"));
        var headerCss = File.ReadAllText(Path.Combine(layoutRoot, "StoreHeader.razor.css"));
        var footer = File.ReadAllText(Path.Combine(layoutRoot, "StoreFooter.razor"));
        var storeNavLinkPath = Path.Combine(layoutRoot, "StoreNavLink.razor");

        Assert.Contains("<header", header, StringComparison.Ordinal);
        Assert.Contains("<nav", header, StringComparison.Ordinal);
        Assert.DoesNotContain("<details", header, StringComparison.Ordinal);
        Assert.DoesNotContain("store-header__mobile-menu", header, StringComparison.Ordinal);
        Assert.DoesNotContain("<NavLink", header, StringComparison.Ordinal);
        Assert.True(File.Exists(storeNavLinkPath), $"Missing exact storefront navigation link: {storeNavLinkPath}");
        var storeNavLink = File.ReadAllText(storeNavLinkPath);
        Assert.Contains("aria-current", storeNavLink, StringComparison.Ordinal);
        Assert.Contains("PathAndQuery", storeNavLink, StringComparison.Ordinal);
        Assert.Contains("<AuthorizeView>", header, StringComparison.Ordinal);
        Assert.Contains("<AntiforgeryToken />", header, StringComparison.Ordinal);
        Assert.Contains("method=\"post\"", header, StringComparison.Ordinal);
        Assert.Contains("Account/Logout", header, StringComparison.Ordinal);
        Assert.Contains("ReturnUrl", header, StringComparison.Ordinal);
        Assert.Contains("IDisposable", header, StringComparison.Ordinal);
        Assert.Contains("@media (min-width: 56.25rem)", headerCss, StringComparison.Ordinal);
        Assert.Contains("min-height: 2.75rem", headerCss, StringComparison.Ordinal);
        Assert.Matches(
            @"(?s)\.store-header__brand\s*\{[^}]*display:\s*inline-flex[^}]*min-height:\s*2\.75rem[^}]*align-items:\s*center",
            headerCss);
        Assert.DoesNotContain("store-header__mobile-menu", headerCss, StringComparison.Ordinal);
        Assert.Contains("justify-content: center", headerCss, StringComparison.Ordinal);
        Assert.Contains("<footer", footer, StringComparison.Ordinal);
        Assert.Contains("href=\"#main-content\"", layout, StringComparison.Ordinal);
        Assert.Contains("<main id=\"main-content\" tabindex=\"-1\">", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("sidebar", layout, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("About", layout, StringComparison.OrdinalIgnoreCase);

        Assert.False(File.Exists(Path.Combine(layoutRoot, "NavMenu.razor")));
        Assert.False(File.Exists(Path.Combine(layoutRoot, "NavMenu.razor.css")));
        Assert.False(File.Exists(Path.Combine(pagesRoot, "Counter.razor")));
        Assert.False(File.Exists(Path.Combine(pagesRoot, "Weather.razor")));
        Assert.False(File.Exists(Path.Combine(pagesRoot, "Auth.razor")));
    }

    [Fact]
    public void MobileStorefrontShellUsesFiveDestinationSafeAreaNavigation()
    {
        var layoutRoot = Path.Combine(GetWebRoot(), "Components", "Layout");
        var layout = File.ReadAllText(Path.Combine(layoutRoot, "MainLayout.razor"));
        var navigation = File.ReadAllText(Path.Combine(layoutRoot, "StoreMobileNavigation.razor"));
        var styles = File.ReadAllText(Path.Combine(layoutRoot, "StoreMobileNavigation.razor.css"));

        Assert.Contains("<StoreMobileNavigation />", layout, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: repeat(5", styles, StringComparison.Ordinal);
        Assert.Contains("env(safe-area-inset-bottom)", styles, StringComparison.Ordinal);
        Assert.Contains("position: fixed", styles, StringComparison.Ordinal);
        Assert.Contains("@media (min-width: 56.25rem)", styles, StringComparison.Ordinal);
        Assert.Contains("CartDrawer.TotalQuantity", navigation, StringComparison.Ordinal);
        Assert.Contains("CartDrawer.Open", navigation, StringComparison.Ordinal);
        Assert.Contains("aria-current", navigation, StringComparison.Ordinal);
        Assert.Contains("เลือกแบรนด์สินค้า", navigation, StringComparison.Ordinal);
        Assert.Contains("href=\"/brands\"", navigation, StringComparison.Ordinal);
        Assert.Contains("<span>สินค้า</span>", navigation, StringComparison.Ordinal);
        Assert.Contains("IsProductSection", navigation, StringComparison.Ordinal);
        Assert.Contains("PolicyNames.CanViewCustomerOrders", navigation, StringComparison.Ordinal);
        Assert.Contains("HideNavigation", navigation, StringComparison.Ordinal);
        Assert.Contains("/checkout", navigation, StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveCustomerRazorFilesDoNotContainEnglishScaffoldCopyOrEmbeddedImages()
    {
        var componentRoot = Path.Combine(GetWebRoot(), "Components");
        var activeRazor = Directory.GetFiles(componentRoot, "*.razor", SearchOption.AllDirectories);

        foreach (var path in activeRazor)
        {
            var source = File.ReadAllText(path);
            Assert.DoesNotContain("Hello, world!", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Welcome to your new app", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Development Mode", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Rejoining the server", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("data:image", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ReusableStorefrontPresentationComponentsAndModelsExist()
    {
        var storefrontRoot = Path.Combine(GetWebRoot(), "Components", "Storefront");
        var componentNames = new[]
        {
            "HeroShowcase",
            "SectionHeader",
            "ProductCard",
            "ProductGallery",
            "CollectionCard",
            "JournalFeature",
            "TrustBenefits",
        };

        foreach (var componentName in componentNames)
        {
            Assert.True(
                File.Exists(Path.Combine(storefrontRoot, $"{componentName}.razor")),
                $"Missing reusable storefront component: {componentName}.razor");
            Assert.True(
                File.Exists(Path.Combine(storefrontRoot, $"{componentName}.razor.css")),
                $"Missing reusable storefront component styles: {componentName}.razor.css");
        }

        foreach (var modelName in new[] { "ProductCardModel", "CollectionCardModel", "JournalStoryModel" })
        {
            var modelPath = Path.Combine(storefrontRoot, "Models", $"{modelName}.cs");
            Assert.True(File.Exists(modelPath), $"Missing storefront presentation model: {modelPath}");
            Assert.Contains("sealed record", File.ReadAllText(modelPath), StringComparison.Ordinal);
        }

        var productModel = File.ReadAllText(
            Path.Combine(storefrontRoot, "Models", "ProductCardModel.cs"));
        Assert.Contains("string Name", productModel, StringComparison.Ordinal);
        Assert.Contains("string Brand", productModel, StringComparison.Ordinal);
        Assert.Contains("string PriceLabel", productModel, StringComparison.Ordinal);
        Assert.Contains("string TypeLabel", productModel, StringComparison.Ordinal);
        Assert.Contains("string? ImageUrl", productModel, StringComparison.Ordinal);
        Assert.Contains("string ProductUrl", productModel, StringComparison.Ordinal);
        Assert.Contains("StorefrontSaleType SaleType", productModel, StringComparison.Ordinal);
        Assert.Contains("StorefrontOfferState OfferState", productModel, StringComparison.Ordinal);
    }

    [Fact]
    public void StorefrontPresentationSourcesRemainDatabaseAndBusinessIndependent()
    {
        var webRoot = GetWebRoot();
        var storefrontRoot = Path.Combine(webRoot, "Components", "Storefront");
        Assert.True(Directory.Exists(storefrontRoot), $"Missing storefront folder: {storefrontRoot}");

        var sourcePaths = Directory.GetFiles(storefrontRoot, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".razor.css", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Append(Path.Combine(webRoot, "Components", "Layout", "StoreFooter.razor"))
            .Append(Path.Combine(webRoot, "Components", "Layout", "StoreFooter.razor.css"))
            .ToArray();
        Assert.NotEmpty(sourcePaths);

        foreach (var path in sourcePaths)
        {
            var source = File.ReadAllText(path);
            Assert.DoesNotContain("ToyStore.Domain", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ToyStore.Infrastructure", source, StringComparison.Ordinal);
            Assert.DoesNotContain("EntityFrameworkCore", source, StringComparison.Ordinal);
            Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ISender", source, StringComparison.Ordinal);
            if (!path.EndsWith("ProductCard.razor", StringComparison.OrdinalIgnoreCase)
                && !path.EndsWith("HeroShowcase.razor", StringComparison.OrdinalIgnoreCase)
                && !path.EndsWith("StorefrontProductGallery.razor", StringComparison.OrdinalIgnoreCase))
                Assert.DoesNotContain("@inject", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("data:image", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("base64", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotMatch(
                """(?i)<img\b[^>]*\bsrc\s*=\s*["']https?://""",
                source);
        }
    }

    [Fact]
    public void ProductPresentationProvidesNormalLoadingEmptyErrorAndDisabledSemantics()
    {
        var storefrontRoot = Path.Combine(GetWebRoot(), "Components", "Storefront");
        var gallery = File.ReadAllText(Path.Combine(storefrontRoot, "ProductGallery.razor"));
        var state = File.ReadAllText(Path.Combine(storefrontRoot, "StorefrontContentState.cs"));
        var productCard = File.ReadAllText(Path.Combine(storefrontRoot, "ProductCard.razor"));

        Assert.Contains("Normal", state, StringComparison.Ordinal);
        Assert.Contains("Loading", state, StringComparison.Ordinal);
        Assert.Contains("Empty", state, StringComparison.Ordinal);
        Assert.Contains("Error", state, StringComparison.Ordinal);
        Assert.Contains("EventCallback", gallery, StringComparison.Ordinal);
        Assert.Contains("role=\"status\"", gallery, StringComparison.Ordinal);
        Assert.Contains("role=\"alert\"", gallery, StringComparison.Ordinal);
        Assert.Contains("@key", gallery, StringComparison.Ordinal);
        Assert.Contains("IsLoading", productCard, StringComparison.Ordinal);
        Assert.Contains("aria-hidden=\"true\"", productCard, StringComparison.Ordinal);
        Assert.Contains("disabled", productCard, StringComparison.Ordinal);
        Assert.Contains("สินค้าหมดชั่วคราว", productCard, StringComparison.Ordinal);
        Assert.Contains("<StoreButton OnClick=\"RetryAsync\"", gallery, StringComparison.Ordinal);
        Assert.Contains("ดูรายละเอียด @Model.Name โดย", productCard, StringComparison.Ordinal);
        Assert.Contains("เพิ่ม @Model.Name ลงตะกร้า", productCard, StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveStorefrontSourcesDoNotEmbedOrRemotelyLoadImages()
    {
        var webRoot = GetWebRoot();
        var sourcePaths = Directory.GetFiles(
                Path.Combine(webRoot, "Components", "Storefront"),
                "*",
                SearchOption.AllDirectories)
            .Append(Path.Combine(webRoot, "Components", "Pages", "Home.razor"))
            .Where(path => path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var path in sourcePaths)
        {
            var source = File.ReadAllText(path);
            Assert.DoesNotContain("data:image", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("base64", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("http://", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("https://", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void StorefrontResponsiveCssPreservesTargetGridsAndTouchActions()
    {
        var storefrontRoot = Path.Combine(GetWebRoot(), "Components", "Storefront");
        var galleryCss = File.ReadAllText(Path.Combine(storefrontRoot, "ProductGallery.razor.css"));
        var productCss = File.ReadAllText(Path.Combine(storefrontRoot, "ProductCard.razor.css"));

        Assert.Contains("grid-template-columns: repeat(2, minmax(0, 1fr))", galleryCss, StringComparison.Ordinal);
        Assert.Contains("@media (min-width: 35rem)", galleryCss, StringComparison.Ordinal);
        Assert.Contains("repeat(3, minmax(0, 1fr))", galleryCss, StringComparison.Ordinal);
        Assert.Contains("@media (min-width: 56.25rem)", galleryCss, StringComparison.Ordinal);
        Assert.Contains("@media (min-width: 80rem)", galleryCss, StringComparison.Ordinal);
        Assert.Contains("repeat(4, minmax(0, 1fr))", galleryCss, StringComparison.Ordinal);
        Assert.Contains(".product-gallery--large", galleryCss, StringComparison.Ordinal);
        Assert.Matches(
            @"(?s)@media \(min-width: 80rem\).*?\.product-gallery--large\s*\{[^}]*grid-template-columns:\s*repeat\(4, minmax\(0, 1fr\)\)",
            galleryCss);
        Assert.Contains("min-height: 2.75rem", productCss, StringComparison.Ordinal);
        Assert.Contains("var(--duration-normal)", productCss, StringComparison.Ordinal);
        Assert.Contains("var(--ease-standard)", productCss, StringComparison.Ordinal);
    }

    [Fact]
    public void TrustBenefitDescriptionsUseReadableCustomerBodyText()
    {
        var trustCss = File.ReadAllText(Path.Combine(
            GetWebRoot(),
            "Components",
            "Storefront",
            "TrustBenefits.razor.css"));

        Assert.Matches(
            @"(?s)small\s*\{[^}]*font-size:\s*(?:var\(--font-size-body-min\)|\.875rem|14px)",
            trustCss);
        Assert.DoesNotMatch(@"(?s)small\s*\{[^}]*font-size:\s*\.8125rem", trustCss);
    }

    [Fact]
    public void StorefrontCardAndHeroTypographyRemainReadableForThaiText()
    {
        var storefrontRoot = Path.Combine(GetWebRoot(), "Components", "Storefront");
        var hero = File.ReadAllText(Path.Combine(storefrontRoot, "HeroShowcase.razor"));
        var heroCss = File.ReadAllText(Path.Combine(storefrontRoot, "HeroShowcase.razor.css"));
        var productCss = File.ReadAllText(Path.Combine(storefrontRoot, "ProductCard.razor.css"));
        var journalCss = File.ReadAllText(Path.Combine(storefrontRoot, "JournalFeature.razor.css"));

        Assert.Contains("<h1 id=\"hero-showcase-title\" class=\"visually-hidden\">", hero, StringComparison.Ordinal);
        Assert.Contains("สินค้าพรีออเดอร์อาร์ตทอยและกันดั้ม", hero, StringComparison.Ordinal);
        Assert.Matches(
            @"(?s)\.hero-showcase__copy h2,[^{]*\{[^}]*font-size:\s*2rem[^}]*line-height:\s*1\.08",
            heroCss);
        Assert.Matches(
            @"(?s)\.product-card__body h3\s*\{[^}]*font-size:\s*1rem[^}]*font-weight:\s*300",
            productCss);
        Assert.DoesNotContain("product-card__badge--scale", productCss, StringComparison.Ordinal);
        Assert.Matches(
            @"(?s)h3\s*\{[^}]*font-size:\s*var\(--font-size-h3-mobile\)",
            journalCss);
    }

    [Fact]
    public void PreOrderHeroIsOneSlideEditorialCarouselWithAccessibleMotionControls()
    {
        var storefrontRoot = Path.Combine(GetWebRoot(), "Components", "Storefront");
        var hero = File.ReadAllText(Path.Combine(storefrontRoot, "HeroShowcase.razor"));
        var styles = File.ReadAllText(Path.Combine(storefrontRoot, "HeroShowcase.razor.css"));
        var script = File.ReadAllText(Path.Combine(storefrontRoot, "HeroShowcase.razor.js"));

        Assert.Contains("product.SaleType == StorefrontSaleType.PreOrder", hero, StringComparison.Ordinal);
        Assert.Contains(".Take(5)", hero, StringComparison.Ordinal);
        Assert.Contains("data-carousel-track", hero, StringComparison.Ordinal);
        Assert.Contains("data-carousel-previous", hero, StringComparison.Ordinal);
        Assert.Contains("data-carousel-next", hero, StringComparison.Ordinal);
        Assert.Contains("data-carousel-autoplay", hero, StringComparison.Ordinal);
        Assert.Contains("ดูรายละเอียดพรีออเดอร์", hero, StringComparison.Ordinal);
        Assert.Contains("เปิดพรีออเดอร์", hero, StringComparison.Ordinal);
        Assert.Contains("scroll-snap-type: inline mandatory", styles, StringComparison.Ordinal);
        Assert.Contains("scrollbar-width: none", styles, StringComparison.Ordinal);
        Assert.Contains("::-webkit-scrollbar", styles, StringComparison.Ordinal);
        Assert.Contains("grid-template-areas: \"copy media\"", styles, StringComparison.Ordinal);
        Assert.Contains("object-position: center 20%", styles, StringComparison.Ordinal);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", styles, StringComparison.Ordinal);
        Assert.Contains("module.InvokeVoidAsync(\"initialize\", rootElement, 3000)", hero, StringComparison.Ordinal);
        Assert.Contains("window.setTimeout", script, StringComparison.Ordinal);
        Assert.Contains("scrollTo", script, StringComparison.Ordinal);
        Assert.Contains("pointerdown", script, StringComparison.Ordinal);
        Assert.Contains("focusin", script, StringComparison.Ordinal);
        Assert.Contains("IntersectionObserver", script, StringComparison.Ordinal);
        Assert.Contains("visibilitychange", script, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion: reduce", script, StringComparison.Ordinal);
        Assert.Contains("slide.inert = inactive", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SectionHeaderOwnsTheIdUsedByHomeRegions()
    {
        var webRoot = GetWebRoot();
        var sectionHeader = File.ReadAllText(Path.Combine(
            webRoot,
            "Components",
            "Storefront",
            "SectionHeader.razor"));
        var home = File.ReadAllText(Path.Combine(webRoot, "Components", "Pages", "Home.razor"));

        Assert.Contains("HeadingId", sectionHeader, StringComparison.Ordinal);
        Assert.Contains("<h2 id=\"@HeadingId\">", sectionHeader, StringComparison.Ordinal);
        Assert.Contains("<h3 id=\"@HeadingId\">", sectionHeader, StringComparison.Ordinal);
        Assert.Contains("HeadingId=\"featured-products-title\"", home, StringComparison.Ordinal);
        Assert.Contains("HeadingId=\"collections-title\"", home, StringComparison.Ordinal);
        Assert.DoesNotContain("<div id=\"featured-products-title\">", home, StringComparison.Ordinal);
        Assert.DoesNotContain("<div id=\"collections-title\">", home, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ToyStore.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate ToyStore.sln.");
    }

    private static string GetWebRoot() =>
        Path.Combine(FindRepositoryRoot(), "src", "ToyStore.Web");

    private static void AssertCssCustomProperty(string css, string name, string valuePattern)
    {
        Assert.Matches($@"--{Regex.Escape(name)}:\s*{valuePattern}\s*;", css);
    }
}
