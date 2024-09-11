using Shared.Models;

namespace ExchangeRateManagerAPI.Interfaces
{
    public interface IYahooFinanceScaper
    {

        Task<List<ExchangeRate>> GetFinancialDataRange(ExchangeInformation exchangeInfo, DateTime fromDate);
    }
}