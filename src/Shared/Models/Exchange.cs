using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Models
{

    public abstract class BaseExchangeInformation
    {
        public string? Kind { get; set; }
        public string? Symbol { get; set; }
        public string? ExchangeName { get; set; }
        public string? ExchangeSymbol { get; set; }
        public string? ExchangeCurrency { get; set; }
        public DateTime? LastExchangeRateEntryDate { get; set; }
        public bool IsActive { get; set; } = false;

    }

    public class ExchangeInformation: BaseExchangeInformation
    {

    }

    public class ExchangeInformationRetrievalFailure : BaseExchangeInformation
    {       
        public ExchangeInformationRetrievalFailureProperties retrievalDetails { get; set; }
    }
    public class ExchangeInformationRetrievalFailureProperties
    {
        public DateTime Date { get; set; }
        public bool IsDateFromQuery { get; set; }
        public string Comments { get; set; } = string.Empty;
    }
}
