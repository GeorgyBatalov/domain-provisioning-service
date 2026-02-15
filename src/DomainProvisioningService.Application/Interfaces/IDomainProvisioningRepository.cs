using DomainProvisioningService.Domain;

namespace DomainProvisioningService.Application.Interfaces;

/// <summary>
/// Repository for DomainProvisioningContext persistence
/// </summary>
public interface IDomainProvisioningRepository
{
    /// <summary>
    /// Get context by CustomDomainId
    /// </summary>
    Task<DomainProvisioningContext?> GetByCustomDomainIdAsync(
        Guid customDomainId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all contexts not in terminal state
    /// </summary>
    Task<List<DomainProvisioningContext>> GetNonTerminalContextsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get contexts in specific state
    /// </summary>
    Task<List<DomainProvisioningContext>> GetContextsByStateAsync(
        DomainProvisioningState state,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Save or update context
    /// </summary>
    Task SaveAsync(
        DomainProvisioningContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Save transition history
    /// </summary>
    Task SaveTransitionHistoryAsync(
        StateTransitionHistory history,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get transition history for domain
    /// </summary>
    Task<List<StateTransitionHistory>> GetTransitionHistoryAsync(
        Guid customDomainId,
        CancellationToken cancellationToken = default);
}
