using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models
{
    public class ExchangeInformationDetail
    {
        public string? Kind { get; set; }
        public string? Symbol { get; set; }
        public string? ExchangeName { get; set; }
        public string? ExchangeSymbol { get; set; }
        public string? ExchangeCurrency { get; set; }
        public bool FiatExchangeRate { get; set; }
        public string? LastSyncDate { get; set; }
        public string? FirstEntryDate { get; set; }
        public string? LastEntryDate { get; set; }
        public double DaysOfDataExpected { get; set; }
        public double DaysOfDataActual { get; set; }
        public double DaysOfDataMissing { get; set; }

    }
}
