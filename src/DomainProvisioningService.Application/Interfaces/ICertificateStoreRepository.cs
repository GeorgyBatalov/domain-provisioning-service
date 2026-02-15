using DomainProvisioningService.Domain;

namespace DomainProvisioningService.Application.Interfaces;

/// <summary>
/// Repository for CertificateStore (PostgreSQL)
/// </summary>
public interface ICertificateStoreRepository
{
    /// <summary>
    /// Save certificate to store
    /// </summary>
    Task<CertificateEntry> SaveCertificateAsync(
        string domain,
        string certificatePem,
        DateTime certificateNotAfter,
        Guid? customDomainId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Save ACME challenge for domain
    /// </summary>
    Task SaveAcmeChallengeAsync(
        string domain,
        string token,
        string keyAuthorization,
        DateTime expiresAt,
        Guid? customDomainId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get certificates expiring within days
    /// </summary>
    Task<List<CertificateEntry>> GetExpiringCertificatesAsync(
        int daysBeforeExpiration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get certificate by domain
    /// </summary>
    Task<CertificateEntry?> GetCertificateByDomainAsync(
        string domain,
        CancellationToken cancellationToken = default);
}
