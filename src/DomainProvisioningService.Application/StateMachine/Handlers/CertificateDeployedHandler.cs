using DomainProvisioningService.Application.Interfaces;
using DomainProvisioningService.Domain;
using Microsoft.Extensions.Logging;

namespace DomainProvisioningService.Application.StateMachine.Handlers;

/// <summary>
/// Handler for certificate deployed state
/// Transition: CertificateDeployed → Active
/// Updates Cabinet API with Active status
/// </summary>
public class CertificateDeployedHandler : IStateTransitionHandler
{
    private readonly ICabinetApiClient _cabinetApi;
    private readonly ILogger<CertificateDeployedHandler> _logger;

    public DomainProvisioningState HandlesState => DomainProvisioningState.CertificateDeployed;

    public CertificateDeployedHandler(
        ICabinetApiClient cabinetApi,
        ILogger<CertificateDeployedHandler> logger)
    {
        _cabinetApi = cabinetApi ?? throw new ArgumentNullException(nameof(cabinetApi));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<StateTransitionResult> ExecuteAsync(
        DomainProvisioningContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Marking domain {Domain} as Active in Cabinet API", context.Domain);

            if (!context.CertificateNotAfter.HasValue)
            {
                return StateTransitionResult.FailureResult(
                    DomainProvisioningState.Failed,
                    DomainErrorCode.AcmeChallengeFailed,
                    "CertificateNotAfter is missing");
            }

            // Update Cabinet API: domain is now Active
            await _cabinetApi.UpdateCustomDomainStatusAsync(
                context.CustomDomainId,
                CustomDomainStatus.Active,
                cancellationToken: cancellationToken);

            await _cabinetApi.UpdateCustomDomainCertificateAsync(
                context.CustomDomainId,
                context.CertificateNotAfter.Value,
                cancellationToken);

            _logger.LogInformation("Domain {Domain} is now Active with HTTPS ✅", context.Domain);

            return StateTransitionResult.SuccessResult(DomainProvisioningState.Active);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Cabinet API for {Domain}", context.Domain);
            // Don't fail the whole process - certificate is already deployed
            // Just log error and move to Active anyway
            _logger.LogWarning("Proceeding to Active state despite Cabinet API error");
            return StateTransitionResult.SuccessResult(DomainProvisioningState.Active);
        }
    }
}
