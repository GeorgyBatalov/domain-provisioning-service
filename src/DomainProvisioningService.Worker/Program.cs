using DomainProvisioningService.Application.Interfaces;
using DomainProvisioningService.Application.StateMachine;
using DomainProvisioningService.Application.StateMachine.Handlers;
using DomainProvisioningService.Infrastructure.Clients;
using DomainProvisioningService.Infrastructure.Data;
using DomainProvisioningService.Infrastructure.Repositories;
using DomainProvisioningService.Infrastructure.Services;
using DomainProvisioningService.Worker.Workers;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "DomainProvisioningService")
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting DomainProvisioningService...");

    var builder = Host.CreateApplicationBuilder(args);

    // Add Serilog
    builder.Services.AddSerilog();

    // Add DbContexts
    var connectionString = builder.Configuration.GetConnectionString("CertificateStore")
        ?? throw new InvalidOperationException("ConnectionString 'CertificateStore' not found");

    builder.Services.AddDbContext<CertificateStoreDbContext>(options =>
    {
        options.UseNpgsql(connectionString);
    });

    builder.Services.AddDbContext<DomainProvisioningDbContext>(options =>
    {
        options.UseNpgsql(connectionString); // Same DB
    });

    // Add repositories
    builder.Services.AddScoped<ICertificateStoreRepository, CertificateStoreRepository>();
    builder.Services.AddScoped<IDomainProvisioningRepository, DomainProvisioningRepository>();

    // Add HTTP clients with Polly retry policies
    builder.Services.AddHttpClient<ICabinetApiClient, CabinetApiClient>(client =>
    {
        var baseUrl = builder.Configuration["CabinetApi:BaseUrl"] ?? "http://localhost:5000";
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    builder.Services.AddHttpClient<IHAProxyAgentClient, HAProxyAgentClient>(client =>
    {
        var baseUrl = builder.Configuration["HAProxyAgent:BaseUrl"] ?? "http://localhost:8081";
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    // Add services
    builder.Services.AddSingleton<IDnsVerificationService, DnsVerificationService>();
    builder.Services.AddScoped<IAcmeService>(sp =>
    {
        var certificateStore = sp.GetRequiredService<ICertificateStoreRepository>();
        var logger = sp.GetRequiredService<ILogger<AcmeService>>();
        var configuration = sp.GetRequiredService<IConfiguration>();
        return new AcmeService(certificateStore, logger, configuration);
    });

    // Add State Machine and Handlers
    builder.Services.AddScoped<IDomainProvisioningStateMachine, DomainProvisioningStateMachine>();

    // Register all state transition handlers
    builder.Services.AddScoped<IStateTransitionHandler, InitialStateHandler>();
    builder.Services.AddScoped<IStateTransitionHandler, DnsVerificationHandler>();
    builder.Services.AddScoped<IStateTransitionHandler, DnsVerifiedHandler>();
    builder.Services.AddScoped<IStateTransitionHandler, AcmeOrderingHandler>();
    builder.Services.AddScoped<IStateTransitionHandler, AcmeChallengePreparingHandler>();
    builder.Services.AddScoped<IStateTransitionHandler, AcmeChallengeValidatingHandler>();
    builder.Services.AddScoped<IStateTransitionHandler, AcmeChallengeValidatedHandler>();
    builder.Services.AddScoped<IStateTransitionHandler, CertificateDownloadingHandler>();
    builder.Services.AddScoped<IStateTransitionHandler, CertificateDownloadedHandler>();
    builder.Services.AddScoped<IStateTransitionHandler, CertificateDeployingHandler>();
    builder.Services.AddScoped<IStateTransitionHandler, CertificateDeployedHandler>();

    // Add State Machine Worker (replaces 3 old workers)
    builder.Services.AddHostedService<DomainProvisioningStateMachineWorker>();

    // OLD WORKERS DISABLED - replaced by state machine
    // builder.Services.AddHostedService<DomainVerificationWorker>();
    // builder.Services.AddHostedService<AcmeIssuanceWorker>();
    // builder.Services.AddHostedService<RenewalWorker>();

    // Add health checks
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<CertificateStoreDbContext>("certificate_store_db")
        .AddDbContextCheck<DomainProvisioningDbContext>("domain_provisioning_db");

    var host = builder.Build();

    Log.Information("DomainProvisioningService started successfully");
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
