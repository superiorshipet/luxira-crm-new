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
