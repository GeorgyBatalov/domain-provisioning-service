using DomainProvisioningService.Application.Interfaces;
using DomainProvisioningService.Domain;
using Microsoft.Extensions.Logging;

namespace DomainProvisioningService.Application.StateMachine.Handlers;

/// <summary>
/// Handler for certificate deployment state
/// Transition: CertificateDeploying → CertificateDeployed
/// Deploys certificate to HAProxy via Agent
/// </summary>
public class CertificateDeployingHandler : IStateTransitionHandler
{
    private readonly IHAProxyAgentClient _haproxyAgent;
    private readonly ILogger<CertificateDeployingHandler> _logger;

    public DomainProvisioningState HandlesState => DomainProvisioningState.CertificateDeploying;

    public CertificateDeployingHandler(
        IHAProxyAgentClient haproxyAgent,
        ILogger<CertificateDeployingHandler> logger)
    {
        _haproxyAgent = haproxyAgent ?? throw new ArgumentNullException(nameof(haproxyAgent));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<StateTransitionResult> ExecuteAsync(
        DomainProvisioningContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Deploying certificate to HAProxy for domain: {Domain}", context.Domain);

            if (string.IsNullOrEmpty(context.CertificatePem) || !context.CertificateNotAfter.HasValue)
            {
                return StateTransitionResult.FailureResult(
                    DomainProvisioningState.CertificateDeploymentFailed,
                    DomainErrorCode.AcmeChallengeFailed,
                    "Certificate PEM or NotAfter is missing");
            }

            // Deploy certificate to HAProxy via Agent
            var success = await _haproxyAgent.ApplyCertificateAsync(
                context.Domain,
                context.CertificatePem,
                context.CertificateNotAfter.Value,
                cancellationToken);

            if (!success)
            {
                _logger.LogError("Failed to deploy certificate to HAProxy for {Domain}", context.Domain);
                return StateTransitionResult.FailureResult(
                    DomainProvisioningState.CertificateDeploymentFailed,
                    DomainErrorCode.AcmeChallengeFailed,
                    "HAProxy Agent returned failure");
            }

            _logger.LogInformation("Certificate deployed successfully to HAProxy for {Domain}", context.Domain);

            return StateTransitionResult.SuccessResult(DomainProvisioningState.CertificateDeployed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deploy certificate for {Domain}", context.Domain);
            return StateTransitionResult.FailureResult(
                DomainProvisioningState.CertificateDeploymentFailed,
                DomainErrorCode.AcmeChallengeFailed,
                $"Deployment failed: {ex.Message}");
        }
    }
}
