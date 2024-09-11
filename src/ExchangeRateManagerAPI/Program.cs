
using ExchangeRateManagerAPI.Interfaces;
using ExchangeRateManagerAPI.Services;
using Microsoft.EntityFrameworkCore;
using Polly;

namespace ExchangeRateManagerAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var assetsPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
           // var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection").Replace("{AssetsPath}", assetsPath);

            // Add services to the container.
            builder.Services.AddSingleton<IAsyncPolicy<HttpResponseMessage>>(Policy.HandleResult<HttpResponseMessage>(r => r.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(10, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));


            // Add DbContext to the DI container
           // builder.Services.AddDbContextFactory<CryptoTaxManDbContext>(options => options.UseSqlite(defaultConnection));
            builder.Services.AddDbContextFactory<CryptoTaxManDbContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"), ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))));

            builder.Services.AddHttpClient<ISologenic, SologenicService>().SetHandlerLifetime(TimeSpan.FromMinutes(5));
           // builder.Services.AddHttpClient<IYahooFinance, YahooFinanceService>().SetHandlerLifetime(TimeSpan.FromMinutes(5));
            builder.Services.AddHttpClient<ICoinGecko, CoinGeckoService>().SetHandlerLifetime(TimeSpan.FromMinutes(5));

            builder.Services.AddSingleton<IYahooFinanceScaper, YahooFinanceScraperService>();
            builder.Services.AddSingleton<ICoinGecko, CoinGeckoService>();
            builder.Services.AddSingleton<ISologenic, SologenicService>();

            builder.Services.AddSingleton<ExchangeRateCrawlerService>();
            builder.Services.AddSingleton<DatabaseAdminService>();            
            builder.Services.AddSingleton<DatabaseService>();

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            
            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
