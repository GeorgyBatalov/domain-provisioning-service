using System.Net.Http.Json;
using System.Text.Json;
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
    private readonly JsonSerializerOptions _jsonOptions;

    public HAProxyAgentClient(HttpClient httpClient, ILogger<HAProxyAgentClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<bool> ApplyCertificateAsync(
        string domain,
        string certificatePem,
        DateTime certificateNotAfter,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = "/internal/v1/certificates/apply";
            var payload = new
            {
                domain,
                certificatePem,
                certificateNotAfter
            };

            _logger.LogInformation("Applying certificate for domain: {Domain}", domain);

            var response = await _httpClient.PostAsJsonAsync(url, payload, _jsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();

            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to apply certificate for domain: {Domain}", domain);
            return false;
        }
    }

    public async Task<bool> DeleteCertificateAsync(
        string domain,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/internal/v1/certificates/{domain}";

            _logger.LogInformation("Deleting certificate for domain: {Domain}", domain);

            var response = await _httpClient.DeleteAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to delete certificate for domain: {Domain}", domain);
            return false;
        }
    }
}
