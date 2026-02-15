namespace DomainProvisioningService.Domain;

/// <summary>
/// Status of custom domain in the provisioning workflow
/// </summary>
public enum CustomDomainStatus
{
    /// <summary>
    /// Waiting for CNAME record to be created by user
    /// </summary>
    PendingDns,

    /// <summary>
    /// CNAME verified, waiting for HTTP preflight probe
    /// </summary>
    PendingHttpProbe,

    /// <summary>
    /// HTTP probe passed, ACME certificate issuance in progress
    /// </summary>
    Issuing,

    /// <summary>
    /// Certificate issued and deployed, domain is active
    /// </summary>
    Active,

    /// <summary>
    /// Certificate needs renewal (30 days before expiration)
    /// </summary>
    RenewDue,

    /// <summary>
    /// Failed at some step (check lastErrorCode)
    /// </summary>
    Failed,

    /// <summary>
    /// Domain deleted or expired
    /// </summary>
    Inactive
}
