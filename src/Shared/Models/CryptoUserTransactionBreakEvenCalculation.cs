using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Models
{

    public class CryptoUserTransactionBreakEvenCalculationResult
    {
        public decimal BreakEvenPrice { get; set; }
        public List<CryptoUserTransactionBreakEvenCalculationStep> Steps { get; set; } = new List<CryptoUserTransactionBreakEvenCalculationStep>();
    }

    public class CryptoUserTransactionBreakEvenCalculationStep
    {

        public DateTime TransactionDate { get; set; }
        public string TransactionType { get; set; }
        public decimal Amount { get; set; }
        public decimal Value { get; set; }
        public decimal TotalAmountAvailable { get; set; }
        public decimal TotalCost { get; set; }
        public decimal AverageBuyPrice { get; set; }
    }
}

