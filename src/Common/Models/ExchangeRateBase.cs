using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileHelpers;

namespace Common.Models
{
    [DelimitedRecord(",")]
    [IgnoreFirst(1)]
    public class ExchangeRateBase
    {
        [FieldConverter(ConverterKind.Date, "yyyy-MM-dd")]
        public DateTime Date { get; set; }
        [FieldOptional]
        public decimal? Open { get; set; }
        [FieldOptional]
        public decimal? High { get; set; }
        [FieldOptional]
        public decimal? Low { get; set; }
        [FieldOptional]
        public decimal? Close { get; set; }
        [FieldOptional]
        [DisplayName("Adj Close")]
        public decimal? AdjClose { get; set; }
        [FieldOptional]
        public decimal? Volume { get; set; }
    }
}
