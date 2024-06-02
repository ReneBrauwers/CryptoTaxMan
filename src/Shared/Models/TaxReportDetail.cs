using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Models
{
    public class TaxReportDetail
    {
        public int TaxYear { get; set; }
        public string Asset { get; set; }
        public DateOnly SellDate { get; set; }
        public decimal QuantitySold { get; set; }
        public int SellRecordSequenceNr { get; set; }
        public decimal SellExchangeRate { get; set; }
        public decimal SaleProceeds { get; set; }
        public DateOnly BuyDate { get; set; }
        public decimal BuyExchangeRate { get; set; }
        public int BuyRecordSequenceNr { get; set; }
        public decimal CapitalGains { get; set; }
        public decimal QuantityRemaining { get; set; }
        public bool IsDiscounted { get; set; }
        public int TotalHoldingDays { get; set; }
        public decimal CapitalGainTaxPercentage { get; set; }
        public decimal TaxesDue { get; set; }
        public string TaxCurrency { get; set; }
    }
}
