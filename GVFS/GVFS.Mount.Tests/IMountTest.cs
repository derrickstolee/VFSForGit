using System;
using System.Collections.Generic;
using System.Text;

namespace GVFS.Mount.Tests
{
    public interface IMountTest
    {
        string Name { get; }

        bool RunTest(object input, out object output);
    }
}
