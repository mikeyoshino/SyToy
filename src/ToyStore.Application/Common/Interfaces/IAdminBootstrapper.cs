using ToyStore.Application.Accounts.BootstrapAdmin;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Common.Interfaces;

public interface IAdminBootstrapper
{
    Task<Result<BootstrapAdminResult>> CreateFirstAdminAsync(
        string email,
        string temporaryPassword,
        CancellationToken cancellationToken);
}
