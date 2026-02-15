using DomainProvisioningService.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace DomainProvisioningService.Infrastructure.Clients;

/// <summary>
/// HTTP client for HAProxyDomainAgent API
/// </summary>
public class HAProxyAgentClient : IHAProxyAgentClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HAProxyAgentClient> _logger;

    public HAProxyAgentClient(HttpClient httpClient, ILogger<HAProxyAgentClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> ApplyCertificateAsync(
        string domain,
        string certificatePem,
        DateTime certificateNotAfter,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement actual HTTP call
        // POST /internal/v1/certificates/apply
        _logger.LogInformation("Applying certificate for domain: {Domain}", domain);

        // Stub implementation
        return true;
    }

    public async Task<bool> DeleteCertificateAsync(
        string domain,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement actual HTTP call
        // DELETE /internal/v1/certificates/{domain}
        _logger.LogInformation("Deleting certificate for domain: {Domain}", domain);

        // Stub implementation
        return true;
    }
}
