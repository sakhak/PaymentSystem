
using Microsoft.EntityFrameworkCore;
using PaymentSystem.API.Models;

namespace PaymentSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Payment> Payments { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Payment>()
                .HasIndex(p => p.OrderId)
                .IsUnique();
        }
    }
}
