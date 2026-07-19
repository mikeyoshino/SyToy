using MediatR;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Addresses.SavedAddresses.SetDefaultSavedAddress;

public sealed class SetDefaultSavedAddressHandler(ISavedAddressStore store, TimeProvider timeProvider)
    : IRequestHandler<SetDefaultSavedAddressCommand, Result>
{
    public Task<Result> Handle(SetDefaultSavedAddressCommand request,
        CancellationToken cancellationToken)
    {
        var customerId = request.AuthorizedActorId
            ?? throw new InvalidOperationException("Saved address command requires an authorized customer.");
        return store.SetDefaultAsync(customerId, request.AddressId, timeProvider.GetUtcNow(), cancellationToken);
    }
}
