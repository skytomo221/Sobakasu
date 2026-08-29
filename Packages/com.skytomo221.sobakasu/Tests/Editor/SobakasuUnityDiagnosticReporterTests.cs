using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using DiagnosticItem = Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class SobakasuUnityDiagnosticReporterTests
    {
        private const string FallbackPath = "Assets/Scripts/Door.sobakasu";
        private string _folderPath;

        [SetUp]
        public void SetUp()
        {
            var folderGuid = AssetDatabase.CreateFolder(
                "Assets",
                $"SobakasuDiagnosticReporterTests_{Guid.NewGuid():N}");
            _folderPath = AssetDatabase.GUIDToAssetPath(folderGuid);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrWhiteSpace(_folderPath) &&
                AssetDatabase.IsValidFolder(_folderPath))
            {
                AssetDatabase.DeleteAsset(_folderPath);
            }

            AssetDatabase.Refresh();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Report_EmitsOneLogPerDiagnosticWithSeverityMapping()
        {
            const string source = "first\nsecond\nthird";
            var diagnostics = new[]
            {
                new DiagnosticItem(
                    DiagnosticSeverity.Error,
                    "SBK1001",
                    new TextSpan(0, 1),
                    "error message",
                    "error hint"),
                new DiagnosticItem(
                    DiagnosticSeverity.Warning,
                    "SBK1002",
                    new TextSpan(6, 1),
                    "warning message"),
                new DiagnosticItem(
                    DiagnosticSeverity.Info,
                    "SBK1003",
                    new TextSpan(13, 1),
                    "info message")
            };

            LogAssert.Expect(
                LogType.Error,
                CreateExpectedMessage(
                    FallbackPath,
                    1,
                    1,
                    "error SBK1001: error message\nhint: error hint"));
            LogAssert.Expect(
                LogType.Warning,
                CreateExpectedMessage(
                    FallbackPath,
                    2,
                    1,
                    "warning SBK1002: warning message"));
            LogAssert.Expect(
                LogType.Log,
                CreateExpectedMessage(
                    FallbackPath,
                    3,
                    1,
                    "info SBK1003: info message"));

            SobakasuUnityDiagnosticReporter.Report(
                null,
                FallbackPath,
                source,
                diagnostics);
        }

        [TestCase(0, 1, 1)]
        [TestCase(6, 2, 1)]
        [TestCase(8, 2, 3)]
        public void FormatMessage_ComputesOneOriginLineAndColumn(
            int spanStart,
            int expectedLine,
            int expectedColumn)
        {
            var diagnostic = new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2001",
                new TextSpan(spanStart, 1),
                "message");

            var message = SobakasuUnityDiagnosticReporter.FormatMessage(
                null,
                FallbackPath,
                "alpha\nbeta",
                diagnostic);

            Assert.That(
                message,
                Is.EqualTo(CreateExpectedMessage(
                    FallbackPath,
                    expectedLine,
                    expectedColumn,
                    "error SBK2001: message")));
        }

        [Test]
        public void FormatMessage_OmitsBlankCodeAndHintWithoutExtraPunctuation()
        {
            var diagnostic = new DiagnosticItem(
                DiagnosticSeverity.Error,
                "",
                new TextSpan(0, 0),
                "message",
                "  ");

            var message = SobakasuUnityDiagnosticReporter.FormatMessage(
                null,
                FallbackPath,
                string.Empty,
                diagnostic);

            Assert.That(
                message,
                Is.EqualTo(CreateExpectedMessage(
                    FallbackPath,
                    1,
                    1,
                    "error: message")));
            Assert.That(message, Does.Not.Contain("hint:"));
        }

        [Test]
        public void FormatMessage_PrefersDiagnosticSourcePathAndItsSourceText()
        {
            var sourcePath = ImportTextAsset("Dependency.sobakasu.txt", "first\nsecond");
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var absolutePath = Path.GetFullPath(Path.Combine(projectRoot, sourcePath));
            var diagnostic = new DiagnosticItem(
                DiagnosticSeverity.Warning,
                "SBK3001",
                new TextSpan(8, 1),
                "dependency warning",
                sourcePath: absolutePath);

            var message = SobakasuUnityDiagnosticReporter.FormatMessage(
                null,
                FallbackPath,
                "entry",
                diagnostic);

            Assert.That(
                message,
                Is.EqualTo(CreateExpectedMessage(
                    sourcePath,
                    2,
                    3,
                    "warning SBK3001: dependency warning")));
        }

        [Test]
        public void TryResolveHyperlink_ResolvesAssetAndPosition()
        {
            var sourcePath = ImportTextAsset("Linked.sobakasu.txt", "source");
            var expectedAsset = AssetDatabase.LoadMainAssetAtPath(sourcePath);
            var hyperlinkData = new Dictionary<string, string>
            {
                [SobakasuUnityDiagnosticReporter.PathAttribute] = sourcePath,
                [SobakasuUnityDiagnosticReporter.LineAttribute] = "12",
                [SobakasuUnityDiagnosticReporter.ColumnAttribute] = "8"
            };

            var resolved = SobakasuUnityDiagnosticReporter.TryResolveHyperlink(
                hyperlinkData,
                out var sourceAsset,
                out var line,
                out var column);

            Assert.That(resolved, Is.True);
            Assert.That(sourceAsset, Is.SameAs(expectedAsset));
            Assert.That(line, Is.EqualTo(12));
            Assert.That(column, Is.EqualTo(8));
        }

        [Test]
        public void TryResolveHyperlink_IgnoresUnrelatedLink()
        {
            var hyperlinkData = new Dictionary<string, string>
            {
                ["href"] = "https://example.com"
            };

            var resolved = SobakasuUnityDiagnosticReporter.TryResolveHyperlink(
                hyperlinkData,
                out var sourceAsset,
                out var line,
                out var column);

            Assert.That(resolved, Is.False);
            Assert.That(sourceAsset, Is.Null);
            Assert.That(line, Is.Zero);
            Assert.That(column, Is.Zero);
        }

        private string ImportTextAsset(string fileName, string sourceText)
        {
            var sourcePath = $"{_folderPath}/{fileName}";
            SobakasuTestAssetFactory.WriteSource(sourcePath, sourceText);
            AssetDatabase.ImportAsset(
                sourcePath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            return sourcePath;
        }

        private static string CreateExpectedMessage(
            string path,
            int line,
            int column,
            string diagnosticText)
        {
            return $"<a {SobakasuUnityDiagnosticReporter.PathAttribute}=\"{path}\" " +
                $"{SobakasuUnityDiagnosticReporter.LineAttribute}=\"{line}\" " +
                $"{SobakasuUnityDiagnosticReporter.ColumnAttribute}=\"{column}\">" +
                $"{path}:{line}:{column}</a>: {diagnosticText}";
        }
    }
}
