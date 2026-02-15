using DomainProvisioningService.Application.Interfaces;
using DomainProvisioningService.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DomainProvisioningService.Worker.Workers;

/// <summary>
/// Main state machine worker - replaces DomainVerificationWorker, AcmeIssuanceWorker, RenewalWorker
/// Processes all domain provisioning via state machine
/// </summary>
public class DomainProvisioningStateMachineWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DomainProvisioningStateMachineWorker> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(10);

    public DomainProvisioningStateMachineWorker(
        IServiceProvider serviceProvider,
        ILogger<DomainProvisioningStateMachineWorker> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DomainProvisioningStateMachineWorker started");

        // Wait a bit on startup
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingContextsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in DomainProvisioningStateMachineWorker");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("DomainProvisioningStateMachineWorker stopped");
    }

    private async Task ProcessPendingContextsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var stateMachine = scope.ServiceProvider.GetRequiredService<IDomainProvisioningStateMachine>();

        // Get all contexts that need processing (not in terminal state)
        var pendingContexts = await stateMachine.GetPendingContextsAsync(cancellationToken);

        if (!pendingContexts.Any())
        {
            _logger.LogDebug("No pending contexts to process");
            return;
        }

        _logger.LogInformation("Processing {Count} pending contexts", pendingContexts.Count);

        foreach (var context in pendingContexts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await ProcessContextAsync(stateMachine, context, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing context for domain {Domain}", context.Domain);

                // Update context to Failed state
                context.PreviousState = context.CurrentState;
                context.CurrentState = DomainProvisioningState.Failed;
                context.LastError = $"Unexpected error: {ex.Message}";
                context.LastErrorCode = DomainErrorCode.AcmeChallengeFailed;
                context.UpdatedAt = DateTime.UtcNow;

                await stateMachine.SaveContextAsync(context, cancellationToken);

                // Record transition
                await stateMachine.RecordTransitionAsync(new StateTransitionHistory
                {
                    CustomDomainId = context.CustomDomainId,
                    Domain = context.Domain,
                    FromState = context.PreviousState ?? DomainProvisioningState.Initial,
                    ToState = DomainProvisioningState.Failed,
                    ErrorMessage = ex.Message,
                    ErrorCode = DomainErrorCode.AcmeChallengeFailed
                }, cancellationToken);
            }
        }
    }

    private async Task ProcessContextAsync(
        IDomainProvisioningStateMachine stateMachine,
        DomainProvisioningContext context,
        CancellationToken cancellationToken)
    {
        var previousState = context.CurrentState;

        _logger.LogDebug("Processing domain {Domain} in state {State}", context.Domain, previousState);

        // Execute one transition
        var result = await stateMachine.ExecuteTransitionAsync(context, cancellationToken);

        if (result.Success && result.NewState.HasValue)
        {
            // Successful transition
            var duration = DateTime.UtcNow - context.StateEnteredAt;

            context.PreviousState = previousState;
            context.CurrentState = result.NewState.Value;
            context.StateEnteredAt = DateTime.UtcNow;
            context.LastError = null;
            context.LastErrorCode = null;

            // Save context
            await stateMachine.SaveContextAsync(context, cancellationToken);

            // Record transition
            await stateMachine.RecordTransitionAsync(new StateTransitionHistory
            {
                CustomDomainId = context.CustomDomainId,
                Domain = context.Domain,
                FromState = previousState,
                ToState = result.NewState.Value,
                Reason = "Successful transition",
                Duration = duration
            }, cancellationToken);

            _logger.LogInformation("Domain {Domain}: {From} → {To} ({Duration}ms)",
                context.Domain, previousState, result.NewState.Value, duration.TotalMilliseconds);
        }
        else if (result.ShouldRetry)
        {
            // Retry - context.RetryCount already incremented by state machine
            context.LastError = result.ErrorMessage;
            context.LastErrorCode = result.ErrorCode;

            await stateMachine.SaveContextAsync(context, cancellationToken);

            _logger.LogDebug("Domain {Domain}: retry {Retry}/{Max} in state {State}",
                context.Domain, context.RetryCount, context.MaxRetries, previousState);
        }
        else if (result.NewState.HasValue)
        {
            // Transition to failure state
            var duration = DateTime.UtcNow - context.StateEnteredAt;

            context.PreviousState = previousState;
            context.CurrentState = result.NewState.Value;
            context.StateEnteredAt = DateTime.UtcNow;
            context.LastError = result.ErrorMessage;
            context.LastErrorCode = result.ErrorCode;

            await stateMachine.SaveContextAsync(context, cancellationToken);

            // Record transition
            await stateMachine.RecordTransitionAsync(new StateTransitionHistory
            {
                CustomDomainId = context.CustomDomainId,
                Domain = context.Domain,
                FromState = previousState,
                ToState = result.NewState.Value,
                ErrorMessage = result.ErrorMessage,
                ErrorCode = result.ErrorCode,
                RetryAttempt = context.RetryCount,
                Duration = duration
            }, cancellationToken);

            _logger.LogWarning("Domain {Domain}: {From} → {To} (failure: {Error})",
                context.Domain, previousState, result.NewState.Value, result.ErrorMessage);
        }
    }
}
