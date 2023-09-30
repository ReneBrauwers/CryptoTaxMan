using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models
{
    public class ExchangeRate
    {
        public string? Symbol { get; set; }
        public DateTime Date { get; set; }
        public decimal Open { get; set; }
        public decimal Close { get; set; }
        public decimal Low { get; set; }
        public decimal High { get; set; }
        public decimal OpenCloseAverage { get; set; }
        public decimal LowHighAverage { get; set; }
        public string? ExchangeCurrency { get; set; }
        public string? DataSource { get; set; }
        public bool LookupOptional { get; set; }
    }

  
}
