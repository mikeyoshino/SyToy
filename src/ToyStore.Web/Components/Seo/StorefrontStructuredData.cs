using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ToyStore.Application.Storefront.Catalog;

namespace ToyStore.Web.Components.Seo;

public static class StorefrontStructuredData
{
    private const string SchemaContext = "https://schema.org";
    private static readonly TimeZoneInfo BangkokTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    public static string BuildHome(Uri canonicalUrl, Uri logoUrl, Uri? primaryImageUrl, string description)
    {
        ArgumentNullException.ThrowIfNull(canonicalUrl);
        ArgumentNullException.ThrowIfNull(logoUrl);

        var root = Root(canonicalUrl);
        var organizationId = $"{root}#organization";
        var websiteId = $"{root}#website";
        var webpage = new JsonObject
        {
            ["@type"] = "WebPage",
            ["@id"] = $"{root}#webpage",
            ["url"] = root,
            ["name"] = "SY TOY | อาร์ตทอยและกันดั้ม พร้อมส่งและพรีออเดอร์",
            ["description"] = Normalize(description),
            ["inLanguage"] = "th-TH",
            ["isPartOf"] = Reference(websiteId),
            ["about"] = Reference(organizationId),
        };
        if (primaryImageUrl is not null)
        {
            webpage["primaryImageOfPage"] = new JsonObject
            {
                ["@type"] = "ImageObject",
                ["url"] = primaryImageUrl.ToString(),
            };
        }

        return Serialize(
            new JsonObject
            {
                ["@type"] = "Organization",
                ["@id"] = organizationId,
                ["name"] = "SY TOYS",
                ["alternateName"] = "SY TOY",
                ["url"] = root,
                ["logo"] = new JsonObject
                {
                    ["@type"] = "ImageObject",
                    ["url"] = logoUrl.ToString(),
                    ["width"] = 256,
                    ["height"] = 256,
                },
            },
            new JsonObject
            {
                ["@type"] = "WebSite",
                ["@id"] = websiteId,
                ["url"] = root,
                ["name"] = "SY TOYS",
                ["alternateName"] = "SY TOY",
                ["inLanguage"] = "th-TH",
                ["publisher"] = Reference(organizationId),
            },
            webpage);
    }

    public static string BuildProduct(
        StorefrontProductDetail product,
        Uri canonicalUrl,
        string description)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(canonicalUrl);

        var root = Root(canonicalUrl);
        var organizationId = $"{root}#organization";
        var productId = $"{canonicalUrl}#product";
        var imageUrls = product.Images
            .OrderByDescending(image => image.IsPrimary)
            .ThenBy(image => image.SortOrder)
            .Select(image => new Uri(canonicalUrl, image.Url).ToString())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var offer = new JsonObject
        {
            ["@type"] = "Offer",
            ["url"] = canonicalUrl.ToString(),
            ["price"] = product.Price,
            ["priceCurrency"] = "THB",
            ["availability"] = Availability(product.OfferState),
            ["itemCondition"] = "https://schema.org/NewCondition",
            ["seller"] = Reference(organizationId),
        };
        if (product.OfferState == StorefrontOfferState.PreOrderOpen
            && product.PreOrderCloseAtUtc is { } closeAtUtc)
        {
            offer["priceValidUntil"] = TimeZoneInfo.ConvertTime(closeAtUtc, BangkokTimeZone)
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        if (product.SaleType == StorefrontSaleType.InStock)
        {
            offer["shippingDetails"] = FreeThailandShipping();
        }

        var productNode = new JsonObject
        {
            ["@type"] = "Product",
            ["@id"] = productId,
            ["name"] = product.DisplayName,
            ["description"] = Normalize(description),
            ["category"] = product.CategoryName,
            ["brand"] = new JsonObject
            {
                ["@type"] = "Brand",
                ["name"] = product.BrandName,
            },
            ["offers"] = offer,
        };
        if (!string.IsNullOrWhiteSpace(product.EnglishName))
        {
            productNode["alternateName"] = product.EnglishName;
        }
        if (imageUrls.Length != 0)
        {
            productNode["image"] = new JsonArray(
                imageUrls.Select(url => (JsonNode?)JsonValue.Create(url)).ToArray());
        }
        if (!string.IsNullOrWhiteSpace(product.ModelScale))
        {
            productNode["additionalProperty"] = new JsonArray(
                new JsonObject
                {
                    ["@type"] = "PropertyValue",
                    ["name"] = "สเกลโมเดล",
                    ["value"] = product.ModelScale,
                });
        }

        var breadcrumbId = $"{canonicalUrl}#breadcrumb";
        var webpage = new JsonObject
        {
            ["@type"] = "WebPage",
            ["@id"] = $"{canonicalUrl}#webpage",
            ["url"] = canonicalUrl.ToString(),
            ["name"] = product.DisplayName,
            ["description"] = Normalize(description),
            ["inLanguage"] = "th-TH",
            ["breadcrumb"] = Reference(breadcrumbId),
            ["mainEntity"] = Reference(productId),
        };
        if (imageUrls.Length != 0)
        {
            webpage["primaryImageOfPage"] = new JsonObject
            {
                ["@type"] = "ImageObject",
                ["url"] = imageUrls[0],
            };
        }

        return Serialize(
            new JsonObject
            {
                ["@type"] = "Organization",
                ["@id"] = organizationId,
                ["name"] = "SY TOYS",
                ["url"] = root,
            },
            webpage,
            new JsonObject
            {
                ["@type"] = "BreadcrumbList",
                ["@id"] = breadcrumbId,
                ["itemListElement"] = new JsonArray(
                    Breadcrumb(1, "หน้าหลัก", root),
                    Breadcrumb(2, "สินค้า", new Uri(canonicalUrl, "/products").ToString()),
                    Breadcrumb(3, product.DisplayName, canonicalUrl.ToString())),
            },
            productNode);
    }

    public static string BuildContact(Uri canonicalUrl, Uri logoUrl, string facebookUrl)
    {
        ArgumentNullException.ThrowIfNull(canonicalUrl);
        ArgumentNullException.ThrowIfNull(logoUrl);

        var root = Root(canonicalUrl);
        var organizationId = $"{root}#organization";
        var breadcrumbId = $"{canonicalUrl}#breadcrumb";
        return Serialize(
            new JsonObject
            {
                ["@type"] = "Organization",
                ["@id"] = organizationId,
                ["name"] = "SY TOYS",
                ["url"] = root,
                ["logo"] = logoUrl.ToString(),
                ["telephone"] = "+66-98-254-0399",
                ["email"] = "sytoys.official@gmail.com",
                ["sameAs"] = new JsonArray(facebookUrl),
                ["address"] = new JsonObject
                {
                    ["@type"] = "PostalAddress",
                    ["streetAddress"] = "47/27 หมู่ 1",
                    ["addressLocality"] = "สารภี",
                    ["addressRegion"] = "เชียงใหม่",
                    ["postalCode"] = "50140",
                    ["addressCountry"] = "TH",
                },
                ["contactPoint"] = new JsonObject
                {
                    ["@type"] = "ContactPoint",
                    ["telephone"] = "+66-98-254-0399",
                    ["email"] = "sytoys.official@gmail.com",
                    ["contactType"] = "customer service",
                    ["availableLanguage"] = new JsonArray("Thai"),
                },
            },
            new JsonObject
            {
                ["@type"] = "ContactPage",
                ["@id"] = $"{canonicalUrl}#webpage",
                ["url"] = canonicalUrl.ToString(),
                ["name"] = "ติดต่อ SY TOYS",
                ["description"] = "ข้อมูลติดต่อ ที่อยู่ โทรศัพท์ อีเมล และ Facebook ของ SY TOYS",
                ["inLanguage"] = "th-TH",
                ["about"] = Reference(organizationId),
                ["breadcrumb"] = Reference(breadcrumbId),
            },
            new JsonObject
            {
                ["@type"] = "BreadcrumbList",
                ["@id"] = breadcrumbId,
                ["itemListElement"] = new JsonArray(
                    Breadcrumb(1, "หน้าหลัก", root),
                    Breadcrumb(2, "ติดต่อเรา", canonicalUrl.ToString())),
            });
    }

    public static string Normalize(string value) =>
        string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string Availability(StorefrontOfferState state) => state switch
    {
        StorefrontOfferState.InStockAvailable => "https://schema.org/InStock",
        StorefrontOfferState.PreOrderOpen => "https://schema.org/PreOrder",
        StorefrontOfferState.PreOrderFull => "https://schema.org/SoldOut",
        _ => "https://schema.org/OutOfStock",
    };

    private static JsonObject FreeThailandShipping() => new()
    {
        ["@type"] = "OfferShippingDetails",
        ["shippingDestination"] = new JsonObject
        {
            ["@type"] = "DefinedRegion",
            ["addressCountry"] = "TH",
        },
        ["shippingRate"] = new JsonObject
        {
            ["@type"] = "MonetaryAmount",
            ["value"] = 0,
            ["currency"] = "THB",
        },
        ["deliveryTime"] = new JsonObject
        {
            ["@type"] = "ShippingDeliveryTime",
            ["transitTime"] = new JsonObject
            {
                ["@type"] = "QuantitativeValue",
                ["minValue"] = 2,
                ["maxValue"] = 5,
                ["unitCode"] = "DAY",
            },
        },
    };

    private static JsonObject Breadcrumb(int position, string name, string url) => new()
    {
        ["@type"] = "ListItem",
        ["position"] = position,
        ["name"] = name,
        ["item"] = url,
    };

    private static JsonObject Reference(string id) => new() { ["@id"] = id };

    private static string Root(Uri url) => url.GetLeftPart(UriPartial.Authority).TrimEnd('/') + "/";

    private static string Serialize(params JsonObject[] nodes)
    {
        var graph = new JsonArray();
        foreach (var node in nodes)
        {
            graph.Add(node);
        }

        return new JsonObject
        {
            ["@context"] = SchemaContext,
            ["@graph"] = graph,
        }.ToJsonString(JsonOptions);
    }
}
