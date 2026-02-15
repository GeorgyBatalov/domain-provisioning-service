using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using DomainProvisioningService.Application.Interfaces;
using DomainProvisioningService.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DomainProvisioningService.Application.StateMachine.Handlers;

/// <summary>
/// Handler for ACME challenge validation state
/// Transition: AcmeChallengeValidating → AcmeChallengeValidated (with retry)
/// </summary>
public class AcmeChallengeValidatingHandler : IStateTransitionHandler
{
    private readonly ILogger<AcmeChallengeValidatingHandler> _logger;
    private readonly Uri _acmeDirectoryUri;
    private readonly string _acmeEmail;

    public DomainProvisioningState HandlesState => DomainProvisioningState.AcmeChallengeValidating;

    public AcmeChallengeValidatingHandler(
        ILogger<AcmeChallengeValidatingHandler> logger,
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
            _logger.LogInformation("Checking ACME challenge validation status for domain: {Domain} (retry: {Retry})",
                context.Domain, context.RetryCount);

            if (string.IsNullOrEmpty(context.AcmeOrderUrl))
            {
                return StateTransitionResult.FailureResult(
                    DomainProvisioningState.AcmeChallengeFailed,
                    DomainErrorCode.AcmeChallengeFailed,
                    "AcmeOrderUrl is missing");
            }

            // Recreate ACME context
            var acme = new AcmeContext(_acmeDirectoryUri);
            await acme.NewAccount(_acmeEmail, termsOfServiceAgreed: true);

            // Get order by URL
            var order = acme.Order(new Uri(context.AcmeOrderUrl));
            var authz = (await order.Authorizations()).First();
            var httpChallenge = await authz.Http();

            if (httpChallenge == null)
            {
                return StateTransitionResult.FailureResult(
                    DomainProvisioningState.AcmeChallengeFailed,
                    DomainErrorCode.AcmeChallengeFailed,
                    "HTTP-01 challenge not found");
            }

            // Trigger validation on first retry
            if (context.RetryCount == 0)
            {
                _logger.LogInformation("Triggering ACME challenge validation for {Domain}", context.Domain);
                await httpChallenge.Validate();
            }

            // Check challenge status
            var resource = await httpChallenge.Resource();

            _logger.LogDebug("ACME challenge status for {Domain}: {Status}", context.Domain, resource.Status);

            if (resource.Status == ChallengeStatus.Valid)
            {
                _logger.LogInformation("ACME challenge validated successfully for {Domain}", context.Domain);
                return StateTransitionResult.SuccessResult(DomainProvisioningState.AcmeChallengeValidated);
            }

            if (resource.Status == ChallengeStatus.Invalid)
            {
                var error = resource.Error?.Detail ?? "Unknown error";
                _logger.LogError("ACME challenge failed for {Domain}: {Error}", context.Domain, error);

                return StateTransitionResult.FailureResult(
                    DomainProvisioningState.AcmeChallengeFailed,
                    DomainErrorCode.AcmeChallengeFailed,
                    $"Challenge validation failed: {error}");
            }

            // Still pending - retry
            _logger.LogDebug("ACME challenge still pending for {Domain}, will retry", context.Domain);
            return StateTransitionResult.RetryResult("Challenge validation in progress");
        }
        catch (AcmeRequestException ex)
        {
            _logger.LogError(ex, "ACME request failed for {Domain}", context.Domain);
            return StateTransitionResult.FailureResult(
                DomainProvisioningState.AcmeChallengeFailed,
                DomainErrorCode.AcmeChallengeFailed,
                $"ACME error: {ex.Error?.Detail ?? ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error validating challenge for {Domain}", context.Domain);
            return StateTransitionResult.RetryResult($"Unexpected error: {ex.Message}");
        }
    }
}
