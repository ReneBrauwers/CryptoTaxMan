using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Models
{
    public class ExchangeRateSynchronisationResult
    {
        public Dictionary<string, (DateTime from, DateTime to, int records)>? Added { get; set; }
        
        public Dictionary<string, (DateTime from, DateTime to, int records)>? Totals { get; set; }
        public List<ExchangeInformationRetrievalFailure>? RetrievalFailures { get; set; }
    }
}
