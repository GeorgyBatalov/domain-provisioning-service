using Certes;
using Certes.Acme;
using DomainProvisioningService.Application.Interfaces;
using DomainProvisioningService.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DomainProvisioningService.Application.StateMachine.Handlers;

/// <summary>
/// Handler for ACME ordering state
/// Transition: AcmeOrdering → AcmeChallengePreparing
/// </summary>
public class AcmeOrderingHandler : IStateTransitionHandler
{
    private readonly ILogger<AcmeOrderingHandler> _logger;
    private readonly Uri _acmeDirectoryUri;
    private readonly string _acmeEmail;

    public DomainProvisioningState HandlesState => DomainProvisioningState.AcmeOrdering;

    public AcmeOrderingHandler(ILogger<AcmeOrderingHandler> logger, IConfiguration configuration)
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
            _logger.LogInformation("Creating ACME order for domain: {Domain}", context.Domain);

            var acme = new AcmeContext(_acmeDirectoryUri);
            await acme.NewAccount(_acmeEmail, termsOfServiceAgreed: true);

            var order = await acme.NewOrder(new[] { context.Domain });
            context.AcmeOrderUrl = order.Location.ToString();

            var authz = (await order.Authorizations()).First();
            var httpChallenge = await authz.Http();

            if (httpChallenge == null)
            {
                return StateTransitionResult.FailureResult(
                    DomainProvisioningState.AcmeOrderFailed,
                    DomainErrorCode.AcmeChallengeFailed,
                    "HTTP-01 challenge not available");
            }

            context.AcmeChallengeToken = httpChallenge.Token;
            context.AcmeChallengeKeyAuth = httpChallenge.KeyAuthz;

            _logger.LogInformation("ACME order created for {Domain}, challenge token: {Token}",
                context.Domain, context.AcmeChallengeToken);

            return StateTransitionResult.SuccessResult(DomainProvisioningState.AcmeChallengePreparing);
        }
        catch (AcmeRequestException ex)
        {
            _logger.LogError(ex, "ACME order failed for {Domain}", context.Domain);
            return StateTransitionResult.FailureResult(
                DomainProvisioningState.AcmeOrderFailed,
                DomainErrorCode.AcmeChallengeFailed,
                $"ACME error: {ex.Error?.Detail ?? ex.Message}");
        }
    }
}
