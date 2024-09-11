using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Models
{
    public class CryptoUserTransactionProfitLossCalculation
    {
        public DateTime SoldOn { get; set; }
        public decimal TotalAmountAvailableBeforeSell { get; set; }
        public decimal TotalAmountAvailableAfterSell { get; set; }
        public decimal TotalAmountSold { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal AverageBuyPrice { get; set; }
        public string AmountAssetType { get; set; }
        public string Currency { get; set; }
    }
}
