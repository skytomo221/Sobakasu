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
                    CountTypes(_lastCatalog, true).ToString());
                EditorGUILayout.LabelField(
                    "Unexposed CLR types",
                    CountTypes(_lastCatalog, false).ToString());
                EditorGUILayout.LabelField(
                    "Udon exposed members",
                    CountMembers(_lastCatalog, true).ToString());
                EditorGUILayout.LabelField(
                    "Unexposed members",
                    CountMembers(_lastCatalog, false).ToString());
                EditorGUILayout.LabelField("Unmodeled Udon signatures",
                    (_lastCatalog.unmodeledUdonSignatures?.Count ?? 0).ToString());
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

        private static int CountTypes(UdonApiCatalogData catalog, bool exposed)
        {
            var count = 0;
            foreach (var ns in catalog.namespaces ?? new System.Collections.Generic.List<UdonApiNamespaceRecord>())
            {
                var set = exposed ? ns.exposed : ns.unexposed;
                if (set == null) continue;
                count += set.classes?.Count ?? 0;
                count += set.interfaces?.Count ?? 0;
                count += set.structs?.Count ?? 0;
                count += set.enums?.Count ?? 0;
            }
            return count;
        }

        private static int CountMembers(UdonApiCatalogData catalog, bool exposed)
        {
            var count = 0;
            foreach (var ns in catalog.namespaces ?? new System.Collections.Generic.List<UdonApiNamespaceRecord>())
            {
                var set = ns.exposed;
                if (set == null) continue;
                foreach (var type in Enumerate(set))
                {
                    var members = exposed ? type.exposed : type.unexposed;
                    if (members == null) continue;
                    count += members.constructors?.Count ?? 0;
                    count += members.operators?.Count ?? 0;
                    count += CountSet(members.instance) + CountSet(members.@static);
                }
            }
            return count;
        }

        private static int CountSet(UdonApiMemberSet set)
        {
            return (set?.methods?.Count ?? 0) + (set?.propertyGetters?.Count ?? 0) +
                (set?.propertySetters?.Count ?? 0) + (set?.fieldGetters?.Count ?? 0) +
                (set?.fieldSetters?.Count ?? 0) + (set?.events?.Count ?? 0);
        }

        private static System.Collections.Generic.IEnumerable<UdonApiTypeRecord> Enumerate(UdonApiTypeSet set)
        {
            foreach (var type in set.classes ?? new System.Collections.Generic.List<UdonApiClassRecord>()) yield return type;
            foreach (var type in set.interfaces ?? new System.Collections.Generic.List<UdonApiInterfaceRecord>()) yield return type;
            foreach (var type in set.structs ?? new System.Collections.Generic.List<UdonApiStructRecord>()) yield return type;
            foreach (var type in set.enums ?? new System.Collections.Generic.List<UdonApiEnumTypeRecord>()) yield return type;
        }
    }
}
