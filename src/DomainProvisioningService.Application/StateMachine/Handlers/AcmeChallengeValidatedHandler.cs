using DomainProvisioningService.Application.Interfaces;
using DomainProvisioningService.Domain;
using Microsoft.Extensions.Logging;

namespace DomainProvisioningService.Application.StateMachine.Handlers;

/// <summary>
/// Handler for ACME challenge validated state
/// Transition: AcmeChallengeValidated → CertificateDownloading
/// </summary>
public class AcmeChallengeValidatedHandler : IStateTransitionHandler
{
    private readonly ILogger<AcmeChallengeValidatedHandler> _logger;

    public DomainProvisioningState HandlesState => DomainProvisioningState.AcmeChallengeValidated;

    public AcmeChallengeValidatedHandler(ILogger<AcmeChallengeValidatedHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<StateTransitionResult> ExecuteAsync(
        DomainProvisioningContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Challenge validated for {Domain}, proceeding to download certificate", context.Domain);
        return Task.FromResult(StateTransitionResult.SuccessResult(DomainProvisioningState.CertificateDownloading));
    }
}
