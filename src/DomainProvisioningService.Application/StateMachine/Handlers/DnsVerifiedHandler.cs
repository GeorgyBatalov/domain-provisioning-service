using DomainProvisioningService.Application.Interfaces;
using DomainProvisioningService.Domain;
using Microsoft.Extensions.Logging;

namespace DomainProvisioningService.Application.StateMachine.Handlers;

/// <summary>
/// Handler for DnsVerified state
/// Transition: DnsVerified → AcmeOrdering
/// </summary>
public class DnsVerifiedHandler : IStateTransitionHandler
{
    private readonly ILogger<DnsVerifiedHandler> _logger;

    public DomainProvisioningState HandlesState => DomainProvisioningState.DnsVerified;

    public DnsVerifiedHandler(ILogger<DnsVerifiedHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<StateTransitionResult> ExecuteAsync(
        DomainProvisioningContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("DNS verified for {Domain}, proceeding to ACME ordering", context.Domain);

        // DNS verified, proceed to ACME
        return Task.FromResult(StateTransitionResult.SuccessResult(DomainProvisioningState.AcmeOrdering));
    }
}
