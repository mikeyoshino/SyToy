using System.Collections.ObjectModel;
using System.Reflection;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Products;

namespace ToyStore.UnitTests.Domain.Catalog;

public sealed class CatalogArchitectureTests
{
    [Fact]
    public void DomainDoesNotReferenceFrameworkOrUseCaseLibraries()
    {
        var references = typeof(Product).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .ToArray();

        Assert.DoesNotContain(references, name => name!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name!.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name!.StartsWith("MediatR", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name!.StartsWith("FluentValidation", StringComparison.Ordinal));
    }

    [Fact]
    public void CatalogDoesNotIntroduceVariantCategoryManagementHierarchyOrFilesystemContracts()
    {
        var domainTypes = typeof(Product).Assembly.GetTypes();

        Assert.DoesNotContain(domainTypes, type => type.Name.Contains("ProductVariant", StringComparison.Ordinal));
        Assert.DoesNotContain(domainTypes, type => type.Name.Contains("CategoryService", StringComparison.Ordinal));
        Assert.DoesNotContain(domainTypes, type => type.Name.Contains("CategoryCommand", StringComparison.Ordinal));
        Assert.DoesNotContain(domainTypes, type => type.Name.Contains("CategoryHierarchy", StringComparison.Ordinal));
        Assert.DoesNotContain(
            domainTypes.SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)),
            method => method.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(Stream) ||
                parameter.ParameterType == typeof(FileInfo) ||
                parameter.ParameterType == typeof(DirectoryInfo)));
    }

    [Fact]
    public void SeedCollectionsAreReadOnlyAndReferenceEntitiesHaveNoPublicParameterlessConstructor()
    {
        Assert.IsAssignableFrom<IReadOnlyList<ProductCategory>>(ProductCategorySeeds.All);
        Assert.IsType<ReadOnlyCollection<ProductCategory>>(ProductCategorySeeds.All);
        Assert.IsAssignableFrom<IReadOnlyList<UniverseSeedDefinition>>(UniverseSeeds.All);
        Assert.IsType<ReadOnlyCollection<UniverseSeedDefinition>>(UniverseSeeds.All);

        Type[] entities =
        [
            typeof(ProductCategory),
            typeof(Brand),
            typeof(Universe),
            typeof(Character),
            typeof(ProductCharacter),
        ];
        Assert.All(
            entities,
            entity => Assert.DoesNotContain(
                entity.GetConstructors(BindingFlags.Public | BindingFlags.Instance),
                constructor => constructor.GetParameters().Length == 0));
    }

    [Fact]
    public void ProductOwnsCharacterLinksAndTheRelationHasNoPublicMutationFactory()
    {
        var characterField = typeof(Product).GetField(
            "_characters",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var charactersProperty = typeof(Product).GetProperty(nameof(Product.Characters));
        var relationFactories = typeof(ProductCharacter).GetMethods(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .ToArray();

        Assert.NotNull(characterField);
        Assert.Equal(typeof(List<ProductCharacter>), characterField.FieldType);
        Assert.NotNull(charactersProperty);
        Assert.Equal(typeof(IReadOnlyList<ProductCharacter>), charactersProperty.PropertyType);
        Assert.NotNull(typeof(Product).GetMethod(nameof(Product.UpdateDraftInStock)));
        Assert.Empty(relationFactories);
    }

    [Fact]
    public void CharacterMutationDoesNotPretendToValidateCrossAggregateUniverseMembership()
    {
        var updateParameters = typeof(Product).GetMethod(nameof(Product.UpdateDraftInStock))!
            .GetParameters();

        Assert.Contains(
            updateParameters,
            parameter => parameter.ParameterType == typeof(IReadOnlyCollection<Guid>));
        Assert.DoesNotContain(
            updateParameters,
            parameter => parameter.ParameterType == typeof(Character));
    }
}
