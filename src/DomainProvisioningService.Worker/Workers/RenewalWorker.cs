using DomainProvisioningService.Application.Interfaces;
using DomainProvisioningService.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DomainProvisioningService.Worker.Workers;

/// <summary>
/// Background worker that automatically renews expiring Let's Encrypt certificates
/// </summary>
public class RenewalWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RenewalWorker> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly int _renewalThresholdDays;

    public RenewalWorker(
        IServiceProvider serviceProvider,
        ILogger<RenewalWorker> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Poll every 6 hours for expiring certificates
        _pollInterval = TimeSpan.FromHours(configuration.GetValue<int>("Renewal:PollIntervalHours", 6));

        // Renew certificates 30 days before expiration
        _renewalThresholdDays = configuration.GetValue<int>("Renewal:ThresholdDays", 30);

        _logger.LogInformation("RenewalWorker configured: PollInterval={PollInterval}, ThresholdDays={ThresholdDays}",
            _pollInterval, _renewalThresholdDays);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RenewalWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiringCertificatesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in RenewalWorker");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("RenewalWorker stopped");
    }

    private async Task ProcessExpiringCertificatesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var certificateStore = scope.ServiceProvider.GetRequiredService<ICertificateStoreRepository>();
        var cabinetApiClient = scope.ServiceProvider.GetRequiredService<ICabinetApiClient>();

        // 1. Get certificates expiring within threshold
        var expiringCerts = await certificateStore.GetExpiringCertificatesAsync(
            _renewalThresholdDays,
            cancellationToken);

        if (!expiringCerts.Any())
        {
            _logger.LogDebug("No certificates expiring within {Days} days", _renewalThresholdDays);
            return;
        }

        _logger.LogInformation("Found {Count} certificates expiring within {Days} days",
            expiringCerts.Count, _renewalThresholdDays);

        // 2. Mark each domain for renewal
        foreach (var cert in expiringCerts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (!cert.CustomDomainId.HasValue)
                {
                    _logger.LogWarning("Certificate for {Domain} has no CustomDomainId, skipping renewal",
                        cert.Domain);
                    continue;
                }

                _logger.LogInformation("Marking domain {Domain} for renewal (expires: {ExpiresAt})",
                    cert.Domain, cert.CertificateNotAfter);

                // Update status to RenewDue (which will trigger AcmeIssuanceWorker to re-issue)
                // Note: We might need to add RenewDue to the statuses that AcmeIssuanceWorker checks
                // For now, we can set it to Issuing to trigger immediate renewal
                await cabinetApiClient.UpdateCustomDomainStatusAsync(
                    cert.CustomDomainId.Value,
                    CustomDomainStatus.Issuing,
                    cancellationToken: cancellationToken);

                _logger.LogInformation("Domain {Domain} marked for renewal", cert.Domain);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking domain {Domain} for renewal", cert.Domain);
            }
        }
    }
}
