using ExchangeRateManagerAPI.Controllers;
using ExchangeRateManagerAPI.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Polly;
using Shared.Models;
using System.Text;

namespace ExchangeRateManagerAPI.Services
{
    public class DatabaseService
    {
       
        private readonly ILogger<DatabaseService> _logger; 
        private readonly IDbContextFactory<CryptoTaxManDbContext> _dbContextFactory;
        private readonly IWebHostEnvironment _environment;

        public DatabaseService(ILogger<DatabaseService> logger, IWebHostEnvironment environment, IDbContextFactory<CryptoTaxManDbContext> dbContextFactory) // CryptoTaxManDbContext dbContext)
        {
            _logger = logger;
            _environment = environment;
            _dbContextFactory = dbContextFactory;
        }


        public async Task<ExchangeRate?> FetchExchangeRateInformation(DateTime transactionDate, string currencyIn, string exchangeCurrency)
        {
            try
            {

                var compositeKey = new object[] { transactionDate, currencyIn, exchangeCurrency };

                using var dbContext = _dbContextFactory.CreateDbContext();
                var result = await dbContext.ExchangeRates.FindAsync(compositeKey);
                if (result is not null)
                {
                    return result;
                }
                
            }
            catch (Exception ex)
            {
                _logger.LogError($"{transactionDate}, {currencyIn},{exchangeCurrency} caused error {ex.Message}");
            }

            return null;
 
        }

        public async Task<(int records, string message, bool error)> InsertCryptoUserTransactions(List<CryptoUserTransactionStaging> transactions)
        {
            try
            {
                using var dbContext = _dbContextFactory.CreateDbContext();
                await dbContext.CryptoUserTransactionsStaging.AddRangeAsync(transactions);
                return (await dbContext.SaveChangesAsync(), "Inserted", false);
            }
            catch (Exception ex)
            {
                _logger.LogError($"InsertCryptoUserTransactions caused error {ex.Message}");
                return (0, $"InsertCryptoUserTransactions caused error {ex.Message}",true);
            }

            
        }
 

    }
}
