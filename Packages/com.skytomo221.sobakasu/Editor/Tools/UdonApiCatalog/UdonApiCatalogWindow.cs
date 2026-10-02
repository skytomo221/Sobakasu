using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog;

namespace Skytomo221.Sobakasu.Tools.UdonApiCatalog
{
    internal sealed class UdonApiCatalogWindow : EditorWindow
    {
        private string _outputFile;
        private UdonApiCatalogData _lastCatalog;

#pragma warning disable IDE0051
        [MenuItem("Window/Sobakasu/Build Udon API Catalog")]
        private static void Open()
        {
            var window = GetWindow<UdonApiCatalogWindow>();
            window.titleContent = new GUIContent("Udon API Catalog");
            window.minSize = new Vector2(520.0f, 260.0f);
            window.Show();
        }

        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(_outputFile))
                _outputFile = UdonApiCatalogGenerator.DefaultOutputPath;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Udon API Catalog", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Builds a deterministic reflection-free catalog from the installed Unity and VRChat SDK.",
                MessageType.Info);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Output file");
            EditorGUILayout.BeginHorizontal();
            _outputFile = EditorGUILayout.TextField(_outputFile ?? string.Empty);
            if (GUILayout.Button("Choose...", GUILayout.Width(90.0f)))
                ChooseOutputFile();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            EditorGUI.BeginDisabledGroup(EditorApplication.isCompiling || EditorApplication.isUpdating);
            if (GUILayout.Button("Build Udon API Catalog", GUILayout.Height(32.0f)))
                Generate();
            EditorGUI.EndDisabledGroup();
            if (File.Exists(_outputFile) && GUILayout.Button("Reveal output"))
                EditorUtility.RevealInFinder(_outputFile);
            if (_lastCatalog != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Last generation summary", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "Udon exposed types",
                    _lastCatalog.types.Count.ToString());
                EditorGUILayout.LabelField(
                    "Unexposed CLR types",
                    _lastCatalog.unexposedClrTypeNames.Count.ToString());
                EditorGUILayout.LabelField(
                    "Udon exposed members",
                    _lastCatalog.members.Count.ToString());
                EditorGUILayout.LabelField(
                    "Unexposed members",
                    _lastCatalog.unexposedMembers.Count.ToString());
                EditorGUILayout.LabelField("Unmatched Udon signatures",
                    _lastCatalog.unmatchedUdonSignatures.Count.ToString());
            }
        }
#pragma warning restore IDE0051

        private void ChooseOutputFile()
        {
            var selected = EditorUtility.SaveFilePanel(
                "Choose Udon API catalog output",
                Path.GetDirectoryName(_outputFile),
                UdonApiCatalogGenerator.OutputFileName,
                "json");
            if (!string.IsNullOrWhiteSpace(selected))
                _outputFile = selected;
        }

        private void Generate()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Sobakasu", "Building Udon API Catalog...", 0.5f);
                var generator = new UdonApiCatalogGenerator();
                generator.GenerateToFile(_outputFile);
                _lastCatalog = UdonApiCatalogGenerator.Deserialize(File.ReadAllText(_outputFile));
                AssetDatabase.Refresh();
                Debug.Log($"Generated Udon API Catalog at '{_outputFile}'.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Udon API Catalog", exception.Message, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
