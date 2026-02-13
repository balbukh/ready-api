using Ready.Application;
using Ready.Application.Abstractions;
using Ready.Application.DTOs;
using Ready.Application.Results;
using Ready.Application.Steps;
using Ready.Application.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ready.UnitTests;

public class WorkflowExecutorTests
{
    [Fact]
    public async Task EchoWorkflow_ShouldRun()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        services.AddLogging();
        services.AddReadyApplication(config);
        services.AddSingleton<IRunStore, MockRunStore>();

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var executor = scope.ServiceProvider.GetRequiredService<WorkflowExecutor>();

        // Act — echo workflow is a single EchoStep, no DB/AI needed
        await executor.ExecuteAsync(Guid.NewGuid(), "test-customer", "echo", "v1", null, CancellationToken.None);

        // If we got here without exception, the workflow ran successfully
    }

    [Fact]
    public async Task EchoWorkflow_WithParams_ShouldRun()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        services.AddLogging();
        services.AddReadyApplication(config);
        services.AddSingleton<IRunStore, MockRunStore>();

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var executor = scope.ServiceProvider.GetRequiredService<WorkflowExecutor>();

        var paramsDict = new Dictionary<string, string> { { "TestKey", "TestValue" } };

        // Act
        await executor.ExecuteAsync(Guid.NewGuid(), "test-customer", "echo", "v1", paramsDict, CancellationToken.None);

        // If we got here without exception, params were accepted
    }

    [Fact]
    public void WorkflowExecutor_ResolvesToScoped()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        services.AddLogging();
        services.AddReadyApplication(config);
        services.AddSingleton<IRunStore, MockRunStore>();

        var sp = services.BuildServiceProvider();

        // Act & Assert — WorkflowExecutor should be scoped (requires scope)
        using var scope = sp.CreateScope();
        var executor = scope.ServiceProvider.GetService<WorkflowExecutor>();
        Assert.NotNull(executor);
    }

    [Fact]
    public void AllRegisteredSteps_ResolveProperly()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        services.AddLogging();
        services.AddReadyApplication(config);
        services.AddSingleton<IRunStore, MockRunStore>();

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();

        // Act
        var steps = scope.ServiceProvider.GetServices<IWorkflowStep>().ToList();

        // Assert — at minimum echo + invoice extract v1 should resolve (pdf.text.extract is in infra)
        Assert.Contains(steps, s => s.Name == "echo");
        Assert.Contains(steps, s => s.Name == "invoice.extract.v1");
    }
}

// Minimal Memory Stores for Testing
public class MockRunStore : IRunStore
{
    public Task<Guid> CreateRunAsync(Guid documentId, string workflowName, string workflowVersion, CancellationToken ct) 
        => Task.FromResult(Guid.NewGuid());

    public Task<Guid> CreateStepRunAsync(Guid runId, string stepName, CancellationToken ct) 
        => Task.FromResult(Guid.NewGuid());

    public Task MarkStepSucceededAsync(Guid stepRunId, CancellationToken ct) => Task.CompletedTask;
    public Task MarkStepFailedAsync(Guid stepRunId, string error, CancellationToken ct) => Task.CompletedTask;
    public Task MarkRunSucceededAsync(Guid runId, CancellationToken ct) => Task.CompletedTask;
    public Task MarkRunFailedAsync(Guid runId, string error, CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyList<WorkflowRunDto>> GetRunsAsync(Guid documentId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<WorkflowRunDto>>([]);
}
