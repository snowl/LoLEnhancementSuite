using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LESs
{
    public class TraitNotFoundException :Exception
    {
        public TraitNotFoundException() : base() { }
        public TraitNotFoundException(string msg) : base(msg) { }
    }
}
