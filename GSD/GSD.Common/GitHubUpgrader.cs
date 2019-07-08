using GSD.Common.FileSystem;
using GSD.Common.Git;
using GSD.Common.Tracing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;

namespace GSD.Common
{
    public class GitHubUpgrader : ProductUpgrader
    {
        private const string GitHubReleaseURL = @"https://api.github.com/repos/microsoft/GSD/releases";
        private const string JSONMediaType = @"application/vnd.github.v3+json";
        private const string UserAgent = @"GSD_Auto_Upgrader";
        private const string CommonInstallerArgs = "/VERYSILENT /CLOSEAPPLICATIONS /SUPPRESSMSGBOXES /NORESTART";
        private const string GSDInstallerArgs = CommonInstallerArgs + " /REMOUNTREPOS=false";
        private const string GitInstallerArgs = CommonInstallerArgs + " /ALLOWDOWNGRADE=1";
        private const string GitAssetId = "Git";
        private const string GSDAssetId = "GSD";
        private const string GitInstallerFileNamePrefix = "Git-";
        private const string GSDSigner = "Microsoft Corporation";
        private const string GSDCertIssuer = "Microsoft Code Signing PCA";
        private const string GitSigner = "Johannes Schindelin";
        private const string GitCertIssuer = "COMODO RSA Code Signing CA";

        private static readonly HashSet<string> GSDInstallerFileNamePrefixCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SetupGSD",
            "GSD"
        };

        private Version newestVersion;
        private Release newestRelease;

        public GitHubUpgrader(
            string currentVersion,
            ITracer tracer,
            PhysicalFileSystem fileSystem,
            GitHubUpgraderConfig upgraderConfig,
            bool dryRun = false,
            bool noVerify = false)
            : base(currentVersion, tracer, dryRun, noVerify, fileSystem)
        {
            this.Config = upgraderConfig;
        }

        public GitHubUpgraderConfig Config { get; private set; }

        public override bool SupportsAnonymousVersionQuery { get => true; }

        public static GitHubUpgrader Create(
            ITracer tracer,
            PhysicalFileSystem fileSystem,
            LocalGSDConfig gvfsConfig,
            bool dryRun,
            bool noVerify,
            out string error)
        {
            return Create(tracer, fileSystem, dryRun, noVerify, gvfsConfig, out error);
        }

        public static GitHubUpgrader Create(
            ITracer tracer,
            PhysicalFileSystem fileSystem,
            bool dryRun,
            bool noVerify,
            LocalGSDConfig localConfig,
            out string error)
        {
            GitHubUpgrader upgrader = null;
            GitHubUpgraderConfig gitHubUpgraderConfig = new GitHubUpgraderConfig(tracer, localConfig);

            if (!gitHubUpgraderConfig.TryLoad(out error))
            {
                return null;
            }

            if (gitHubUpgraderConfig.ConfigError())
            {
                gitHubUpgraderConfig.ConfigAlertMessage(out error);
                return null;
            }

            upgrader = new GitHubUpgrader(
                    ProcessHelper.GetCurrentProcessVersion(),
                    tracer,
                    fileSystem,
                    gitHubUpgraderConfig,
                    dryRun,
                    noVerify);

            return upgrader;
        }

        public override bool UpgradeAllowed(out string message)
        {
            return this.Config.UpgradeAllowed(out message);
        }

        public override bool TryQueryNewestVersion(
            out Version newVersion,
            out string message)
        {
            List<Release> releases;

            newVersion = null;
            if (this.TryFetchReleases(out releases, out message))
            {
                foreach (Release nextRelease in releases)
                {
                    Version releaseVersion = null;

                    if (nextRelease.Ring <= this.Config.UpgradeRing &&
                        nextRelease.TryParseVersion(out releaseVersion) &&
                        releaseVersion > this.installedVersion)
                    {
                        newVersion = releaseVersion;
                        this.newestVersion = releaseVersion;
                        this.newestRelease = nextRelease;
                        message = $"New GSD version {newVersion.ToString()} available in ring {this.Config.UpgradeRing}.";
                        break;
                    }
                }

                if (newVersion == null)
                {
                    message = $"Great news, you're all caught up on upgrades in the {this.Config.UpgradeRing} ring!";
                }

                return true;
            }

            return false;
        }

        public override bool TryDownloadNewestVersion(out string errorMessage)
        {
            if (!this.TryCreateAndConfigureDownloadDirectory(this.tracer, out errorMessage))
            {
                this.tracer.RelatedError($"{nameof(GitHubUpgrader)}.{nameof(this.TryCreateAndConfigureDownloadDirectory)} failed. {errorMessage}");
                return false;
            }

            bool downloadedGit = false;
            bool downloadedGSD = false;

            foreach (Asset asset in this.newestRelease.Assets)
            {
                bool targetOSMatch = string.Equals(Path.GetExtension(asset.Name), GSDPlatform.Instance.Constants.InstallerExtension, StringComparison.OrdinalIgnoreCase);
                bool isGitAsset = this.IsGitAsset(asset);
                bool isGSDAsset = isGitAsset ? false : this.IsGSDAsset(asset);
                if (!targetOSMatch || (!isGSDAsset && !isGitAsset))
                {
                    continue;
                }

                if (!this.TryDownloadAsset(asset, out errorMessage))
                {
                    errorMessage = $"Could not download {(isGSDAsset ? GSDAssetId : GitAssetId)} installer. {errorMessage}";
                    return false;
                }
                else
                {
                    downloadedGit = isGitAsset ? true : downloadedGit;
                    downloadedGSD = isGSDAsset ? true : downloadedGSD;
                }
            }

            if (!downloadedGit || !downloadedGSD)
            {
                errorMessage = $"Could not find {(!downloadedGit ? GitAssetId : GSDAssetId)} installer in the latest release.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        public override bool TryRunInstaller(InstallActionWrapper installActionWrapper, out string error)
        {
            string localError;

            this.TryGetGitVersion(out GitVersion newGitVersion, out localError);

            if (!installActionWrapper(
                 () =>
                 {
                     if (!this.TryInstallUpgrade(GitAssetId, newGitVersion.ToString(), out localError))
                     {
                         return false;
                     }

                     return true;
                 },
                $"Installing Git version: {newGitVersion}"))
            {
                error = localError;
                return false;
            }

            if (!installActionWrapper(
                 () =>
                 {
                     if (!this.TryInstallUpgrade(GSDAssetId, this.newestVersion.ToString(), out localError))
                     {
                         return false;
                     }

                     return true;
                 },
                $"Installing GSD version: {this.newestVersion}"))
            {
                error = localError;
                return false;
            }

            this.LogVersionInfo(this.newestVersion, newGitVersion, "Newly Installed Version");

            error = null;
            return true;
        }

        public override bool TryCleanup(out string error)
        {
            error = string.Empty;
            if (this.newestRelease == null)
            {
                return true;
            }

            foreach (Asset asset in this.newestRelease.Assets)
            {
                Exception exception;
                if (!this.TryDeleteDownloadedAsset(asset, out exception))
                {
                    error += $"Could not delete {asset.LocalPath}. {exception.ToString()}." + Environment.NewLine;
                }
            }

            if (!string.IsNullOrEmpty(error))
            {
                error.TrimEnd(Environment.NewLine.ToCharArray());
                return false;
            }

            error = null;
            return true;
        }

        protected virtual bool TryDeleteDownloadedAsset(Asset asset, out Exception exception)
        {
            return this.fileSystem.TryDeleteFile(asset.LocalPath, out exception);
        }

        protected virtual bool TryDownloadAsset(Asset asset, out string errorMessage)
        {
            errorMessage = null;

            string downloadPath = ProductUpgraderInfo.GetAssetDownloadsPath();
            string localPath = Path.Combine(downloadPath, asset.Name);
            WebClient webClient = new WebClient();

            try
            {
                webClient.DownloadFile(asset.DownloadURL, localPath);
                asset.LocalPath = localPath;
            }
            catch (WebException webException)
            {
                errorMessage = "Download error: " + webException.Message;
                this.TraceException(webException, nameof(this.TryDownloadAsset), $"Error downloading asset {asset.Name}.");
                return false;
            }

            return true;
        }

        protected virtual bool TryFetchReleases(out List<Release> releases, out string errorMessage)
        {
            HttpClient client = new HttpClient();

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(JSONMediaType));
            client.DefaultRequestHeaders.Add("User-Agent", UserAgent);

            releases = null;
            errorMessage = null;

            try
            {
                Stream result = client.GetStreamAsync(GitHubReleaseURL).GetAwaiter().GetResult();

                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<Release>));
                releases = serializer.ReadObject(result) as List<Release>;
                return true;
            }
            catch (HttpRequestException exception)
            {
                errorMessage = string.Format("Network error: could not connect to GitHub({0}). {1}", GitHubReleaseURL, exception.Message);
                this.TraceException(exception, nameof(this.TryFetchReleases), $"Error fetching release info.");
            }
            catch (TaskCanceledException exception)
            {
                // GetStreamAsync can also throw a TaskCanceledException to indicate a timeout
                // https://github.com/dotnet/corefx/issues/20296
                errorMessage = string.Format("Network error: could not connect to GitHub({0}). {1}", GitHubReleaseURL, exception.Message);
                this.TraceException(exception, nameof(this.TryFetchReleases), $"Error fetching release info.");
            }
            catch (SerializationException exception)
            {
                errorMessage = string.Format("Parse error: could not parse releases info from GitHub({0}). {1}", GitHubReleaseURL, exception.Message);
                this.TraceException(exception, nameof(this.TryFetchReleases), $"Error parsing release info.");
            }

            return false;
        }

        protected virtual void RunInstaller(string path, string args, string certCN, string issuerCN, out int exitCode, out string error)
        {
            using (Stream stream = this.fileSystem.OpenFileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, false))
            {
                string expectedCNPrefix = $"CN={certCN}, ";
                string expectecIssuerCNPrefix = $"CN={issuerCN}";
                string subject;
                string issuer;
                if (!GSDPlatform.Instance.TryVerifyAuthenticodeSignature(path, out subject, out issuer, out error))
                {
                    exitCode = -1;
                    return;
                }

                if (!subject.StartsWith(expectedCNPrefix) || !issuer.StartsWith(expectecIssuerCNPrefix))
                {
                    exitCode = -1;
                    error = $"Installer {path} is signed by unknown signer.";
                    this.tracer.RelatedError($"Installer {path} is signed by unknown signer. Signed by {subject}, issued by {issuer} expected signer is {certCN}, issuer {issuerCN}.");
                    return;
                }

                this.RunInstaller(path, args, out exitCode, out error);
            }
        }

        private bool TryGetGitVersion(out GitVersion gitVersion, out string error)
        {
            error = null;

            foreach (Asset asset in this.newestRelease.Assets)
            {
                if (asset.Name.StartsWith(GitInstallerFileNamePrefix) &&
                    GitVersion.TryParseInstallerName(asset.Name, GSDPlatform.Instance.Constants.InstallerExtension, out gitVersion))
                {
                    return true;
                }
            }

            error = "Could not find Git version info in newest release";
            gitVersion = null;

            return false;
        }

        private bool TryInstallUpgrade(string assetId, string version, out string consoleError)
        {
            bool installSuccess = false;
            EventMetadata metadata = new EventMetadata();
            metadata.Add("Upgrade Step", nameof(this.TryInstallUpgrade));
            metadata.Add("AssetId", assetId);
            metadata.Add("Version", version);

            using (ITracer activity = this.tracer.StartActivity($"{nameof(this.TryInstallUpgrade)}", EventLevel.Informational, metadata))
            {
                if (!this.TryRunInstaller(assetId, out installSuccess, out consoleError) ||
                !installSuccess)
                {
                    this.tracer.RelatedError(metadata, $"{nameof(this.TryInstallUpgrade)} failed. {consoleError}");
                    return false;
                }

                activity.RelatedInfo("Successfully installed GSD version: " + version);
            }

            return installSuccess;
        }

        private bool TryRunInstaller(string assetId, out bool installationSucceeded, out string error)
        {
            error = null;
            installationSucceeded = false;

            int exitCode = 0;
            bool launched = this.TryRunInstallerForAsset(assetId, out exitCode, out error);
            installationSucceeded = exitCode == 0;

            return launched;
        }

        private bool TryRunInstallerForAsset(string assetId, out int installerExitCode, out string error)
        {
            error = null;
            installerExitCode = 0;

            bool installerIsRun = false;
            string path;
            string installerArgs;
            if (this.TryGetLocalInstallerPath(assetId, out path, out installerArgs))
            {
                if (!this.dryRun)
                {
                    string logFilePath = GSDEnlistment.GetNewLogFileName(
                        ProductUpgraderInfo.GetLogDirectoryPath(),
                        Path.GetFileNameWithoutExtension(path),
                        this.UpgradeInstanceId,
                        this.fileSystem);

                    string args = installerArgs + " /Log=" + logFilePath;
                    string certCN = null;
                    string issuerCN = null;
                    switch (assetId)
                    {
                        case GSDAssetId:
                        {
                            certCN = GSDSigner;
                            issuerCN = GSDCertIssuer;
                            break;
                        }

                        case GitAssetId:
                        {
                            certCN = GitSigner;
                            issuerCN = GitCertIssuer;
                            break;
                        }
                    }

                    this.RunInstaller(path, args, certCN, issuerCN, out installerExitCode, out error);

                    if (installerExitCode != 0 && string.IsNullOrEmpty(error))
                    {
                        error = assetId + " installer failed. Error log: " + logFilePath;
                    }
                }

                installerIsRun = true;
            }
            else
            {
                error = "Could not find downloaded installer for " + assetId;
            }

            return installerIsRun;
        }

        private bool TryGetLocalInstallerPath(string assetId, out string path, out string args)
        {
            foreach (Asset asset in this.newestRelease.Assets)
            {
                if (string.Equals(Path.GetExtension(asset.Name), GSDPlatform.Instance.Constants.InstallerExtension, StringComparison.OrdinalIgnoreCase))
                {
                    path = asset.LocalPath;
                    if (assetId == GitAssetId && this.IsGitAsset(asset))
                    {
                        args = GitInstallerArgs;
                        return true;
                    }

                    if (assetId == GSDAssetId && this.IsGSDAsset(asset))
                    {
                        args = GSDInstallerArgs;
                        return true;
                    }
                }
            }

            path = null;
            args = null;
            return false;
        }

        private bool IsGSDAsset(Asset asset)
        {
            return this.AssetInstallerNameCompare(asset, GSDInstallerFileNamePrefixCandidates);
        }

        private bool IsGitAsset(Asset asset)
        {
            return this.AssetInstallerNameCompare(asset, new string[] { GitInstallerFileNamePrefix });
        }

        private bool AssetInstallerNameCompare(Asset asset, IEnumerable<string> expectedFileNamePrefixes)
        {
            foreach (string fileNamePrefix in expectedFileNamePrefixes)
            {
                if (asset.Name.StartsWith(fileNamePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void LogVersionInfo(
            Version gvfsVersion,
            GitVersion gitVersion,
            string message)
        {
            EventMetadata metadata = new EventMetadata();
            metadata.Add(nameof(gvfsVersion), gvfsVersion.ToString());
            metadata.Add(nameof(gitVersion), gitVersion.ToString());

            this.tracer.RelatedEvent(EventLevel.Informational, message, metadata);
        }

        public class GitHubUpgraderConfig
        {
            public GitHubUpgraderConfig(ITracer tracer, LocalGSDConfig localGSDConfig)
            {
                this.Tracer = tracer;
                this.LocalConfig = localGSDConfig;
            }

            public enum RingType
            {
                // The values here should be ascending.
                // Invalid - User has set an incorrect ring
                // NoConfig - User has Not set any ring yet
                // None - User has set a valid "None" ring
                // (Fast should be greater than Slow,
                //  Slow should be greater than None, None greater than Invalid.)
                // This is required for the correct implementation of Ring based
                // upgrade logic.
                Invalid = 0,
                NoConfig = None - 1,
                None = 10,
                Slow = None + 1,
                Fast = Slow + 1,
            }

            public RingType UpgradeRing { get; private set; }
            public LocalGSDConfig LocalConfig { get; private set; }
            private ITracer Tracer { get; set; }

            public bool TryLoad(out string error)
            {
                this.UpgradeRing = RingType.NoConfig;

                string ringConfig = null;
                string loadError = "Could not read GSD Config." + Environment.NewLine + GSDConstants.UpgradeVerbMessages.SetUpgradeRingCommand;

                if (!this.LocalConfig.TryGetConfig(GSDConstants.LocalGSDConfig.UpgradeRing, out ringConfig, out error))
                {
                    error = loadError;
                    return false;
                }

                this.ParseUpgradeRing(ringConfig);
                return true;
            }

            public void ParseUpgradeRing(string ringConfig)
            {
                if (string.IsNullOrEmpty(ringConfig))
                {
                    this.UpgradeRing = RingType.None;
                    return;
                }

                RingType ringType;
                if (Enum.TryParse(ringConfig, ignoreCase: true, result: out ringType) &&
                    Enum.IsDefined(typeof(RingType), ringType) &&
                    ringType != RingType.Invalid)
                {
                    this.UpgradeRing = ringType;
                }
                else
                {
                    this.UpgradeRing = RingType.Invalid;
                }
            }

            public bool ConfigError()
            {
                return this.UpgradeRing == RingType.Invalid;
            }

            public bool UpgradeAllowed(out string message)
            {
                if (this.UpgradeRing == RingType.Slow || this.UpgradeRing == RingType.Fast)
                {
                    message = null;
                    return true;
                }

                this.ConfigAlertMessage(out message);
                return false;
            }

            public void ConfigAlertMessage(out string message)
            {
                message = null;

                if (this.UpgradeRing == GitHubUpgraderConfig.RingType.None)
                {
                    message = GSDConstants.UpgradeVerbMessages.NoneRingConsoleAlert + Environment.NewLine + GSDConstants.UpgradeVerbMessages.SetUpgradeRingCommand;
                }

                if (this.UpgradeRing == GitHubUpgraderConfig.RingType.NoConfig)
                {
                    message = GSDConstants.UpgradeVerbMessages.NoRingConfigConsoleAlert + Environment.NewLine + GSDConstants.UpgradeVerbMessages.SetUpgradeRingCommand;
                }

                if (this.UpgradeRing == GitHubUpgraderConfig.RingType.Invalid)
                {
                    string ring;
                    string error;
                    string prefix = string.Empty;
                    if (this.LocalConfig.TryGetConfig(GSDConstants.LocalGSDConfig.UpgradeRing, out ring, out error))
                    {
                        prefix = $"Invalid upgrade ring `{ring}` specified in gvfs config. ";
                    }

                    message = prefix + Environment.NewLine + GSDConstants.UpgradeVerbMessages.SetUpgradeRingCommand;
                }
            }
        }

        [DataContract(Name = "asset")]
        protected class Asset
        {
            [DataMember(Name = "name")]
            public string Name { get; set; }

            [DataMember(Name = "size")]
            public long Size { get; set; }

            [DataMember(Name = "browser_download_url")]
            public Uri DownloadURL { get; set; }

            [IgnoreDataMember]
            public string LocalPath { get; set; }
        }

        [DataContract(Name = "release")]
        protected class Release
        {
            [DataMember(Name = "name")]
            public string Name { get; set; }

            [DataMember(Name = "tag_name")]
            public string Tag { get; set; }

            [DataMember(Name = "prerelease")]
            public bool PreRelease { get; set; }

            [DataMember(Name = "assets")]
            public List<Asset> Assets { get; set; }

            [IgnoreDataMember]
            public GitHubUpgraderConfig.RingType Ring
            {
                get
                {
                    return this.PreRelease == true ? GitHubUpgraderConfig.RingType.Fast : GitHubUpgraderConfig.RingType.Slow;
                }
            }

            public bool TryParseVersion(out Version version)
            {
                version = null;

                if (this.Tag.StartsWith("v", StringComparison.CurrentCultureIgnoreCase))
                {
                    return Version.TryParse(this.Tag.Substring(1), out version);
                }

                return false;
            }
        }
    }
}
