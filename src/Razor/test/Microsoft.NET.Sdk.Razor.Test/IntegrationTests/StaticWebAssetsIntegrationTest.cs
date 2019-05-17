// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.Design.IntegrationTests
{
    public class StaticWebAssetsIntegrationTest : MSBuildIntegrationTestBase, IClassFixture<BuildServerTestFixture>, IClassFixture<PackageTestProjectsFixture>, IAsyncLifetime
    {
        public StaticWebAssetsIntegrationTest(
            BuildServerTestFixture buildServer,
            PackageTestProjectsFixture packageTestProjects,
            ITestOutputHelper output)
            : base(buildServer)
        {
            UseLocalPackageCache = true;
            PackageTestProjects = packageTestProjects;
            Output = output;
        }

        public PackageTestProjectsFixture PackageTestProjects { get; private set; }

        public ITestOutputHelper Output { get; private set; }

        [Fact]
        [InitializeTestProject("AppWithPackageAndP2PReference")]
        public async Task Build_GeneratesStaticWebAssetsManifest_Success_CreatesManifest()
        {
            var result = await DotnetMSBuild("Build", "/restore");

            var expectedManifest = GetExpectedManifest();

            Assert.BuildPassed(result);

            // GenerateStaticWebAssetsManifest should generate the manifest and the cache.
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticWebAssets.xml");
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticWebAssets.cache");

            var path = Assert.FileExists(result, OutputPath, "AppWithPackageAndP2PReference.dll");
            var assembly = Assert.ContainsEmbeddedResource(path, "Microsoft.AspNetCore.StaticWebAssets.xml");
            using (var reader = new StreamReader(assembly))
            {
                var data = await reader.ReadToEndAsync();
                Assert.Equal(expectedManifest, data);
            }
        }

        [Fact]
        [InitializeTestProject("AppWithPackageAndP2PReference")]
        public async Task Publish_CopiesStaticWebAssetsToDestinationFolder()
        {
            var result = await DotnetMSBuild("Publish", "/restore");

            Assert.BuildPassed(result);

            // GenerateStaticWebAssetsManifest should generate the manifest and the cache.
            Assert.FileExists(result, PublishOutputPath, Path.Combine("wwwroot", "_content", "PackageLibraryDirectDependency", "css", "site.css"));
            Assert.FileExists(result, PublishOutputPath, Path.Combine("wwwroot", "_content", "PackageLibraryDirectDependency", "js", "pkg-direct-dep.js"));
            Assert.FileExists(result, PublishOutputPath, Path.Combine("wwwroot", "_content", "PackageLibraryTransitiveDependency", "js", "pkg-transitive-dep.js"));
        }

        [Fact]
        [InitializeTestProject("AppWithPackageAndP2PReference")]
        public async Task Publish_Incremental_DoesNotCopyAnyFiles()
        {
            var result = await DotnetMSBuild("Publish", "/restore");

            Assert.BuildPassed(result);

            // GenerateStaticWebAssetsManifest should generate the manifest and the cache.
            Assert.FileExists(result, PublishOutputPath, Path.Combine("wwwroot", "_content", "PackageLibraryDirectDependency", "css", "site.css"));
            Assert.FileExists(result, PublishOutputPath, Path.Combine("wwwroot", "_content", "PackageLibraryDirectDependency", "js", "pkg-direct-dep.js"));
            Assert.FileExists(result, PublishOutputPath, Path.Combine("wwwroot", "_content", "PackageLibraryTransitiveDependency", "js", "pkg-transitive-dep.js"));
            var thumbPrints = new Dictionary<string, FileThumbPrint>();
            var thumbPrintFiles = new[]
            {
                Path.Combine(PublishOutputPath, "wwwroot", "_content", "PackageLibraryDirectDependency", "css", "site.css"),
                Path.Combine(PublishOutputPath, "wwwroot", "_content", "PackageLibraryDirectDependency", "js", "pkg-direct-dep.js"),
                Path.Combine(PublishOutputPath, "wwwroot", "_content", "PackageLibraryTransitiveDependency", "js", "pkg-transitive-dep.js"),
            };

            foreach (var file in thumbPrintFiles)
            {
                var thumbprint = GetThumbPrint(file);
                thumbPrints[file] = thumbprint;
            }

            // Act
            var incremental = await DotnetMSBuild("Publish");

            // Assert
            Assert.BuildPassed(incremental);

            foreach (var file in thumbPrintFiles)
            {
                var thumbprint = GetThumbPrint(file);
                Assert.Equal(thumbPrints[file], thumbprint);
            }
        }

        [Fact]
        [InitializeTestProject("SimpleMvc")]
        public async Task Build_DoesNotEmbedManifestWhen_NoStaticResourcesAvailable()
        {
            var result = await DotnetMSBuild("Build", "/restore");

            Assert.BuildPassed(result);

            // GenerateStaticWebAssetsManifest should generate the manifest and the cache.
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticWebAssets.xml");
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticWebAssets.cache");

            var path = Assert.FileExists(result, OutputPath, "SimpleMvc.dll");
            Assert.DoesNotContainEmbeddedResource(path, "Microsoft.AspNetCore.StaticWebAssets.xml");
        }

        [Fact]
        [InitializeTestProject("AppWithPackageAndP2PReference")]
        public async Task Clean_Success_RemovesManifestAndCache()
        {
            var result = await DotnetMSBuild("Build", "/restore");

            Assert.BuildPassed(result);

            // GenerateStaticWebAssetsManifest should generate the manifest and the cache.
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticWebAssets.xml");
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticWebAssets.cache");

            var cleanResult = await DotnetMSBuild("Clean");

            Assert.BuildPassed(cleanResult);

            // Clean should delete the manifest and the cache.
            Assert.FileDoesNotExist(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticWebAssets.cache");
            Assert.FileDoesNotExist(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticWebAssets.xml");
        }

        [Fact]
        [InitializeTestProject("AppWithPackageAndP2PReference")]
        public async Task Rebuild_Success_RecreatesManifestAndCache()
        {
            // Arrange
            var result = await DotnetMSBuild("Build", "/restore");

            var expectedManifest = GetExpectedManifest();

            Assert.BuildPassed(result);

            // GenerateStaticWebAssetsManifest should generate the manifest and the cache.
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticWebAssets.xml");
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticWebAssets.cache");

            var directoryPath = Path.Combine(result.Project.DirectoryPath, IntermediateOutputPath);
            var thumbPrints = new Dictionary<string, FileThumbPrint>();
            var thumbPrintFiles = new[]
            {
                Path.Combine(directoryPath, "Microsoft.AspNetCore.StaticWebAssets.xml"),
                Path.Combine(directoryPath, "Microsoft.AspNetCore.StaticWebAssets.cache"),
            };

            foreach (var file in thumbPrintFiles)
            {
                var thumbprint = GetThumbPrint(file);
                thumbPrints[file] = thumbprint;
            }

            // Act
            var rebuild = await DotnetMSBuild("Rebuild");

            // Assert
            Assert.BuildPassed(rebuild);

            foreach (var file in thumbPrintFiles)
            {
                var thumbprint = GetThumbPrint(file);
                Assert.NotEqual(thumbPrints[file], thumbprint);
            }

            var path = Assert.FileExists(result, OutputPath, "AppWithPackageAndP2PReference.dll");
            var assembly = Assert.ContainsEmbeddedResource(path, "Microsoft.AspNetCore.StaticWebAssets.xml");
            using (var reader = new StreamReader(assembly))
            {
                var data = reader.ReadToEnd();
                Assert.Equal(expectedManifest, data);
            }
        }

        [Fact]
        [InitializeTestProject("AppWithPackageAndP2PReference")]
        public async Task GenerateStaticWebAssetsManifest_IncrementalBuild_ReusesManifest()
        {
            var result = await DotnetMSBuild("GenerateStaticWebAssetsManifest", "/restore");

            Assert.BuildPassed(result);

            // GenerateStaticWebAssetsManifest should generate the manifest and the cache.
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticWebAssets.xml");
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticWebAssets.cache");

            var directoryPath = Path.Combine(result.Project.DirectoryPath, IntermediateOutputPath);
            var thumbPrints = new Dictionary<string, FileThumbPrint>();
            var thumbPrintFiles = new[]
            {
                Path.Combine(directoryPath, "Microsoft.AspNetCore.StaticWebAssets.xml"),
                Path.Combine(directoryPath, "Microsoft.AspNetCore.StaticWebAssets.cache"),
            };

            foreach (var file in thumbPrintFiles)
            {
                var thumbprint = GetThumbPrint(file);
                thumbPrints[file] = thumbprint;
            }

            // Act
            var incremental = await DotnetMSBuild("GenerateStaticWebAssetsManifest");

            // Assert
            Assert.BuildPassed(incremental);

            foreach (var file in thumbPrintFiles)
            {
                var thumbprint = GetThumbPrint(file);
                Assert.Equal(thumbPrints[file], thumbprint);
            }
        }

        public Task InitializeAsync()
        {
            return PackageTestProjects.PackAsync(Output);
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        private string GetExpectedManifest()
        {
            var restorePath = LocalNugetPackagesCacheTempPath;
            var projects = new[]
            {
                Path.Combine(restorePath, "packagelibrarytransitivedependency", "1.0.0", "buildTransitive", "..", "razorContent") + Path.DirectorySeparatorChar,
                Path.Combine(restorePath, "packagelibrarydirectdependency", "1.0.0", "build", "..", "razorContent") + Path.DirectorySeparatorChar
            };

            return $@"<StaticWebAssets Version=""1.0"">
  <ContentRoot BasePath=""_content/PackageLibraryTransitiveDependency"" Path=""{projects[0]}"" />
  <ContentRoot BasePath=""_content/PackageLibraryDirectDependency"" Path=""{projects[1]}"" />
</StaticWebAssets>";
        }
    }

    public class PackageTestProjectsFixture
    {
        private bool _packed;

        internal async Task PackAsync(ITestOutputHelper output)
        {
            if (_packed)
            {
                return;
            }

            var projectsToPack = GetProjectsToPack();

            foreach (var project in projectsToPack)
            {
                output.WriteLine(project);
            }

            foreach (var project in projectsToPack)
            {
                var psi = new ProcessStartInfo
                {
                    FileName = DotNetMuxer.MuxerPathOrDefault(),
#if DEBUG
                    Arguments = "msbuild /t:Restore;Pack /p:Configuration=Debug",
#else
                    Arguments = "msbuild /t:Restore;Pack /p:Configuration=Release",
#endif
                    WorkingDirectory = project,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                var tcs = new TaskCompletionSource<Process>();
                var process = Process.Start(psi);
                process.Exited += (s, a) =>
                {
                    tcs.SetResult(process);
                };
                process.EnableRaisingEvents = true;

                await Task.WhenAny(Task.Delay(TimeSpan.FromMinutes(2)), tcs.Task);

                // Wait for 10 seconds or fail. If we take longer
                // it likely means we are adding unexpected dependencies.
                Assert.True(process.HasExited, "Process did not finish running.");
                output.WriteLine(process.StandardOutput.ReadToEnd());
                Assert.Equal(0, process.ExitCode);
            }

            _packed = true;
        }

        public static string[] GetProjectsToPack()
        {
            return typeof(PackageTestProjectsFixture).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Where(a => a.Key == "Testing.ProjectToPack")
                .Select(a => a.Value)
                .ToArray();
        }
    }
}
