namespace DomainProvisioningService.Application.Interfaces;

/// <summary>
/// Client for HAProxyDomainAgent API
/// </summary>
public interface IHAProxyAgentClient
{
    /// <summary>
    /// Deploy certificate to HAProxy via agent
    /// POST /internal/v1/certificates/apply
    /// </summary>
    Task<bool> ApplyCertificateAsync(
        string domain,
        string certificatePem,
        DateTime certificateNotAfter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete certificate from HAProxy
    /// DELETE /internal/v1/certificates/{domain}
    /// </summary>
    Task<bool> DeleteCertificateAsync(
        string domain,
        CancellationToken cancellationToken = default);
}
