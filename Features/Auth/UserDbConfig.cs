using Luxira.Api.Core;
using Luxira.Api.Features.Auth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Luxira.Api.Features.Auth;

public class UserDbConfig : IDbConfig<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("AspNetUsers");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.UserName).HasMaxLength(256);
        builder.Property(u => u.Email).HasMaxLength(256);
        builder.HasMany(u => u.UserRoles)
            .WithOne(userRole => userRole.User)
            .HasForeignKey(userRole => userRole.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RoleDbConfig : IDbConfig<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.ToTable("AspNetRoles");
        builder.HasKey(role => role.Id);
        builder.Property(role => role.Name).HasMaxLength(256);
        builder.Property(role => role.NormalizedName).HasMaxLength(256);
        builder.HasIndex(role => role.NormalizedName).IsUnique();
    }
}

public class UserRoleDbConfig : IDbConfig<ApplicationUserRole>
{
    public void Configure(EntityTypeBuilder<ApplicationUserRole> builder)
    {
        builder.ToTable("AspNetUserRoles");
        builder.HasKey(userRole => new { userRole.UserId, userRole.RoleId });
        builder.HasOne(userRole => userRole.Role)
            .WithMany()
            .HasForeignKey(userRole => userRole.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserSwitchGroupDbConfig : IDbConfig<UserSwitchGroup>
{
    public void Configure(EntityTypeBuilder<UserSwitchGroup> builder)
    {
        builder.ToTable("UserSwitchGroups");
        builder.HasKey(g => g.Id);
        builder.HasMany(g => g.Members)
            .WithOne(m => m.UserSwitchGroup)
            .HasForeignKey(m => m.UserSwitchGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserSwitchGroupMemberDbConfig : IDbConfig<UserSwitchGroupMember>
{
    public void Configure(EntityTypeBuilder<UserSwitchGroupMember> builder)
    {
        builder.ToTable("UserSwitchGroupMembers");
        builder.HasKey(m => m.Id);
    }
}
