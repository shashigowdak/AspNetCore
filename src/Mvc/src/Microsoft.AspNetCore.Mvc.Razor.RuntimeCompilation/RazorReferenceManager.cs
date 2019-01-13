// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Threading;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation
{
    internal class RazorReferenceManager
    {
        private readonly ApplicationPartManager _partManager;
        private object _compilationReferencesLock = new object();
        private bool _compilationReferencesInitialized;
        private IReadOnlyList<MetadataReference> _compilationReferences;

        public RazorReferenceManager(ApplicationPartManager partManager)
        {
            _partManager = partManager;
        }

        public virtual IReadOnlyList<MetadataReference> CompilationReferences
        {
            get
            {
                return LazyInitializer.EnsureInitialized(
                    ref _compilationReferences,
                    ref _compilationReferencesInitialized,
                    ref _compilationReferencesLock,
                    GetCompilationReferences);
            }
        }

        private IReadOnlyList<MetadataReference> GetCompilationReferences()
        {
            var referencePaths = _partManager
                .ApplicationParts
                .OfType<ICompilationReferencesProvider>()
                .SelectMany(part => part.GetReferencePaths())
                .Distinct(StringComparer.OrdinalIgnoreCase);

            return referencePaths
                .Select(CreateMetadataReference)
                .ToList();
        }

        private static MetadataReference CreateMetadataReference(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                var moduleMetadata = ModuleMetadata.CreateFromStream(stream, PEStreamOptions.PrefetchMetadata);
                var assemblyMetadata = AssemblyMetadata.Create(moduleMetadata);

                return assemblyMetadata.GetReference(filePath: path);
            }
        }
    }
}
