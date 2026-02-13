using Microsoft.Extensions.DependencyInjection;
using Ready.Application.Results;
using Ready.Application.AI;
using Microsoft.Extensions.Configuration;
using Ready.Application.Steps;
using Ready.Application.Workflows;
using MediatR; // Added for MediatR registration
using Microsoft.Extensions.Options; // Added for IOptions usage

namespace Ready.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddReadyApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ServiceCollectionExtensions).Assembly));

        // AI
        services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.SectionName));
        services.AddHttpClient<IOpenAiClient, OpenAiClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<OpenAiOptions>>().Value;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey);
            client.Timeout = TimeSpan.FromSeconds(90);
        });

        // Workflows (поки in-memory)
        services.AddSingleton<IWorkflowRegistry>(_ =>
            new InMemoryWorkflowRegistry(new[]
            {
                new WorkflowDefinition(
                    Name: "invoice",
                    Version: "v1",
                    Steps: new[] { "pdf.text.extract", "invoice.extract.v1", "invoice.export.csv.v1" }
                ),
                 // Fallback for "echo" test
                new WorkflowDefinition("echo", "v1", new[] { "echo" })
            }));

        // Steps
        services.AddSingleton<Abstractions.IWorkflowStep, EchoStep>();

        services.AddScoped<Abstractions.IWorkflowStep, InvoiceExtractV1Step>();
        services.AddScoped<Abstractions.IWorkflowStep, InvoiceExportCsvV1Step>();
        services.AddSingleton<Abstractions.IWorkflowStep, InvoiceValidateStep>();
        services.AddSingleton<Abstractions.IWorkflowStep, ExportJsonStep>();

        // Result store
        services.AddSingleton<IResultStore, InMemoryResultStore>();

        // Executor
        services.AddScoped<WorkflowExecutor>();

        return services;
    }
}