namespace DomainProvisioningService.Domain;

/// <summary>
/// Audit log for state transitions
/// </summary>
public class StateTransitionHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// CustomDomain ID
    /// </summary>
    public Guid CustomDomainId { get; set; }

    /// <summary>
    /// Domain name
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// From state
    /// </summary>
    public DomainProvisioningState FromState { get; set; }

    /// <summary>
    /// To state
    /// </summary>
    public DomainProvisioningState ToState { get; set; }

    /// <summary>
    /// Transition reason/trigger
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Error message (if transition due to error)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Error code (if transition due to error)
    /// </summary>
    public DomainErrorCode? ErrorCode { get; set; }

    /// <summary>
    /// Retry attempt number (if applicable)
    /// </summary>
    public int? RetryAttempt { get; set; }

    /// <summary>
    /// Duration spent in FromState
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// Timestamp of transition
    /// </summary>
    public DateTime TransitionedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional metadata (JSON)
    /// </summary>
    public string? Metadata { get; set; }
}
