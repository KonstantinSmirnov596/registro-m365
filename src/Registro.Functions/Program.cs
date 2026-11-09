using Azure.Data.Tables;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using Registro.Core.Numeracion;
using Registro.Functions.Numeracion;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}

// Numerador: counters live in Azure Table Storage. Locally "UseDevelopmentStorage=true" points to Azurite.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = configuration["NumeradorStorage"] ?? configuration["AzureWebJobsStorage"]
        ?? throw new InvalidOperationException("Set 'NumeradorStorage' or 'AzureWebJobsStorage'.");
    var tableName = configuration["Numerador:Table"] ?? "Contadores";
    return new TableClient(connectionString, tableName);
});
builder.Services.AddSingleton<ICounterStore, TableCounterStore>();
builder.Services.AddSingleton(new NumeradorOptions());
builder.Services.AddSingleton<Numerador>();

builder.Build().Run();
