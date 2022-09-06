using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CISSA_REST_API.Models
{
    public class AdoptedChildrenReportResponse
    {
        public AdoptedChildrenReportItem[] ByAge { get; set; }
        public AdoptedChildrenReportItem[] ByNationalities { get; set; }
        public AdoptedChildrenReportItem[] ByGeography { get; set; }
    }
}