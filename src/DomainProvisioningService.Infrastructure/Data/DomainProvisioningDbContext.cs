using DomainProvisioningService.Domain;
using Microsoft.EntityFrameworkCore;

namespace DomainProvisioningService.Infrastructure.Data;

/// <summary>
/// DbContext for DomainProvisioning state machine persistence
/// </summary>
public class DomainProvisioningDbContext : DbContext
{
    public DomainProvisioningDbContext(DbContextOptions<DomainProvisioningDbContext> options)
        : base(options)
    {
    }

    public DbSet<DomainProvisioningContext> DomainProvisioningContexts { get; set; }
    public DbSet<StateTransitionHistory> StateTransitionHistory { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // DomainProvisioningContext table
        modelBuilder.Entity<DomainProvisioningContext>(entity =>
        {
            entity.ToTable("domain_provisioning_contexts");
            entity.HasKey(e => e.CustomDomainId);

            entity.Property(e => e.CustomDomainId).HasColumnName("custom_domain_id");
            entity.Property(e => e.Domain).HasColumnName("domain").HasMaxLength(255).IsRequired();
            entity.Property(e => e.ExpectedCnameValue).HasColumnName("expected_cname_value").HasMaxLength(500);
            entity.Property(e => e.CurrentState).HasColumnName("current_state").IsRequired();
            entity.Property(e => e.PreviousState).HasColumnName("previous_state");
            entity.Property(e => e.RetryCount).HasColumnName("retry_count").HasDefaultValue(0);
            entity.Property(e => e.MaxRetries).HasColumnName("max_retries").HasDefaultValue(5);
            entity.Property(e => e.StateEnteredAt).HasColumnName("state_entered_at").IsRequired();
            entity.Property(e => e.StateTimeout).HasColumnName("state_timeout_seconds")
                .HasConversion(
                    v => v.HasValue ? (int?)v.Value.TotalSeconds : null,
                    v => v.HasValue ? TimeSpan.FromSeconds(v.Value) : null);
            entity.Property(e => e.LastError).HasColumnName("last_error").HasMaxLength(2000);
            entity.Property(e => e.LastErrorCode).HasColumnName("last_error_code");
            entity.Property(e => e.AcmeOrderUrl).HasColumnName("acme_order_url").HasMaxLength(500);
            entity.Property(e => e.AcmeChallengeToken).HasColumnName("acme_challenge_token").HasMaxLength(200);
            entity.Property(e => e.AcmeChallengeKeyAuth).HasColumnName("acme_challenge_key_auth").HasMaxLength(500);
            entity.Property(e => e.CertificatePem).HasColumnName("certificate_pem");
            entity.Property(e => e.CertificateNotAfter).HasColumnName("certificate_not_after");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

            entity.HasIndex(e => e.Domain).IsUnique();
            entity.HasIndex(e => e.CurrentState);
            entity.HasIndex(e => e.UpdatedAt);
        });

        // StateTransitionHistory table
        modelBuilder.Entity<StateTransitionHistory>(entity =>
        {
            entity.ToTable("state_transition_history");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CustomDomainId).HasColumnName("custom_domain_id").IsRequired();
            entity.Property(e => e.Domain).HasColumnName("domain").HasMaxLength(255).IsRequired();
            entity.Property(e => e.FromState).HasColumnName("from_state").IsRequired();
            entity.Property(e => e.ToState).HasColumnName("to_state").IsRequired();
            entity.Property(e => e.Reason).HasColumnName("reason").HasMaxLength(500);
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
            entity.Property(e => e.ErrorCode).HasColumnName("error_code");
            entity.Property(e => e.RetryAttempt).HasColumnName("retry_attempt");
            entity.Property(e => e.Duration).HasColumnName("duration_seconds")
                .HasConversion(
                    v => v.HasValue ? (double?)v.Value.TotalSeconds : null,
                    v => v.HasValue ? TimeSpan.FromSeconds(v.Value) : null);
            entity.Property(e => e.TransitionedAt).HasColumnName("transitioned_at").IsRequired();
            entity.Property(e => e.Metadata).HasColumnName("metadata");

            entity.HasIndex(e => e.CustomDomainId);
            entity.HasIndex(e => e.TransitionedAt);
            entity.HasIndex(e => new { e.CustomDomainId, e.TransitionedAt });
        });
    }
}
