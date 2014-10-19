using System;

namespace LESs
{
    public class TraitNotFoundException :Exception
    {
        public TraitNotFoundException() : base() { }
        public TraitNotFoundException(string msg) : base(msg) { }
    }
}
