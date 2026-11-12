
using Microsoft.EntityFrameworkCore;
using PaymentSystem.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace PaymentSystem.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.TransactionHash).IsUnique();
            entity.HasIndex(p => new { p.Status, p.NextVerificationAt });
            entity.Property(p => p.VerificationCode).HasMaxLength(40);
            entity.Property(p => p.VerificationMessage).HasMaxLength(500);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Product>().WithMany().HasForeignKey(p => p.ProductId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(p => p.ProductName).HasMaxLength(100);

            entity.HasIndex(p => p.OrderId)
                  .IsUnique();

            entity.HasIndex(p => p.Md5)
                  .IsUnique();

            entity.Property(p => p.OrderId)
                  .IsRequired()
                  .HasMaxLength(50);

            entity.Property(p => p.Currency)
                  .IsRequired()
                  .HasMaxLength(3);

            entity.Property(p => p.Status)
                  .IsRequired()
                  .HasMaxLength(20);

            entity.Property(p => p.Md5)
                  .IsRequired(false);
        });
        modelBuilder.Entity<Product>().ToTable(t => t.HasCheckConstraint("CK_Product_Price", "CAST(Price AS REAL) > 0"));
    }
}
