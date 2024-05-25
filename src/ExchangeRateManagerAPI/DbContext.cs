using Microsoft.EntityFrameworkCore;
using Shared.Models;

namespace ExchangeRateManagerAPI
{


    public class CryptoTaxManDbContext : DbContext
    {
        public DbSet<ExchangeRate> ExchangeRates { get; set; }
        public DbSet<CryptoUserTransactionStaging> CryptoUserTransactionsStaging { get; set; }
        public DbSet<CryptoUserTransaction> CryptoUserTransactions { get; set; }

        public DbSet<TradingPairInformation> TradingPairInformation { get; set; }

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
            cryptoUserTransactionEntity.HasKey(c => new { c.Sequence, c.TransactionType });
            cryptoUserTransactionEntity.Property(c => c.Sequence).ValueGeneratedOnAdd();

            // Index for sequence
            cryptoUserTransactionEntity.HasIndex(c => c.Sequence).IsUnique(false).HasDatabaseName("Idx_Sequence");

            // Index for TransactionTypes
            cryptoUserTransactionEntity.HasIndex(c => c.TransactionType).IsUnique(false).HasDatabaseName("Idx_TransactionType");

            // Index for CurrencyIn
            cryptoUserTransactionEntity.HasIndex(c => c.Amount).IsUnique(false).HasDatabaseName("Idx_Amount");

            // Index for CurrencyOut
            cryptoUserTransactionEntity.HasIndex(c => c.AmountAssetType).IsUnique(false).HasDatabaseName("Idx_AmountAssetType");

            // Index for TransactionDate
            cryptoUserTransactionEntity.HasIndex(c => c.TransactionDate).IsUnique(false).HasDatabaseName("Idx_TransactionDate");

            // Composite index for TransactionDate and CurrencyIn
            cryptoUserTransactionEntity.HasIndex(c => new { c.TransactionDate, c.AmountAssetType }).IsUnique(false).HasDatabaseName("Idx_TransactionDateAndAmountAssetType");

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

            //configure tradingpair
            modelBuilder.Entity<TradingPairInformation>()
               .ToTable("TradingPairInformation")
                .HasKey(pd => new { pd.Symbol, pd.ExchangeCurrency });

            modelBuilder.Entity<TradingPairInformation>()
            .HasIndex(c => c.Symbol).IsUnique(true).HasDatabaseName("Idx_Symbol");

            modelBuilder.Entity<TradingPairInformation>()
   .HasIndex(c => c.ExchangeCurrency).IsUnique(false).HasDatabaseName("Idx_ExchangeCurrency");

            modelBuilder.Entity<TradingPairInformation>()
.HasIndex(c => c.IsActive).IsUnique(false).HasDatabaseName("Idx_IsActive");



        }

    }

}
