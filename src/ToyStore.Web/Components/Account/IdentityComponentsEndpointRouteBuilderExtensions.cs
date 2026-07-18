using MediatR;
using Microsoft.AspNetCore.Antiforgery;
using ToyStore.Application.Accounts.Logout;
using ToyStore.Web.Components.Account;

namespace Microsoft.AspNetCore.Routing;

internal static class IdentityComponentsEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapAdditionalIdentityEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints.MapPost("/Account/Logout", async (
                HttpContext httpContext,
                IAntiforgery antiforgery,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                await antiforgery.ValidateRequestAsync(httpContext);
                var form = await httpContext.Request.ReadFormAsync(cancellationToken);
                await sender.Send(new LogoutCommand(), cancellationToken);
                return TypedResults.LocalRedirect(
                    LocalReturnUrl.Normalize(form["returnUrl"].FirstOrDefault()));
            })
            .RequireAuthorization();
    }

}
