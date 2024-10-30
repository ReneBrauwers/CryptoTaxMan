using FileHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Models
{
    [DelimitedRecord(",")]
    public class IncomeTaxReportSummary
    {
        [FieldOrder(1)]
        public int TaxYear { get; set; }
 
        [FieldOrder(2)]
        public decimal TotalAdditionalIncome { get; set; }
      
        [FieldOrder(3)]
        public string Currency { get; set; }

    }
}
