using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
    private readonly JsonSerializerOptions _jsonOptions;

    public CabinetApiClient(HttpClient httpClient, ILogger<CabinetApiClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<List<CustomDomain>> GetCustomDomainsByStatusAsync(
        CustomDomainStatus[] statuses,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var statusesParam = string.Join(",", statuses.Select(s => s.ToString()));
            var url = $"/internal/v1/custom-domains?statuses={statusesParam}";

            _logger.LogDebug("Getting custom domains by status: {Statuses}", statusesParam);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var domains = await response.Content.ReadFromJsonAsync<List<CustomDomain>>(_jsonOptions, cancellationToken);
            return domains ?? new List<CustomDomain>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to get custom domains from Cabinet API");
            return new List<CustomDomain>();
        }
    }

    public async Task<bool> UpdateCustomDomainStatusAsync(
        Guid customDomainId,
        CustomDomainStatus status,
        DomainErrorCode? errorCode = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/internal/v1/custom-domains/{customDomainId}/status";
            var payload = new
            {
                status = status.ToString(),
                errorCode = errorCode?.ToString(),
                errorMessage
            };

            _logger.LogInformation("Updating custom domain {Id} to status: {Status}", customDomainId, status);

            var response = await _httpClient.PostAsJsonAsync(url, payload, _jsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();

            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to update custom domain {Id} status", customDomainId);
            return false;
        }
    }

    public async Task<bool> UpdateCustomDomainCertificateAsync(
        Guid customDomainId,
        DateTime certificateNotAfter,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/internal/v1/custom-domains/{customDomainId}/certificate";
            var payload = new
            {
                certificateNotAfter
            };

            _logger.LogInformation("Updating certificate info for custom domain {Id}", customDomainId);

            var response = await _httpClient.PostAsJsonAsync(url, payload, _jsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();

            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to update certificate for custom domain {Id}", customDomainId);
            return false;
        }
    }
}
