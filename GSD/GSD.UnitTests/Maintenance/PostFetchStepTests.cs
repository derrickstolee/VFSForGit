﻿using GSD.Common;
using GSD.Common.FileSystem;
using GSD.Common.Git;
using GSD.Common.Maintenance;
using GSD.Tests.Should;
using GSD.UnitTests.Mock.Common;
using GSD.UnitTests.Mock.FileSystem;
using GSD.UnitTests.Mock.Git;
using NUnit.Framework;
using System.Collections.Generic;

namespace GSD.UnitTests.Maintenance
{
    [TestFixture]
    public class PostFetchStepTests
    {
        private MockTracer tracer;
        private MockGitProcess gitProcess;
        private GSDContext context;

        private string MultiPackIndexWriteCommand => $"-c core.multiPackIndex=true multi-pack-index write --object-dir=\"{this.context.Enlistment.GitObjectsRoot}\"";
        private string MultiPackIndexVerifyCommand => $"-c core.multiPackIndex=true multi-pack-index verify --object-dir=\"{this.context.Enlistment.GitObjectsRoot}\"";
        private string CommitGraphWriteCommand => $"commit-graph write --stdin-packs --split --size-multiple=4 --object-dir \"{this.context.Enlistment.GitObjectsRoot}\"";
        private string CommitGraphVerifyCommand => $"commit-graph verify --shallow --object-dir \"{this.context.Enlistment.GitObjectsRoot}\"";

        [TestCase]
        public void WriteMultiPackIndexNoGraphOnEmptyPacks()
        {
            this.TestSetup();

            this.gitProcess.SetExpectedCommandResult(
                this.MultiPackIndexWriteCommand,
                () => new GitProcess.Result(string.Empty, string.Empty, GitProcess.Result.SuccessCode));
            this.gitProcess.SetExpectedCommandResult(
                this.MultiPackIndexVerifyCommand,
                () => new GitProcess.Result(string.Empty, string.Empty, GitProcess.Result.SuccessCode));

            PostFetchStep step = new PostFetchStep(this.context, new List<string>());
            step.Execute();

            this.tracer.StartActivityTracer.RelatedErrorEvents.Count.ShouldEqual(0);
            this.tracer.StartActivityTracer.RelatedWarningEvents.Count.ShouldEqual(0);
            this.tracer.RelatedInfoEvents.Count.ShouldEqual(1);

            List<string> commands = this.gitProcess.CommandsRun;
            commands.Count.ShouldEqual(2);
            commands[0].ShouldEqual(this.MultiPackIndexWriteCommand);
            commands[1].ShouldEqual(this.MultiPackIndexVerifyCommand);
        }

        [TestCase]
        public void WriteMultiPackIndexAndGraphWithPacks()
        {
            this.TestSetup();

            this.gitProcess.SetExpectedCommandResult(
                this.MultiPackIndexWriteCommand,
                () => new GitProcess.Result(string.Empty, string.Empty, GitProcess.Result.SuccessCode));
            this.gitProcess.SetExpectedCommandResult(
                this.MultiPackIndexVerifyCommand,
                () => new GitProcess.Result(string.Empty, string.Empty, GitProcess.Result.SuccessCode));
            this.gitProcess.SetExpectedCommandResult(
                this.CommitGraphWriteCommand,
                () => new GitProcess.Result(string.Empty, string.Empty, GitProcess.Result.SuccessCode));
            this.gitProcess.SetExpectedCommandResult(
                this.CommitGraphVerifyCommand,
                () => new GitProcess.Result(string.Empty, string.Empty, GitProcess.Result.SuccessCode));

            PostFetchStep step = new PostFetchStep(this.context, new List<string>() { "pack" }, requireObjectCacheLock: false);
            step.Execute();

            this.tracer.StartActivityTracer.RelatedErrorEvents.Count.ShouldEqual(0);
            this.tracer.StartActivityTracer.RelatedWarningEvents.Count.ShouldEqual(0);
            this.tracer.RelatedInfoEvents.Count.ShouldEqual(0);

            List<string> commands = this.gitProcess.CommandsRun;

            commands.Count.ShouldEqual(4);
            commands[0].ShouldEqual(this.MultiPackIndexWriteCommand);
            commands[1].ShouldEqual(this.MultiPackIndexVerifyCommand);
            commands[2].ShouldEqual(this.CommitGraphWriteCommand);
            commands[3].ShouldEqual(this.CommitGraphVerifyCommand);
        }

        [TestCase]
        public void RewriteMultiPackIndexOnBadVerify()
        {
            this.TestSetup();

            this.gitProcess.SetExpectedCommandResult(
                this.MultiPackIndexWriteCommand,
                () => new GitProcess.Result(string.Empty, string.Empty, GitProcess.Result.SuccessCode));
            this.gitProcess.SetExpectedCommandResult(
                this.MultiPackIndexVerifyCommand,
                () => new GitProcess.Result(string.Empty, string.Empty, GitProcess.Result.GenericFailureCode));
            this.gitProcess.SetExpectedCommandResult(
                this.CommitGraphWriteCommand,
                () => new GitProcess.Result(string.Empty, string.Empty, GitProcess.Result.SuccessCode));
            this.gitProcess.SetExpectedCommandResult(
                this.CommitGraphVerifyCommand,
                () => new GitProcess.Result(string.Empty, string.Empty, GitProcess.Result.SuccessCode));

            PostFetchStep step = new PostFetchStep(this.context, new List<string>() { "pack" }, requireObjectCacheLock: false);
            step.Execute();

            this.tracer.StartActivityTracer.RelatedErrorEvents.Count.ShouldEqual(0);
            this.tracer.StartActivityTracer.RelatedWarningEvents.Count.ShouldEqual(1);

            List<string> commands = this.gitProcess.CommandsRun;

            commands.Count.ShouldEqual(5);
            commands[0].ShouldEqual(this.MultiPackIndexWriteCommand);
            commands[1].ShouldEqual(this.MultiPackIndexVerifyCommand);
            commands[2].ShouldEqual(this.MultiPackIndexWriteCommand);
            commands[3].ShouldEqual(this.CommitGraphWriteCommand);
            commands[4].ShouldEqual(this.CommitGraphVerifyCommand);
        }

        [TestCase]
        public void RewriteCommitGraphOnBadVerify()
        {
            this.TestSetup();

            this.gitProcess.SetExpectedCommandResult(
                this.MultiPackIndexWriteCommand,
                () => new GitProcess.Result(string.Empty, string.Empty, GitProcess.Result.SuccessCode));
            this.gitProcess.SetExpectedCommandResult(
                this.MultiPackIndexVerifyCommand,
                () => new GitProcess.Result(string.Empty, string.Empty, GitProcess.Result.SuccessCode));
            this.gitProcess.SetExpectedCommandResult(
                this.CommitGraphWriteCommand,
                () => new GitProcess.Result(string.Empty, string.Empty, GitProcess.Result.SuccessCode));
            this.gitProcess.SetExpectedCommandResult(
                this.CommitGraphVerifyCommand,
                () => new GitProcess.Result(string.Empty, string.Empty, GitProcess.Result.GenericFailureCode));

            PostFetchStep step = new PostFetchStep(this.context, new List<string>() { "pack" }, requireObjectCacheLock: false);
            step.Execute();

            this.tracer.StartActivityTracer.RelatedErrorEvents.Count.ShouldEqual(0);
            this.tracer.StartActivityTracer.RelatedWarningEvents.Count.ShouldEqual(1);

            List<string> commands = this.gitProcess.CommandsRun;
            commands.Count.ShouldEqual(5);
            commands[0].ShouldEqual(this.MultiPackIndexWriteCommand);
            commands[1].ShouldEqual(this.MultiPackIndexVerifyCommand);
            commands[2].ShouldEqual(this.CommitGraphWriteCommand);
            commands[3].ShouldEqual(this.CommitGraphVerifyCommand);
            commands[4].ShouldEqual(this.CommitGraphWriteCommand);
        }

        private void TestSetup()
        {
            this.gitProcess = new MockGitProcess();

            // Create enlistment using git process
            GSDEnlistment enlistment = new MockGSDEnlistment(this.gitProcess);

            PhysicalFileSystem fileSystem = new MockFileSystem(new MockDirectory(enlistment.EnlistmentRoot, null, null));

            // Create and return Context
            this.tracer = new MockTracer();
            this.context = new GSDContext(this.tracer, fileSystem, repository: null, enlistment: enlistment);
        }
    }
}
