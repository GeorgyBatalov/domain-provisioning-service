using DomainProvisioningService.Application.Interfaces;
using DomainProvisioningService.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DomainProvisioningService.Worker.Workers;

/// <summary>
/// Background worker that verifies CNAME records for domains in PendingDns status
/// </summary>
public class DomainVerificationWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DomainVerificationWorker> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(30);

    public DomainVerificationWorker(
        IServiceProvider serviceProvider,
        ILogger<DomainVerificationWorker> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DomainVerificationWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingDomainsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in DomainVerificationWorker");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("DomainVerificationWorker stopped");
    }

    private async Task ProcessPendingDomainsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var cabinetApiClient = scope.ServiceProvider.GetRequiredService<ICabinetApiClient>();
        var dnsVerificationService = scope.ServiceProvider.GetRequiredService<IDnsVerificationService>();

        // 1. Get all domains in PendingDns status
        var pendingDomains = await cabinetApiClient.GetCustomDomainsByStatusAsync(
            new[] { CustomDomainStatus.PendingDns },
            cancellationToken);

        if (!pendingDomains.Any())
        {
            _logger.LogDebug("No domains in PendingDns status");
            return;
        }

        _logger.LogInformation("Found {Count} domains in PendingDns status", pendingDomains.Count);

        // 2. Verify CNAME for each domain
        foreach (var domain in pendingDomains)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                _logger.LogInformation("Verifying CNAME for domain: {Domain}", domain.Domain);

                if (string.IsNullOrEmpty(domain.ExpectedCnameValue))
                {
                    _logger.LogWarning("Domain {Domain} has no ExpectedCnameValue, skipping", domain.Domain);
                    continue;
                }

                var dnsResult = await dnsVerificationService.VerifyCnameAsync(
                    domain.Domain,
                    domain.ExpectedCnameValue,
                    cancellationToken);

                if (dnsResult.IsValid)
                {
                    // CNAME is valid, move to PendingHttpProbe status
                    _logger.LogInformation("CNAME verified for {Domain}, moving to PendingHttpProbe", domain.Domain);

                    await cabinetApiClient.UpdateCustomDomainStatusAsync(
                        domain.Id,
                        CustomDomainStatus.PendingHttpProbe,
                        cancellationToken: cancellationToken);
                }
                else
                {
                    // CNAME verification failed, update error
                    _logger.LogWarning("CNAME verification failed for {Domain}: {Error}",
                        domain.Domain, dnsResult.ErrorMessage);

                    // Don't fail the domain immediately - DNS propagation can take time
                    // Just log the error and retry on next iteration
                    // Optionally: implement retry count and fail after N attempts
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying domain: {Domain}", domain.Domain);

                // Update domain status to Failed
                await cabinetApiClient.UpdateCustomDomainStatusAsync(
                    domain.Id,
                    CustomDomainStatus.Failed,
                    DomainErrorCode.CnameNotFound,
                    $"Unexpected error: {ex.Message}",
                    cancellationToken);
            }
        }
    }
}
