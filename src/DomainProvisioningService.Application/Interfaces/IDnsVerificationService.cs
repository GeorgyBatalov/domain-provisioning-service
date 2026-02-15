using DomainProvisioningService.Domain;

namespace DomainProvisioningService.Application.Interfaces;

/// <summary>
/// DNS verification service (CNAME check)
/// </summary>
public interface IDnsVerificationService
{
    /// <summary>
    /// Verify that domain has CNAME pointing to expected value
    /// </summary>
    Task<DnsCheckResult> VerifyCnameAsync(
        string domain,
        string expectedCname,
        CancellationToken cancellationToken = default);
}
