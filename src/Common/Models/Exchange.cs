using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models
{

    public abstract class BaseExchangeInformation
    {
        public string? Kind { get; set; }
        public string? Symbol { get; set; }
        public string? ExchangeName { get; set; }
        public string? ExchangeSymbol { get; set; }
        public string? ExchangeCurrency { get; set; }
        

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
    }
}
