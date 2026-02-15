using DomainProvisioningService.Application.Interfaces;
using DomainProvisioningService.Domain;
using Microsoft.Extensions.Logging;

namespace DomainProvisioningService.Application.StateMachine.Handlers;

/// <summary>
/// Handler for certificate downloaded state
/// Transition: CertificateDownloaded → CertificateDeploying
/// Saves certificate to CertificateStore before deployment
/// </summary>
public class CertificateDownloadedHandler : IStateTransitionHandler
{
    private readonly ICertificateStoreRepository _certificateStore;
    private readonly ILogger<CertificateDownloadedHandler> _logger;

    public DomainProvisioningState HandlesState => DomainProvisioningState.CertificateDownloaded;

    public CertificateDownloadedHandler(
        ICertificateStoreRepository certificateStore,
        ILogger<CertificateDownloadedHandler> logger)
    {
        _certificateStore = certificateStore ?? throw new ArgumentNullException(nameof(certificateStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<StateTransitionResult> ExecuteAsync(
        DomainProvisioningContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Saving certificate to CertificateStore for domain: {Domain}", context.Domain);

            if (string.IsNullOrEmpty(context.CertificatePem) || !context.CertificateNotAfter.HasValue)
            {
                return StateTransitionResult.FailureResult(
                    DomainProvisioningState.CertificateDownloadFailed,
                    DomainErrorCode.AcmeChallengeFailed,
                    "Certificate PEM or NotAfter is missing");
            }

            // Save certificate to CertificateStore
            await _certificateStore.SaveCertificateAsync(
                context.Domain,
                context.CertificatePem,
                context.CertificateNotAfter.Value,
                context.CustomDomainId,
                cancellationToken);

            _logger.LogInformation("Certificate saved to CertificateStore for {Domain}, proceeding to deployment",
                context.Domain);

            return StateTransitionResult.SuccessResult(DomainProvisioningState.CertificateDeploying);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save certificate for {Domain}", context.Domain);
            return StateTransitionResult.FailureResult(
                DomainProvisioningState.CertificateDownloadFailed,
                DomainErrorCode.AcmeChallengeFailed,
                $"Certificate save failed: {ex.Message}");
        }
    }
}
