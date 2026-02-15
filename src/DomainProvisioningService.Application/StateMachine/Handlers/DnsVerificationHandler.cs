using DomainProvisioningService.Application.Interfaces;
using DomainProvisioningService.Domain;
using Microsoft.Extensions.Logging;

namespace DomainProvisioningService.Application.StateMachine.Handlers;

/// <summary>
/// Handler for DNS verification state
/// Transition: Initial/DnsVerifying → DnsVerified or DnsVerificationFailed
/// </summary>
public class DnsVerificationHandler : IStateTransitionHandler
{
    private readonly IDnsVerificationService _dnsService;
    private readonly ILogger<DnsVerificationHandler> _logger;

    public DomainProvisioningState HandlesState => DomainProvisioningState.DnsVerifying;

    public DnsVerificationHandler(
        IDnsVerificationService dnsService,
        ILogger<DnsVerificationHandler> logger)
    {
        _dnsService = dnsService ?? throw new ArgumentNullException(nameof(dnsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<StateTransitionResult> ExecuteAsync(
        DomainProvisioningContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Verifying DNS CNAME for domain: {Domain}", context.Domain);

        var result = await _dnsService.VerifyCnameAsync(
            context.Domain,
            context.ExpectedCnameValue,
            cancellationToken);

        if (result.IsValid)
        {
            _logger.LogInformation("DNS verification successful for {Domain}", context.Domain);
            return StateTransitionResult.SuccessResult(DomainProvisioningState.DnsVerified);
        }

        // CNAME not found or points elsewhere - retry
        _logger.LogWarning("DNS verification failed for {Domain}: {Error}. Will retry.",
            context.Domain, result.ErrorMessage);

        return StateTransitionResult.RetryResult(result.ErrorMessage ?? "CNAME not found");
    }
}
