using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ODSReportsConfigurations : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("Reports");
        builder.HasKey(r => r.Id);
        builder.HasIndex(r => r.ReportId).IsUnique();
        builder.HasOne<Raw>()
       .WithOne()
       .HasForeignKey<Report>(r => r.RawId)
       .OnDelete(DeleteBehavior.Restrict);
    }
}

