using System;
using System.Collections.Generic;
using System.Text;

namespace DraughtsGameAPIModels
{
    public class Response 
    {
        public bool Successful { get; set; }

        public NextMove NextMove { get; set; }

        public string ErrorMessage { get; set; }
    }
}
