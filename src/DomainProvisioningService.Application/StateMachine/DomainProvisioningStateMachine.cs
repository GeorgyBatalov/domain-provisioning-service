using DomainProvisioningService.Application.Interfaces;
using DomainProvisioningService.Domain;
using Microsoft.Extensions.Logging;

namespace DomainProvisioningService.Application.StateMachine;

/// <summary>
/// State machine engine for domain provisioning
/// Coordinates state transitions using registered handlers
/// </summary>
public class DomainProvisioningStateMachine : IDomainProvisioningStateMachine
{
    private readonly IDomainProvisioningRepository _repository;
    private readonly IEnumerable<IStateTransitionHandler> _handlers;
    private readonly ILogger<DomainProvisioningStateMachine> _logger;
    private readonly Dictionary<DomainProvisioningState, IStateTransitionHandler> _handlerMap;

    public DomainProvisioningStateMachine(
        IDomainProvisioningRepository repository,
        IEnumerable<IStateTransitionHandler> handlers,
        ILogger<DomainProvisioningStateMachine> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Build handler map for O(1) lookup
        _handlerMap = _handlers.ToDictionary(h => h.HandlesState);
    }

    public async Task<StateTransitionResult> ExecuteTransitionAsync(
        DomainProvisioningContext context,
        CancellationToken cancellationToken = default)
    {
        var currentState = context.CurrentState;

        _logger.LogInformation(
            "Executing transition for domain {Domain} in state {State} (retry: {Retry}/{Max})",
            context.Domain, currentState, context.RetryCount, context.MaxRetries);

        // Check if state is terminal
        if (currentState.IsTerminal())
        {
            _logger.LogDebug("Domain {Domain} is in terminal state {State}, no transition needed",
                context.Domain, currentState);
            return StateTransitionResult.SuccessResult(currentState);
        }

        // Check for timeout
        if (context.HasTimedOut())
        {
            _logger.LogWarning("Domain {Domain} timed out in state {State} after {Duration}",
                context.Domain, currentState, DateTime.UtcNow - context.StateEnteredAt);

            var timeoutState = GetTimeoutFailureState(currentState);
            return StateTransitionResult.FailureResult(
                timeoutState,
                DomainErrorCode.AcmeChallengeFailed,
                $"Timeout in state {currentState}");
        }

        // Get handler for current state
        if (!_handlerMap.TryGetValue(currentState, out var handler))
        {
            _logger.LogError("No handler found for state {State}", currentState);
            return StateTransitionResult.FailureResult(
                DomainProvisioningState.Failed,
                DomainErrorCode.AcmeChallengeFailed,
                $"No handler for state {currentState}");
        }

        try
        {
            // Execute handler
            var result = await handler.ExecuteAsync(context, cancellationToken);

            if (result.Success && result.NewState.HasValue)
            {
                // Successful transition
                _logger.LogInformation("Domain {Domain}: {FromState} → {ToState}",
                    context.Domain, currentState, result.NewState.Value);

                // Reset retry count on successful transition
                context.RetryCount = 0;
                return result;
            }
            else if (result.ShouldRetry)
            {
                // Retry needed
                context.RetryCount++;

                _logger.LogWarning("Domain {Domain}: retry {Retry}/{Max} in state {State}: {Reason}",
                    context.Domain, context.RetryCount, context.MaxRetries, currentState, result.ErrorMessage);

                // Check if max retries exceeded
                if (context.HasExceededMaxRetries())
                {
                    _logger.LogError("Domain {Domain}: max retries exceeded in state {State}",
                        context.Domain, currentState);

                    var failureState = GetRetryFailureState(currentState);
                    return StateTransitionResult.FailureResult(
                        failureState,
                        result.ErrorCode ?? DomainErrorCode.AcmeChallengeFailed,
                        $"Max retries exceeded: {result.ErrorMessage}");
                }

                // Stay in current state, will retry later
                return result;
            }
            else
            {
                // Permanent failure
                _logger.LogError("Domain {Domain}: permanent failure in state {State}: {Error}",
                    context.Domain, currentState, result.ErrorMessage);

                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing transition for domain {Domain} in state {State}",
                context.Domain, currentState);

            return StateTransitionResult.FailureResult(
                DomainProvisioningState.Failed,
                DomainErrorCode.AcmeChallengeFailed,
                $"Unexpected error: {ex.Message}");
        }
    }

    public async Task<List<DomainProvisioningContext>> GetPendingContextsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetNonTerminalContextsAsync(cancellationToken);
    }

    public async Task SaveContextAsync(
        DomainProvisioningContext context,
        CancellationToken cancellationToken = default)
    {
        context.UpdatedAt = DateTime.UtcNow;
        await _repository.SaveAsync(context, cancellationToken);
    }

    public async Task RecordTransitionAsync(
        StateTransitionHistory history,
        CancellationToken cancellationToken = default)
    {
        await _repository.SaveTransitionHistoryAsync(history, cancellationToken);
    }

    private static DomainProvisioningState GetTimeoutFailureState(DomainProvisioningState currentState)
    {
        return currentState switch
        {
            DomainProvisioningState.DnsVerifying => DomainProvisioningState.DnsVerificationFailed,
            DomainProvisioningState.AcmeOrdering => DomainProvisioningState.AcmeOrderFailed,
            DomainProvisioningState.AcmeChallengeValidating => DomainProvisioningState.AcmeChallengeFailed,
            DomainProvisioningState.CertificateDownloading => DomainProvisioningState.CertificateDownloadFailed,
            DomainProvisioningState.CertificateDeploying => DomainProvisioningState.CertificateDeploymentFailed,
            _ => DomainProvisioningState.Failed
        };
    }

    private static DomainProvisioningState GetRetryFailureState(DomainProvisioningState currentState)
    {
        return currentState switch
        {
            DomainProvisioningState.DnsVerifying => DomainProvisioningState.DnsVerificationFailed,
            DomainProvisioningState.AcmeOrdering => DomainProvisioningState.AcmeOrderFailed,
            DomainProvisioningState.AcmeChallengeValidating => DomainProvisioningState.AcmeChallengeFailed,
            DomainProvisioningState.CertificateDownloading => DomainProvisioningState.CertificateDownloadFailed,
            DomainProvisioningState.CertificateDeploying => DomainProvisioningState.CertificateDeploymentFailed,
            _ => DomainProvisioningState.Failed
        };
    }
}
