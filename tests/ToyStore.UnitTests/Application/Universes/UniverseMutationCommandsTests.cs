using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Application.Universes;
using ToyStore.Application.Universes.ArchiveUniverse;
using ToyStore.Application.Universes.CreateUniverse;
using ToyStore.Application.Universes.UpdateUniverse;

namespace ToyStore.UnitTests.Application.Universes;

public sealed class UniverseMutationCommandsTests
{
    [Fact]
    public async Task CreateValidatorRequiresNamesSlugSourceAndLogo()
    {
        var result = await new CreateUniverseValidator().ValidateAsync(
            new CreateUniverseCommand(" ", "ภาษาไทย", null),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, failure =>
            failure.PropertyName == nameof(CreateUniverseCommand.DisplayName)
            && failure.ErrorMessage == "กรุณากรอกชื่อจักรวาล");
        Assert.Contains(result.Errors, failure =>
            failure.PropertyName == nameof(CreateUniverseCommand.EnglishName)
            && failure.ErrorMessage == "ชื่อภาษาอังกฤษต้องสร้างส่วน URL ได้ด้วยตัวอักษรอังกฤษหรือตัวเลข");
        Assert.Contains(result.Errors, failure =>
            failure.PropertyName == nameof(CreateUniverseCommand.Logo)
            && failure.ErrorMessage == "กรุณาเลือกโลโก้จักรวาล");
    }

    [Fact]
    public async Task UpdateValidatorRequiresIdentityExpectedVersionAndValidNames()
    {
        var result = await new UpdateUniverseValidator().ValidateAsync(
            new UpdateUniverseCommand(Guid.Empty, 0, " ", " ", null),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, failure =>
            failure.PropertyName == nameof(UpdateUniverseCommand.Id));
        Assert.Contains(result.Errors, failure =>
            failure.PropertyName == nameof(UpdateUniverseCommand.ExpectedVersion));
        Assert.Contains(result.Errors, failure =>
            failure.PropertyName == nameof(UpdateUniverseCommand.DisplayName));
        Assert.Contains(result.Errors, failure =>
            failure.PropertyName == nameof(UpdateUniverseCommand.EnglishName));
    }

    [Fact]
    public async Task ArchiveValidatorRequiresIdentityAndExpectedVersion()
    {
        var result = await new ArchiveUniverseValidator().ValidateAsync(
            new ArchiveUniverseCommand(Guid.Empty, 0),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, failure =>
            failure.PropertyName == nameof(ArchiveUniverseCommand.Id));
        Assert.Contains(result.Errors, failure =>
            failure.PropertyName == nameof(ArchiveUniverseCommand.ExpectedVersion));
    }

    [Fact]
    public void CommandsUseUniverseAuthorizationAndPersistenceMappingWithoutAutomaticTransaction()
    {
        var commands = new AuthorizedUniverseMutationRequest<Result<UniverseMutationResult>>[]
        {
            new CreateUniverseCommand("จักรวาล", "Universe", Upload()),
            new UpdateUniverseCommand(Guid.NewGuid(), 1, "จักรวาล", "Universe", null),
            new ArchiveUniverseCommand(Guid.NewGuid(), 1),
        };

        foreach (var command in commands)
        {
            Assert.Equal(PolicyNames.CanManageProducts, command.RequiredPolicy);
            Assert.Equal(
                UniverseErrors.DuplicateDisplayName,
                command.MapPersistenceFailure(new PersistenceFailure(
                    PersistenceFailureTarget.Universe,
                    PersistenceFailureKind.DuplicateDisplayName)));
            Assert.Equal(
                UniverseErrors.StaleVersion,
                command.MapPersistenceFailure(new PersistenceFailure(
                    PersistenceFailureTarget.Request,
                    PersistenceFailureKind.ConcurrencyConflict)));
            Assert.Null(command.MapPersistenceFailure(new PersistenceFailure(
                PersistenceFailureTarget.Brand,
                PersistenceFailureKind.DuplicateDisplayName)));
            Assert.DoesNotContain(
                command.GetType().GetInterfaces(),
                contract => contract.IsGenericType
                    && contract.GetGenericTypeDefinition().Name == "ICommand`1");
        }
    }

    private static MediaUpload Upload() =>
        new(new MemoryStream([0xff, 0xd8, 0xff]), "image/jpeg");
}
