using _build;
using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Coverlet;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.MinVer;
using Nuke.Common.Tools.ReportGenerator;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.ReportGenerator.ReportGeneratorTasks;

class Build : NukeBuild
{
    public static int Main () => Execute<Build>(x => x.Test);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Required] [Solution] readonly Solution Solution;
    [MinVer(Framework = "net7.0")] readonly MinVer MinVer;
    [Required] [GitRepository] readonly GitRepository GitRepository;
    
    static AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    static AbsolutePath PackagesDirectory => ArtifactsDirectory / "packages";
    static AbsolutePath CoverageReportDirectory => ArtifactsDirectory / "coverage-report";
    static AbsolutePath TestResultDirectory => ArtifactsDirectory / "test-results";
    static AbsolutePath CoverletResultDirectory => TestResultDirectory / "coverlet";
    static AbsolutePath JunitResultDirectory => TestResultDirectory / "junit";
    
    [Parameter] readonly string NugetApiUrl = "https://api.nuget.org/v3/index.json";
    [Parameter] readonly string NugetApiKey;
    
    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            ArtifactsDirectory.CreateOrCleanDirectory();
        });

    Target Restore => _ => _
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
                .SetAssemblyVersion(MinVer.AssemblyVersion)
                .SetFileVersion(MinVer.FileVersion)
                .SetInformationalVersion(MinVer.Version));
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var testProjects = Solution.GetAllProjects("*Tests");
            
            
            DotNetTest(_ => _
                .SetConfiguration(Configuration)
                .SetNoBuild(InvokedTargets.Contains(Compile))
                .SetNoRestore(InvokedTargets.Contains(Compile))
                .ResetVerbosity()
                .SetResultsDirectory(TestResultDirectory)
                .EnableCollectCoverage()
                .SetCoverletOutputFormat(CoverletOutputFormat.cobertura)
                .SetExcludeByFile("*.Generated.cs")
                .CombineWith(testProjects, (_, v) => _
                    .SetProjectFile(v)
                    .SetLoggers(
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
                .SetVersion(MinVer.PackageVersion));
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
                .SetFramework("net7.0"));
        });
}
