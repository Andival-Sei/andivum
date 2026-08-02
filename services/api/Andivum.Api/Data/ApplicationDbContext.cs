using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Andivum.Api.Data;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<
        ApplicationUser,
        IdentityRole<Guid>,
        Guid,
        IdentityUserClaim<Guid>,
        IdentityUserRole<Guid>,
        IdentityUserLogin<Guid>,
        IdentityRoleClaim<Guid>,
        IdentityUserToken<Guid>,
        IdentityUserPasskey<Guid>>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<IdentityUserPasskey<Guid>>(entity =>
        {
            entity.ToTable("AspNetUserPasskeys");
            entity.HasKey(passkey => new { passkey.UserId, passkey.CredentialId });
            entity.Property(passkey => passkey.CredentialId)
                .HasMaxLength(1024);
            entity.Property(passkey => passkey.Data)
                .HasConversion(
                    data => JsonSerializer.Serialize(data, JsonSerializerOptions.Web),
                    data => JsonSerializer.Deserialize<IdentityPasskeyData>(
                        data,
                        JsonSerializerOptions.Web)!)
                .HasColumnType("jsonb")
                .IsRequired();
        });

        builder.UseOpenIddict();
    }
}
