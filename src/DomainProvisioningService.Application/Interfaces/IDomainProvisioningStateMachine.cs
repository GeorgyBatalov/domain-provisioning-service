using DomainProvisioningService.Domain;

namespace DomainProvisioningService.Application.Interfaces;

/// <summary>
/// State machine engine for domain provisioning
/// </summary>
public interface IDomainProvisioningStateMachine
{
    /// <summary>
    /// Execute one transition for given context
    /// </summary>
    Task<StateTransitionResult> ExecuteTransitionAsync(
        DomainProvisioningContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all contexts that need processing (not in terminal state)
    /// </summary>
    Task<List<DomainProvisioningContext>> GetPendingContextsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Save context after transition
    /// </summary>
    Task SaveContextAsync(
        DomainProvisioningContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Record state transition in history
    /// </summary>
    Task RecordTransitionAsync(
        StateTransitionHistory history,
        CancellationToken cancellationToken = default);
}
