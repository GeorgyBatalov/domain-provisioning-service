using DomainProvisioningService.Domain;

namespace DomainProvisioningService.Application.Interfaces;

/// <summary>
/// ACME service for Let's Encrypt certificate issuance
/// </summary>
public interface IAcmeService
{
    /// <summary>
    /// Request certificate for domain using HTTP-01 challenge
    /// Saves challenge to CertificateStore for HAProxyDomainAgent to serve
    /// </summary>
    Task<AcmeIssuanceResult> IssueCertificateAsync(
        string domain,
        Guid customDomainId,
        CancellationToken cancellationToken = default);
}
