namespace DomainProvisioningService.Domain;

/// <summary>
/// Error codes for custom domain provisioning failures
/// </summary>
public enum DomainErrorCode
{
    None,
    CnameNotFound,
    CnamePointsElsewhere,
    HttpProbeFailed,
    AcmeChallengeFailed,
    AcmeRateLimitExceeded,
    CertificateDeployFailed,
    DnsTimeout,
    InvalidDomain
}
