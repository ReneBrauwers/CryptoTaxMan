using FileHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Models
{
    [IgnoreFirst(1)]
    [DelimitedRecord(",")]
    public class TaxReportDetail
    {
        [FieldOrder(1)]
        public int TaxYear { get; set; }
        [FieldOrder(2)]
        public string Asset { get; set; }
        [FieldOrder(3)]
        public decimal SellAmount { get; set; }
        [FieldOrder(4)]
        [FieldConverter(ConverterKind.Date, "yyyy-MM-dd")] //THH:mm:ss")]
        public DateTime BoughtDate { get; set; }
        [FieldOrder(5)]
        public decimal BuyPrice { get; set; }
        [FieldOrder(6)]
        [FieldConverter(ConverterKind.Date, "yyyy-MM-dd")] //THH:mm:ss")]
        public DateTime SellDate { get; set; }
        [FieldOrder(7)]
        public decimal SellPrice { get; set; }
        [FieldOrder(8)]
        public string Currency { get; set; }
        [FieldOrder(9)]
        public decimal CapitalGainAmount { get; set; }
        [FieldOrder(10)]
        public string Calculation { get; set; }
    }
}
