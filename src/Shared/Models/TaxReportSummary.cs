using FileHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Models
{
    [DelimitedRecord(",")]
    public class TaxReportSummary
    {
        [FieldOrder(1)]
        public int TaxYear { get; set; }
        [FieldOrder(2)]
        public decimal TotalSaleProceeds { get; set; }
        [FieldOrder(3)]
        public decimal TotalCapitalGains { get; set; }
        [FieldOrder(4)]
        public decimal TotalReportableAsIncome { get; set; }
        [FieldOrder(5)]
        public string TaxCurrency { get; set; }
    }
}
