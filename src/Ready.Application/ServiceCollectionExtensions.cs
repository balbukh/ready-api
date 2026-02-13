using Microsoft.Extensions.DependencyInjection;
using Ready.Application.Results;
using Ready.Application.Steps;
using Ready.Application.Workflows;

namespace Ready.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddReadyApplication(this IServiceCollection services)
    {
        // Workflows (поки in-memory)
        services.AddSingleton<IWorkflowRegistry>(_ =>
            new InMemoryWorkflowRegistry(new[]
            {
                new WorkflowDefinition(
                    Name: "invoice",
                    Version: "v1",
                    Steps: new[] { "text.extract", "invoice.extract.v1", "invoice.validate.v1", "export.json" }
                ),
                 // Fallback for "echo" test
                new WorkflowDefinition("echo", "v1", new[] { "echo" })
            }));

        // Steps
        services.AddSingleton<Abstractions.IWorkflowStep, EchoStep>();
        services.AddSingleton<Abstractions.IWorkflowStep, TextExtractStep>();
        services.AddSingleton<Abstractions.IWorkflowStep, InvoiceExtractStep>();
        services.AddSingleton<Abstractions.IWorkflowStep, InvoiceValidateStep>();
        services.AddSingleton<Abstractions.IWorkflowStep, ExportJsonStep>();

        // Result store
        services.AddSingleton<IResultStore, InMemoryResultStore>();

        // Executor
        services.AddScoped<WorkflowExecutor>();

        return services;
    }
}