using Jusoor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jusoor.Infrastructure.Persistence.Configurations;

/// <summary>
/// Everything about Case's schema EXCEPT the encrypted columns' value
/// converters. Those are wired in ApplicationDbContext.OnModelCreating
/// instead, deliberately outside this file: IEntityTypeConfiguration
/// implementations are instantiated via ApplyConfigurationsFromAssembly's
/// Activator.CreateInstance(type) call, which requires a public parameterless
/// constructor — this class must stay parameterless to keep being picked up
/// automatically alongside every other configuration in this codebase. The
/// encrypted-column converters need IFieldEncryptionService injected, which
/// only ApplicationDbContext's own constructor can supply. Do not "clean up"
/// by moving the converter wiring in here — it will break assembly scanning
/// for this and, if this class throws during scanning, potentially every
/// other configuration too.
/// </summary>
public class CaseConfiguration : IEntityTypeConfiguration<Case>
{
    public void Configure(EntityTypeBuilder<Case> builder)
    {
        builder.ToTable("Cases");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Status).IsRequired().HasConversion<int>();

        // No HasMaxLength on the encrypted columns (SubjectName,
        // SubjectContactPhone, ConcernDescription, ApplicantPhoneE164):
        // their stored representation is a base64-encoded encryption
        // envelope (wrapped key + nonce + tag + ciphertext + JSON overhead),
        // not the plaintext — its length is a function of that envelope,
        // not of any reasonable plaintext limit, and is meaningfully larger
        // than the plaintext itself. Left as Postgres's default unbounded
        // `text` column rather than guessing a plaintext-shaped limit that
        // would be wrong for what's actually stored.
        builder.Property(c => c.OtpVerifiedAtUtc).IsRequired();

        // Plain string, deliberately NOT an EF foreign key to AspNetUsers —
        // same as AuditableEntity's CreatedByUserId/LastModifiedByUserId
        // (no existing configuration in this codebase FK-constrains a user
        // reference), so a CrisisEditor account can never be blocked from
        // deletion by a Case having once been assigned to it.
        builder.Property(c => c.AssignedToCrisisEditorUserId).HasMaxLength(450); // matches Identity's own string-key column width
        builder.Property(c => c.AssignedAtUtc);

        // "My queue" (GetMyAssignedCasesQuery) filters on this column for
        // every CrisisEditor request — same "don't discover the missing
        // index under load" reasoning as the Status index below.
        builder.HasIndex(c => c.AssignedToCrisisEditorUserId);

        // Required, non-nullable FKs — Restrict rather than Cascade/SetNull:
        // a Case's country/city reference must survive even if reference
        // geography data is ever pruned, matching StoryConfiguration's own
        // "history must survive" reasoning for its Article relationship.
        // (SetNull is also not valid EF Core here regardless of preference —
        // CountryId/CityId are non-nullable Guid, not Guid?.)
        builder.HasOne<Country>()
            .WithMany()
            .HasForeignKey(c => c.CountryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<City>()
            .WithMany()
            .HasForeignKey(c => c.CityId)
            .OnDelete(DeleteBehavior.Restrict);

        // A CrisisEditor's queue (step 3, not built yet) will filter by
        // active status — indexing now is the same "don't discover the
        // missing index under load" reasoning StoryConfiguration used for
        // Status/LastUpdatedAtUtc.
        builder.HasIndex(c => c.Status);
    }
}
