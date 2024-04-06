using CsvHelper.Configuration;
using Shared.Models;


public class CryptoUserTransactionStagingMap : ClassMap<CryptoUserTransactionStaging>
{
    public CryptoUserTransactionStagingMap()
    {
        Map(m => m.Sequence).Name("Sequence");
        Map(m => m.TransactionDate).Name("TransactionDate").TypeConverterOption.Format("yyyy-MM-ddTHH:mm:ss");
        Map(m => m.TransactionType).Name("TransactionType");
        Map(m => m.AmountIn).Name("AmountIn").Optional();
        Map(m => m.CurrencyIn).Name("CurrencyIn").Optional();
        Map(m => m.AmountOut).Name("AmountOut").Optional();
        Map(m => m.CurrencyOut).Name("CurrencyOut").Optional();
        Map(m => m.TransactionEvent).Name("TransactionEvent");
        Map(m => m.Fee).Name("Fee").Optional();
        Map(m => m.FeeCurrency).Name("FeeCurrency").Optional();
        Map(m => m.Notes).Name("Notes").Optional();      
    }
}
