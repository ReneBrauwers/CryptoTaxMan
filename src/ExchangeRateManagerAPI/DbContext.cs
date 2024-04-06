using Microsoft.EntityFrameworkCore;
using Shared.Models;

namespace ExchangeRateManagerAPI
{


    public class CryptoTaxManDbContext : DbContext
    {
        public DbSet<ExchangeRate> ExchangeRates { get; set; }
        public DbSet<CryptoUserTransactionStaging> CryptoUserTransactionsStaging { get; set; }
        public DbSet<CryptoUserTransaction> CryptoUserTransactions { get; set; }

        public CryptoTaxManDbContext(DbContextOptions<CryptoTaxManDbContext> options) : base(options)
        {
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            // Configure ExchangeRate entity

            modelBuilder.Entity<ExchangeRate>()
                .ToTable("ExchangeRates") // Explicitly map to its table
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

            // Configure CryptoUserTransaction entity for production
            var cryptoUserTransactionEntity = modelBuilder.Entity<CryptoUserTransaction>()
                .ToTable("CryptoUserTransactions"); // Explicitly map to the production table

            // Primary Key and Auto-Increment configuration
            cryptoUserTransactionEntity.HasKey(c => c.Sequence);
            cryptoUserTransactionEntity.Property(c => c.Sequence).ValueGeneratedOnAdd();

            // Index for CurrencyIn
            cryptoUserTransactionEntity.HasIndex(c => c.CurrencyIn).IsUnique(false).HasDatabaseName("Idx_CurrencyIn");

            // Index for CurrencyOut
            cryptoUserTransactionEntity.HasIndex(c => c.CurrencyOut).IsUnique(false).HasDatabaseName("Idx_CurrencyOut");

            // Index for TransactionDate
            cryptoUserTransactionEntity.HasIndex(c => c.TransactionDate).IsUnique(false).HasDatabaseName("Idx_TransactionDate");

            // Composite index for TransactionDate and CurrencyIn
            cryptoUserTransactionEntity.HasIndex(c => new { c.TransactionDate, c.CurrencyIn }).IsUnique(false).HasDatabaseName("Idx_TransactionDateAndCurrencyIn");

            // Configure CryptoUserTransactionStaging entity for staging
            var cryptoUserTransactionStagingEntity = modelBuilder.Entity<CryptoUserTransactionStaging>()
                .ToTable("CryptoUserTransactionsStaging"); // Explicitly map to the staging table

            // Primary Key and Auto-Increment configuration
            cryptoUserTransactionStagingEntity.HasKey(c => c.Sequence);
            cryptoUserTransactionStagingEntity.Property(c => c.Sequence).ValueGeneratedOnAdd();


            // Index for TransactionDate
            cryptoUserTransactionStagingEntity.HasIndex(c => c.TransactionDate).IsUnique(false).HasDatabaseName("Idx_TransactionDate_Staging");

            // Index for TransactionDate
            cryptoUserTransactionStagingEntity.HasIndex(c => c.IsProcessed).IsUnique(false).HasDatabaseName("Idx_IsProcessed_Staging");

            // Composite index for TransactionDate and CurrencyIn
            cryptoUserTransactionStagingEntity.HasIndex(c => new { c.TransactionDate, c.CurrencyIn }).IsUnique(false).HasDatabaseName("Idx_TransactionDateAndCurrencyIn_Staging");


        }

    }

}
