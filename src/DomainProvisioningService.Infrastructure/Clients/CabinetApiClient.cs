using DomainProvisioningService.Application.Interfaces;
using DomainProvisioningService.Domain;
using Microsoft.Extensions.Logging;

namespace DomainProvisioningService.Infrastructure.Clients;

/// <summary>
/// HTTP client for Cabinet API
/// </summary>
public class CabinetApiClient : ICabinetApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CabinetApiClient> _logger;

    public CabinetApiClient(HttpClient httpClient, ILogger<CabinetApiClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<CustomDomain>> GetCustomDomainsByStatusAsync(
        CustomDomainStatus[] statuses,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement actual HTTP call to Cabinet API
        // GET /internal/v1/custom-domains?statuses=PendingDns,PendingHttpProbe
        _logger.LogDebug("Getting custom domains by status: {Statuses}", string.Join(",", statuses));

        // Stub implementation
        return new List<CustomDomain>();
    }

    public async Task<bool> UpdateCustomDomainStatusAsync(
        Guid customDomainId,
        CustomDomainStatus status,
        DomainErrorCode? errorCode = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement actual HTTP call
        // POST /internal/v1/custom-domains/{id}/status
        _logger.LogInformation("Updating custom domain {Id} to status: {Status}", customDomainId, status);

        // Stub implementation
        return true;
    }

    public async Task<bool> UpdateCustomDomainCertificateAsync(
        Guid customDomainId,
        DateTime certificateNotAfter,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement actual HTTP call
        // POST /internal/v1/custom-domains/{id}/certificate
        _logger.LogInformation("Updating certificate info for custom domain {Id}", customDomainId);

        // Stub implementation
        return true;
    }
}
