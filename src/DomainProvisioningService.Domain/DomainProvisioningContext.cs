namespace DomainProvisioningService.Domain;

/// <summary>
/// State machine context for domain provisioning
/// Contains all data needed to execute state transitions
/// </summary>
public class DomainProvisioningContext
{
    /// <summary>
    /// CustomDomain ID from Cabinet API
    /// </summary>
    public Guid CustomDomainId { get; set; }

    /// <summary>
    /// Domain name (e.g., "example.com")
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// Expected CNAME value for DNS verification
    /// </summary>
    public string ExpectedCnameValue { get; set; } = string.Empty;

    /// <summary>
    /// Current state in the state machine
    /// </summary>
    public DomainProvisioningState CurrentState { get; set; } = DomainProvisioningState.Initial;

    /// <summary>
    /// Previous state (for rollback/debugging)
    /// </summary>
    public DomainProvisioningState? PreviousState { get; set; }

    /// <summary>
    /// Number of retry attempts for current state
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Maximum retry attempts before failure
    /// </summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>
    /// Timestamp when entered current state
    /// </summary>
    public DateTime StateEnteredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timeout for current state (if null, no timeout)
    /// </summary>
    public TimeSpan? StateTimeout { get; set; }

    /// <summary>
    /// Last error message (if any)
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Last error code (if any)
    /// </summary>
    public DomainErrorCode? LastErrorCode { get; set; }

    /// <summary>
    /// ACME context data (serialized JSON or separate fields)
    /// </summary>
    public string? AcmeOrderUrl { get; set; }
    public string? AcmeChallengeToken { get; set; }
    public string? AcmeChallengeKeyAuth { get; set; }

    /// <summary>
    /// Certificate data
    /// </summary>
    public string? CertificatePem { get; set; }
    public DateTime? CertificateNotAfter { get; set; }

    /// <summary>
    /// Timestamps
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Check if state has timed out
    /// </summary>
    public bool HasTimedOut()
    {
        if (!StateTimeout.HasValue)
            return false;

        return DateTime.UtcNow - StateEnteredAt > StateTimeout.Value;
    }

    /// <summary>
    /// Check if max retries exceeded
    /// </summary>
    public bool HasExceededMaxRetries()
    {
        return RetryCount >= MaxRetries;
    }
}
