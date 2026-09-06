using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Skytomo221.Sobakasu.Tools.StandardLibraryGenerator
{
    internal sealed class StandardLibraryGeneratorWindow : EditorWindow
    {
        private string _configurationFile;
        private string _outputDirectory;
        private readonly string _additionsDirectory;
        private readonly string _diagnosticsDirectory;
        private UdonApiGenerationReport _lastReport;
    }
}
