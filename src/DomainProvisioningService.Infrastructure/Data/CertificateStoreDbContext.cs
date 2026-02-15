using DomainProvisioningService.Domain;
using Microsoft.EntityFrameworkCore;

namespace DomainProvisioningService.Infrastructure.Data;

/// <summary>
/// DbContext for CertificateStore (PostgreSQL)
/// Shared database with Cabinet API
/// </summary>
public class CertificateStoreDbContext : DbContext
{
    public CertificateStoreDbContext(DbContextOptions<CertificateStoreDbContext> options)
        : base(options)
    {
    }

    public DbSet<CertificateEntry> CertificateStore => Set<CertificateEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<CertificateEntry>(entity =>
        {
            entity.ToTable("certificate_store");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id");

            entity.Property(e => e.Domain)
                .HasColumnName("domain")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.CertificatePem)
                .HasColumnName("certificate_pem")
                .IsRequired();

            entity.Property(e => e.CertificateNotAfter)
                .HasColumnName("certificate_not_after")
                .IsRequired();

            entity.Property(e => e.IssuedAt)
                .HasColumnName("issued_at")
                .IsRequired();

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .IsRequired();

            entity.Property(e => e.AcmeChallengeToken)
                .HasColumnName("acme_challenge_token")
                .HasMaxLength(255);

            entity.Property(e => e.AcmeChallengeKeyAuth)
                .HasColumnName("acme_challenge_key_auth");

            entity.Property(e => e.AcmeChallengeExpiresAt)
                .HasColumnName("acme_challenge_expires_at");

            entity.Property(e => e.CustomDomainId)
                .HasColumnName("custom_domain_id");

            entity.HasIndex(e => e.Domain)
                .IsUnique();

            entity.HasIndex(e => e.CertificateNotAfter);

            entity.HasIndex(e => e.AcmeChallengeToken);
        });
    }
}
