namespace DomainProvisioningService.Domain;

/// <summary>
/// State machine states for domain provisioning with Let's Encrypt
/// </summary>
public enum DomainProvisioningState
{
    // Initial state
    Initial = 0,

    // DNS verification phase
    DnsVerifying = 10,
    DnsVerified = 11,
    DnsVerificationFailed = 12,

    // ACME order phase
    AcmeOrdering = 20,
    AcmeOrderCreated = 21,
    AcmeOrderFailed = 22,

    // ACME challenge phase
    AcmeChallengePreparing = 30,
    AcmeChallengePrepared = 31,
    AcmeChallengeValidating = 32,
    AcmeChallengeValidated = 33,
    AcmeChallengeFailed = 34,

    // Certificate download phase
    CertificateDownloading = 40,
    CertificateDownloaded = 41,
    CertificateDownloadFailed = 42,

    // Certificate deployment phase
    CertificateDeploying = 50,
    CertificateDeployed = 51,
    CertificateDeploymentFailed = 52,

    // Final states
    Active = 100,
    Failed = 999,

    // Renewal states
    RenewalDue = 200,
    Renewing = 201
}

/// <summary>
/// Extension methods for DomainProvisioningState
/// </summary>
public static class DomainProvisioningStateExtensions
{
    /// <summary>
    /// Check if state is terminal (no further automatic transitions)
    /// </summary>
    public static bool IsTerminal(this DomainProvisioningState state)
    {
        return state is DomainProvisioningState.Active
            or DomainProvisioningState.Failed
            or DomainProvisioningState.DnsVerificationFailed
            or DomainProvisioningState.AcmeOrderFailed
            or DomainProvisioningState.AcmeChallengeFailed
            or DomainProvisioningState.CertificateDownloadFailed
            or DomainProvisioningState.CertificateDeploymentFailed;
    }

    /// <summary>
    /// Check if state is a failure state
    /// </summary>
    public static bool IsFailure(this DomainProvisioningState state)
    {
        return state is DomainProvisioningState.Failed
            or DomainProvisioningState.DnsVerificationFailed
            or DomainProvisioningState.AcmeOrderFailed
            or DomainProvisioningState.AcmeChallengeFailed
            or DomainProvisioningState.CertificateDownloadFailed
            or DomainProvisioningState.CertificateDeploymentFailed;
    }

    /// <summary>
    /// Check if state requires retry logic
    /// </summary>
    public static bool SupportsRetry(this DomainProvisioningState state)
    {
        return state is DomainProvisioningState.DnsVerifying
            or DomainProvisioningState.AcmeChallengeValidating;
    }

    /// <summary>
    /// Get human-readable description
    /// </summary>
    public static string GetDescription(this DomainProvisioningState state)
    {
        return state switch
        {
            DomainProvisioningState.Initial => "Initial state, waiting to start",
            DomainProvisioningState.DnsVerifying => "Verifying CNAME DNS record",
            DomainProvisioningState.DnsVerified => "DNS verification successful",
            DomainProvisioningState.DnsVerificationFailed => "DNS verification failed after retries",
            DomainProvisioningState.AcmeOrdering => "Creating ACME order with Let's Encrypt",
            DomainProvisioningState.AcmeOrderCreated => "ACME order created",
            DomainProvisioningState.AcmeOrderFailed => "ACME order creation failed",
            DomainProvisioningState.AcmeChallengePreparing => "Preparing HTTP-01 challenge",
            DomainProvisioningState.AcmeChallengePrepared => "HTTP-01 challenge saved to CertificateStore",
            DomainProvisioningState.AcmeChallengeValidating => "Waiting for Let's Encrypt to validate challenge",
            DomainProvisioningState.AcmeChallengeValidated => "Challenge validated by Let's Encrypt",
            DomainProvisioningState.AcmeChallengeFailed => "Challenge validation failed",
            DomainProvisioningState.CertificateDownloading => "Downloading certificate from Let's Encrypt",
            DomainProvisioningState.CertificateDownloaded => "Certificate downloaded successfully",
            DomainProvisioningState.CertificateDownloadFailed => "Certificate download failed",
            DomainProvisioningState.CertificateDeploying => "Deploying certificate to HAProxy",
            DomainProvisioningState.CertificateDeployed => "Certificate deployed to HAProxy",
            DomainProvisioningState.CertificateDeploymentFailed => "Certificate deployment to HAProxy failed",
            DomainProvisioningState.Active => "Domain is active with HTTPS",
            DomainProvisioningState.Failed => "Provisioning failed",
            DomainProvisioningState.RenewalDue => "Certificate renewal is due",
            DomainProvisioningState.Renewing => "Certificate is being renewed",
            _ => "Unknown state"
        };
    }
}
