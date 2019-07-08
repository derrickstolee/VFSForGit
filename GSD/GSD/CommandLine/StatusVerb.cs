﻿using CommandLine;
using GSD.Common;
using GSD.Common.NamedPipes;

namespace GSD.CommandLine
{
    [Verb(StatusVerb.StatusVerbName, HelpText = "Get the status of the GSD virtual repo")]
    public class StatusVerb : GSDVerb.ForExistingEnlistment
    {
        private const string StatusVerbName = "status";

        protected override string VerbName
        {
            get { return StatusVerbName; }
        }

        protected override void Execute(GSDEnlistment enlistment)
        {
            using (NamedPipeClient pipeClient = new NamedPipeClient(enlistment.NamedPipeName))
            {
                if (!pipeClient.Connect())
                {
                    this.ReportErrorAndExit("Unable to connect to GSD.  Try running 'gvfs mount'");
                }

                try
                {
                    pipeClient.SendRequest(NamedPipeMessages.GetStatus.Request);
                    NamedPipeMessages.GetStatus.Response getStatusResponse =
                        NamedPipeMessages.GetStatus.Response.FromJson(pipeClient.ReadRawResponse());

                    this.Output.WriteLine("Enlistment root: " + getStatusResponse.EnlistmentRoot);
                    this.Output.WriteLine("Repo URL: " + getStatusResponse.RepoUrl);
                    this.Output.WriteLine("Cache Server: " + getStatusResponse.CacheServer);
                    this.Output.WriteLine("Local Cache: " + getStatusResponse.LocalCacheRoot);
                    this.Output.WriteLine("Mount status: " + getStatusResponse.MountStatus);
                    this.Output.WriteLine("GSD Lock: " + getStatusResponse.LockStatus);
                    this.Output.WriteLine("Background operations: " + getStatusResponse.BackgroundOperationCount);
                    this.Output.WriteLine("Disk layout version: " + getStatusResponse.DiskLayoutVersion);
                }
                catch (BrokenPipeException e)
                {
                    this.ReportErrorAndExit("Unable to communicate with GSD: " + e.ToString());
                }
            }
        }
    }
}
