using System.Collections.Generic;

namespace GVFS.Mount.Tests
{
    public static class TestRunner
    {
        public static Dictionary<string, IMountTest> Tests = new Dictionary<string, IMountTest>();

        static TestRunner()
        {
        }

        public static bool RunTest(string testName, string inputData, out string errors, out string testData)
        {
            errors = $"No errors for {testName}";
            testData = $"I received '{inputData}' as input";
            return true;
        }
    }
}
