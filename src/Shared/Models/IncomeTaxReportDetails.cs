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
    public class IncomeTaxReportDetails
    {
        [FieldOrder(1)]
        public int TaxYear { get; set; }
        [FieldOrder(2)]
        public string Asset { get; set; }
        [FieldOrder(3)]
        public decimal Amount { get; set; }
        [FieldOrder(4)]
        [FieldConverter(ConverterKind.Date, "yyyy-MM-dd")] //THH:mm:ss")]
        public DateTime IncomeRegistrationDate { get; set; }
        [FieldOrder(5)]
        public decimal IncomeValue { get; set; }
        [FieldOrder(6)]
        public string Currency { get; set; }
        [FieldOrder(7)]
        public string Comments { get; set; }
    }
}
