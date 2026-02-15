using Certes;
using Certes.Acme;
using DomainProvisioningService.Application.Interfaces;
using DomainProvisioningService.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DomainProvisioningService.Application.StateMachine.Handlers;

/// <summary>
/// Handler for certificate downloading state
/// Transition: CertificateDownloading → CertificateDownloaded
/// </summary>
public class CertificateDownloadingHandler : IStateTransitionHandler
{
    private readonly ILogger<CertificateDownloadingHandler> _logger;
    private readonly Uri _acmeDirectoryUri;
    private readonly string _acmeEmail;

    public DomainProvisioningState HandlesState => DomainProvisioningState.CertificateDownloading;

    public CertificateDownloadingHandler(
        ILogger<CertificateDownloadingHandler> logger,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var useStaging = configuration.GetValue<bool>("Acme:UseStaging", true);
        _acmeDirectoryUri = useStaging
            ? WellKnownServers.LetsEncryptStagingV2
            : WellKnownServers.LetsEncryptV2;
        _acmeEmail = configuration.GetValue<string>("Acme:Email")
            ?? throw new InvalidOperationException("Acme:Email required");
    }

    public async Task<StateTransitionResult> ExecuteAsync(
        DomainProvisioningContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Downloading certificate for domain: {Domain}", context.Domain);

            if (string.IsNullOrEmpty(context.AcmeOrderUrl))
            {
                return StateTransitionResult.FailureResult(
                    DomainProvisioningState.CertificateDownloadFailed,
                    DomainErrorCode.AcmeChallengeFailed,
                    "AcmeOrderUrl is missing");
            }

            // Recreate ACME context
            var acme = new AcmeContext(_acmeDirectoryUri);
            await acme.NewAccount(_acmeEmail, termsOfServiceAgreed: true);

            // Get order
            var order = acme.Order(new Uri(context.AcmeOrderUrl));

            // Generate private key and download certificate
            var privateKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
            var cert = await order.Generate(new CsrInfo { CommonName = context.Domain }, privateKey);

            var certPem = cert.ToPem();
            var privateKeyPem = privateKey.ToPem();

            // Combine certificate and private key into single PEM (HAProxy format)
            context.CertificatePem = certPem + privateKeyPem;

            // Parse certificate to get expiration date
            using var x509Cert = System.Security.Cryptography.X509Certificates.X509CertificateLoader
                .LoadCertificate(System.Text.Encoding.UTF8.GetBytes(certPem));
            context.CertificateNotAfter = x509Cert.NotAfter;

            _logger.LogInformation("Certificate downloaded successfully for {Domain}, expires: {NotAfter}",
                context.Domain, context.CertificateNotAfter);

            return StateTransitionResult.SuccessResult(DomainProvisioningState.CertificateDownloaded);
        }
        catch (AcmeRequestException ex)
        {
            _logger.LogError(ex, "Failed to download certificate for {Domain}", context.Domain);
            return StateTransitionResult.FailureResult(
                DomainProvisioningState.CertificateDownloadFailed,
                DomainErrorCode.AcmeChallengeFailed,
                $"ACME error: {ex.Error?.Detail ?? ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error downloading certificate for {Domain}", context.Domain);
            return StateTransitionResult.FailureResult(
                DomainProvisioningState.CertificateDownloadFailed,
                DomainErrorCode.AcmeChallengeFailed,
                $"Unexpected error: {ex.Message}");
        }
    }
}
