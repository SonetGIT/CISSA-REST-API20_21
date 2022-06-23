using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CISSA_REST_API.Models.Address
{
    public class village
    {
        public Guid id { get; set; }
        public string name { get; set; }
        public Guid? settlementId { get; set; }
        public double? coefficient { get; set; }
    }
}