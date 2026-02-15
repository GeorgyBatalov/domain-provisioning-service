using DomainProvisioningService.Application.Interfaces;
using DomainProvisioningService.Domain;
using Microsoft.Extensions.Logging;

namespace DomainProvisioningService.Application.StateMachine.Handlers;

/// <summary>
/// Handler for Initial state
/// Transition: Initial → DnsVerifying
/// </summary>
public class InitialStateHandler : IStateTransitionHandler
{
    private readonly ILogger<InitialStateHandler> _logger;

    public DomainProvisioningState HandlesState => DomainProvisioningState.Initial;

    public InitialStateHandler(ILogger<InitialStateHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<StateTransitionResult> ExecuteAsync(
        DomainProvisioningContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting provisioning for domain: {Domain}", context.Domain);

        // Initial state always transitions to DnsVerifying
        return Task.FromResult(StateTransitionResult.SuccessResult(DomainProvisioningState.DnsVerifying));
    }
}
