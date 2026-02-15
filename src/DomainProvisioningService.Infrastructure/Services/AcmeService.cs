using System.Security.Cryptography;
using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using DomainProvisioningService.Application.Interfaces;
using DomainProvisioningService.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;

namespace DomainProvisioningService.Infrastructure.Services;

/// <summary>
/// ACME service using Certes library for Let's Encrypt
/// </summary>
public class AcmeService : IAcmeService
{
    private readonly ICertificateStoreRepository _certificateStore;
    private readonly ILogger<AcmeService> _logger;
    private readonly Uri _acmeDirectoryUri;
    private readonly string _acmeEmail;

    public AcmeService(
        ICertificateStoreRepository certificateStore,
        ILogger<AcmeService> logger,
        IConfiguration configuration)
    {
        _certificateStore = certificateStore ?? throw new ArgumentNullException(nameof(certificateStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Read configuration
        var useStaging = configuration.GetValue<bool>("Acme:UseStaging", true);
        _acmeDirectoryUri = useStaging
            ? WellKnownServers.LetsEncryptStagingV2
            : WellKnownServers.LetsEncryptV2;

        _acmeEmail = configuration.GetValue<string>("Acme:Email")
            ?? throw new InvalidOperationException("Acme:Email configuration is required");

        _logger.LogInformation("ACME service initialized with {Server} (Email: {Email})",
            useStaging ? "Let's Encrypt Staging" : "Let's Encrypt Production", _acmeEmail);
    }

    public async Task<AcmeIssuanceResult> IssueCertificateAsync(
        string domain,
        Guid customDomainId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting ACME certificate issuance for domain: {Domain}", domain);

            // 1. Create ACME context
            var acme = new AcmeContext(_acmeDirectoryUri);
            var account = await acme.NewAccount(_acmeEmail, termsOfServiceAgreed: true);

            _logger.LogDebug("ACME account created/retrieved for {Email}", _acmeEmail);

            // 2. Create order for domain
            var order = await acme.NewOrder(new[] { domain });
            _logger.LogDebug("ACME order created for {Domain}", domain);

            // 3. Get HTTP-01 authorization
            var authz = (await order.Authorizations()).First();
            var httpChallenge = await authz.Http();

            if (httpChallenge == null)
            {
                return AcmeIssuanceResult.FailureResult(
                    DomainErrorCode.AcmeChallengeFailed,
                    "HTTP-01 challenge not available");
            }

            var token = httpChallenge.Token;
            var keyAuthz = httpChallenge.KeyAuthz;

            _logger.LogInformation("HTTP-01 challenge obtained for {Domain}: token={Token}", domain, token);

            // 4. Save challenge token + keyAuth to CertificateStore
            // ACME challenges expire after 1 hour typically
            var challengeExpiresAt = DateTime.UtcNow.AddHours(1);

            await _certificateStore.SaveAcmeChallengeAsync(
                domain,
                token,
                keyAuthz,
                challengeExpiresAt,
                customDomainId,
                cancellationToken);

            _logger.LogDebug("ACME challenge saved to CertificateStore for {Domain}", domain);

            // 5. Validate challenge
            _logger.LogInformation("Validating HTTP-01 challenge for {Domain}...", domain);
            var challengeResult = await httpChallenge.Validate();

            // Wait for validation to complete (max 60 seconds)
            var maxAttempts = 30;
            var attempt = 0;

            while (attempt < maxAttempts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var resource = await httpChallenge.Resource();

                if (resource.Status == ChallengeStatus.Valid)
                {
                    _logger.LogInformation("HTTP-01 challenge validated successfully for {Domain}", domain);
                    break;
                }

                if (resource.Status == ChallengeStatus.Invalid)
                {
                    var error = resource.Error?.Detail ?? "Unknown error";
                    _logger.LogError("HTTP-01 challenge failed for {Domain}: {Error}", domain, error);

                    return AcmeIssuanceResult.FailureResult(
                        DomainErrorCode.AcmeChallengeFailed,
                        $"Challenge validation failed: {error}");
                }

                // Still pending, wait and retry
                await Task.Delay(2000, cancellationToken);
                attempt++;
            }

            if (attempt >= maxAttempts)
            {
                return AcmeIssuanceResult.FailureResult(
                    DomainErrorCode.AcmeChallengeFailed,
                    "Challenge validation timed out");
            }

            // 6. Download certificate
            _logger.LogInformation("Downloading certificate for {Domain}...", domain);

            var privateKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
            var cert = await order.Generate(new CsrInfo { CommonName = domain }, privateKey);

            var certPem = cert.ToPem();
            var privateKeyPem = privateKey.ToPem();

            // Combine certificate and private key into single PEM (HAProxy format)
            var fullChainPem = certPem + privateKeyPem;

            // Parse certificate to get expiration date
            // Use X509CertificateLoader for .NET 9+
            using var x509Cert = System.Security.Cryptography.X509Certificates.X509CertificateLoader
                .LoadCertificate(System.Text.Encoding.UTF8.GetBytes(certPem));
            var certificateNotAfter = x509Cert.NotAfter;

            _logger.LogInformation("Certificate issued successfully for {Domain}, expires: {NotAfter}",
                domain, certificateNotAfter);

            return AcmeIssuanceResult.SuccessResult(fullChainPem, certificateNotAfter);
        }
        catch (AcmeRequestException ex)
        {
            _logger.LogError(ex, "ACME request failed for {Domain}: {Error}", domain, ex.Error?.Detail);

            return AcmeIssuanceResult.FailureResult(
                DomainErrorCode.AcmeChallengeFailed,
                $"ACME error: {ex.Error?.Detail ?? ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during certificate issuance for {Domain}", domain);

            return AcmeIssuanceResult.FailureResult(
                DomainErrorCode.AcmeChallengeFailed,
                $"Unexpected error: {ex.Message}");
        }
    }
}
