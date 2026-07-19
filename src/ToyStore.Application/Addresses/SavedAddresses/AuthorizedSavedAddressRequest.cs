using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Addresses.SavedAddresses;

public abstract record AuthorizedSavedAddressRequest<TResponse> : AuthorizedResultRequest<TResponse>
{
    public override string RequiredPolicy => PolicyNames.CanUseCustomerCart;
}
