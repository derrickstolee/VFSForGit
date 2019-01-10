using CommandLine;
using GVFS.Common;
using GVFS.Common.NamedPipes;

namespace GVFS.CommandLine
{
    [Verb(TestVerb.TestVerbName, HelpText = "Send a request to the mount process to run a test")]
    public class TestVerb : GVFSVerb.ForExistingEnlistment
    {
        private const string TestVerbName = "Test";

        [Value(
            0,
            Required = true,
            MetaName = "Test Name",
            HelpText = "The exact name of the test to run")]
        public string TestName { get; set; }

        protected override string VerbName
        {
            get { return TestVerbName; }
        }

        protected override void Execute(GVFSEnlistment enlistment)
        {
            using (NamedPipeClient pipeClient = new NamedPipeClient(enlistment.NamedPipeName))
            {
                if (!pipeClient.Connect())
                {
                    this.ReportErrorAndExit("Unable to connect to GVFS.  Try running 'gvfs mount'");
                }

                try
                {
                    pipeClient.SendRequest(new NamedPipeMessages.RunTest.Request(this.TestName).CreateMessage());
                    NamedPipeMessages.RunTest.Response runTestResponse =
                        NamedPipeMessages.RunTest.Response.FromJson(pipeClient.ReadRawResponse());

                    this.Output.WriteLine($"{nameof(runTestResponse.TestRan)}: {runTestResponse.TestRan}");
                    this.Output.WriteLine($"{nameof(runTestResponse.TestSucceeded)}: {runTestResponse.TestSucceeded}");
                    this.Output.WriteLine($"{nameof(runTestResponse.TestData)}: {runTestResponse.TestData}");
                }
                catch (BrokenPipeException e)
                {
                    this.ReportErrorAndExit("Unable to communicate with GVFS: " + e.ToString());
                }
            }
        }
    }
}
