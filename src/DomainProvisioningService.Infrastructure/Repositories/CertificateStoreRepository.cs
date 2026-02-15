using DomainProvisioningService.Application.Interfaces;
using DomainProvisioningService.Domain;
using DomainProvisioningService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DomainProvisioningService.Infrastructure.Repositories;

/// <summary>
/// Repository for CertificateStore (PostgreSQL)
/// </summary>
public class CertificateStoreRepository : ICertificateStoreRepository
{
    private readonly CertificateStoreDbContext _dbContext;
    private readonly ILogger<CertificateStoreRepository> _logger;

    public CertificateStoreRepository(
        CertificateStoreDbContext dbContext,
        ILogger<CertificateStoreRepository> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CertificateEntry> SaveCertificateAsync(
        string domain,
        string certificatePem,
        DateTime certificateNotAfter,
        Guid? customDomainId = null,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.CertificateStore
            .FirstOrDefaultAsync(c => c.Domain == domain.ToLowerInvariant(), cancellationToken);

        if (existing != null)
        {
            // Update existing
            existing.CertificatePem = certificatePem;
            existing.CertificateNotAfter = certificateNotAfter;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.CustomDomainId = customDomainId;

            _logger.LogInformation("Updated certificate for domain: {Domain}", domain);
        }
        else
        {
            // Create new
            existing = new CertificateEntry
            {
                Id = Guid.NewGuid(),
                Domain = domain.ToLowerInvariant(),
                CertificatePem = certificatePem,
                CertificateNotAfter = certificateNotAfter,
                IssuedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CustomDomainId = customDomainId
            };

            _dbContext.CertificateStore.Add(existing);
            _logger.LogInformation("Created new certificate for domain: {Domain}", domain);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task SaveAcmeChallengeAsync(
        string domain,
        string token,
        string keyAuthorization,
        DateTime expiresAt,
        Guid? customDomainId = null,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.CertificateStore
            .FirstOrDefaultAsync(c => c.Domain == domain.ToLowerInvariant(), cancellationToken);

        if (existing != null)
        {
            existing.AcmeChallengeToken = token;
            existing.AcmeChallengeKeyAuth = keyAuthorization;
            existing.AcmeChallengeExpiresAt = expiresAt;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            existing = new CertificateEntry
            {
                Id = Guid.NewGuid(),
                Domain = domain.ToLowerInvariant(),
                CertificatePem = string.Empty, // Will be filled after ACME validation
                CertificateNotAfter = DateTime.MinValue,
                IssuedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                AcmeChallengeToken = token,
                AcmeChallengeKeyAuth = keyAuthorization,
                AcmeChallengeExpiresAt = expiresAt,
                CustomDomainId = customDomainId
            };

            _dbContext.CertificateStore.Add(existing);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Saved ACME challenge for domain: {Domain}, token: {Token}", domain, token);
    }

    public async Task<List<CertificateEntry>> GetExpiringCertificatesAsync(
        int daysBeforeExpiration,
        CancellationToken cancellationToken = default)
    {
        var threshold = DateTime.UtcNow.AddDays(daysBeforeExpiration);

        var certificates = await _dbContext.CertificateStore
            .Where(c => c.CertificateNotAfter < threshold && c.CertificateNotAfter > DateTime.UtcNow)
            .OrderBy(c => c.CertificateNotAfter)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Found {Count} expiring certificates (within {Days} days)", certificates.Count, daysBeforeExpiration);
        return certificates;
    }

    public async Task<CertificateEntry?> GetCertificateByDomainAsync(
        string domain,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.CertificateStore
            .FirstOrDefaultAsync(c => c.Domain == domain.ToLowerInvariant(), cancellationToken);
    }
}
