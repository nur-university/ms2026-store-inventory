using Inventory.Domain.Items;
using Inventory.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Inventory.Infrastructure.Persistence.DomainModel.Config
{
    internal class ItemConfig : IEntityTypeConfiguration<Item>
    {
        public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Item> builder)
        {
            builder.ToTable("item");

            builder.HasKey(x => x.Id);
            
            builder.Property(x => x.Id)
                .HasColumnName("itemId");

            var quantityConverter = new ValueConverter<QuantityValue, int>(
                quantityValue => quantityValue.Value,
                quantity => new QuantityValue(quantity)
            );

            builder.Property(x => x.Stock)
                .HasColumnName("stock")
                .HasConversion(quantityConverter);

            builder.Property(x => x.Reserved)
                .HasColumnName("reserved")
                .HasConversion(quantityConverter);

            builder.Property(x => x.Available)
                .HasColumnName("available")
                .HasConversion(quantityConverter);

            builder.Property(x => x.Name)
                .HasColumnName("itemName");

            var converter = new ValueConverter<CostValue, decimal>(
                costValue => costValue.Value, // CostValue to decimal
                cost => new CostValue(cost) // decimal to CostValue
            );

            builder.Property(x => x.UnitaryCost)
                .HasConversion(converter)
                .HasColumnName("unitaryCost");

            builder.Ignore("_domainEvents");
            builder.Ignore(x => x.DomainEvents);

        }
    }
}
