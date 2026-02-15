namespace DomainProvisioningService.Domain;

/// <summary>
/// Result of DNS CNAME verification
/// </summary>
public class DnsCheckResult
{
    public bool IsValid { get; set; }

    public string? ActualCname { get; set; }

    public DomainErrorCode? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public static DnsCheckResult Success(string actualCname) => new()
    {
        IsValid = true,
        ActualCname = actualCname
    };

    public static DnsCheckResult Failure(DomainErrorCode errorCode, string errorMessage) => new()
    {
        IsValid = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };
}
