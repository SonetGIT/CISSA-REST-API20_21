using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CISSA_REST_API.Models.Address
{
    public class settlement
    {
        public Guid id { get; set; }
        public string name { get; set; }
        public Guid? districtType { get; set; }
        public Guid? districtId { get; set; }
    }
}