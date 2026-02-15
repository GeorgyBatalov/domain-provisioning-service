namespace DomainProvisioningService.Domain;

/// <summary>
/// Certificate entry (stored in CertificateStore)
/// </summary>
public class CertificateEntry
{
    public Guid Id { get; set; }

    public string Domain { get; set; } = string.Empty;

    public string CertificatePem { get; set; } = string.Empty;

    public DateTime CertificateNotAfter { get; set; }

    public DateTime IssuedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? AcmeChallengeToken { get; set; }

    public string? AcmeChallengeKeyAuth { get; set; }

    public DateTime? AcmeChallengeExpiresAt { get; set; }

    public Guid? CustomDomainId { get; set; }
}
