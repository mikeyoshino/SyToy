using Microsoft.Net.Http.Headers;
using ToyStore.Application.Common.Files;

namespace ToyStore.Web.Media;

public static class MediaEndpointExtensions
{
    public static IEndpointConventionBuilder MapToyStoreMedia(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var readEndpoint = endpoints.MapMethods(
                "/media/{batchId}/{fileName}",
                [HttpMethods.Get, HttpMethods.Head],
                ServeAsync)
            .AllowAnonymous();

        endpoints.MapMethods(
                "/media/{batchId}/{fileName}",
                [HttpMethods.Post, HttpMethods.Put, HttpMethods.Delete, HttpMethods.Patch, HttpMethods.Options],
                RejectUnsupportedMethod)
            .AllowAnonymous()
            .DisableAntiforgery();

        endpoints.Map(
                "/media/{batchId}/{fileName}",
                RejectUnsupportedMethod)
            .WithOrder(1000)
            .AllowAnonymous()
            .DisableAntiforgery();

        return readEndpoint;
    }

    private static IResult RejectUnsupportedMethod(HttpContext context)
    {
        context.Response.Headers.Allow = "GET, HEAD";
        return Results.Text(
            "Method Not Allowed",
            statusCode: StatusCodes.Status405MethodNotAllowed);
    }

    private static async Task<IResult> ServeAsync(
        string batchId,
        string fileName,
        IFileStorage storage,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var stored = await storage.OpenReadAsync(
            $"{batchId}/{fileName}",
            cancellationToken);
        if (stored is null)
        {
            return Results.NotFound();
        }

        try
        {
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
            return Results.File(
                stored.Content,
                stored.ContentType,
                enableRangeProcessing: true,
                lastModified: stored.LastModifiedUtc,
                entityTag: EntityTagHeaderValue.Parse(stored.EntityTag));
        }
        catch
        {
            await stored.DisposeAsync();
            throw;
        }
    }
}
