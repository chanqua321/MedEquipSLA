using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Maintenance.Domain;
namespace MedEquipSLA.Maintenance.Infrastructure.Persistence.Configurations;
public sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    { builder.ToTable("WorkOrders"); builder.HasKey(x => x.Id); builder.Property(x => x.RowVersion).IsRowVersion(); }
}
