using Shared.Models;

namespace ExchangeRateManagerAPI.Interfaces
{

    public interface ISologenic
    {

        Task<List<ExchangeRate>> GetCryptoDataRange(ExchangeInformation exchangeInfo, DateTime fromDate);

    }
}
