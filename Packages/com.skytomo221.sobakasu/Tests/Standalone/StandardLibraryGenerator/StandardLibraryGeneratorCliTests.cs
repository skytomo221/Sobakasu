using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Skytomo221.Sobakasu.Tests.Standalone
{
    [NonParallelizable]
    public sealed class StandardLibraryGeneratorCliTests
    {
        [Test]
        public void FullGeneration_MatchesCheckedInStandardLibrary()
        {
            var repositoryRoot = FindRepositoryRoot();
            var packageRoot = GetPackageRoot(repositoryRoot);
            var tempRoot = CreateTempDirectory();
            try
            {
                var generated = Path.Combine(tempRoot, "generated");
                var result = RunCli(repositoryRoot,
                    "--output", generated,
                    "--diagnostics", Path.Combine(tempRoot, "diagnostics"));
                Assert.That(result.ExitCode, Is.Zero, result.DiagnosticText);
                var checkedIn = Path.Combine(packageRoot, "StandardLibrary~");
                string[] Files(string root) => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
                    .OrderBy(path => path, StringComparer.Ordinal).ToArray();
                Assert.That(Files(generated), Is.EqualTo(Files(checkedIn)), "Generated file set differs.");
                foreach (var relativePath in Files(generated))
                {
                    string Read(string root) => File.ReadAllText(Path.Combine(root, relativePath))
                        .Replace("\r\n", "\n").Replace('\r', '\n');
                    Assert.That(Read(generated), Is.EqualTo(Read(checkedIn)), relativePath);
                }
            }
            finally
            {
                DeleteDirectory(tempRoot);
            }
        }

        [TestCase("--unknown", "value")]
        [TestCase("--catalog")]
        [TestCase("--catalog", "one", "--catalog", "two")]
        public void InvalidArguments_ReturnFailure(params string[] arguments)
        {
            var repositoryRoot = FindRepositoryRoot();
            var result = RunCli(repositoryRoot, arguments);

            Assert.That(result.ExitCode, Is.EqualTo(1), result.DiagnosticText);
            Assert.That(result.StandardError, Is.Not.Empty);
        }

        [Test]
        public void MissingCatalog_ReturnsFailure()
        {
            var repositoryRoot = FindRepositoryRoot();
            var packageRoot = GetPackageRoot(repositoryRoot);
            var tempRoot = CreateTempDirectory();
            try
            {
                Directory.CreateDirectory(Path.Combine(tempRoot, "additions"));
                var result = RunCli(
                    repositoryRoot,
                    "--project", tempRoot,
                    "--catalog", "missing-catalog.json",
                    "--config", GetConfigurationPath(packageRoot),
                    "--output", "generated",
                    "--additions", "additions",
                    "--diagnostics", "diagnostics");

                Assert.That(result.ExitCode, Is.EqualTo(1), result.DiagnosticText);
                Assert.That(result.StandardError, Does.Contain("Udon API catalog was not found"));
            }
            finally
            {
                DeleteDirectory(tempRoot);
            }
        }

        [Test]
        public void InvalidCatalog_ReturnsFailure()
        {
            var repositoryRoot = FindRepositoryRoot();
            var packageRoot = GetPackageRoot(repositoryRoot);
            var tempRoot = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(tempRoot, "catalog.json"), "{");
                Directory.CreateDirectory(Path.Combine(tempRoot, "additions"));
                var result = RunCli(
                    repositoryRoot,
                    "--project", tempRoot,
                    "--catalog", "catalog.json",
                    "--config", GetConfigurationPath(packageRoot),
                    "--output", "generated",
                    "--additions", "additions",
                    "--diagnostics", "diagnostics");

                Assert.That(result.ExitCode, Is.EqualTo(1), result.DiagnosticText);
                Assert.That(result.StandardError, Is.Not.Empty);
            }
            finally
            {
                DeleteDirectory(tempRoot);
            }
        }

        [Test]
        public void InvalidConfiguration_ReturnsFailure()
        {
            var repositoryRoot = FindRepositoryRoot();
            var packageRoot = GetPackageRoot(repositoryRoot);
            var tempRoot = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(tempRoot, "config.json"), "{}");
                Directory.CreateDirectory(Path.Combine(tempRoot, "additions"));
                var result = RunCli(
                    repositoryRoot,
                    "--project", tempRoot,
                    "--catalog", GetCatalogPath(packageRoot),
                    "--config", "config.json",
                    "--output", "generated",
                    "--additions", "additions",
                    "--diagnostics", "diagnostics");

                Assert.That(result.ExitCode, Is.EqualTo(1), result.DiagnosticText);
                Assert.That(result.StandardError, Does.Contain("Invalid Udon binding configuration"));
            }
            finally
            {
                DeleteDirectory(tempRoot);
            }
        }

        [Test]
        public void MissingAdditionsDirectory_ReturnsFailure()
        {
            var repositoryRoot = FindRepositoryRoot();
            var packageRoot = GetPackageRoot(repositoryRoot);
            var tempRoot = CreateTempDirectory();
            try
            {
                var result = RunCli(
                    repositoryRoot,
                    "--project", tempRoot,
                    "--catalog", GetCatalogPath(packageRoot),
                    "--config", GetConfigurationPath(packageRoot),
                    "--output", "generated",
                    "--additions", "missing-additions",
                    "--diagnostics", "diagnostics");

                Assert.That(result.ExitCode, Is.EqualTo(1), result.DiagnosticText);
                Assert.That(result.StandardError, Does.Contain("additions directory does not exist"));
            }
            finally
            {
                DeleteDirectory(tempRoot);
            }
        }

        [Test]
        public void RelativePathArguments_GenerateIntoTemporaryProject()
        {
            var repositoryRoot = FindRepositoryRoot();
            var packageRoot = GetPackageRoot(repositoryRoot);
            var tempRoot = CreateTempDirectory();
            try
            {
                var inputDirectory = Path.Combine(tempRoot, "input");
                Directory.CreateDirectory(inputDirectory);
                Directory.CreateDirectory(Path.Combine(tempRoot, "additions"));
                File.Copy(GetCatalogPath(packageRoot), Path.Combine(inputDirectory, "catalog.json"));
                File.Copy(GetConfigurationPath(packageRoot), Path.Combine(inputDirectory, "config.json"));

                var result = RunCli(
                    repositoryRoot,
                    "--project", tempRoot,
                    "--catalog", Path.Combine("input", "catalog.json"),
                    "--config", Path.Combine("input", "config.json"),
                    "--output", "generated",
                    "--additions", "additions",
                    "--diagnostics", "diagnostics");

                Assert.That(result.ExitCode, Is.EqualTo(0), result.DiagnosticText);
                Assert.That(
                    File.Exists(Path.Combine(tempRoot, "diagnostics", "generation_report.json")),
                    Is.True);
                Assert.That(
                    Directory.GetFiles(
                        Path.Combine(tempRoot, "generated"),
                        "*.library.sobakasu",
                        SearchOption.AllDirectories).Any(),
                    Is.True);
            }
            finally
            {
                DeleteDirectory(tempRoot);
            }
        }

        private static CliResult RunCli(string repositoryRoot, params string[] arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = repositoryRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("run");
            startInfo.ArgumentList.Add("--no-build");
            startInfo.ArgumentList.Add("--project");
            startInfo.ArgumentList.Add(Path.Combine(
                repositoryRoot,
                "Sobakasu.StandardLibraryGenerator.Standalone.csproj"));
            startInfo.ArgumentList.Add("--");
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo) ??
                throw new InvalidOperationException("Failed to start the standalone Standard Library Generator CLI.");
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(120000))
            {
                process.Kill(entireProcessTree: true);
                Assert.Fail("The standalone Standard Library Generator CLI timed out.");
            }

            return new CliResult(
                process.ExitCode,
                standardOutput.GetAwaiter().GetResult(),
                standardError.GetAwaiter().GetResult());
        }

        private static string FindRepositoryRoot()
        {
            var current = TryFindRepositoryRoot(Directory.GetCurrentDirectory());
            if (current != null)
                return current;

            current = TryFindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
            if (current != null)
                return current;

            throw new DirectoryNotFoundException("Could not locate the Sobakasu repository root.");
        }

        private static string TryFindRepositoryRoot(string startDirectory)
        {
            if (string.IsNullOrWhiteSpace(startDirectory))
                return null;

            var current = new DirectoryInfo(Path.GetFullPath(startDirectory));
            while (current != null)
            {
                if (File.Exists(Path.Combine(
                        current.FullName,
                        "Sobakasu.StandardLibraryGenerator.Standalone.csproj")))
                {
                    return current.FullName;
                }
                current = current.Parent;
            }
            return null;
        }

        private static string GetPackageRoot(string repositoryRoot) =>
            Path.Combine(repositoryRoot, "Packages", "com.skytomo221.sobakasu");

        private static string GetCatalogPath(string packageRoot) =>
            Path.Combine(packageRoot, "UdonApiCatalog~", "udon-api-catalog.json");

        private static string GetConfigurationPath(string packageRoot) =>
            Path.Combine(
                packageRoot,
                "Editor",
                "Tools",
                "StandardLibraryGenerator",
                "standard-library-generation-config.json");

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(
                Path.GetTempPath(),
                "sobakasu-stdlib-cli-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteDirectory(string path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }

        private sealed class CliResult
        {
            public int ExitCode { get; }
            public string StandardOutput { get; }
            public string StandardError { get; }
            public string DiagnosticText =>
                $"Exit code: {ExitCode}{Environment.NewLine}" +
                $"stdout:{Environment.NewLine}{StandardOutput}{Environment.NewLine}" +
                $"stderr:{Environment.NewLine}{StandardError}";

            public CliResult(int exitCode, string standardOutput, string standardError)
            {
                ExitCode = exitCode;
                StandardOutput = standardOutput ?? string.Empty;
                StandardError = standardError ?? string.Empty;
            }
        }
    }
}
