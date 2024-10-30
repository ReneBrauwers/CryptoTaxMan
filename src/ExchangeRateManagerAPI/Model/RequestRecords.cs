using Shared.Enums;

namespace ExchangeRateManagerAPI.Model
{
    public record ProcessCryptoUserTransactionsStagingRequest(string transactionId, string baseCurrency = "aud");
    public record StartCurrencyConversionRequest(string targetCurrency = "aud");
   public record StartSynchronisationRequest(string fromDateString = "20230701", string exchangeSupported = "All");

    public record TaxSummaryReportRequest(string transactionId,int taxYear= -1, bool formatAsCSV = true);

    public record IncomeTaxReportRequest(string transactionId, int taxYear = -1,  bool formatAsCSV = true);

    public record CryptoUserTransactionsRequest(string transactionId, string? startDate, string? endDate, string? asset, bool formatAsCSV = true);
}
