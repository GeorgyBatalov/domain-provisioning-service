using DomainProvisioningService.Application.Interfaces;
using DomainProvisioningService.Domain;
using DomainProvisioningService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DomainProvisioningService.Infrastructure.Repositories;

/// <summary>
/// Repository for DomainProvisioningContext persistence
/// </summary>
public class DomainProvisioningRepository : IDomainProvisioningRepository
{
    private readonly DomainProvisioningDbContext _dbContext;
    private readonly ILogger<DomainProvisioningRepository> _logger;

    public DomainProvisioningRepository(
        DomainProvisioningDbContext dbContext,
        ILogger<DomainProvisioningRepository> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DomainProvisioningContext?> GetByCustomDomainIdAsync(
        Guid customDomainId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.DomainProvisioningContexts
            .FirstOrDefaultAsync(c => c.CustomDomainId == customDomainId, cancellationToken);
    }

    public async Task<List<DomainProvisioningContext>> GetNonTerminalContextsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.DomainProvisioningContexts
            .Where(c => !c.CurrentState.IsTerminal())
            .OrderBy(c => c.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<DomainProvisioningContext>> GetContextsByStateAsync(
        DomainProvisioningState state,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.DomainProvisioningContexts
            .Where(c => c.CurrentState == state)
            .OrderBy(c => c.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveAsync(
        DomainProvisioningContext context,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.DomainProvisioningContexts
            .FirstOrDefaultAsync(c => c.CustomDomainId == context.CustomDomainId, cancellationToken);

        if (existing != null)
        {
            // Update existing
            _dbContext.Entry(existing).CurrentValues.SetValues(context);
        }
        else
        {
            // Create new
            _dbContext.DomainProvisioningContexts.Add(context);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Saved context for domain {Domain} in state {State}",
            context.Domain, context.CurrentState);
    }

    public async Task SaveTransitionHistoryAsync(
        StateTransitionHistory history,
        CancellationToken cancellationToken = default)
    {
        _dbContext.StateTransitionHistory.Add(history);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Saved transition history for domain {Domain}: {From} → {To}",
            history.Domain, history.FromState, history.ToState);
    }

    public async Task<List<StateTransitionHistory>> GetTransitionHistoryAsync(
        Guid customDomainId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.StateTransitionHistory
            .Where(h => h.CustomDomainId == customDomainId)
            .OrderByDescending(h => h.TransitionedAt)
            .ToListAsync(cancellationToken);
    }
}
