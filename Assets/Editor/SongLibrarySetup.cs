using System;
using System.Collections.Generic;
using System.IO;
using IdiotTape.Gameplay;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdiotTape.EditorTools
{

    public static class SongLibrarySetup
    {

        public const string CatalogPath = "Assets/Data/SongCatalog.asset";
        public const string ScenePath = "Assets/Scenes/SongLibrary.unity";

        [MenuItem("Tools/Idiot Tape/Rebuild Song Library")]
        public static void Rebuild()
        {

            RefreshCatalog();
            EnsureEntryScene();

        }

        public static SongCatalog RefreshCatalog()
        {

            Directory.CreateDirectory("Assets/Data/Songs");
            AssetDatabase.Refresh();
            SongCatalog catalog = AssetDatabase.LoadAssetAtPath<SongCatalog>(CatalogPath);
            List<PrototypeChart> sources = new();
            if (catalog != null)
            {

                foreach (CatalogChart entry in catalog.Charts)
                {

                    sources.Add(entry?.Chart);

                }

            }
            else
            {

                sources.Add(AssetDatabase.LoadAssetAtPath<PrototypeChart>("Assets/Data/SnowPrototypeChart.asset"));
                sources.Add(AssetDatabase.LoadAssetAtPath<PrototypeChart>("Assets/Data/PlutoPrototypeChart.asset"));
                catalog = ScriptableObject.CreateInstance<SongCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);

            }

            List<PrototypeChart> charts = new();
            Dictionary<string, SongDefinition> byEvent = new();
            foreach (string guid in AssetDatabase.FindAssets("t:SongDefinition", new[] { "Assets/Data" }))
            {

                SongDefinition song = AssetDatabase.LoadAssetAtPath<SongDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                byEvent[song.EventPath] = song;

            }

            foreach (PrototypeChart chart in sources)
            {

                if (chart == null)
                {

                    throw new InvalidOperationException("Catalog chart reference is missing.");

                }
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(chart));
                if (!chart.TryValidate(out string error))
                {

                    throw new InvalidOperationException($"{chart.name}: {error}");

                }
                SerializedObject serialized = new(chart);
                if (chart.Song == null)
                {

                    if (!byEvent.TryGetValue(chart.SongEventPath, out SongDefinition song))
                    {

                        song = ScriptableObject.CreateInstance<SongDefinition>();
                        AssetDatabase.CreateAsset(song, AssetDatabase.GenerateUniqueAssetPath($"Assets/Data/Songs/{chart.name}Song.asset"));
                        SerializedObject songData = new(song);
                        songData.FindProperty("id").stringValue = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(song));
                        songData.FindProperty("title").stringValue = chart.SongTitle;
                        songData.FindProperty("artist").stringValue = chart.ArtistName;
                        songData.FindProperty("eventPath").stringValue = chart.SongEventPath;
                        CopyStems(serialized.FindProperty("stemParameters"), songData.FindProperty("stems"));
                        songData.FindProperty("previewStart").floatValue = chart.Notes.Count > 0 ? (float)chart.Notes[0].HitTime : 0f;
                        songData.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(song);
                        AssetDatabase.SaveAssetIfDirty(song);
                        byEvent[chart.SongEventPath] = song;

                    }

                    serialized.FindProperty("song").objectReferenceValue = song;

                }

                if (string.IsNullOrWhiteSpace(chart.ChartId))
                {

                    serialized.FindProperty("chartId").stringValue = guid;

                }
                if (serialized.ApplyModifiedPropertiesWithoutUndo())
                {

                    EditorUtility.SetDirty(chart);
                    AssetDatabase.SaveAssetIfDirty(chart);

                }
                charts.Add(chart);

            }

            SerializedObject catalogData = new(catalog);
            SerializedProperty entries = catalogData.FindProperty("charts");
            charts.Sort((a, b) => string.CompareOrdinal(a.ChartId, b.ChartId));
            entries.arraySize = charts.Count;
            for (int i = 0; i < charts.Count; i++)
            {

                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                PrototypeChart chart = charts[i];
                entry.FindPropertyRelative("chart").objectReferenceValue = chart;
                entry.FindPropertyRelative("partSummary").stringValue = GetPartSummary(chart);
                int playable = 0;
                foreach (ChartNote note in chart.Notes)
                {

                    if (chart.IsNotePlayable(note))
                    {

                        playable++;

                    }

                }
                entry.FindPropertyRelative("playableNotes").intValue = playable;

            }

            bool catalogChanged = catalogData.ApplyModifiedPropertiesWithoutUndo();
            if (!catalog.TryValidate(out string catalogError))
            {

                throw new InvalidOperationException(catalogError);

            }
            if (catalogChanged)
            {

                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssetIfDirty(catalog);

            }

            Debug.Log($"Song catalog refreshed: {charts.Count} charts.");
            return catalog;

        }

        private static void EnsureEntryScene()
        {

            Scene active = SceneManager.GetActiveScene();
            bool useSingle = string.IsNullOrEmpty(active.path) && !active.isDirty;
            bool alreadyOpen = active.path == ScenePath;
            Scene library = alreadyOpen ? active : File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, useSingle ? OpenSceneMode.Single : OpenSceneMode.Additive)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, useSingle ? NewSceneMode.Single : NewSceneMode.Additive);
            SongLibraryFlow flow = null;
            foreach (GameObject root in library.GetRootGameObjects())
            {

                flow = root.GetComponent<SongLibraryFlow>();
                if (flow != null)
                {

                    break;

                }

            }
            if (flow == null)
            {

                GameObject root = new("Song Library");
                SceneManager.MoveGameObjectToScene(root, library);
                flow = root.AddComponent<SongLibraryFlow>();

            }
            // Opening a scene may unload editor asset instances. Resolve the asset afterwards.
            SongCatalog catalog = AssetDatabase.LoadAssetAtPath<SongCatalog>(CatalogPath);
            if (catalog == null)
            {

                throw new InvalidOperationException("Song catalog did not load after scene creation.");

            }
            SerializedObject flowData = new(flow);
            flowData.FindProperty("catalog").objectReferenceValue = catalog;
            Transform listenerRoot = flow.transform.Find("Preview Audio Listener");
            if (listenerRoot == null)
            {

                GameObject listener = new("Preview Audio Listener");
                listener.transform.SetParent(flow.transform, false);
                listener.AddComponent<FMODUnity.StudioListener>();
                listenerRoot = listener.transform;

            }

            flowData.FindProperty("previewAudioRoot").objectReferenceValue = listenerRoot.gameObject;
            Camera menuCamera = listenerRoot.GetComponent<Camera>();
            if (menuCamera == null)
            {

                menuCamera = listenerRoot.gameObject.AddComponent<Camera>();

            }

            // Overlay UI still needs an active display camera in the normal Game view.
            // The listener root already hands off its lifetime to the gameplay scene.
            menuCamera.clearFlags = CameraClearFlags.SolidColor;
            menuCamera.backgroundColor = new Color(0.063f, 0.086f, 0.098f);
            menuCamera.cullingMask = 0;
            menuCamera.orthographic = true;
            if (listenerRoot.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() == null)
            {

                listenerRoot.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();

            }
            flowData.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(library, ScenePath);
            if (!alreadyOpen && !useSingle)
            {

                EditorSceneManager.CloseScene(library, true);

            }

            List<EditorBuildSettingsScene> scenes = new() { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {

                if (scene.path != ScenePath)
                {

                    scenes.Add(scene);

                }

            }

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("Song library entry scene is ready.");

        }

        public static string GetPartSummary(PrototypeChart chart)
        {

            List<MusicalPartActivationWindow> windows = new(chart.ActivationWindows);
            windows.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
            List<string> parts = new();
            string last = null;
            foreach (MusicalPartActivationWindow window in windows)
            {

                if (window.MusicalPartId == last)
                {

                    continue;

                }
                last = window.MusicalPartId;
                parts.Add(chart.GetPartDisplayName(last));

            }

            return parts.Count == 0 ? "파트 구성 준비 중" : string.Join(" → ", parts);

        }

        // Setup tools explicitly author event/stem defaults. Route those writes to the
        // new owner too; ordinary recorder edits only read the chart's resolved properties.
        public static void ApplySetupAudio(PrototypeChart chart)
        {

            if (chart.Song == null)
            {

                return;

            }
            Undo.RecordObject(chart.Song, "Configure song audio");
            SerializedObject legacy = new(chart);
            SerializedObject song = new(chart.Song);
            song.FindProperty("eventPath").stringValue = legacy.FindProperty("songEventPath").stringValue;
            CopyStems(legacy.FindProperty("stemParameters"), song.FindProperty("stems"));
            song.ApplyModifiedProperties();
            EditorUtility.SetDirty(chart.Song);
            AssetDatabase.SaveAssetIfDirty(chart.Song);

        }

        private static void CopyStems(SerializedProperty source, SerializedProperty target)
        {

            target.arraySize = source.arraySize;
            for (int i = 0; i < source.arraySize; i++)
            {

                target.GetArrayElementAtIndex(i).FindPropertyRelative("stemId").stringValue = source.GetArrayElementAtIndex(i).FindPropertyRelative("stemId").stringValue;
                target.GetArrayElementAtIndex(i).FindPropertyRelative("parameterName").stringValue = source.GetArrayElementAtIndex(i).FindPropertyRelative("parameterName").stringValue;

            }

        }

    }

    public sealed class SongLibraryBuildProcessor : IPreprocessBuildWithReport
    {

        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) => SongLibrarySetup.RefreshCatalog();

    }

}
