﻿using GSD.Common;
using GSD.Common.Git;
using GSD.Common.Tracing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GSD.RepairJobs
{
    public class GitHeadRepairJob : RepairJob
    {
        public GitHeadRepairJob(ITracer tracer, TextWriter output, GSDEnlistment enlistment)
            : base(tracer, output, enlistment)
        {
        }

        public override string Name
        {
            get { return @".git\HEAD"; }
        }

        public override IssueType HasIssue(List<string> messages)
        {
            if (TryParseHead(this.Enlistment, messages))
            {
                return IssueType.None;
            }

            if (!this.CanBeRepaired(messages))
            {
                return IssueType.CantFix;
            }

            return IssueType.Fixable;
        }

        /// <summary>
        /// Fixes the HEAD using the reflog to find the last SHA.
        /// We detach HEAD as a side-effect of repair.
        /// </summary>
        public override FixResult TryFixIssues(List<string> messages)
        {
            string error;
            RefLogEntry refLog;
            if (!TryReadLastRefLogEntry(this.Enlistment, GSDConstants.DotGit.HeadName, out refLog, out error))
            {
                this.Tracer.RelatedError(error);
                messages.Add(error);
                return FixResult.Failure;
            }

            try
            {
                string refPath = Path.Combine(this.Enlistment.WorkingDirectoryRoot, GSDConstants.DotGit.Head);
                File.WriteAllText(refPath, refLog.TargetSha);
            }
            catch (IOException ex)
            {
                EventMetadata metadata = new EventMetadata();
                this.Tracer.RelatedError(metadata, "Failed to write HEAD: " + ex.ToString());
                return FixResult.Failure;
            }

            this.Tracer.RelatedEvent(
                EventLevel.Informational,
                "MovedHead",
                new EventMetadata
                {
                    { "DestinationCommit", refLog.TargetSha }
                });

            messages.Add("As a result of the repair, 'git status' will now complain that HEAD is detached");
            messages.Add("You can fix this by creating a branch using 'git checkout -b <branchName>'");

            return FixResult.Success;
        }

        /// <summary>
        /// 'git ref-log' doesn't work if the repo is corrupted, so parsing reflogs seems like the only solution.
        /// </summary>
        /// <param name="fullSymbolicRef">A full symbolic ref name. eg. HEAD, refs/remotes/origin/HEAD, refs/heads/master</param>
        private static bool TryReadLastRefLogEntry(Enlistment enlistment, string fullSymbolicRef, out RefLogEntry refLog, out string error)
        {
            string refLogPath = Path.Combine(enlistment.WorkingDirectoryRoot, GSDConstants.DotGit.Logs.Root, fullSymbolicRef);
            if (!File.Exists(refLogPath))
            {
                refLog = null;
                error = "Could not find reflog for ref '" + fullSymbolicRef + "'";
                return false;
            }

            try
            {
                string refLogContents = File.ReadLines(refLogPath).Last();
                if (!RefLogEntry.TryParse(refLogContents, out refLog))
                {
                    error = "Last ref log entry for " + fullSymbolicRef + " is unparsable.";
                    return false;
                }
            }
            catch (IOException ex)
            {
                refLog = null;
                error = "IOException while reading reflog '" + refLogPath + "': " + ex.Message;
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryParseHead(Enlistment enlistment, List<string> messages)
        {
            string refPath = Path.Combine(enlistment.WorkingDirectoryRoot, GSDConstants.DotGit.Head);
            if (!File.Exists(refPath))
            {
                messages.Add("Could not find ref file for '" + GSDConstants.DotGit.Head + "'");
                return false;
            }

            string refContents;
            try
            {
                refContents = File.ReadAllText(refPath).Trim();
            }
            catch (IOException ex)
            {
                messages.Add("IOException while reading .git\\HEAD: " + ex.Message);
                return false;
            }

            const string MinimallyValidRef = "ref: refs/";
            if (refContents.StartsWith(MinimallyValidRef, StringComparison.OrdinalIgnoreCase) ||
                SHA1Util.IsValidShaFormat(refContents))
            {
                return true;
            }

            messages.Add("Invalid contents found in '" + GSDConstants.DotGit.Head + "': " + refContents);
            return false;
        }

        private bool CanBeRepaired(List<string> messages)
        {
            Func<string, string> createErrorMessage = operation => string.Format("Can't repair HEAD while a {0} operation is in progress", operation);

            string rebasePath = Path.Combine(this.Enlistment.WorkingDirectoryRoot, GSDConstants.DotGit.RebaseApply);
            if (Directory.Exists(rebasePath))
            {
                messages.Add(createErrorMessage("rebase"));
                return false;
            }

            string mergeHeadPath = Path.Combine(this.Enlistment.WorkingDirectoryRoot, GSDConstants.DotGit.MergeHead);
            if (File.Exists(mergeHeadPath))
            {
                messages.Add(createErrorMessage("merge"));
                return false;
            }

            string bisectStartPath = Path.Combine(this.Enlistment.WorkingDirectoryRoot, GSDConstants.DotGit.BisectStart);
            if (File.Exists(bisectStartPath))
            {
                messages.Add(createErrorMessage("bisect"));
                return false;
            }

            string cherrypickHeadPath = Path.Combine(this.Enlistment.WorkingDirectoryRoot, GSDConstants.DotGit.CherryPickHead);
            if (File.Exists(cherrypickHeadPath))
            {
                messages.Add(createErrorMessage("cherry-pick"));
                return false;
            }

            string revertHeadPath = Path.Combine(this.Enlistment.WorkingDirectoryRoot, GSDConstants.DotGit.RevertHead);
            if (File.Exists(revertHeadPath))
            {
                messages.Add(createErrorMessage("revert"));
                return false;
            }

            return true;
        }
    }
}
