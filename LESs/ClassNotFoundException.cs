using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LESs
{
    public class ClassNotFoundException :Exception
    {
        public ClassNotFoundException() : base() { }
        public ClassNotFoundException(string msg) : base(msg) { }
    }
}
