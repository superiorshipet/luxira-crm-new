using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Luxira.Api.Core;

public interface IDbConfig<T> : IEntityTypeConfiguration<T> where T : class
{
    public new void Configure(EntityTypeBuilder<T> builder);
}
