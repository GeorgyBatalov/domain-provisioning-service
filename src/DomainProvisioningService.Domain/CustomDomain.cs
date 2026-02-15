namespace DomainProvisioningService.Domain;

/// <summary>
/// Custom domain entity (read from Cabinet API)
/// </summary>
public class CustomDomain
{
    public Guid Id { get; set; }

    public string Domain { get; set; } = string.Empty;

    public Guid LicenseId { get; set; }

    public CustomDomainStatus Status { get; set; }

    public string? ExpectedCnameValue { get; set; }

    public DateTime? LastValidatedAt { get; set; }

    public DateTime? ActivatedAt { get; set; }

    public string? ValidationError { get; set; }

    public DomainErrorCode? LastErrorCode { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? ModifiedDate { get; set; }
}
