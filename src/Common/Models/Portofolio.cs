using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models
{
    public class Portofolio
    {
        public string Token { get; set; }
        public double Amount { get; set; }
        public double InvestmentValue { get; set; }
        public string InvestmentValueCurrency { get; set; }
        public double AverageInvestmentValue { get; set; }
        public string AverageInvestmentValueCurrency { get; set; }
        public double CurrentValue { get; set; }
        public string CurrentValueCurrency { get; set; }
        public DateTime LastUpdated { get; set; }
        
    }
}
