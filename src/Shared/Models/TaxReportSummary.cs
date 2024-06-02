using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Models
{
    public class TaxReportSummary
    {
        public int TaxYear { get; set; }   
        public decimal TotalSaleProceeds { get; set; }
        public decimal TotalCapitalGains { get; set; }
        public decimal CapitalGainTaxPercentage { get; set; }
        public decimal TaxesDue { get; set; }
        public string TaxCurrency { get; set; }
    }
}
