﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CISSA_REST_API.Models
{
    public class AdoptedChildrenReportItem
    {
        public int No { get; set; }
        public string Name { get; set; }
        public int Boys { get; set; }
        public int Girls { get; set; }
        public int Total { get; set; }
    }
}