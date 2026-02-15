using DomainProvisioningService.Application.Interfaces;
using DomainProvisioningService.Domain;
using Microsoft.Extensions.Logging;

namespace DomainProvisioningService.Infrastructure.Services;

/// <summary>
/// ACME service using Certes library for Let's Encrypt
/// </summary>
public class AcmeService : IAcmeService
{
    private readonly ICertificateStoreRepository _certificateStore;
    private readonly ILogger<AcmeService> _logger;

    public AcmeService(
        ICertificateStoreRepository certificateStore,
        ILogger<AcmeService> logger)
    {
        _certificateStore = certificateStore ?? throw new ArgumentNullException(nameof(certificateStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AcmeIssuanceResult> IssueCertificateAsync(
        string domain,
        Guid customDomainId,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement actual ACME flow using Certes:
        // 1. Create ACME context (staging or production)
        // 2. Create order for domain
        // 3. Get HTTP-01 authorization
        // 4. Save challenge token + keyAuth to CertificateStore
        // 5. Wait for Let's Encrypt to validate (GET /.well-known/acme-challenge/{token} via HAProxy Agent)
        // 6. Download certificate
        // 7. Return certificate PEM

        _logger.LogInformation("Issuing certificate for domain: {Domain}", domain);

        // Stub implementation
        return AcmeIssuanceResult.SuccessResult("STUB_CERTIFICATE_PEM", DateTime.UtcNow.AddDays(90));
    }
}
