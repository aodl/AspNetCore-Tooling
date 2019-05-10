// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.CommandLineUtils;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.Design.IntegrationTests
{
    public class StaticWebAssetssIntegrationTest : MSBuildIntegrationTestBase, IClassFixture<BuildServerTestFixture>, IClassFixture<PackageTestProjectsFixture>, IAsyncLifetime
    {
        public StaticWebAssetssIntegrationTest(
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
        public async Task Build_GeneratesStaticWebAssetssManifest_Success_CreatesManifest()
        {
            // For some reason when using a custom package cache the imports won't get added on
            // the initial restore, so we restore the packages ourselves.
            await DotnetMSBuild("Restore");

            var expectedManifest = GetExpectedManifest();

            var result = await DotnetMSBuild("Build");

            Assert.BuildPassed(result);

            // GenerateStaticWebAssetssManifest should generate the manifest and the cache.
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticWebAssetss.xml");
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticWebAssetss.cache");

            var path = Assert.FileExists(result, OutputPath, "AppWithPackageAndP2PReference.dll");
            var assembly = Assert.ContainsEmbeddedResource(path, "Microsoft.AspNetCore.StaticWebAssetss.xml");
            using (var reader = new StreamReader(assembly))
            {
                var data = await reader.ReadToEndAsync();
                Assert.Equal(expectedManifest, data);
            }
        }

        private string GetExpectedManifest()
        {
            var projectNames = PackageTestProjectsFixture.GetProjectsToPack()
                .Select(p => Path.GetFileNameWithoutExtension(p).ToLowerInvariant())
                .OrderByDescending(p => p.Length)
                .ToArray();

            var restorePath = LocalNugetPackagesCacheTempPath;
            var projects = projectNames.Select(p =>
                Path.Combine(
                    restorePath,
                    p,
                    "1.0.0",
                    p.Contains("transitive", StringComparison.OrdinalIgnoreCase) ? "buildTransitive" : "build", "..", "razorContent") + Path.DirectorySeparatorChar)
                .ToArray();

            return $@"<StaticWebAssetss Version=""1.0"">
  <ContentRoot BasePath=""_content/PackageLibraryTransitiveDependency"" Path=""{projects[0]}"" />
  <ContentRoot BasePath=""_content/PackageLibraryDirectDependency"" Path=""{projects[1]}"" />
</StaticWebAssetss>";
        }

        [Fact]
        [InitializeTestProject("SimpleMvc")]
        public async Task Build_DoesNotEmbedManifestWhen_NoStaticResourcesAvailable()
        {
            // For some reason when using a custom package cache the imports won't get added on
            // the initial restore, so we restore the packages ourselves.
            await DotnetMSBuild("Restore");

            var result = await DotnetMSBuild("Build");

            Assert.BuildPassed(result);

            // GenerateStaticWebAssetssManifest should generate the manifest and the cache.
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticWebAssetss.xml");
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticWebAssetss.cache");

            var path = Assert.FileExists(result, OutputPath, "SimpleMvc.dll");
            Assert.DoesNotContainEmbeddedResource(path, "Microsoft.AspNetCore.StaticWebAssetss.xml");
        }

        [Fact]
        [InitializeTestProject("AppWithPackageAndP2PReference")]
        public async Task Clean_Success_RemovesManifestAndCache()
        {
            // For some reason when using a custom package cache the imports won't get added on
            // the initial restore, so we restore the packages ourselves.
            await DotnetMSBuild("Restore");

            var result = await DotnetMSBuild("Build");

            Assert.BuildPassed(result);

            // GenerateStaticWebAssetssManifest should generate the manifest and the cache.
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticWebAssetss.xml");
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticWebAssetss.cache");

            var cleanResult = await DotnetMSBuild("Clean");

            Assert.BuildPassed(cleanResult);

            // Clean should delete the manifest and the cache.
            Assert.FileDoesNotExist(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticWebAssetss.cache");
            Assert.FileDoesNotExist(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticWebAssetss.xml");
        }

        [Fact]
        [InitializeTestProject("AppWithPackageAndP2PReference")]
        public async Task Rebuild_Success_RecreatesManifestAndCache()
        {
            // Arrange
            // For some reason when using a custom package cache the imports won't get added on
            // the initial restore, so we restore the packages ourselves.
            await DotnetMSBuild("Restore");

            var result = await DotnetMSBuild("Build");

            Assert.BuildPassed(result);

            // GenerateStaticWebAssetssManifest should generate the manifest and the cache.
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticWebAssetss.xml");
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticWebAssetss.cache");

            var directoryPath = Path.Combine(result.Project.DirectoryPath, IntermediateOutputPath);
            var thumbPrints = new Dictionary<string, FileThumbPrint>();
            var thumbPrintFiles = new[]
            {
                Path.Combine(directoryPath, "Microsoft.AspNetCore.StaticWebAssetss.xml"),
                Path.Combine(directoryPath, "Microsoft.AspNetCore.StaticWebAssetss.cache"),
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
            var assembly = Assert.ContainsEmbeddedResource(path, "Microsoft.AspNetCore.StaticWebAssetss.xml");
            using (var reader = new StreamReader(assembly))
            {
                var data = reader.ReadToEnd();
                Assert.Equal(GetExpectedManifest(), data);
            }
        }

        [Fact]
        [InitializeTestProject("AppWithPackageAndP2PReference")]
        public async Task GenerateStaticWebAssetssManifest_IncrementalBuild_ReusesManifest()
        {
            // Arrange
            // For some reason when using a custom package cache the imports won't get added on
            // the initial restore, so we restore the packages ourselves.
            await DotnetMSBuild("Restore");

            var result = await DotnetMSBuild("GenerateStaticWebAssetssManifest");

            Assert.BuildPassed(result);

            // GenerateStaticWebAssetssManifest should generate the manifest and the cache.
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticWebAssetss.xml");
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticWebAssetss.cache");

            var directoryPath = Path.Combine(result.Project.DirectoryPath, IntermediateOutputPath);
            var thumbPrints = new Dictionary<string, FileThumbPrint>();
            var thumbPrintFiles = new[]
            {
                Path.Combine(directoryPath, "Microsoft.AspNetCore.StaticWebAssetss.xml"),
                Path.Combine(directoryPath, "Microsoft.AspNetCore.StaticWebAssetss.cache"),
            };

            foreach (var file in thumbPrintFiles)
            {
                var thumbprint = GetThumbPrint(file);
                thumbPrints[file] = thumbprint;
            }

            // Act
            var incremental = await DotnetMSBuild("GenerateStaticWebAssetssManifest");

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
                    Arguments = "pack -c Debug",
#else
                    Arguments = "pack -c Release",
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
