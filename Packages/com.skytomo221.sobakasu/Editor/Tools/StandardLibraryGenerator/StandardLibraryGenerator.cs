using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Skytomo221.Sobakasu.Tools.StandardLibraryGenerator
{
    internal sealed class StandardLibraryGenerationResult
    {
        public IReadOnlyDictionary<string, byte[]> Files { get; }
        public UdonApiGenerationReport Report { get; }
        public string OutputDirectory { get; }
        public string AdditionsDirectory { get; }
        public string DiagnosticsDirectory { get; }

        public StandardLibraryGenerationResult(
            IReadOnlyDictionary<string, byte[]> files,
            UdonApiGenerationReport report,
            string outputDirectory,
            string additionsDirectory,
            string diagnosticsDirectory)
        {
            Files = files ?? throw new ArgumentNullException(nameof(files));
            Report = report ?? throw new ArgumentNullException(nameof(report));
            OutputDirectory = outputDirectory ?? throw new ArgumentNullException(nameof(outputDirectory));
            AdditionsDirectory = additionsDirectory ?? throw new ArgumentNullException(nameof(additionsDirectory));
            DiagnosticsDirectory = diagnosticsDirectory ?? string.Empty;
        }
    }

    internal sealed class StandardLibraryGenerator
    {
        public const string PackageName = "com.skytomo221.sobakasu";
        public const string StandardLibraryDirectoryName = "StandardLibrary~";
        public const string AdditionsDirectoryName = "StandardLibraryAdditions~";
        public const string DiagnosticsDirectoryName = "StandardLibraryGenerationReports~";
        public const string ConfigurationFileName = "standard-library-generation-config.json";

        private readonly Func<UdonBindingGenerationResult> _generateBindings;
        private readonly string _packageRoot;

        public static string PackageRoot => StandardLibraryPaths.ResolvePackageRoot();
        public static string DefaultOutputDirectory => Path.Combine(
            PackageRoot,
            StandardLibraryDirectoryName);
        public static string DefaultAdditionsDirectory => Path.Combine(
            PackageRoot,
            AdditionsDirectoryName);
        public static string DefaultDiagnosticsDirectory => Path.Combine(
            PackageRoot,
            DiagnosticsDirectoryName);
        public static string DefaultConfigurationPath => Path.Combine(
            PackageRoot,
            "Editor",
            "Tools",
            "StandardLibraryGenerator",
            ConfigurationFileName);

        internal StandardLibraryGenerator(
            Func<UdonBindingGenerationResult> generateBindings,
            string packageRoot)
        {
            _generateBindings = generateBindings ??
                throw new ArgumentNullException(nameof(generateBindings));
            _packageRoot = StandardLibraryPaths.ValidatePackageRoot(packageRoot);
        }

        public static StandardLibraryGenerator CreateDefault(string configurationPath = null)
        {
            var packageRoot = StandardLibraryPaths.ResolvePackageRoot();
            var resolvedConfigurationPath = string.IsNullOrWhiteSpace(configurationPath)
                ? Path.Combine(
                    packageRoot,
                    "Editor",
                    "Tools",
                    "StandardLibraryGenerator",
                    ConfigurationFileName)
                : Path.GetFullPath(configurationPath);
            var bindingGenerator = UdonBindingGenerator.CreateDefault(resolvedConfigurationPath);
            return new StandardLibraryGenerator(bindingGenerator.Generate, packageRoot);
        }

        public StandardLibraryGenerationResult Generate()
        {
            return GenerateToDirectory(
                Path.Combine(_packageRoot, StandardLibraryDirectoryName),
                Path.Combine(_packageRoot, AdditionsDirectoryName),
                Path.Combine(_packageRoot, DiagnosticsDirectoryName));
        }

        internal StandardLibraryGenerationResult GenerateToDirectory(
            string outputDirectory,
            string additionsDirectory,
            string diagnosticsDirectory = null)
        {
            var paths = StandardLibraryPathSafety.Validate(
                _packageRoot,
                outputDirectory,
                additionsDirectory,
                diagnosticsDirectory);
            if (!Directory.Exists(paths.AdditionsDirectory))
            {
                throw new DirectoryNotFoundException(
                    $"The standard-library additions directory does not exist: " +
                    $"'{paths.AdditionsDirectory}'.");
            }

            var bindings = _generateBindings();
            var files = StandardLibraryComposer.Compose(
                bindings.Files,
                paths.AdditionsDirectory);
            var diagnostics = StandardLibraryComposer.EncodeTextFiles(bindings.Diagnostics);

            if (!string.IsNullOrEmpty(paths.DiagnosticsDirectory))
            {
                StandardLibraryDirectoryTransaction.Rebuild(
                    paths.DiagnosticsDirectory,
                    diagnostics);
            }
            StandardLibraryDirectoryTransaction.Rebuild(paths.OutputDirectory, files);

            return new StandardLibraryGenerationResult(
                files,
                bindings.Report,
                paths.OutputDirectory,
                paths.AdditionsDirectory,
                paths.DiagnosticsDirectory);
        }
    }

    internal static class StandardLibraryPaths
    {
        public static string ResolvePackageRoot()
        {
            try
            {
                var package = PackageInfo.FindForAssembly(typeof(StandardLibraryGenerator).Assembly);
                if (package != null && IsPackageRoot(package.resolvedPath))
                    return Path.GetFullPath(package.resolvedPath);
            }
            catch (Exception)
            {
                // Fall through to stable project and assembly-location probes.
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var embeddedPackage = Path.Combine(
                projectRoot,
                "Packages",
                StandardLibraryGenerator.PackageName);
            if (IsPackageRoot(embeddedPackage))
                return Path.GetFullPath(embeddedPackage);

            var assemblyDirectory = Path.GetDirectoryName(
                typeof(StandardLibraryGenerator).Assembly.Location);
            var discovered = FindPackageRootFrom(assemblyDirectory);
            if (discovered != null)
                return discovered;

            throw new DirectoryNotFoundException(
                $"Could not locate package '{StandardLibraryGenerator.PackageName}'.");
        }

        internal static string ValidatePackageRoot(string packageRoot)
        {
            if (string.IsNullOrWhiteSpace(packageRoot))
                throw new ArgumentException("A package root is required.", nameof(packageRoot));

            var fullPath = Path.GetFullPath(packageRoot);
            if (!IsPackageRoot(fullPath))
            {
                throw new DirectoryNotFoundException(
                    $"The Sobakasu package root is invalid: '{fullPath}'.");
            }
            return fullPath;
        }

        private static string FindPackageRootFrom(string startDirectory)
        {
            if (string.IsNullOrWhiteSpace(startDirectory))
                return null;

            var current = new DirectoryInfo(Path.GetFullPath(startDirectory));
            while (current != null)
            {
                if (IsPackageRoot(current.FullName))
                    return current.FullName;

                var embeddedPackage = Path.Combine(
                    current.FullName,
                    "Packages",
                    StandardLibraryGenerator.PackageName);
                if (IsPackageRoot(embeddedPackage))
                    return Path.GetFullPath(embeddedPackage);
                current = current.Parent;
            }
            return null;
        }

        private static bool IsPackageRoot(string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate) || !Directory.Exists(candidate))
                return false;
            var manifestPath = Path.Combine(candidate, "package.json");
            if (!File.Exists(manifestPath))
                return false;

            try
            {
                var manifest = JsonUtility.FromJson<PackageManifest>(
                    File.ReadAllText(manifestPath, Encoding.UTF8));
                return string.Equals(
                    manifest?.name,
                    StandardLibraryGenerator.PackageName,
                    StringComparison.Ordinal);
            }
            catch (Exception)
            {
                return false;
            }
        }

        [Serializable]
        private sealed class PackageManifest
        {
            public string name;
        }
    }

    internal sealed class StandardLibraryGenerationPaths
    {
        public string OutputDirectory { get; }
        public string AdditionsDirectory { get; }
        public string DiagnosticsDirectory { get; }

        public StandardLibraryGenerationPaths(
            string outputDirectory,
            string additionsDirectory,
            string diagnosticsDirectory)
        {
            OutputDirectory = outputDirectory;
            AdditionsDirectory = additionsDirectory;
            DiagnosticsDirectory = diagnosticsDirectory ?? string.Empty;
        }
    }

    internal static class StandardLibraryPathSafety
    {
        public static StandardLibraryGenerationPaths Validate(
            string packageRoot,
            string outputDirectory,
            string additionsDirectory,
            string diagnosticsDirectory)
        {
            packageRoot = Canonicalize(packageRoot, nameof(packageRoot));
            var output = Canonicalize(outputDirectory, nameof(outputDirectory));
            var additions = Canonicalize(additionsDirectory, nameof(additionsDirectory));
            var diagnostics = string.IsNullOrWhiteSpace(diagnosticsDirectory)
                ? string.Empty
                : Canonicalize(diagnosticsDirectory, nameof(diagnosticsDirectory));
            var defaultOutput = Canonicalize(
                Path.Combine(packageRoot, StandardLibraryGenerator.StandardLibraryDirectoryName),
                nameof(outputDirectory));
            var defaultDiagnostics = Canonicalize(
                Path.Combine(packageRoot, StandardLibraryGenerator.DiagnosticsDirectoryName),
                nameof(diagnosticsDirectory));

            ValidateReplaceTarget(packageRoot, output, defaultOutput, "output");
            if (PathsOverlap(output, additions))
            {
                throw new InvalidOperationException(
                    "The output and additions directories must not overlap.");
            }

            if (!string.IsNullOrEmpty(diagnostics))
            {
                ValidateReplaceTarget(
                    packageRoot,
                    diagnostics,
                    defaultDiagnostics,
                    "diagnostics");
                if (PathsOverlap(diagnostics, output) || PathsOverlap(diagnostics, additions))
                {
                    throw new InvalidOperationException(
                        "The diagnostics directory must not overlap output or additions.");
                }
            }

            return new StandardLibraryGenerationPaths(output, additions, diagnostics);
        }

        internal static bool IsSameOrDescendant(string parent, string candidate)
        {
            var parentPath = Canonicalize(parent, nameof(parent));
            var candidatePath = Canonicalize(candidate, nameof(candidate));
            if (PathsEqual(parentPath, candidatePath))
                return true;

            var prefix = parentPath.EndsWith(
                Path.DirectorySeparatorChar.ToString(),
                StringComparison.Ordinal)
                ? parentPath
                : parentPath + Path.DirectorySeparatorChar;
            return candidatePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidateReplaceTarget(
            string packageRoot,
            string target,
            string allowedPackageTarget,
            string label)
        {
            if (File.Exists(target))
                throw new IOException($"The {label} path is a file: '{target}'.");

            var fileSystemRoot = Path.GetPathRoot(target);
            if (PathsEqual(target, fileSystemRoot))
            {
                throw new InvalidOperationException(
                    $"The filesystem root cannot be used as the {label} directory.");
            }

            if (IsSameOrDescendant(target, packageRoot))
            {
                throw new InvalidOperationException(
                    $"The {label} directory cannot contain the Sobakasu package root.");
            }

            var repositoryRoot = TryGetRepositoryRoot(packageRoot);
            if (!string.IsNullOrEmpty(repositoryRoot) &&
                IsSameOrDescendant(repositoryRoot, target) &&
                !PathsEqual(target, allowedPackageTarget))
            {
                throw new InvalidOperationException(
                    $"The {label} directory cannot replace repository content: '{target}'.");
            }

            if (IsSameOrDescendant(packageRoot, target) &&
                !PathsEqual(target, allowedPackageTarget))
            {
                throw new InvalidOperationException(
                    $"The {label} directory cannot replace package content: '{target}'.");
            }
        }

        private static string TryGetRepositoryRoot(string packageRoot)
        {
            var packageParent = Directory.GetParent(packageRoot);
            if (packageParent == null || !string.Equals(
                    packageParent.Name,
                    "Packages",
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            return packageParent.Parent?.FullName;
        }

        private static bool PathsOverlap(string left, string right)
        {
            return IsSameOrDescendant(left, right) || IsSameOrDescendant(right, left);
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
                return false;
            return string.Equals(
                Canonicalize(left, nameof(left)),
                Canonicalize(right, nameof(right)),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string Canonicalize(string path, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A directory path is required.", parameterName);

            var fullPath = Path.GetFullPath(path);
            var root = Path.GetPathRoot(fullPath);
            if (string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
                return root;
            return fullPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
        }
    }

    internal static class StandardLibraryComposer
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new(false, true);

        public static IReadOnlyDictionary<string, byte[]> Compose(
            IReadOnlyDictionary<string, string> generatedFiles,
            string additionsDirectory)
        {
            if (generatedFiles == null)
                throw new ArgumentNullException(nameof(generatedFiles));
            if (string.IsNullOrWhiteSpace(additionsDirectory))
            {
                throw new ArgumentException(
                    "An additions directory is required.",
                    nameof(additionsDirectory));
            }

            var additionsRoot = Path.GetFullPath(additionsDirectory);
            var files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            var canonicalNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var generatedNames = new List<string>(generatedFiles.Keys);
            generatedNames.Sort(StringComparer.Ordinal);
            foreach (var sourceName in generatedNames)
            {
                var relativePath = NormalizeRelativePath(sourceName);
                AddCanonicalName(canonicalNames, relativePath);
                files.Add(
                    relativePath,
                    IsSobakasuSource(relativePath)
                        ? EncodeSobakasuSource(generatedFiles[sourceName])
                        : Utf8WithoutBom.GetBytes(generatedFiles[sourceName] ?? string.Empty));
            }

            var additionFiles = new List<string>(Directory.GetFiles(
                additionsRoot,
                "*",
                SearchOption.AllDirectories));
            additionFiles.Sort(StringComparer.Ordinal);
            foreach (var additionPath in additionFiles)
            {
                var relativePath = GetRelativePath(additionsRoot, additionPath);
                if (canonicalNames.TryGetValue(relativePath, out var generatedPath))
                {
                    if (!string.Equals(relativePath, generatedPath, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Case-insensitive standard-library path collision: " +
                            $"'{generatedPath}' and '{relativePath}'.");
                    }
                    if (!IsSobakasuSource(relativePath))
                    {
                        throw new InvalidOperationException(
                            $"Generated and manually-authored non-.sobakasu files collide: " +
                            $"'{relativePath}'.");
                    }

                    var generatedSource = Utf8WithoutBom.GetString(files[relativePath]);
                    var additionSource = Utf8WithoutBom.GetString(File.ReadAllBytes(additionPath));
                    files[relativePath] = EncodeSobakasuSource(
                        ComposeSobakasuSources(generatedSource, additionSource));
                    continue;
                }

                AddCanonicalName(canonicalNames, relativePath);
                files.Add(
                    relativePath,
                    IsSobakasuSource(relativePath)
                        ? EncodeSobakasuSource(
                            Utf8WithoutBom.GetString(File.ReadAllBytes(additionPath)))
                        : File.ReadAllBytes(additionPath));
            }
            return files;
        }

        public static IReadOnlyDictionary<string, byte[]> EncodeTextFiles(
            IReadOnlyDictionary<string, string> textFiles)
        {
            if (textFiles == null)
                throw new ArgumentNullException(nameof(textFiles));

            var files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            var canonicalNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var names = new List<string>(textFiles.Keys);
            names.Sort(StringComparer.Ordinal);
            foreach (var sourceName in names)
            {
                var relativePath = NormalizeRelativePath(sourceName);
                AddCanonicalName(canonicalNames, relativePath);
                files.Add(relativePath, EncodeText(textFiles[sourceName]));
            }
            return files;
        }

        internal static string ComposeSobakasuSources(
            string generatedSource,
            string additionSource)
        {
            var generated = NormalizeBody(generatedSource);
            var addition = NormalizeBody(additionSource);
            if (generated.Length == 0)
                return addition + "\n";
            if (addition.Length == 0)
                return generated + "\n";
            return generated + "\n\n" + addition + "\n";
        }

        internal static string NormalizeRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
                throw new InvalidOperationException($"Invalid generated path: '{relativePath}'.");

            var normalized = relativePath.Replace('\\', '/');
            var segments = normalized.Split('/');
            foreach (var segment in segments)
            {
                if (string.IsNullOrEmpty(segment) || segment == "." || segment == ".." ||
                    segment.IndexOf(':') >= 0)
                {
                    throw new InvalidOperationException($"Invalid generated path: '{relativePath}'.");
                }
            }
            return string.Join("/", segments);
        }

        private static string GetRelativePath(string root, string filePath)
        {
            var normalizedRoot = Path.GetFullPath(root).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var normalizedFile = Path.GetFullPath(filePath);
            if (!normalizedFile.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"An addition escaped the additions directory: '{normalizedFile}'.");
            }
            return NormalizeRelativePath(normalizedFile[normalizedRoot.Length..]);
        }

        private static byte[] EncodeSobakasuSource(string source)
        {
            return Utf8WithoutBom.GetBytes(NormalizeBody(source) + "\n");
        }

        private static byte[] EncodeText(string source)
        {
            return Utf8WithoutBom.GetBytes(NormalizeBody(source) + "\n");
        }

        private static string NormalizeBody(string source)
        {
            return (source ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Trim('\n');
        }

        private static bool IsSobakasuSource(string relativePath)
        {
            return relativePath.EndsWith(".sobakasu", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddCanonicalName(
            IDictionary<string, string> canonicalNames,
            string relativePath)
        {
            if (canonicalNames.TryGetValue(relativePath, out var existing))
            {
                throw new InvalidOperationException(
                    $"Case-insensitive standard-library path collision: " +
                    $"'{existing}' and '{relativePath}'.");
            }
            canonicalNames.Add(relativePath, relativePath);
        }
    }

    internal static class StandardLibraryDirectoryTransaction
    {
        public static void Rebuild(
            string outputDirectory,
            IReadOnlyDictionary<string, byte[]> files)
        {
            if (files == null)
                throw new ArgumentNullException(nameof(files));

            var output = Path.GetFullPath(outputDirectory);
            var parent = Path.GetDirectoryName(output);
            if (string.IsNullOrEmpty(parent))
                throw new InvalidOperationException($"Invalid output directory: '{output}'.");
            Directory.CreateDirectory(parent);

            var name = Path.GetFileName(output);
            var transactionId = Guid.NewGuid().ToString("N");
            var staging = Path.Combine(parent, $".{name}.staging-{transactionId}");
            var backup = Path.Combine(parent, $".{name}.backup-{transactionId}");
            var existingMoved = false;
            try
            {
                WriteStagingDirectory(staging, files);
                if (Directory.Exists(output))
                {
                    MoveDirectoryWithRetry(output, backup);
                    existingMoved = true;
                }
                MoveDirectoryWithRetry(staging, output);
            }
            catch (Exception replacementException)
            {
                try
                {
                    if (existingMoved)
                    {
                        if (Directory.Exists(output))
                            Directory.Delete(output, true);
                        if (Directory.Exists(backup))
                            MoveDirectoryWithRetry(backup, output);
                    }
                }
                catch (Exception restoreException)
                {
                    throw new AggregateException(
                        "Standard-library replacement failed and the previous output could not be restored.",
                        replacementException,
                        restoreException);
                }
                throw;
            }
            finally
            {
                if (Directory.Exists(staging))
                    Directory.Delete(staging, true);
            }

            if (Directory.Exists(backup))
            {
                try
                {
                    Directory.Delete(backup, true);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"The previous generated directory could not be removed: " +
                        $"'{backup}'. {exception.Message}");
                }
            }
        }

        private static void MoveDirectoryWithRetry(string source, string destination)
        {
            const int attemptCount = 20;
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    Directory.Move(source, destination);
                    return;
                }
                catch (IOException) when (attempt + 1 < attemptCount)
                {
                    Thread.Sleep((attempt + 1) * 25);
                }
                catch (UnauthorizedAccessException) when (attempt + 1 < attemptCount)
                {
                    Thread.Sleep((attempt + 1) * 25);
                }
            }
        }

        private static void WriteStagingDirectory(
            string stagingDirectory,
            IReadOnlyDictionary<string, byte[]> files)
        {
            Directory.CreateDirectory(stagingDirectory);
            foreach (var pair in files)
            {
                var relativePath = StandardLibraryComposer.NormalizeRelativePath(pair.Key)
                    .Replace('/', Path.DirectorySeparatorChar);
                var filePath = Path.GetFullPath(Path.Combine(stagingDirectory, relativePath));
                if (!StandardLibraryPathSafety.IsSameOrDescendant(stagingDirectory, filePath) ||
                    string.Equals(
                        Path.GetFullPath(stagingDirectory),
                        filePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"A generated file escaped the staging directory: '{pair.Key}'.");
                }

                var parent = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(parent))
                    Directory.CreateDirectory(parent);
                using var stream = new FileStream(
                    filePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None);
                var content = pair.Value ?? Array.Empty<byte>();
                stream.Write(content, 0, content.Length);
            }
        }
    }

    public static class StandardLibraryGeneratorCommandLine
    {
        public static void Generate()
        {
            var configurationPath = GetArgument(
                "-standardLibraryConfig",
                "--config",
                "-udonApiStubConfig");
            var generator = StandardLibraryGenerator.CreateDefault(configurationPath);
            var outputDirectory = GetArgument(
                "-standardLibraryOutput",
                "--output",
                "-udonApiStubOutput") ?? StandardLibraryGenerator.DefaultOutputDirectory;
            var additionsDirectory = GetArgument(
                "-standardLibraryAdditions",
                "--additions") ?? StandardLibraryGenerator.DefaultAdditionsDirectory;
            var diagnosticsDirectory = GetArgument(
                "-standardLibraryDiagnostics",
                "--diagnostics") ?? StandardLibraryGenerator.DefaultDiagnosticsDirectory;
            var result = generator.GenerateToDirectory(
                outputDirectory,
                additionsDirectory,
                diagnosticsDirectory);

            Debug.Log(
                $"Sobakasu StandardLibrary~ generated at '{result.OutputDirectory}'.\n" +
                $"Files: {result.Files.Count}; " +
                $"types: {result.Report.types_generated}/{result.Report.types_discovered}; " +
                $"Udon API coverage: {result.Report.udon_signatures_covered}/" +
                $"{result.Report.udon_signatures_exposed} " +
                $"({result.Report.udon_api_coverage_percent:F2}%).");
        }

        private static string GetArgument(params string[] names)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index + 1 < arguments.Length; index++)
            {
                foreach (var name in names)
                {
                    if (string.Equals(arguments[index], name, StringComparison.Ordinal))
                        return arguments[index + 1];
                }
            }
            return null;
        }
    }
}
