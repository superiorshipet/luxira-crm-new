using Luxira.Api.Core;
using Luxira.Api.Features.Employees.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Luxira.Api.Features.Employees;

public class EmployeeDbConfig : IDbConfig<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.DisplayName).HasMaxLength(100);
        builder.Property(e => e.IdNumber).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Nationality).HasMaxLength(100).IsRequired();
        builder.Property(e => e.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Address).HasMaxLength(255).IsRequired();
        builder.Property(e => e.Salary).HasPrecision(18, 2);

        builder.HasMany(e => e.AttendanceLogs)
            .WithOne(a => a.Employee)
            .HasForeignKey(a => a.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.SalaryPayments)
            .WithOne(s => s.Employee)
            .HasForeignKey(s => s.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class EmployeeAttendanceLogDbConfig : IDbConfig<EmployeeAttendanceLog>
{
    public void Configure(EntityTypeBuilder<EmployeeAttendanceLog> builder)
    {
        builder.ToTable("EmployeeAttendanceLogs");
        builder.HasKey(a => a.Id);
    }
}

public class EmployeeWorkShiftDbConfig : IDbConfig<EmployeeWorkShift>
{
    public void Configure(EntityTypeBuilder<EmployeeWorkShift> builder)
    {
        builder.ToTable("EmployeeWorkShifts");
        builder.HasKey(s => s.Id);
    }
}

public class EmployeeActivityLogDbConfig : IDbConfig<EmployeeActivityLog>
{
    public void Configure(EntityTypeBuilder<EmployeeActivityLog> builder)
    {
        builder.ToTable("EmployeeActivityLogs");
        builder.HasKey(l => l.Id);
    }
}

public class EmployeeSalaryPaymentDbConfig : IDbConfig<EmployeeSalaryPayment>
{
    public void Configure(EntityTypeBuilder<EmployeeSalaryPayment> builder)
    {
        builder.ToTable("EmployeeSalaryPayments");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Amount).HasPrecision(18, 2);
    }
}

public class EmployeeBonusRateDbConfig : IDbConfig<EmployeeBonusRate>
{
    public void Configure(EntityTypeBuilder<EmployeeBonusRate> builder)
    {
        builder.ToTable("EmployeeBonusRates");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Rate).HasPrecision(18, 2);
    }
}

public class EmployeeBonusPaymentDbConfig : IDbConfig<EmployeeBonusPayment>
{
    public void Configure(EntityTypeBuilder<EmployeeBonusPayment> builder)
    {
        builder.ToTable("EmployeeBonusPayments");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Amount).HasPrecision(18, 2);
    }
}

public class EmployeeTaskDbConfig : IDbConfig<EmployeeTask>
{
    public void Configure(EntityTypeBuilder<EmployeeTask> builder)
    {
        builder.ToTable("EmployeeTasks");
        builder.HasKey(t => t.Id);
    }
}

public class EmployeeErrorDbConfig : IDbConfig<EmployeeError>
{
    public void Configure(EntityTypeBuilder<EmployeeError> builder)
    {
        builder.ToTable("EmployeeErrors");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.DeductionAmount).HasPrecision(18, 2);
    }
}

public class EmployeeTransactionDbConfig : IDbConfig<EmployeeTransaction>
{
    public void Configure(EntityTypeBuilder<EmployeeTransaction> builder)
    {
        builder.ToTable("EmployeeTransactions");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Amount).HasPrecision(18, 2);
    }
}

public class EmployeeBreakDbConfig : IDbConfig<EmployeeBreak>
{
    public void Configure(EntityTypeBuilder<EmployeeBreak> builder)
    {
        builder.ToTable("EmployeeBreaks");
        builder.HasKey(b => b.Id);
    }
}

public class EmployeeViolationDbConfig : IDbConfig<EmployeeViolation>
{
    public void Configure(EntityTypeBuilder<EmployeeViolation> builder)
    {
        builder.ToTable("EmployeeViolations");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.PenaltyAmount).HasPrecision(18, 2);
    }
}

public class EmployeeRatingDbConfig : IDbConfig<EmployeeRating>
{
    public void Configure(EntityTypeBuilder<EmployeeRating> builder)
    {
        builder.ToTable("EmployeeRatings");
        builder.HasKey(r => r.Id);
    }
}

public class PersonalNoteDbConfig : IDbConfig<PersonalNote>
{
    public void Configure(EntityTypeBuilder<PersonalNote> builder)
    {
        builder.ToTable("PersonalNotes");
        builder.HasKey(n => n.Id);
    }
}

public class ManagementRequestDbConfig : IDbConfig<ManagementRequest>
{
    public void Configure(EntityTypeBuilder<ManagementRequest> builder)
    {
        builder.ToTable("ManagementRequests");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Title).HasMaxLength(255).IsRequired();
        builder.Property(m => m.RequestType).HasMaxLength(100).IsRequired();
        builder.Property(m => m.Status).HasMaxLength(50).IsRequired();
        builder.Property(m => m.RequestedAmount).HasPrecision(18, 2);
    }
}

public class ScreenRecordDbConfig : IDbConfig<ScreenRecord>
{
    public void Configure(EntityTypeBuilder<ScreenRecord> builder)
    {
        builder.ToTable("ScreenRecords");
        builder.HasKey(s => s.Id);
    }
}


