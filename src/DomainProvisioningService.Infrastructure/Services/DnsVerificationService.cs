using DnsClient;
using DomainProvisioningService.Application.Interfaces;
using DomainProvisioningService.Domain;
using Microsoft.Extensions.Logging;

namespace DomainProvisioningService.Infrastructure.Services;

/// <summary>
/// DNS verification service using DnsClient.NET
/// </summary>
public class DnsVerificationService : IDnsVerificationService
{
    private readonly ILogger<DnsVerificationService> _logger;
    private readonly LookupClient _dnsClient;

    public DnsVerificationService(ILogger<DnsVerificationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dnsClient = new LookupClient();
    }

    public async Task<DnsCheckResult> VerifyCnameAsync(
        string domain,
        string expectedCname,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Verifying CNAME for domain: {Domain}, expected: {Expected}", domain, expectedCname);

            // Normalize expected CNAME (remove trailing dot if present)
            var normalizedExpected = expectedCname.TrimEnd('.');

            // Query CNAME records
            var result = await _dnsClient.QueryAsync(domain, QueryType.CNAME, cancellationToken: cancellationToken);

            if (result.HasError)
            {
                _logger.LogWarning("DNS query failed for {Domain}: {Error}", domain, result.ErrorMessage);
                return DnsCheckResult.Failure(
                    DomainErrorCode.CnameNotFound,
                    $"DNS query failed: {result.ErrorMessage}");
            }

            var cnameRecords = result.Answers.CnameRecords().ToList();

            if (!cnameRecords.Any())
            {
                _logger.LogWarning("No CNAME record found for {Domain}", domain);
                return DnsCheckResult.Failure(
                    DomainErrorCode.CnameNotFound,
                    "No CNAME record found");
            }

            // Get the first CNAME record and normalize it
            var actualCname = cnameRecords.First().CanonicalName.Value.TrimEnd('.');

            _logger.LogDebug("Found CNAME for {Domain}: {ActualCname}", domain, actualCname);

            // Compare normalized values
            if (actualCname.Equals(normalizedExpected, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("CNAME verification successful for {Domain}: {ActualCname}", domain, actualCname);
                return DnsCheckResult.Success(actualCname);
            }

            _logger.LogWarning("CNAME mismatch for {Domain}: expected {Expected}, got {Actual}",
                domain, normalizedExpected, actualCname);

            return DnsCheckResult.Failure(
                DomainErrorCode.CnamePointsElsewhere,
                $"CNAME points to {actualCname}, expected {normalizedExpected}");
        }
        catch (DnsResponseException ex)
        {
            _logger.LogError(ex, "DNS query exception for {Domain}", domain);
            return DnsCheckResult.Failure(
                DomainErrorCode.CnameNotFound,
                $"DNS query exception: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during CNAME verification for {Domain}", domain);
            return DnsCheckResult.Failure(
                DomainErrorCode.CnameNotFound,
                $"Unexpected error: {ex.Message}");
        }
    }
}
