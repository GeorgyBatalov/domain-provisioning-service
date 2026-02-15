using DomainProvisioningService.Application.Interfaces;
using DomainProvisioningService.Domain;
using Microsoft.Extensions.Logging;

namespace DomainProvisioningService.Application.StateMachine.Handlers;

/// <summary>
/// Handler for ACME challenge preparing state
/// Transition: AcmeChallengePreparing → AcmeChallengeValidating
/// </summary>
public class AcmeChallengePreparingHandler : IStateTransitionHandler
{
    private readonly ICertificateStoreRepository _certificateStore;
    private readonly ILogger<AcmeChallengePreparingHandler> _logger;

    public DomainProvisioningState HandlesState => DomainProvisioningState.AcmeChallengePreparing;

    public AcmeChallengePreparingHandler(
        ICertificateStoreRepository certificateStore,
        ILogger<AcmeChallengePreparingHandler> logger)
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
            _logger.LogInformation("Saving ACME challenge for domain: {Domain}", context.Domain);

            if (string.IsNullOrEmpty(context.AcmeChallengeToken) ||
                string.IsNullOrEmpty(context.AcmeChallengeKeyAuth))
            {
                return StateTransitionResult.FailureResult(
                    DomainProvisioningState.AcmeOrderFailed,
                    DomainErrorCode.AcmeChallengeFailed,
                    "Challenge token or keyAuth missing");
            }

            // Save challenge to CertificateStore (expires in 1 hour)
            await _certificateStore.SaveAcmeChallengeAsync(
                context.Domain,
                context.AcmeChallengeToken,
                context.AcmeChallengeKeyAuth,
                DateTime.UtcNow.AddHours(1),
                context.CustomDomainId,
                cancellationToken);

            _logger.LogInformation("ACME challenge saved for {Domain}, proceeding to validation", context.Domain);

            return StateTransitionResult.SuccessResult(DomainProvisioningState.AcmeChallengeValidating);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save ACME challenge for {Domain}", context.Domain);
            return StateTransitionResult.FailureResult(
                DomainProvisioningState.AcmeOrderFailed,
                DomainErrorCode.AcmeChallengeFailed,
                $"Challenge save failed: {ex.Message}");
        }
    }
}
