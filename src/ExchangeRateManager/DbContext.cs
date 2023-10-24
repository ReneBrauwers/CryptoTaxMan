using Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ExchangeRateManager
{


    public class CryptoTaxManDbContext : DbContext
    {
        public DbSet<ExchangeRate> ExchangeRates { get; set; }

        public CryptoTaxManDbContext(DbContextOptions<CryptoTaxManDbContext> options) : base(options)
        {
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            modelBuilder.Entity<ExchangeRate>()
                .HasKey(pd => new { pd.Date, pd.Symbol, pd.ExchangeCurrency });

            modelBuilder.Entity<ExchangeRate>()
      .HasIndex(pd => pd.Date)
      .IsUnique(false)
      .HasDatabaseName("Idx_Date");

            modelBuilder.Entity<ExchangeRate>()
      .HasIndex(pd => pd.Symbol)
      .IsUnique(false)
      .HasDatabaseName("Idx_Symbol");

            modelBuilder.Entity<ExchangeRate>()
      .HasIndex(pd => pd.ExchangeCurrency)
      .IsUnique(false)
      .HasDatabaseName("Idx_ExchangeCurrency");


        }

    }

}
