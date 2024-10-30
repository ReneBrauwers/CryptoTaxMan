using Shared.Models;

namespace ExchangeRateManagerAPI.Interfaces
{
    public interface ILiveCoinWatch
    {
       
        Task<List<ExchangeRate>> GetCryptoDataRange(ExchangeInformation exchangeInfo, DateTime fromDate);

    }
}