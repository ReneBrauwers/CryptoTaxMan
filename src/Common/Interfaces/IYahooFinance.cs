using Common.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Common.Interfaces
{
    public interface IYahooFinance
    {
        Task<ExchangeRate> GetFinancialData(ExchangeInformation exchangeInfo, DateTime histDate);

        Task<List<ExchangeRate>> GetFinancialData(ExchangeInformation exchangeInfo, List<DateTime> histDate);

        Task<List<ExchangeRate>> GetFinancialDataRange(ExchangeInformation exchangeInfo, DateTime fromDate);
    }
}