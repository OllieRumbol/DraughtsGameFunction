using DraughtsGameFunctionModels.Service;
using System;
using System.Collections.Generic;
using System.Text;

namespace DraughtsGameFunctionModels.Controller
{
    public class AutomatedPlayerResponse : Response
    {
        public NextMove NextMove { get; set; }
    }
}
