using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ToyStore.UnitTests.Architecture;

public sealed class LayerRegistrationTests
{
    [Theory]
    [InlineData("ToyStore.Application", "ToyStore.Application.AssemblyReference")]
    [InlineData("ToyStore.Domain", "ToyStore.Domain.AssemblyReference")]
    [InlineData("ToyStore.Infrastructure", "ToyStore.Infrastructure.AssemblyReference")]
    public void LayerExposesAnAssemblyMarker(string assemblyName, string markerTypeName)
    {
        var assembly = Assembly.Load(assemblyName);
        var markerType = assembly.GetType(markerTypeName);

        Assert.NotNull(markerType);
        Assert.True(markerType.IsClass);
        Assert.True(markerType.IsAbstract);
        Assert.True(markerType.IsSealed);
    }

    [Theory]
    [InlineData(
        "ToyStore.Application",
        "ToyStore.Application.DependencyInjection",
        "AddApplication")]
    [InlineData(
        "ToyStore.Infrastructure",
        "ToyStore.Infrastructure.DependencyInjection",
        "AddInfrastructure")]
    public void LayerRegistrationReturnsTheSameServiceCollection(
        string assemblyName,
        string dependencyInjectionTypeName,
        string methodName)
    {
        var assembly = Assembly.Load(assemblyName);
        var dependencyInjectionType = assembly.GetType(dependencyInjectionTypeName);

        Assert.NotNull(dependencyInjectionType);

        var method = dependencyInjectionType.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(method);
        Assert.True(method.IsDefined(typeof(ExtensionAttribute), inherit: false));
        Assert.Equal(typeof(IServiceCollection), method.ReturnType);

        var parameters = method.GetParameters();

        Assert.Equal(typeof(IServiceCollection), parameters[0].ParameterType);

        var services = new ServiceCollection();
        object?[] arguments = methodName == "AddInfrastructure"
            ? [services, new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Database"] =
                        "Host=localhost;Database=toystore_tests;Username=test;Password=test",
                }).Build()]
            : [services];

        if (methodName == "AddInfrastructure")
        {
            Assert.Equal(2, parameters.Length);
            Assert.Equal(typeof(IConfiguration), parameters[1].ParameterType);
        }
        else
        {
            Assert.Single(parameters);
        }

        var result = method.Invoke(null, arguments);

        Assert.Same(services, result);
    }
}
