using System;

namespace LESs
{
    public class ClassNotFoundException :Exception
    {
        public ClassNotFoundException() : base() { }
        public ClassNotFoundException(string msg) : base(msg) { }
    }
}
