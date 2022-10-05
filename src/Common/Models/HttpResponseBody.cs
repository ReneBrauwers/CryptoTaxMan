using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models
{
    public class HttpResponseBody<T>
    {
        public bool IsValid { get; set; }

        public T? Value { get; set; }

        public IEnumerable<ValidationResult>? ValidationResults { get; set; }
    }
}
