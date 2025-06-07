using AdventureWorksAIHub.Core.Domain.Entities.Product;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdventureWorksAIHub.Infrastructure.Persisitence
{
    public class AdventureWorksContext : DbContext
    {
        public AdventureWorksContext(DbContextOptions<AdventureWorksContext> options)
            : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<ProductDescription> ProductDescriptions { get; set; }
        public DbSet<ProductVector> ProductVectors { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure tables
            modelBuilder.Entity<Product>().ToTable("Product", "Production"); 
            modelBuilder.Entity<ProductDescription>().ToTable("ProductDescription", "Production");

            // Configure relationships
            modelBuilder.Entity<Product>()
                .HasOne(p => p.ProductDescription)
                .WithOne()
                .HasForeignKey<ProductDescription>(pd => pd.ProductDescriptionID);

            modelBuilder.Entity<ProductVector>()
                .HasOne(pv => pv.Product)
                .WithMany()
                .HasForeignKey(pv => pv.ProductID);
        }
    }
}
