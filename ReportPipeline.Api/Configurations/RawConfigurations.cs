using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class RawConfigurations : IEntityTypeConfiguration<Raw>
{
    public void Configure(EntityTypeBuilder<Raw> builder)
    {
        builder.ToTable("Raw");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Payload).IsRequired(); // 設定 Payload 欄位為必填
        builder.HasIndex(r => new { r.Status, r.ClaimedAt });
    }
}
