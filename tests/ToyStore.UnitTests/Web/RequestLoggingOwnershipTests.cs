using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Hosting;
using ToyStore.Application.Common.Diagnostics;
using ToyStore.Web.Diagnostics;

namespace ToyStore.UnitTests.Web;

public sealed class RequestLoggingOwnershipTests
{
    [Fact]
    public void ProductionPlacesRequestDiagnosticsOutsideExceptionHandling()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "ToyStore.Web",
            "Program.cs"));
        var requestLogging = source.IndexOf(
            "UseSerilogRequestLogging",
            StringComparison.Ordinal);
        var exceptionHandling = source.IndexOf(
            "UseExceptionHandler();",
            StringComparison.Ordinal);

        Assert.True(requestLogging >= 0 && exceptionHandling > requestLogging);
    }

    [Fact]
    public async Task ApplicationOwnedExceptionKeepsRequestDiagnosticWithoutDuplicateError()
    {
        var exception = new InvalidOperationException("application failure");
        ExceptionLogOwnership.MarkApplicationLogged(exception);

        var events = await ExecutePipelineAsync(_ => Task.FromException(exception));

        var requestDiagnostic = Assert.Single(events);
        Assert.Equal(LogEventLevel.Information, requestDiagnostic.Level);
        Assert.Null(requestDiagnostic.Exception);
        Assert.Equal(
            "/test-owned-exception",
            requestDiagnostic.Properties["RequestPath"].LiteralValue());
    }

    [Fact]
    public async Task GlobalOwnedExceptionKeepsRequestDiagnosticWithoutPreemptiveError()
    {
        var exception = new InvalidOperationException("global failure");

        var events = await ExecutePipelineAsync(_ => Task.FromException(exception));

        var requestDiagnostic = Assert.Single(events);
        Assert.Equal(LogEventLevel.Information, requestDiagnostic.Level);
        Assert.Null(requestDiagnostic.Exception);
    }

    [Fact]
    public async Task CancellationKeepsRequestDiagnosticButNeverErrorLogs()
    {
        var events = await ExecutePipelineAsync(
            _ => Task.FromException(new OperationCanceledException("request cancelled")));

        var requestDiagnostic = Assert.Single(events);
        Assert.Equal(LogEventLevel.Information, requestDiagnostic.Level);
        Assert.Null(requestDiagnostic.Exception);
    }

    [Fact]
    public async Task UnexceptionalServerFailureRemainsAnErrorDiagnostic()
    {
        var events = await ExecutePipelineAsync(context =>
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return Task.CompletedTask;
        });

        var requestDiagnostic = Assert.Single(events);
        Assert.Equal(LogEventLevel.Error, requestDiagnostic.Level);
        Assert.Null(requestDiagnostic.Exception);
    }

    private static async Task<IReadOnlyList<LogEvent>> ExecutePipelineAsync(
        RequestDelegate terminal)
    {
        var sink = new CapturingSink();
        await using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();
        using var provider = new ServiceCollection()
            .AddSingleton(new DiagnosticContext(logger))
            .BuildServiceProvider();
        var application = new ApplicationBuilder(provider);

        application.UseSerilogRequestLogging(options =>
        {
            options.Logger = logger;
            options.GetLevel = RequestLogLevelSelector.Select;
        });
        application.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (OperationCanceledException)
            {
                RequestLogLevelSelector.MarkExceptionHandled(context);
                context.Response.StatusCode = 499;
            }
            catch
            {
                RequestLogLevelSelector.MarkExceptionHandled(context);
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            }
        });
        application.Run(terminal);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = provider,
        };
        httpContext.Request.Path = "/test-owned-exception";
        httpContext.Response.Body = new MemoryStream();

        await application.Build()(httpContext);
        return sink.Events;
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ToyStore.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate ToyStore.sln.");
    }
}

internal static class LogEventPropertyValueExtensions
{
    public static object? LiteralValue(this LogEventPropertyValue value) =>
        Assert.IsType<ScalarValue>(value).Value;
}
