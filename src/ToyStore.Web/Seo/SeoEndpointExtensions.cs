using System.Text;
using System.Xml;
using MediatR;
using ToyStore.Application.Storefront.Catalog;

namespace ToyStore.Web.Seo;

public static class SeoEndpointExtensions
{
    private const int SitemapPageSize = 48;

    public static IEndpointRouteBuilder MapStorefrontSeo(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/robots.txt", (HttpRequest request) => Results.Text(
            BuildRobots(Origin(request)),
            "text/plain; charset=utf-8"));
        endpoints.MapGet("/sitemap.xml", BuildSitemapAsync);
        return endpoints;
    }

    internal static string BuildRobots(string origin) => $$"""
        User-agent: *
        Allow: /
        Disallow: /admin/
        Disallow: /Account/
        Disallow: /account/
        Disallow: /checkout/

        Sitemap: {{origin}}/sitemap.xml
        """;

    private static async Task<IResult> BuildSitemapAsync(
        HttpRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var origin = Origin(request);
        var urls = new List<string>
        {
            $"{origin}/",
            $"{origin}/products",
            $"{origin}/brands",
        };
        var page = 1;
        var totalPages = 1;
        while (page <= totalPages)
        {
            var result = await sender.Send(
                new ListStorefrontProductsQuery(Page: page, PageSize: SitemapPageSize),
                cancellationToken);
            if (result.IsFailure)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            urls.AddRange(result.Value.Items.Select(item => $"{origin}/products/{item.Slug}"));
            totalPages = Math.Max(1, result.Value.TotalPages);
            page++;
        }

        return Results.Text(BuildSitemap(urls), "application/xml; charset=utf-8");
    }

    internal static string BuildSitemap(IEnumerable<string> urls)
    {
        var output = new StringBuilder();
        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
        };
        using (var writer = XmlWriter.Create(output, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");
            foreach (var url in urls.Distinct(StringComparer.Ordinal))
            {
                writer.WriteStartElement("url");
                writer.WriteElementString("loc", url);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        return output.ToString();
    }

    private static string Origin(HttpRequest request) =>
        $"{request.Scheme}://{request.Host}{request.PathBase}".TrimEnd('/');
}
