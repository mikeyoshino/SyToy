using MediatR;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Addresses.SavedAddresses.DeleteSavedAddress;

public sealed class DeleteSavedAddressHandler(ISavedAddressStore store, TimeProvider timeProvider)
    : IRequestHandler<DeleteSavedAddressCommand, Result>
{
    public Task<Result> Handle(DeleteSavedAddressCommand request, CancellationToken cancellationToken)
    {
        var customerId = request.AuthorizedActorId
            ?? throw new InvalidOperationException("Saved address command requires an authorized customer.");
        return store.DeleteAsync(customerId, request.AddressId, timeProvider.GetUtcNow(), cancellationToken);
    }
}
