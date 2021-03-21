﻿using System;
using System.Collections.Generic;
using System.Text;

namespace DraughtsGameAPIModels
{
    public class GetNextMove
    {
        public int Version { get; set; }

        public int[,] Board { get; set; }

        public int Depth { get; set; }

        public int Player { get; set; }
    }
}
