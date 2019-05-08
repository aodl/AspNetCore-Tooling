// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class GenerateAspNetCoreStaticAssetsManifest : Task
    {
        private const string ContentRoot = nameof(ContentRoot);
        private const string BasePath = nameof(BasePath);
        
        [Required]
        public string TargetManifestPath { get; set; }

        [Required]
        public ITaskItem[] ContentRootDefinitions { get; set; }

        public override bool Execute()
        {
            if(!ValidateArguments())
            {
                return false;
            }

            return ExecuteCore();
        }

        private bool ExecuteCore()
        {
            var document = new XDocument(new XDeclaration("1.0", "utf-8", "yes"));
            var root = new XElement(
                "AspNetCoreStaticAssets",
                new XAttribute("Version", "1.0"),
                CreateNodes());

            document.Add(root);

            var settings = new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                CloseOutput = true,
                OmitXmlDeclaration = true,
                Indent = true,
                NewLineOnAttributes = false,
                Async = true
            };

            using (var xmlWriter = GetXmlWritter(settings))
            {
                document.WriteTo(xmlWriter);
            }

            return true;
        }

        private IEnumerable<XElement> CreateNodes()
        {
            var nodes = new List<XElement>();
            for (var i = 0; i < ContentRootDefinitions.Length; i++)
            {
                var contentRootDefinition = ContentRootDefinitions[i];
                var basePath = contentRootDefinition.GetMetadata(BasePath);
                var contentRoot = contentRootDefinition.GetMetadata(ContentRoot);
                
                nodes.Add(new XElement("ContentRoot",
                    new XAttribute("BasePath", basePath),
                    new XAttribute("Path", contentRoot)));
            }

            return nodes;
        }

        private XmlWriter GetXmlWritter(XmlWriterSettings settings)
        {
            var fileStream = new FileStream(TargetManifestPath, FileMode.Create);
            return XmlWriter.Create(fileStream, settings);
        }

        private bool ValidateArguments()
        {
            for (var i = 0; i < ContentRootDefinitions.Length; i++)
            {
                var contentRootDefinition = ContentRootDefinitions[i];
                if (!EnsureRequiredMetadata(contentRootDefinition, BasePath) ||
                    !EnsureRequiredMetadata(contentRootDefinition, ContentRoot))
                {
                    return false;
                }
            }

            var basePaths = new Dictionary<string, ITaskItem>();
            var contentRootPaths = new Dictionary<string, ITaskItem>();

            for (var i = 0; i < ContentRootDefinitions.Length; i++)
            {
                var contentRootDefinition = ContentRootDefinitions[i];
                var basePath = contentRootDefinition.GetMetadata(BasePath);
                var contentRoot = contentRootDefinition.GetMetadata(ContentRoot);
                
                if(basePaths.TryGetValue(basePath, out var existingBasePath))
                {
                    Log.LogError($"Duplicate base paths '{basePath}' for content root paths '{contentRoot}' and '{existingBasePath.GetMetadata(ContentRoot)}'");
                    return false;
                }

                if(contentRootPaths.TryGetValue(contentRoot, out var existingContentRoot))
                {
                    Log.LogError($"Duplicate content root paths '{contentRoot}' for base paths '{basePath}' and '{existingContentRoot.GetMetadata(BasePath)}'");
                    return false;
                }

                basePaths.Add(basePath, contentRootDefinition);
                contentRootPaths.Add(contentRoot, contentRootDefinition);
            }

            return true;
        }

        private bool EnsureRequiredMetadata(ITaskItem item, string metadataName)
        {
            var value = item.GetMetadata(metadataName);
            if (string.IsNullOrEmpty(value))
            {
                Log.LogError($"Missing required metadata '{metadataName}' for '{item.ItemSpec}.");
                return false;
            }
    
            return true;
        }
    }
}