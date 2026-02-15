using DomainProvisioningService.Domain;

namespace DomainProvisioningService.Application.Interfaces;

/// <summary>
/// Result of state transition attempt
/// </summary>
public class StateTransitionResult
{
    public bool Success { get; set; }
    public DomainProvisioningState? NewState { get; set; }
    public string? ErrorMessage { get; set; }
    public DomainErrorCode? ErrorCode { get; set; }
    public bool ShouldRetry { get; set; }

    public static StateTransitionResult SuccessResult(DomainProvisioningState newState)
    {
        return new StateTransitionResult
        {
            Success = true,
            NewState = newState
        };
    }

    public static StateTransitionResult FailureResult(
        DomainProvisioningState failureState,
        DomainErrorCode errorCode,
        string errorMessage,
        bool shouldRetry = false)
    {
        return new StateTransitionResult
        {
            Success = false,
            NewState = failureState,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            ShouldRetry = shouldRetry
        };
    }

    public static StateTransitionResult RetryResult(string reason)
    {
        return new StateTransitionResult
        {
            Success = false,
            ShouldRetry = true,
            ErrorMessage = reason
        };
    }
}

/// <summary>
/// Handler for state transitions
/// Each handler is responsible for one type of transition
/// </summary>
public interface IStateTransitionHandler
{
    /// <summary>
    /// States that this handler can process
    /// </summary>
    DomainProvisioningState HandlesState { get; }

    /// <summary>
    /// Execute the transition
    /// </summary>
    Task<StateTransitionResult> ExecuteAsync(
        DomainProvisioningContext context,
        CancellationToken cancellationToken = default);
}
