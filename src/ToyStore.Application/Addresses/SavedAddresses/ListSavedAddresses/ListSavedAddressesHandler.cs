using MediatR;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Addresses.SavedAddresses.ListSavedAddresses;

public sealed class ListSavedAddressesHandler(ISavedAddressStore store)
    : IRequestHandler<ListSavedAddressesQuery, Result<IReadOnlyList<SavedAddressView>>>
{
    public async Task<Result<IReadOnlyList<SavedAddressView>>> Handle(
        ListSavedAddressesQuery request, CancellationToken cancellationToken)
    {
        var customerId = request.AuthorizedActorId
            ?? throw new InvalidOperationException("Saved address query requires an authorized customer.");
        return Result<IReadOnlyList<SavedAddressView>>.Success(
            await store.ListAsync(customerId, cancellationToken));
    }
}
