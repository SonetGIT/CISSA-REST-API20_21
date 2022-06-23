using Domain.ResponseTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CISSA_REST_API.Models
{
    public class AdoptedChildrenReportResponse : IAdoptedChildrenReportResponse
    {
        public IAdoptedChildrenReportItem[] ByAge { get; set; }
        public IAdoptedChildrenReportItem[] ByNationalities { get; set; }
        public IAdoptedChildrenReportItem[] ByGeography { get; set; }
    }
}