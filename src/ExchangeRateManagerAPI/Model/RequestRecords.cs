namespace ExchangeRateManagerAPI.Model
{
    public record ProcessCryptoUserTransactionsStagingRequest(string baseCurrency = "aud");
    public record StartCurrencyConversionRequest(string targetCurrency = "aud");
   public record StartSynchronisationRequest(string fromDateString = "20230701");

    public record TaxSummaryReportRequest(int taxYear= -1, decimal capitalGainTaxPercentage = 30m, bool formatAsCSV = true);
}
