using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Models
{
    public class TradingPairInformation
    {
             
        public string? Kind { get; set; }       
        public string? Symbol { get; set; }
        public string? ExchangeName { get; set; }
        public string? ExchangeSymbol { get; set; }
        public string? ExchangeCurrency { get; set; }
        public string TradingPairName
        {
            get
            {
                return $"{Symbol}/{ExchangeCurrency}";
            }
        }

       

        public DateTime? LastExchangeRateEntryDate { get; set; }
        public DateTime? SyncedOn { get; set; }
        public string? LastSyncStatus { get; set; }
        public bool IsActive { get; set; } = true;
        public bool SyncError { get; set; } = false;

    }
}
