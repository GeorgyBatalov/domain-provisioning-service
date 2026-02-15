using DomainProvisioningService.Domain;

namespace DomainProvisioningService.Application.Interfaces;

/// <summary>
/// Client for Cabinet API (read CustomDomains, update status)
/// </summary>
public interface ICabinetApiClient
{
    /// <summary>
    /// Get custom domains by status
    /// </summary>
    Task<List<CustomDomain>> GetCustomDomainsByStatusAsync(
        CustomDomainStatus[] statuses,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update custom domain status
    /// </summary>
    Task<bool> UpdateCustomDomainStatusAsync(
        Guid customDomainId,
        CustomDomainStatus status,
        DomainErrorCode? errorCode = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update custom domain with certificate info
    /// </summary>
    Task<bool> UpdateCustomDomainCertificateAsync(
        Guid customDomainId,
        DateTime certificateNotAfter,
        CancellationToken cancellationToken = default);
}
