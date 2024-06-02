using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Models
{
    public class Holding
    {
        public DateOnly CreatedOn { get; set; }
        public string Name { get; set; }
        public decimal Available { get; set; }
        public decimal BoughtAt { get; set; }
        public string Currency { get; set; }

        public List<TaxReportDetail> RecordedTransactions { get; set; }
    }
}
