using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Maintenance.Domain;
using MedEquipSLA.Maintenance.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Maintenance.Infrastructure.Persistence.Configurations;

public sealed class CalibrationMeasurementConfiguration : IEntityTypeConfiguration<CalibrationMeasurement>
{
    public void Configure(EntityTypeBuilder<CalibrationMeasurement> builder)
    {
        builder.ToTable("CalibrationMeasurements", table =>
        {
            table.HasCheckConstraint("CK_CalibrationMeasurements_Thresholds", "([MinimumAllowed] IS NOT NULL OR [MaximumAllowed] IS NOT NULL) AND ([MinimumAllowed] IS NULL OR [MaximumAllowed] IS NULL OR [MinimumAllowed] <= [MaximumAllowed])");
            table.HasCheckConstraint("CK_CalibrationMeasurements_Result", "([Passed] = 1 AND ([MinimumAllowed] IS NULL OR [MeasuredValue] >= [MinimumAllowed]) AND ([MaximumAllowed] IS NULL OR [MeasuredValue] <= [MaximumAllowed])) OR ([Passed] = 0 AND (([MinimumAllowed] IS NOT NULL AND [MeasuredValue] < [MinimumAllowed]) OR ([MaximumAllowed] IS NOT NULL AND [MeasuredValue] > [MaximumAllowed])))");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.WorkOrderAttemptId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.ParameterCode).HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired(true);
        builder.Property(x => x.ParameterName).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired(true);
        builder.Property(x => x.Unit).HasColumnType("nvarchar(30)").HasMaxLength(30).IsRequired(true);
        builder.Property(x => x.MeasuredValue).HasColumnType("decimal(18,6)").HasPrecision(18, 6).IsRequired(true);
        builder.Property(x => x.MinimumAllowed).HasColumnType("decimal(18,6)").HasPrecision(18, 6).IsRequired(false);
        builder.Property(x => x.MaximumAllowed).HasColumnType("decimal(18,6)").HasPrecision(18, 6).IsRequired(false);
        builder.Property(x => x.Passed).HasColumnType("bit").IsRequired(true);
        builder.HasIndex(x => new { x.WorkOrderAttemptId, x.ParameterCode }).HasDatabaseName("UX_CalibrationMeasurements_WorkOrderAttemptId_ParameterCode").IsUnique();
        builder.HasOne<WorkOrderAttempt>().WithMany().HasForeignKey(x => x.WorkOrderAttemptId).OnDelete(DeleteBehavior.NoAction);
    }
}
