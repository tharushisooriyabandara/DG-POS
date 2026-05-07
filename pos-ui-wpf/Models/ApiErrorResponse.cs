using System;
using System.Linq;
using System.Collections.Generic;

namespace POS_UI.Models
{
    public class ApiErrorResponse
    {
        public int Code { get; set; }
        public string Message { get; set; }
        public Dictionary<string, string> Errors { get; set; }
    }
}