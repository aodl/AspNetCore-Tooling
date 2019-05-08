﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Design.IntegrationTests
{
    public class StaticAssetsIntegrationTest : MSBuildIntegrationTestBase, IClassFixture<BuildServerTestFixture>
    {
        public StaticAssetsIntegrationTest(BuildServerTestFixture buildServer)
            : base(buildServer)
        {
        }

        [Fact]
        [InitializeTestProject("AppWithPackageAndP2PReference")]
        public async Task Build_GeneratesAspNetCoreStaticAssetsManifest_Success_CreatesManifest()
        {
            var result = await DotnetMSBuild("Build");

            Assert.BuildPassed(result);

            // GenerateAspNetCoreStaticAssetsManifest should generate the manifest and the cache.
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticAssets.xml");
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticAssets.cache");

            var path = Assert.FileExists(result, OutputPath, "AppWithPackageAndP2PReference.dll");
            var assembly = Assert.ContainsEmbeddedResource(path, "Microsoft.AspNetCore.StaticAssets.xml");
            using (var reader = new StreamReader(assembly))
            {
                var data = XDocument.Parse(reader.ReadToEnd());
                Assert.Equal("AspNetCoreStaticAssets", data.Root.Name);
                Assert.Equal(2, data.Root.Descendants().Count());
            }
        }

        [Fact]
        [InitializeTestProject("SimpleMvc")]
        public async Task Build_DoesNotEmbedManifestWhen_NoStaticResourcesAvailable()
        {
            var result = await DotnetMSBuild("Build");

            Assert.BuildPassed(result);

            // GenerateAspNetCoreStaticAssetsManifest should generate the manifest and the cache.
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticAssets.xml");
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticAssets.cache");

            var path = Assert.FileExists(result, OutputPath, "SimpleMvc.dll");
            Assert.DoesNotContainEmbeddedResource(path, "Microsoft.AspNetCore.StaticAssets.xml");
        }

        [Fact]
        [InitializeTestProject("AppWithPackageAndP2PReference")]
        public async Task Clean_Success_RemovesManifestAndCache()
        {
            var result = await DotnetMSBuild("Build");

            Assert.BuildPassed(result);

            // GenerateAspNetCoreStaticAssetsManifest should generate the manifest and the cache.
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticAssets.xml");
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticAssets.cache");

            var cleanResult = await DotnetMSBuild("Clean");

            Assert.BuildPassed(cleanResult);

            // Clean should delete the manifest and the cache.
            Assert.FileDoesNotExist(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticAssets.cache");
            Assert.FileDoesNotExist(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticAssets.xml");
        }

        [Fact]
        [InitializeTestProject("AppWithPackageAndP2PReference")]
        public async Task Rebuild_Success_RecreatesManifestAndCache()
        {
            // Arrange
            var result = await DotnetMSBuild("Build");

            Assert.BuildPassed(result);

            // GenerateAspNetCoreStaticAssetsManifest should generate the manifest and the cache.
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticAssets.xml");
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticAssets.cache");

            var directoryPath = Path.Combine(result.Project.DirectoryPath, IntermediateOutputPath);
            var thumbPrints = new Dictionary<string, FileThumbPrint>();
            var thumbPrintFiles = new[]
            {
                Path.Combine(directoryPath, "Microsoft.AspNetCore.StaticAssets.xml"),
                Path.Combine(directoryPath, "Microsoft.AspNetCore.StaticAssets.cache"),
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
        }

        [Fact]
        [InitializeTestProject("AppWithPackageAndP2PReference")]
        public async Task GenerateAspNetCoreStaticAssetsManifest_IncrementalBuild_ReusesManifest()
        {
            // Arrange
            var result = await DotnetMSBuild("GenerateAspNetCoreStaticAssetsManifest");

            Assert.BuildPassed(result);

            // GenerateAspNetCoreStaticAssetsManifest should generate the manifest and the cache.
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticAssets.xml");
            Assert.FileExists(result, IntermediateOutputPath, "Microsoft.AspNetCore.StaticAssets.cache");

            var directoryPath = Path.Combine(result.Project.DirectoryPath, IntermediateOutputPath);
            var thumbPrints = new Dictionary<string, FileThumbPrint>();
            var thumbPrintFiles = new[]
            {
                Path.Combine(directoryPath, "Microsoft.AspNetCore.StaticAssets.xml"),
                Path.Combine(directoryPath, "Microsoft.AspNetCore.StaticAssets.cache"),
            };

            foreach (var file in thumbPrintFiles)
            {
                var thumbprint = GetThumbPrint(file);
                thumbPrints[file] = thumbprint;
            }

            // Act
            var incremental = await DotnetMSBuild("GenerateAspNetCoreStaticAssetsManifest");

            // Assert
            Assert.BuildPassed(incremental);

            foreach (var file in thumbPrintFiles)
            {
                var thumbprint = GetThumbPrint(file);
                Assert.Equal(thumbPrints[file], thumbprint);
            }
        }
    }
}
