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
    private string _additionsDirectory;
    private string _diagnosticsDirectory;
    private UdonApiGenerationReport _lastReport;
    private Vector2 _scrollPosition;

    [MenuItem("Window/Sobakasu/Build Standard Library")]
    private static void Open()
    {
      var window = GetWindow<StandardLibraryGeneratorWindow>();
      window.titleContent = new GUIContent("Standard Library");
      window.minSize = new Vector2(560.0f, 380.0f);
      window.Show();
    }

    private void OnEnable()
    {
      if (string.IsNullOrWhiteSpace(_outputDirectory))
        _outputDirectory = StandardLibraryGenerator.DefaultOutputDirectory;
      if (string.IsNullOrWhiteSpace(_additionsDirectory))
        _additionsDirectory = StandardLibraryGenerator.DefaultAdditionsDirectory;
      if (string.IsNullOrWhiteSpace(_diagnosticsDirectory))
        _diagnosticsDirectory = StandardLibraryGenerator.DefaultDiagnosticsDirectory;
    }

    private void OnGUI()
    {
      _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
      EditorGUILayout.LabelField("Standard Library Generator", EditorStyles.boldLabel);
      EditorGUILayout.HelpBox(
          "Builds StandardLibrary~ from installed Udon API bindings and the manually-authored " +
          "StandardLibraryAdditions~ tree. The existing output is replaced only after the new " +
          "library has been generated successfully.",
          MessageType.Info);

      EditorGUILayout.Space();
      EditorGUILayout.LabelField("Configuration file (optional)");
      EditorGUILayout.BeginHorizontal();
      _configurationFile = EditorGUILayout.TextField(_configurationFile ?? string.Empty);
      if (GUILayout.Button("Choose...", GUILayout.Width(90.0f)))
        ChooseConfigurationFile();
      EditorGUILayout.EndHorizontal();

      EditorGUILayout.Space();
      EditorGUILayout.LabelField("Additions directory");
      EditorGUILayout.BeginHorizontal();
      _additionsDirectory = EditorGUILayout.TextField(_additionsDirectory);
      if (GUILayout.Button("Choose...", GUILayout.Width(90.0f)))
        _additionsDirectory = ChooseDirectory(_additionsDirectory, "Choose additions directory");
      EditorGUILayout.EndHorizontal();

      EditorGUILayout.Space();
      EditorGUILayout.LabelField("Diagnostics directory");
      EditorGUILayout.BeginHorizontal();
      _diagnosticsDirectory = EditorGUILayout.TextField(_diagnosticsDirectory);
      if (GUILayout.Button("Choose...", GUILayout.Width(90.0f)))
        _diagnosticsDirectory = ChooseDirectory(
            _diagnosticsDirectory,
            "Choose diagnostics directory");
      EditorGUILayout.EndHorizontal();

      EditorGUILayout.Space();
      EditorGUILayout.LabelField("Output directory");
      EditorGUILayout.BeginHorizontal();
      _outputDirectory = EditorGUILayout.TextField(_outputDirectory);
      if (GUILayout.Button("Choose...", GUILayout.Width(90.0f)))
        ChooseOutputDirectory();
      EditorGUILayout.EndHorizontal();

      EditorGUILayout.Space();
      EditorGUI.BeginDisabledGroup(
          EditorApplication.isCompiling || EditorApplication.isUpdating);
      if (GUILayout.Button("Build StandardLibrary~", GUILayout.Height(32.0f)))
        Generate();
      EditorGUI.EndDisabledGroup();

      if (Directory.Exists(_outputDirectory) &&
          GUILayout.Button("Reveal output directory"))
      {
        EditorUtility.RevealInFinder(_outputDirectory);
      }

      if (_lastReport != null)
        DrawReport(_lastReport);
      EditorGUILayout.EndScrollView();
    }

    private void ChooseOutputDirectory()
    {
      var currentPath = string.IsNullOrWhiteSpace(_outputDirectory)
          ? StandardLibraryGenerator.DefaultOutputDirectory
          : Path.GetFullPath(_outputDirectory);
      var parent = Path.GetDirectoryName(currentPath);
      var name = Path.GetFileName(currentPath);
      var selected = EditorUtility.SaveFolderPanel(
          "Choose a new or empty output directory",
          parent,
          name);
      if (!string.IsNullOrWhiteSpace(selected))
        _outputDirectory = selected;
    }

    private static string ChooseDirectory(string currentPath, string title)
    {
      var fullPath = Path.GetFullPath(currentPath);
      var selected = EditorUtility.OpenFolderPanel(title, fullPath, string.Empty);
      return string.IsNullOrWhiteSpace(selected) ? currentPath : selected;
    }

    private void ChooseConfigurationFile()
    {
      var currentDirectory = string.IsNullOrWhiteSpace(_configurationFile)
          ? Path.GetDirectoryName(StandardLibraryGenerator.DefaultConfigurationPath)
          : Path.GetDirectoryName(Path.GetFullPath(_configurationFile));
      var selected = EditorUtility.OpenFilePanel(
          "Choose a Udon binding generation configuration",
          currentDirectory,
          "json");
      if (!string.IsNullOrWhiteSpace(selected))
        _configurationFile = selected;
    }

    private void Generate()
    {
      try
      {
        EditorUtility.DisplayProgressBar(
            "Sobakasu",
            "Building StandardLibrary~...",
            0.5f);
        var result = StandardLibraryGenerator.CreateDefault(_configurationFile)
            .GenerateToDirectory(
                _outputDirectory,
                _additionsDirectory,
                _diagnosticsDirectory);
        _lastReport = result.Report;
        AssetDatabase.Refresh();
        Debug.Log(
            $"Generated Sobakasu StandardLibrary~ at '{result.OutputDirectory}'.\n" +
            $"Files: {result.Files.Count}; " +
            $"Types: {_lastReport.types_generated}/{_lastReport.types_discovered}; " +
            $"Member surfaces: {_lastReport.member_surfaces_generated}/" +
            $"{_lastReport.member_surfaces_discovered}; " +
            $"Udon API coverage: {_lastReport.udon_signatures_covered}/" +
            $"{_lastReport.udon_signatures_exposed} " +
            $"({_lastReport.udon_api_coverage_percent:F2}%).");
        EditorUtility.DisplayDialog(
            "Sobakasu",
            "Standard-library generation completed.\n\n" +
            $"Types generated: {_lastReport.types_generated}\n" +
            $"Types skipped: {_lastReport.types_skipped}\n" +
            $"Member surfaces generated: {_lastReport.member_surfaces_generated}\n" +
            $"Member surfaces skipped: {_lastReport.member_surfaces_skipped}\n" +
            $"Udon API coverage: {_lastReport.udon_api_coverage_percent:F2}%",
            "OK");
      }
      catch (Exception exception)
      {
        Debug.LogException(exception);
        EditorUtility.DisplayDialog(
            "Sobakasu Standard Library Generator",
            exception.Message,
            "OK");
      }
      finally
      {
        EditorUtility.ClearProgressBar();
      }
    }

    private static void DrawReport(UdonApiGenerationReport report)
    {
      EditorGUILayout.Space();
      EditorGUILayout.LabelField("Last generation", EditorStyles.boldLabel);
      EditorGUILayout.LabelField(
          "Types",
          $"{report.types_generated} generated / {report.types_skipped} skipped / " +
          $"{report.types_discovered} discovered");
      EditorGUILayout.LabelField(
          "Member surfaces",
          $"{report.member_surfaces_generated} generated / " +
          $"{report.member_surfaces_skipped} skipped / " +
          $"{report.member_surfaces_discovered} discovered");
      EditorGUILayout.LabelField(
          "Physical Udon API",
          $"{report.udon_signatures_covered} covered / " +
          $"{report.udon_signatures_unsupported} unsupported / " +
          $"{report.udon_signatures_exposed} exposed " +
          $"({report.udon_api_coverage_percent:F2}%)");

      var reasonCount = Math.Min(5, report.skip_reasons.Count);
      if (reasonCount == 0)
        return;

      EditorGUILayout.Space();
      EditorGUILayout.LabelField("Most common skip reasons", EditorStyles.boldLabel);
      for (var index = 0; index < reasonCount; index++)
      {
        var reason = report.skip_reasons[index];
        EditorGUILayout.LabelField(reason.count.ToString(), reason.reason);
      }
    }
  }
}
