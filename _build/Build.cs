using System.Collections.Generic;
using _build;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Coverlet;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Tools.ReportGenerator;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.ReportGenerator.ReportGeneratorTasks;

[CheckBuildProjectConfigurations]
[UnsetVisualStudioEnvironmentVariables]
class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Required] [Solution] readonly Solution Solution;
    [Required] [GitVersion(Framework = "net5.0", NoFetch = true)] readonly GitVersion GitVersion;
    [Required] [GitRepository] readonly GitRepository GitRepository;

    static AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    static AbsolutePath TestResultDirectory => ArtifactsDirectory / "test-results";
    static AbsolutePath PackagesDirectory => ArtifactsDirectory / "packages";
    static AbsolutePath CoverletResultDirectory => TestResultDirectory / "coverlet";
    static AbsolutePath JunitResultDirectory => TestResultDirectory / "junit";
    IEnumerable<Project> TestProjects => Solution.GetProjects("*Tests");
    static AbsolutePath CoverageReportDirectory => ArtifactsDirectory / "coverage-report";

    [Parameter] readonly string NugetApiUrl = "https://api.nuget.org/v3/index.json";
    [Parameter] readonly string NugetApiKey;

    Target Clean => _ => _
        .Executes(() =>
        {
            EnsureCleanDirectory(ArtifactsDirectory);
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore(_ => _
                .SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(_ => _
                .SetNoRestore(InvokedTargets.Contains(Restore))
                .SetRepositoryUrl(GitRepository.HttpsUrl)
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.AssemblySemFileVer)
                .SetInformationalVersion(GitVersion.InformationalVersion));
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTest(_ => _
                .SetConfiguration(Configuration)
                .SetNoBuild(InvokedTargets.Contains(Compile))
                .SetNoRestore(InvokedTargets.Contains(Compile))
                .ResetVerbosity()
                .SetResultsDirectory(TestResultDirectory)
                .EnableCollectCoverage()
                .SetCoverletOutputFormat(CoverletOutputFormat.cobertura)
                .SetExcludeByFile("*.Generated.cs")
                .CombineWith(TestProjects, (_, v) => _
                    .SetProjectFile(v)
                    .SetLogger(
                        $"junit;LogFilePath={JunitResultDirectory}/{v.Name}.xml;MethodFormat=Class;FailureBodyFormat=Verbose")
                    .SetCoverletOutput($"{CoverletResultDirectory}/{v.Name}.xml")));
        });

    Target Pack => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetPack(_ => _
                .SetProject(Solution)
                .SetNoBuild(InvokedTargets.Contains(Compile))
                .SetNoRestore(InvokedTargets.Contains(Compile))
                .SetRepositoryUrl(GitRepository.HttpsUrl)
                .SetConfiguration(Configuration)
                .SetOutputDirectory(PackagesDirectory)
                .SetVersion(GitVersion.NuGetVersionV2));
        });

    Target Publish => _ => _
        .DependsOn(Pack)
        .Executes(() =>
        {
            void PushPackages(string pattern)
            {
                DotNetNuGetPush(_ => _
                    .SetSource(NugetApiUrl)
                    .SetApiKey(NugetApiKey)
                    .SetSkipDuplicate(true)
                    .CombineWith(PackagesDirectory.GlobFiles(pattern), (_, v) => _
                        .SetTargetPath(v)));
            }

            PushPackages("*.nupkg");
            PushPackages("*.snupkg");
        });

    Target Coverage => _ => _
        .DependsOn(Test)
        .TriggeredBy(Test)
        .Consumes(Test)
        .Produces(CoverageReportDirectory)
        .Executes(() =>
        {
            ReportGenerator(_ => _
                .SetReports(CoverletResultDirectory / "*.xml")
                .SetReportTypes(ReportTypes.HtmlInline_AzurePipelines, ReportTypes.Badges)
                .SetTargetDirectory(CoverageReportDirectory)
                .SetFramework("netcoreapp2.1"));
        });
}
