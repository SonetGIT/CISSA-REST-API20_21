﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CISSA_REST_API.Models
{
    public class MSECDetails
    {
        public string OrganizationName { get; set; }
        public DateTime ExaminationDate { get; set; }
        public string ExaminationType { get; set; }
        public string DisabilityGroup { get; set; }
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public DateTime TimeOfDisability { get; set; }
        public string ReExamination { get; set; }
        public string StatusCode { get; set; }
    }
}