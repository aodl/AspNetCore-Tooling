// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class GenerateAspNetCoreStaticAssetsManifestTest
    {
        [Fact]
        public void ReturnsError_WhenBasePathIsMissing()
        {
            // Arrange
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new GenerateAspNetCoreStaticAssetsManifest
            {
                BuildEngine = buildEngine.Object,
                ContentRootDefinitions = new TaskItem[]{
                    CreateItem(@"wwwroot\sample.js", new Dictionary<string,string>{
                        ["ContentRoot"] = "/"
                    })
                }
            };

            // Act
            var result = task.Execute();

            // Assert
            Assert.False(result);
            var message = Assert.Single(errorMessages);
            Assert.Equal(@"Missing required metadata 'BasePath' for 'wwwroot\sample.js'.", message);
        }

        [Fact]
        public void ReturnsError_WhenContentRootIsMissing()
        {
            // Arrange
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new GenerateAspNetCoreStaticAssetsManifest
            {
                BuildEngine = buildEngine.Object,
                ContentRootDefinitions = new TaskItem[]{
                    CreateItem(@"wwwroot\sample.js", new Dictionary<string,string>{
                        ["BasePath"] = "MyLibrary"
                    })
                }
            };

            // Act
            var result = task.Execute();

            // Assert
            Assert.False(result);
            var message = Assert.Single(errorMessages);
            Assert.Equal(@"Missing required metadata 'ContentRoot' for 'wwwroot\sample.js'.", message);
        }

        [Fact]
        public void ReturnsError_ForDuplicateBasePaths()
        {
            // Arrange
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new GenerateAspNetCoreStaticAssetsManifest
            {
                BuildEngine = buildEngine.Object,
                ContentRootDefinitions = new TaskItem[]{
                    CreateItem(@"wwwroot\sample.js", new Dictionary<string,string>{
                        ["BasePath"] = "MyLibrary",
                        ["ContentRoot"] = @"c:\nuget\MyLibrary"
                    }),
                    CreateItem(@"wwwroot\otherLib.js", new Dictionary<string,string>{
                        ["BasePath"] = "MyLibrary",
                        ["ContentRoot"] = @"c:\nuget\MyOtherLibrary"
                    })
                }
            };

            // Act
            var result = task.Execute();

            // Assert
            Assert.False(result);
            var message = Assert.Single(errorMessages);
            Assert.Equal(
                @"Duplicate base paths 'MyLibrary' for content root paths 'c:\nuget\MyOtherLibrary' and 'c:\nuget\MyLibrary'. ('wwwroot\otherLib.js', 'wwwroot\sample.js')",
                message);
        }

        [Fact]
        public void ReturnsError_ForDuplicateContentRoots()
        {
            // Arrange
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new GenerateAspNetCoreStaticAssetsManifest
            {
                BuildEngine = buildEngine.Object,
                ContentRootDefinitions = new TaskItem[]{
                    CreateItem(@"wwwroot\sample.js", new Dictionary<string,string>{
                        ["BasePath"] = "MyLibrary",
                        ["ContentRoot"] = @"./MyLibrary"
                    }),
                    CreateItem(@"wwwroot\otherLib.js", new Dictionary<string,string>{
                        ["BasePath"] = "MyOtherLibrary",
                        ["ContentRoot"] = @"./MyLibrary"
                    })
                }
            };

            // Act
            var result = task.Execute();

            // Assert
            Assert.False(result);
            var message = Assert.Single(errorMessages);
            Assert.Equal(
                @"Duplicate content root paths './MyLibrary' for base paths 'MyOtherLibrary' and 'MyLibrary' ('wwwroot\otherLib.js', 'wwwroot\sample.js')",
                message);
        }

        [Fact]
        public void Generates_EmptyManifest_WhenNoItems_Passed()
        {
            // Arrange
            var file = Path.GetTempFileName();
            var expectedDocument = @"<AspNetCoreStaticAssets Version=""1.0"" />";

            try
            {
                var buildEngine = new Mock<IBuildEngine>();

                var task = new GenerateAspNetCoreStaticAssetsManifest
                {
                    BuildEngine = buildEngine.Object,
                    ContentRootDefinitions = new TaskItem[] { },
                    TargetManifestPath = file
                };

                // Act
                var result = task.Execute();

                // Assert
                Assert.True(result);
                var document = File.ReadAllText(file);
                Assert.Equal(expectedDocument, document);
            }
            finally
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }

        [Fact]
        public void Generates_Manifest_WhenContentRootsAvailable()
        {
            // Arrange
            var file = Path.GetTempFileName();
            var expectedDocument = @"<AspNetCoreStaticAssets Version=""1.0"">
  <ContentRoot BasePath=""MyLibrary"" Path=""c:/nuget/MyLibrary/razorContent"" />
</AspNetCoreStaticAssets>";

            try
            {
                var buildEngine = new Mock<IBuildEngine>();

                var task = new GenerateAspNetCoreStaticAssetsManifest
                {
                    BuildEngine = buildEngine.Object,
                    ContentRootDefinitions = new TaskItem[] {
                        CreateItem(@"wwwroot\sample.js", new Dictionary<string,string>{
                            ["BasePath"] = "MyLibrary",
                            ["ContentRoot"] = @"c:/nuget/MyLibrary/razorContent"
                        }),
                    },
                    TargetManifestPath = file
                };

                // Act
                var result = task.Execute();

                // Assert
                Assert.True(result);
                var document = File.ReadAllText(file);
                Assert.Equal(expectedDocument, document);
            }
            finally
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }

        [Fact]
        public void SkipsAdditionalElements_WithSameBasePathAndSameContentRoot()
        {
            // Arrange
            var file = Path.GetTempFileName();
            var expectedDocument = @"<AspNetCoreStaticAssets Version=""1.0"">
  <ContentRoot BasePath=""MyLibrary"" Path=""c:/nuget/MyLibrary/razorContent"" />
</AspNetCoreStaticAssets>";

            try
            {
                var buildEngine = new Mock<IBuildEngine>();

                var task = new GenerateAspNetCoreStaticAssetsManifest
                {
                    BuildEngine = buildEngine.Object,
                    ContentRootDefinitions = new TaskItem[] {
                        CreateItem(@"wwwroot\sample.js", new Dictionary<string,string>{
                            ["BasePath"] = "MyLibrary",
                            ["ContentRoot"] = @"c:/nuget/MyLibrary/razorContent"
                        }),
                        // Comparisons are case insensitive
                        CreateItem(@"wwwroot\site.css", new Dictionary<string,string>{
                            ["BasePath"] = "MyLIBRARY",
                            ["ContentRoot"] = @"c:/nuget/MyLIBRARY/razorContent"
                        }),
                    },
                    TargetManifestPath = file
                };

                // Act
                var result = task.Execute();

                // Assert
                Assert.True(result);
                var document = File.ReadAllText(file);
                Assert.Equal(expectedDocument, document);
            }
            finally
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }

        private static TaskItem CreateItem(
            string spec,
            IDictionary<string,string> metadata)
        {
            var result = new TaskItem(spec);
            foreach (var (key,value) in metadata)
            {
                result.SetMetadata(key, value);
            }

            return result;
        }
    }
}
