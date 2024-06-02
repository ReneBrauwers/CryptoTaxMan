using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Models
{
    public class Holding
    {
        public decimal Amount { get; set; }
        public decimal ExchangeRateValue { get; set; }
        public string AmountAssetType { get; set; }
        public string ExchangeRateCurrency { get; set; }
        public DateTime BuyDate { get; set; }
        public int Sequence { get; set; }

        public Holding(decimal amount, decimal exchangeRateValue, string amountAssetType, string exchangeRateCurrency, DateTime buyDate, int sequence)
        {
            Amount = amount;
            ExchangeRateValue = exchangeRateValue;
            AmountAssetType = amountAssetType;
            ExchangeRateCurrency = exchangeRateCurrency;
            BuyDate = buyDate;
            Sequence = sequence;
        }
    }
}
