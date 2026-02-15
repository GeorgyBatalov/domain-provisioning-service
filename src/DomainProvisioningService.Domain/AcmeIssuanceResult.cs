namespace DomainProvisioningService.Domain;

/// <summary>
/// Result of ACME certificate issuance
/// </summary>
public class AcmeIssuanceResult
{
    public bool Success { get; set; }

    public string? CertificatePem { get; set; }

    public DateTime? NotAfter { get; set; }

    public DomainErrorCode? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public static AcmeIssuanceResult SuccessResult(string certificatePem, DateTime notAfter) => new()
    {
        Success = true,
        CertificatePem = certificatePem,
        NotAfter = notAfter
    };

    public static AcmeIssuanceResult FailureResult(DomainErrorCode errorCode, string errorMessage) => new()
    {
        Success = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };
}
