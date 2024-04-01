using Shared.Models;

namespace ExchangeRateManagerAPI.Interfaces
{
    public interface ICoinGecko
    {
        Task<ExchangeRate> GetCryptoData(ExchangeInformation exchangeInfo, DateTime histDate);

        Task<List<ExchangeRate>> GetCryptoData(ExchangeInformation exchangeInfo, List<DateTime> histDate);
        Task<List<ExchangeRate>> GetCryptoDataRange(ExchangeInformation exchangeInfo, DateTime fromDate);

        Task<List<ExchangeInformation>> GetSupportedSymbols(int total);
    }
}