using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Luxira.Infrastructure.Features.Identity.GetUserProfile;

internal sealed class UserProfileUserReadRowConfiguration
    : IEntityTypeConfiguration<UserProfileUserReadRow>
{
    public void Configure(EntityTypeBuilder<UserProfileUserReadRow> builder)
    {
        builder.ToTable("AspNetUsers");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).HasMaxLength(450);
        builder.Property(user => user.UserName).HasMaxLength(256);
        builder.Property(user => user.Email).HasMaxLength(256);
        builder.Property(user => user.DisplayName).HasColumnName("Name");
        builder.Property(user => user.PhoneNumber);
    }
}

internal sealed class EmployeeProfileReadRowConfiguration
    : IEntityTypeConfiguration<EmployeeProfileReadRow>
{
    public void Configure(EntityTypeBuilder<EmployeeProfileReadRow> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(employee => employee.Id);
        builder.Property(employee => employee.Id).ValueGeneratedOnAdd();
        builder.Property(employee => employee.ApplicationUserId).IsRequired();
        builder.Property(employee => employee.IsActive);
        builder.Property(employee => employee.DisplayName).HasMaxLength(100);
        builder.Property(employee => employee.Name).HasMaxLength(100);
        builder.Property(employee => employee.ImageUrl).HasMaxLength(255);
        builder.Property(employee => employee.JobTitle).HasMaxLength(100);
        builder.Property(employee => employee.PhoneNumber).HasMaxLength(20);
    }
}

internal sealed class UserRoleReadRowConfiguration
    : IEntityTypeConfiguration<UserRoleReadRow>
{
    public void Configure(EntityTypeBuilder<UserRoleReadRow> builder)
    {
        builder.ToTable("AspNetUserRoles");
        builder.HasKey(userRole => new { userRole.UserId, userRole.RoleId });
        builder.Property(userRole => userRole.UserId).HasMaxLength(450);
        builder.Property(userRole => userRole.RoleId).HasMaxLength(450);
    }
}

internal sealed class RoleReadRowConfiguration
    : IEntityTypeConfiguration<RoleReadRow>
{
    public void Configure(EntityTypeBuilder<RoleReadRow> builder)
    {
        builder.ToTable("AspNetRoles");
        builder.HasKey(role => role.Id);
        builder.Property(role => role.Id).HasMaxLength(450);
        builder.Property(role => role.Name).HasMaxLength(256);
    }
}
