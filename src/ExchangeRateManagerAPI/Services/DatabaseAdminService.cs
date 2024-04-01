using ExchangeRateManagerAPI.Controllers;
using ExchangeRateManagerAPI.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Polly;
using Shared.Models;
using System.Text;

namespace ExchangeRateManagerAPI.Services
{
    public class DatabaseAdminService
    {
        private readonly IDictionary<string, TaskCompletionSource<string>> _tasks = new Dictionary<string, TaskCompletionSource<string>>();
        private readonly ILogger<DatabaseAdminService> _logger;
        //private readonly CryptoTaxManDbContext _dbContext;
        private readonly IDbContextFactory<CryptoTaxManDbContext> _dbContextFactory;
        private readonly IWebHostEnvironment _environment;

        public DatabaseAdminService(ILogger<DatabaseAdminService> logger, IWebHostEnvironment environment, IDbContextFactory<CryptoTaxManDbContext> dbContextFactory) // CryptoTaxManDbContext dbContext)
        {
            _logger = logger;
            _environment = environment;
            //_dbContext = dbContext;
            _dbContextFactory = dbContextFactory;

        }

        public string StartNewTask(Func<Task<string>> taskFunc)
        {
            var taskId = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<string>();
            _tasks.Add(taskId, tcs);

            Task.Run(async () =>
            {
                using var dbContext = _dbContextFactory.CreateDbContext();
                try
                {
                    var result = await taskFunc();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return taskId;
        }


        public async Task<string> ExecuteRecreateAndRestoreFromFiles(string restorePath)
        {
            if(string.IsNullOrWhiteSpace(restorePath))
            {
                restorePath = Path.Combine(_environment.ContentRootPath, "Assets/Restore");
            }

            int totalRecordsInserted =0;
            using var dbContext = _dbContextFactory.CreateDbContext();
            try
            {
                // Delete the existing database
                await dbContext.Database.EnsureDeletedAsync();

                // Create a new database
                await dbContext.Database.EnsureCreatedAsync();


                //list all subfolders which are actually years ensure to store them in a List<int> sorted small to large
                var yearFolders = Directory.GetDirectories(restorePath); 

                
                foreach (var year in yearFolders)
                {
                    //list all subfolders which are actually months ensure to store them in a List<int> sorted small to large
                    var monthFolders = Directory.GetDirectories(year);

                    foreach (var month in monthFolders)
                    {
                        var sqlFiles = Directory.GetFiles(month, "*.sql");
                        foreach (var file in sqlFiles)
                        {
                            var sql = await System.IO.File.ReadAllTextAsync(file);
                         
                            totalRecordsInserted += await dbContext.Database.ExecuteSqlRawAsync(sql);
                           
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions appropriately
                throw new InvalidOperationException("An error occurred while executing the API calls.", ex);
            }

            return $"{totalRecordsInserted} rows inserted";
             
        }

        public async Task<string> ExportExchangeRateData(string exportPath)
        {
            if(string.IsNullOrWhiteSpace(exportPath))
            {
                exportPath = Path.Combine(_environment.ContentRootPath, "Assets/Restore");
            }

            int totalRecordsInserted = 0;
            using var dbContext = _dbContextFactory.CreateDbContext();
           
            try
            {
                // Example for exporting data; implement similar logic for each table
                var exchangeRates = await dbContext.ExchangeRates.ToListAsync();

                //group exchangeRates by symbol and sort by date asc
                exchangeRates = exchangeRates.GroupBy(x => x.Symbol).SelectMany(x => x.OrderBy(y => y.Date)).ToList();

                // for each exchange rate group by year and month
                var exchangeRatesGroupedByYearMonth = exchangeRates.GroupBy(x => new { x.Date.Year, x.Date.Month }).ToList();

                // for each group create the insert script and store it in the directory folder using Symbol, Year and Month
                foreach (var exchangeRateGroup in exchangeRatesGroupedByYearMonth)
                {
                    var stringBuilder = new StringBuilder();
                    foreach (var rate in exchangeRateGroup)
                    {
                        //2015-01-02 00:00:00
                        stringBuilder.AppendLine($"INSERT INTO ExchangeRates (Symbol, Date, ExchangeCurrency, Open, Close, Low, High, OpenCloseAverage, LowHighAverage, DataSource, LookupOptional) VALUES ('{rate.Symbol}', '{rate.Date.ToString("yyyy-MM-dd HH:mm:ss")}', '{rate.ExchangeCurrency}', '{rate.Open}', '{rate.Close}','{rate.Low}','{rate.High}','{rate.OpenCloseAverage}','{rate.LowHighAverage}','{rate.DataSource}',{rate.LookupOptional});");
                        totalRecordsInserted++;
                      
                    }


                    var filePath = Path.Combine(exportPath, $"{exchangeRateGroup.Key.Year.ToString()}/{exchangeRateGroup.Key.Month.ToString("00")}", "ExchangeRates.sql");

                    //create folder if it does not exist
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                    await System.IO.File.WriteAllTextAsync(filePath, stringBuilder.ToString());
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions appropriately
                throw new InvalidOperationException("An error occurred while executing the API calls.", ex);
            }

            return $"{totalRecordsInserted} rows exported";

          
        }

        public (bool IsCompleted, string Result) CheckTaskStatus(string taskId)
        {
            if (_tasks.TryGetValue(taskId, out var tcs))
            {
                if (tcs.Task.IsCompleted)
                {
                    return (true, tcs.Task.Result);
                }
            }

            return (false, string.Empty);
        }

    }
}
