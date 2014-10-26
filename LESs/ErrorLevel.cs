using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LESs
{
    public enum ErrorLevel : byte
    {
        NoError=0,
        Warning=1,
        UnableToPatch=2,
        GoodJobYourInstallationIsProbablyCorruptedNow=3
    }
}
