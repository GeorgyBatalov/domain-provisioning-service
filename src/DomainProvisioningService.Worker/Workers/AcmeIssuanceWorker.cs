using DomainProvisioningService.Application.Interfaces;
using DomainProvisioningService.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DomainProvisioningService.Worker.Workers;

/// <summary>
/// Background worker that issues Let's Encrypt certificates for domains in PendingHttpProbe and Issuing status
/// </summary>
public class AcmeIssuanceWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AcmeIssuanceWorker> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(30);

    public AcmeIssuanceWorker(
        IServiceProvider serviceProvider,
        ILogger<AcmeIssuanceWorker> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AcmeIssuanceWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDomainsForIssuanceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in AcmeIssuanceWorker");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("AcmeIssuanceWorker stopped");
    }

    private async Task ProcessDomainsForIssuanceAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var cabinetApiClient = scope.ServiceProvider.GetRequiredService<ICabinetApiClient>();
        var acmeService = scope.ServiceProvider.GetRequiredService<IAcmeService>();
        var certificateStore = scope.ServiceProvider.GetRequiredService<ICertificateStoreRepository>();
        var haproxyAgentClient = scope.ServiceProvider.GetRequiredService<IHAProxyAgentClient>();

        // 1. Get domains in PendingHttpProbe or Issuing status
        var domainsForIssuance = await cabinetApiClient.GetCustomDomainsByStatusAsync(
            new[] { CustomDomainStatus.PendingHttpProbe, CustomDomainStatus.Issuing },
            cancellationToken);

        if (!domainsForIssuance.Any())
        {
            _logger.LogDebug("No domains pending certificate issuance");
            return;
        }

        _logger.LogInformation("Found {Count} domains pending certificate issuance", domainsForIssuance.Count);

        // 2. Issue certificate for each domain
        foreach (var domain in domainsForIssuance)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                _logger.LogInformation("Starting certificate issuance for domain: {Domain}", domain.Domain);

                // Update status to Issuing if it was PendingHttpProbe
                if (domain.Status == CustomDomainStatus.PendingHttpProbe)
                {
                    await cabinetApiClient.UpdateCustomDomainStatusAsync(
                        domain.Id,
                        CustomDomainStatus.Issuing,
                        cancellationToken: cancellationToken);
                }

                // 3. Issue certificate via ACME
                var acmeResult = await acmeService.IssueCertificateAsync(
                    domain.Domain,
                    domain.Id,
                    cancellationToken);

                if (!acmeResult.Success)
                {
                    // Certificate issuance failed
                    _logger.LogError("Certificate issuance failed for {Domain}: {Error}",
                        domain.Domain, acmeResult.ErrorMessage);

                    await cabinetApiClient.UpdateCustomDomainStatusAsync(
                        domain.Id,
                        CustomDomainStatus.Failed,
                        acmeResult.ErrorCode,
                        acmeResult.ErrorMessage,
                        cancellationToken);

                    continue;
                }

                _logger.LogInformation("Certificate issued successfully for {Domain}", domain.Domain);

                // 4. Save certificate to CertificateStore
                await certificateStore.SaveCertificateAsync(
                    domain.Domain,
                    acmeResult.CertificatePem!,
                    acmeResult.NotAfter!.Value,
                    domain.Id,
                    cancellationToken);

                _logger.LogDebug("Certificate saved to CertificateStore for {Domain}", domain.Domain);

                // 5. Deploy certificate to HAProxy via Agent
                var deploySuccess = await haproxyAgentClient.ApplyCertificateAsync(
                    domain.Domain,
                    acmeResult.CertificatePem!,
                    acmeResult.NotAfter!.Value,
                    cancellationToken);

                if (!deploySuccess)
                {
                    _logger.LogError("Failed to deploy certificate to HAProxy for {Domain}", domain.Domain);

                    await cabinetApiClient.UpdateCustomDomainStatusAsync(
                        domain.Id,
                        CustomDomainStatus.Failed,
                        DomainErrorCode.AcmeChallengeFailed,
                        "Failed to deploy certificate to HAProxy",
                        cancellationToken);

                    continue;
                }

                _logger.LogInformation("Certificate deployed successfully for {Domain}", domain.Domain);

                // 6. Update Cabinet API: domain is now Active
                await cabinetApiClient.UpdateCustomDomainStatusAsync(
                    domain.Id,
                    CustomDomainStatus.Active,
                    cancellationToken: cancellationToken);

                await cabinetApiClient.UpdateCustomDomainCertificateAsync(
                    domain.Id,
                    acmeResult.NotAfter!.Value,
                    cancellationToken);

                _logger.LogInformation("Domain {Domain} is now Active with HTTPS", domain.Domain);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error issuing certificate for domain: {Domain}", domain.Domain);

                await cabinetApiClient.UpdateCustomDomainStatusAsync(
                    domain.Id,
                    CustomDomainStatus.Failed,
                    DomainErrorCode.AcmeChallengeFailed,
                    $"Unexpected error: {ex.Message}",
                    cancellationToken);
            }
        }
    }
}
