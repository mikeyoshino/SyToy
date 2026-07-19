using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Storefront;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class StorefrontRenderingTests(PostgreSqlFixture postgreSql)
{
    [Fact]
    public async Task DesignSystemRendersAllThaiFormAndFeedbackExamplesWithoutFeatureDependencies()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var response = await client.GetAsync(
            "/design-system",
            TestContext.Current.CancellationToken);
        var html = WebUtility.HtmlDecode(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        foreach (var thaiExample in new[]
        {
            "ตัวอย่างระบบออกแบบ",
            "อีเมล",
            "จำนวน",
            "ขนาด",
            "ขนาดกลาง",
            "FluentValidation ในชั้น Application เป็นกฎหลัก",
            "ข้อมูลทั่วไป",
            "บันทึกสำเร็จ",
            "โปรดตรวจสอบ",
            "เกิดข้อผิดพลาด",
            "เพิ่มสินค้าลงตะกร้าแล้ว",
            "ยืนยันการทำรายการ",
            "ตัวกรองสินค้า",
            "กำลังโหลดตัวอย่าง",
        })
        {
            Assert.Contains(thaiExample, html, StringComparison.Ordinal);
        }

        Assert.Contains("class=\"store-select", html, StringComparison.Ordinal);
        Assert.Contains("name=\"example.Email\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"example.Quantity\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-haspopup=\"listbox\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<select", html, StringComparison.OrdinalIgnoreCase);
        Assert.Matches(
            "<input(?=[^>]*name=\"example.DisabledNote\")(?=[^>]*disabled)[^>]*>",
            html);
        Assert.Matches(
            "<input(?=[^>]*name=\"invalidExample.Email\")(?=[^>]*aria-invalid=\"true\")(?=[^>]*class=\"[^\"]*invalid)[^>]*>",
            html);
        Assert.Contains("อีเมลตัวอย่างนี้ไม่ถูกต้อง", html, StringComparison.Ordinal);
        Assert.Contains("<dialog", html, StringComparison.Ordinal);
        Assert.Contains("aria-live=\"polite\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("ISender", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HomeRendersTheThaiStoreShellInServerHtml()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var response = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var html = WebUtility.HtmlDecode(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<html lang=\"th\"", html, StringComparison.Ordinal);
        Assert.Contains("<header", html, StringComparison.Ordinal);
        Assert.Contains("<nav", html, StringComparison.Ordinal);
        Assert.Contains("<main id=\"main-content\"", html, StringComparison.Ordinal);
        Assert.Contains("<footer", html, StringComparison.Ordinal);
        Assert.Contains("ข้ามไปยังเนื้อหา", html, StringComparison.Ordinal);
        Assert.Contains("หน้าหลัก", html, StringComparison.Ordinal);
        Assert.Contains("สินค้า", html, StringComparison.Ordinal);
        Assert.Contains("พรีออเดอร์", html, StringComparison.Ordinal);
        Assert.Contains("สินค้าพร้อมส่ง", html, StringComparison.Ordinal);
        Assert.Contains("แบรนด์", html, StringComparison.Ordinal);
        Assert.Contains("เข้าสู่ระบบ", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"เปิดตะกร้าสินค้า มีสินค้า 0 ชิ้น\"", html, StringComparison.Ordinal);
        Assert.Contains("ตะกร้าของคุณ", html, StringComparison.Ordinal);
        Assert.Contains("ตะกร้ายังว่างอยู่", html, StringComparison.Ordinal);
        Assert.Contains("ช้อปปิ้งต่อ", html, StringComparison.Ordinal);
        Assert.Contains("ชำระเงินอย่างปลอดภัย", html, StringComparison.Ordinal);
        AssertAnchor(html, "/", "หน้าหลัก");
        AssertAnchor(html, "/products", "สินค้า");
        AssertAnchor(html, "/products?type=pre-order", "พรีออเดอร์");
        AssertAnchor(html, "/products?type=in-stock", "สินค้าพร้อมส่ง");
        AssertAnchor(html, "/brands", "แบรนด์");
        AssertAnchor(html, "/contact", "ติดต่อเรา");
        AssertAnchor(html, "/Account/Login", "เข้าสู่ระบบ");
        Assert.DoesNotContain("href=\"/cart\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("sidebar", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Hello, world!", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">About<", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Counter<", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Weather<", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HomeComposesThaiStorefrontDisplayContentInServerHtml()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var response = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var html = WebUtility.HtmlDecode(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<title>SY TOY | อาร์ตทอยและกันดั้ม พร้อมส่งและพรีออเดอร์</title>", html, StringComparison.Ordinal);
        Assert.Contains("<meta name=\"description\"", html, StringComparison.Ordinal);
        Assert.Contains("<link rel=\"canonical\" href=\"http://localhost/\"", html, StringComparison.Ordinal);
        Assert.Contains("<meta property=\"og:url\" content=\"http://localhost/\"", html, StringComparison.Ordinal);
        Assert.Contains("<script type=\"application/ld+json\">", html, StringComparison.Ordinal);
        Assert.Contains("\"@type\":\"WebSite\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("// ความคิดสร้างสรรค์ไร้ขีดจำกัด", html, StringComparison.Ordinal);
        Assert.DoesNotContain("ของเล่นดีไซน์จัด", html, StringComparison.Ordinal);
        Assert.DoesNotContain("อาร์ตทอยที่เติมคาแรกเตอร์ให้ทุกพื้นที่", html, StringComparison.Ordinal);
        Assert.Contains("สินค้าพรีออเดอร์อาร์ตทอยและกันดั้มจาก SY TOYS", html, StringComparison.Ordinal);
        Assert.Contains("สินค้าแนะนำ", html, StringComparison.Ordinal);
        Assert.Contains("คอลเลกชันที่น่าสำรวจ", html, StringComparison.Ordinal);
        Assert.Matches(
            @"<section\b[^>]*aria-labelledby=""featured-products-title""[^>]*>\s*<header[^>]*>\s*<h2[^>]*id=""featured-products-title""[^>]*>สินค้าแนะนำ</h2>\s*<a[^>]*>ดูทั้งหมด",
            html);
        Assert.Matches(
            @"<section\b[^>]*aria-labelledby=""collections-title""[^>]*>\s*<header[^>]*>\s*<h2[^>]*id=""collections-title""[^>]*>คอลเลกชันที่น่าสำรวจ</h2>\s*<a[^>]*>สำรวจทั้งหมด",
            html);
        Assert.DoesNotMatch(
            @"<div\b[^>]*id=""(?:featured-products-title|collections-title)""",
            html);

        Assert.Contains("class=\"product-gallery", html, StringComparison.Ordinal);
        Assert.DoesNotContain("ลาบูบู้ นักผจญภัย", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/categories\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/journal", html, StringComparison.Ordinal);
        Assert.Contains("สินค้าคัดสรร", html, StringComparison.Ordinal);
        Assert.Contains("ชำระเงินปลอดภัย", html, StringComparison.Ordinal);
        Assert.Contains("จัดส่งพร้อมติดตาม", html, StringComparison.Ordinal);
        Assert.Contains("พร้อมดูแล", html, StringComparison.Ordinal);
        Assert.Contains("ชำระง่าย ปลอดภัย และเลือกได้", html, StringComparison.Ordinal);
        Assert.Contains("PromptPay", html, StringComparison.Ordinal);
        Assert.Contains("บัตรเครดิตและเดบิต", html, StringComparison.Ordinal);
        Assert.Contains("https://www.facebook.com/sytoysofficial/", html, StringComparison.Ordinal);
        Assert.DoesNotContain("เรื่องเล่าจากโลกของเล่น", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data:image", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("base64", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fonts.googleapis.com", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cdn.", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RobotsAndDynamicSitemapArePublicAndUseTheRequestOrigin()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var robotsResponse = await client.GetAsync(
            "/robots.txt",
            TestContext.Current.CancellationToken);
        var robots = await robotsResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var sitemapResponse = await client.GetAsync(
            "/sitemap.xml",
            TestContext.Current.CancellationToken);
        var sitemap = await sitemapResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, robotsResponse.StatusCode);
        Assert.StartsWith("text/plain", robotsResponse.Content.Headers.ContentType?.MediaType, StringComparison.Ordinal);
        Assert.Contains("Sitemap: http://localhost/sitemap.xml", robots, StringComparison.Ordinal);
        Assert.Contains("Disallow: /admin/", robots, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, sitemapResponse.StatusCode);
        Assert.Equal("application/xml", sitemapResponse.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<loc>http://localhost/</loc>", sitemap, StringComparison.Ordinal);
        Assert.Contains("<loc>http://localhost/products</loc>", sitemap, StringComparison.Ordinal);
        Assert.Contains("<loc>http://localhost/contact</loc>", sitemap, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/counter", "Counter")]
    [InlineData("/weather", "Weather")]
    [InlineData("/auth", "You are authenticated")]
    public async Task RemovedSampleRoutesRenderTheThaiNotFoundPage(
        string path,
        string removedCopy)
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        var html = WebUtility.HtmlDecode(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("ไม่พบหน้าที่คุณต้องการ", html, StringComparison.Ordinal);
        Assert.Contains("<header", html, StringComparison.Ordinal);
        Assert.Contains("<main id=\"main-content\"", html, StringComparison.Ordinal);
        Assert.Contains("<footer", html, StringComparison.Ordinal);
        Assert.DoesNotContain(removedCopy, html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginRendersThaiAccountContentInsideTheStoreShell()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var response = await client.GetAsync(
            "/Account/Login",
            TestContext.Current.CancellationToken);
        var html = WebUtility.HtmlDecode(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<header", html, StringComparison.Ordinal);
        Assert.Contains("<main id=\"main-content\"", html, StringComparison.Ordinal);
        Assert.Contains("<footer", html, StringComparison.Ordinal);
        Assert.Contains("เข้าสู่ระบบ", html, StringComparison.Ordinal);
        Assert.Contains("อีเมล", html, StringComparison.Ordinal);
        Assert.Contains("รหัสผ่าน", html, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(html, "<main"));
        Assert.DoesNotContain(">About<", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/products", "/products")]
    [InlineData("/products?type=pre-order", "/products?type=pre-order")]
    [InlineData("/products?type=in-stock", "/products?type=in-stock")]
    public async Task DesktopProductNavigationMarksOnlyTheExactPathAndQueryAsCurrent(
        string requestPath,
        string expectedCurrentHref)
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var response = await client.GetAsync(
            requestPath,
            TestContext.Current.CancellationToken);
        var html = WebUtility.HtmlDecode(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertCurrentNavigationLink(
            html,
            "class=\"store-header__desktop-nav\"",
            expectedCurrentHref);
    }

    [Fact]
    public async Task SearchRendersMobileNavigationHubInServerHtml()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var response = await client.GetAsync(
            "/search",
            TestContext.Current.CancellationToken);
        var html = WebUtility.HtmlDecode(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("store-header__mobile-menu", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"เมนูร้านค้า\"", html, StringComparison.Ordinal);
        AssertAnchor(html, "/brands", "สินค้า");
        AssertAnchor(html, "/", "หน้าหลัก");
        AssertAnchor(html, "/products", "สินค้าทั้งหมด");
        AssertAnchor(html, "/products?type=pre-order", "เปิดพรีออเดอร์");
        AssertAnchor(html, "/products?type=in-stock", "พร้อมส่ง");
        AssertAnchor(html, "/brands", "แบรนด์ทั้งหมด");
        AssertAnchor(html, "/contact", "ติดต่อ SY TOYS");
        AssertAnchor(html, "/Account/Login", "เข้าสู่ระบบหรือสมัครสมาชิก");
    }

    [Fact]
    public async Task ContactRendersAccessibleContactDetailsSeoAndExternalChannels()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var response = await client.GetAsync(
            "/contact",
            TestContext.Current.CancellationToken);
        var html = WebUtility.HtmlDecode(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<title>ติดต่อ SY TOYS | ร้านอาร์ตทอยและกันดั้มเชียงใหม่</title>", html, StringComparison.Ordinal);
        Assert.Contains("<link rel=\"canonical\" href=\"http://localhost/contact\"", html, StringComparison.Ordinal);
        Assert.Contains("\"@type\":\"ContactPage\"", html, StringComparison.Ordinal);
        Assert.Contains("47/27 หมู่ 1", html, StringComparison.Ordinal);
        Assert.Contains("อำเภอสารภี จังหวัดเชียงใหม่ 50140", html, StringComparison.Ordinal);
        AssertAnchor(html, "tel:+66982540399", "098-254-0399");
        AssertAnchor(html, "mailto:sytoys.official@gmail.com", "sytoys.official@gmail.com");
        AssertAnchor(html, "https://www.facebook.com/sytoysofficial/", "SY TOYS Official");
        Assert.Contains("rel=\"noopener noreferrer\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProductTabRendersBrandFirstDirectorySeparateFromProductResults()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var response = await client.GetAsync(
            "/brands",
            TestContext.Current.CancellationToken);
        var html = WebUtility.HtmlDecode(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("เลือกแบรนด์ก่อนดูสินค้า", html, StringComparison.Ordinal);
        Assert.Contains("แบรนด์", html, StringComparison.Ordinal);
        AssertAnchor(html, "/brands", "สินค้า");
        Assert.DoesNotContain("catalog-page__results", html, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var startIndex = 0;

        while ((startIndex = source.IndexOf(value, startIndex, StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += value.Length;
        }

        return count;
    }

    private static void AssertAnchor(string html, string href, string thaiAccessibleText)
    {
        var anchors = Regex.Matches(
            html,
            @"<a\b(?<attributes>[^>]*)>(?<content>.*?)</a>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        var matchingAnchor = anchors.Cast<Match>().Any(match =>
            Regex.IsMatch(
                match.Groups["attributes"].Value,
                $"""\bhref\s*=\s*["']{Regex.Escape(href)}["']""",
                RegexOptions.IgnoreCase)
            && (match.Groups["content"].Value.Contains(
                    thaiAccessibleText,
                    StringComparison.Ordinal)
                || Regex.IsMatch(
                    match.Groups["attributes"].Value,
                    $"""\baria-label\s*=\s*["'][^"']*{Regex.Escape(thaiAccessibleText)}[^"']*["']""",
                    RegexOptions.IgnoreCase)));

        Assert.True(
            matchingAnchor,
            $"Expected an anchor to '{href}' with Thai text or accessible label containing '{thaiAccessibleText}'.");
    }

    private static void AssertCurrentNavigationLink(
        string html,
        string navigationAttribute,
        string expectedHref)
    {
        var navigation = Regex.Match(
            html,
            $"""<nav\b[^>]*{Regex.Escape(navigationAttribute)}[^>]*>(?<content>.*?)</nav>""",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        Assert.True(navigation.Success, $"Could not find navigation with {navigationAttribute}.");

        var anchors = Regex.Matches(
            navigation.Groups["content"].Value,
            @"<a\b(?<attributes>[^>]*)>(?<content>.*?)</a>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var currentAnchors = anchors.Cast<Match>()
            .Where(match =>
                Regex.IsMatch(
                    match.Groups["attributes"].Value,
                    @"\bclass\s*=\s*[""'][^""']*\bactive\b[^""']*[""']",
                    RegexOptions.IgnoreCase)
                || Regex.IsMatch(
                    match.Groups["attributes"].Value,
                    @"\baria-current\s*=\s*[""']page[""']",
                    RegexOptions.IgnoreCase))
            .ToArray();

        var currentAnchor = Assert.Single(currentAnchors);
        Assert.Matches(
            $"""\bhref\s*=\s*["']{Regex.Escape(expectedHref)}["']""",
            currentAnchor.Groups["attributes"].Value);
        Assert.Matches(
            @"\baria-current\s*=\s*[""']page[""']",
            currentAnchor.Groups["attributes"].Value);
    }
}
