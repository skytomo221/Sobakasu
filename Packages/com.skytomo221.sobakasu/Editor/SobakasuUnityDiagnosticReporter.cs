#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Text;
using UnityEditor;
using UnityEngine;
using DiagnosticItem = Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic;
using Object = UnityEngine.Object;

namespace Skytomo221.Sobakasu
{
    [InitializeOnLoad]
    internal static class SobakasuUnityDiagnosticReporter
    {
        internal const string PathAttribute = "sobakasu-path";
        internal const string LineAttribute = "line";
        internal const string ColumnAttribute = "column";

        static SobakasuUnityDiagnosticReporter()
        {
            EditorGUI.hyperLinkClicked += OnHyperLinkClicked;
        }

        public static void Report(
            Object fallbackSourceAsset,
            string fallbackSourcePath,
            string fallbackSourceText,
            IReadOnlyList<DiagnosticItem> diagnostics)
        {
            if (diagnostics == null)
                return;

            foreach (var diagnostic in diagnostics)
            {
                var location = ResolveLocation(
                    fallbackSourceAsset,
                    fallbackSourcePath,
                    fallbackSourceText,
                    diagnostic);
                var message = FormatMessage(diagnostic, location);
                var context = location.SourceAsset ?? fallbackSourceAsset;

                switch (diagnostic.Severity)
                {
                    case DiagnosticSeverity.Error:
                        Debug.LogError(message, context);
                        break;
                    case DiagnosticSeverity.Warning:
                        Debug.LogWarning(message, context);
                        break;
                    case DiagnosticSeverity.Info:
                    default:
                        Debug.Log(message, context);
                        break;
                }
            }
        }

        internal static string FormatMessage(
            Object fallbackSourceAsset,
            string fallbackSourcePath,
            string fallbackSourceText,
            in DiagnosticItem diagnostic)
        {
            var location = ResolveLocation(
                fallbackSourceAsset,
                fallbackSourcePath,
                fallbackSourceText,
                diagnostic);
            return FormatMessage(diagnostic, location);
        }

        internal static bool TryResolveHyperlink(
            IReadOnlyDictionary<string, string> hyperlinkData,
            out Object sourceAsset,
            out int line,
            out int column)
        {
            sourceAsset = null;
            line = 0;
            column = 0;

            if (hyperlinkData == null ||
                !hyperlinkData.TryGetValue(PathAttribute, out var sourcePath) ||
                string.IsNullOrWhiteSpace(sourcePath) ||
                !hyperlinkData.TryGetValue(LineAttribute, out var lineText) ||
                !hyperlinkData.TryGetValue(ColumnAttribute, out var columnText) ||
                !int.TryParse(
                    lineText,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out line) ||
                !int.TryParse(
                    columnText,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out column) ||
                line < 1 ||
                column < 1)
            {
                return false;
            }

            var assetPath = TryGetProjectRelativePath(sourcePath);
            if (string.IsNullOrEmpty(assetPath))
                return false;

            sourceAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            return sourceAsset != null;
        }

        private static void OnHyperLinkClicked(
            EditorWindow window,
            HyperLinkClickedEventArgs args)
        {
            if (!TryResolveHyperlink(
                    args?.hyperLinkData,
                    out var sourceAsset,
                    out var line,
                    out var column))
            {
                return;
            }

            AssetDatabase.OpenAsset(sourceAsset, line, column);
        }

        private static string FormatMessage(
            in DiagnosticItem diagnostic,
            in SourceLocation location)
        {
            var builder = new StringBuilder();
            var lineText = location.Line.ToString(CultureInfo.InvariantCulture);
            var columnText = location.Column.ToString(CultureInfo.InvariantCulture);
            var displayedLocation =
                $"{location.DisplayPath}:{lineText}:{columnText}";

            builder.Append("<a ");
            builder.Append(PathAttribute);
            builder.Append("=\"");
            builder.Append(EscapeRichText(location.AssetPath));
            builder.Append("\" ");
            builder.Append(LineAttribute);
            builder.Append("=\"");
            builder.Append(lineText);
            builder.Append("\" ");
            builder.Append(ColumnAttribute);
            builder.Append("=\"");
            builder.Append(columnText);
            builder.Append("\">");
            builder.Append(EscapeRichText(displayedLocation));
            builder.Append("</a>: ");
            builder.Append(GetSeverityText(diagnostic.Severity));

            if (!string.IsNullOrWhiteSpace(diagnostic.Code))
            {
                builder.Append(' ');
                builder.Append(EscapeRichText(diagnostic.Code));
            }

            builder.Append(": ");
            builder.Append(EscapeRichText(diagnostic.Message ?? string.Empty));

            if (!string.IsNullOrWhiteSpace(diagnostic.Hint))
            {
                builder.Append("\nhint: ");
                builder.Append(EscapeRichText(diagnostic.Hint));
            }

            return builder.ToString();
        }

        private static SourceLocation ResolveLocation(
            Object fallbackSourceAsset,
            string fallbackSourcePath,
            string fallbackSourceText,
            in DiagnosticItem diagnostic)
        {
            var fallbackAssetPath = NormalizeFallbackPath(
                fallbackSourceAsset,
                fallbackSourcePath);
            var useFallback = IsEntrySourcePath(diagnostic.SourcePath);
            var sourcePath = useFallback
                ? fallbackAssetPath
                : NormalizeDisplayPath(diagnostic.SourcePath);
            var assetPath = TryGetProjectRelativePath(sourcePath);
            if (string.IsNullOrEmpty(assetPath) && useFallback)
                assetPath = fallbackAssetPath;

            var sourceAsset = string.IsNullOrEmpty(assetPath)
                ? null
                : AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (sourceAsset == null && useFallback)
                sourceAsset = fallbackSourceAsset;

            var sourceText = useFallback
                ? fallbackSourceText ?? string.Empty
                : TryReadSourceText(sourcePath, fallbackSourceText);
            var text = SourceText.From(sourceText ?? string.Empty);
            var position = Math.Max(0, Math.Min(diagnostic.Span.Start, text.Length));
            var line = text.GetLineFromPosition(position);
            var lineIndex = GetLineIndex(text, line);
            var column = position - line.Start + 1;
            var displayPath = string.IsNullOrEmpty(sourcePath)
                ? "<entry>"
                : sourcePath;
            var linkPath = string.IsNullOrEmpty(assetPath)
                ? displayPath
                : assetPath;

            return new SourceLocation(
                displayPath,
                linkPath,
                sourceAsset,
                lineIndex + 1,
                column);
        }

        private static string NormalizeFallbackPath(
            Object fallbackSourceAsset,
            string fallbackSourcePath)
        {
            var path = fallbackSourcePath;
            if (string.IsNullOrWhiteSpace(path) && fallbackSourceAsset != null)
                path = AssetDatabase.GetAssetPath(fallbackSourceAsset);
            return NormalizeDisplayPath(path);
        }

        private static bool IsEntrySourcePath(string sourcePath)
        {
            return string.IsNullOrWhiteSpace(sourcePath) ||
                string.Equals(sourcePath, "<entry>", StringComparison.Ordinal);
        }

        private static string NormalizeDisplayPath(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                return string.Empty;

            var assetPath = TryGetProjectRelativePath(sourcePath);
            return string.IsNullOrEmpty(assetPath)
                ? sourcePath.Replace('\\', '/')
                : assetPath;
        }

        private static string TryGetProjectRelativePath(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                return string.Empty;

            var normalized = sourcePath.Replace('\\', '/');
            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            try
            {
                var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrEmpty(projectRoot))
                    return string.Empty;

                var fullPath = Path.GetFullPath(sourcePath);
                var normalizedRoot = Path.GetFullPath(projectRoot)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                    Path.DirectorySeparatorChar;
                if (!fullPath.StartsWith(
                        normalizedRoot,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return string.Empty;
                }

                return fullPath[normalizedRoot.Length..].Replace('\\', '/');
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static string TryReadSourceText(
            string sourcePath,
            string fallbackSourceText)
        {
            try
            {
                var fullPath = sourcePath;
                if (!Path.IsPathRooted(fullPath))
                {
                    var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                    if (!string.IsNullOrEmpty(projectRoot))
                        fullPath = Path.Combine(projectRoot, fullPath);
                }

                if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
                    return File.ReadAllText(fullPath);
            }
            catch (Exception)
            {
                // Use the entry source as a best-effort fallback for location display.
            }

            return fallbackSourceText ?? string.Empty;
        }

        private static int GetLineIndex(SourceText sourceText, TextLine targetLine)
        {
            for (var index = 0; index < sourceText.Lines.Count; index++)
            {
                if (ReferenceEquals(sourceText.Lines[index], targetLine))
                    return index;
            }

            return 0;
        }

        private static string GetSeverityText(DiagnosticSeverity severity)
        {
            return severity switch
            {
                DiagnosticSeverity.Error => "error",
                DiagnosticSeverity.Warning => "warning",
                DiagnosticSeverity.Info => "info",
                _ => severity.ToString().ToLowerInvariant()
            };
        }

        private static string EscapeRichText(string text)
        {
            return (text ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("\"", "&quot;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        private readonly struct SourceLocation
        {
            public string DisplayPath { get; }
            public string AssetPath { get; }
            public Object SourceAsset { get; }
            public int Line { get; }
            public int Column { get; }

            public SourceLocation(
                string displayPath,
                string assetPath,
                Object sourceAsset,
                int line,
                int column)
            {
                DisplayPath = displayPath;
                AssetPath = assetPath;
                SourceAsset = sourceAsset;
                Line = line;
                Column = column;
            }
        }
    }
}
#endif
