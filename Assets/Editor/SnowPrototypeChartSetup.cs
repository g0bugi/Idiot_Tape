using IdiotTape.Gameplay;
using UnityEditor;
using UnityEngine;

namespace IdiotTape.EditorTools
{

    public static class SnowPrototypeChartSetup
    {

        public const string ChartPath = "Assets/Data/SnowPrototypeChart.asset";
        public const string SongEventPath = "event:/Music/KIRARA/Snow";
        private const double SnowDurationSeconds = 488.557619047619d;
        private const double SnowFirstDownbeatSeconds = 0d;

        [MenuItem("Tools/Idiot Tape/Snow 채보 생성 또는 갱신")]
        public static void CreateOrUpdateFromMenu()
        {

            PrototypeChart chart = CreateOrUpdateSnowChart();
            Selection.activeObject = chart;
            EditorGUIUtility.PingObject(chart);

        }

        public static PrototypeChart CreateOrUpdateSnowChart()
        {

            PrototypeChart chart = AssetDatabase.LoadAssetAtPath<PrototypeChart>(ChartPath);
            bool created = chart == null;

            if (created)
            {

                chart = ScriptableObject.CreateInstance<PrototypeChart>();
                AssetDatabase.CreateAsset(chart, ChartPath);

            }

            Undo.RecordObject(chart, "Snow prototype chart setup");
            SerializedObject serializedChart = new(chart);
            serializedChart.FindProperty("songEventPath").stringValue = SongEventPath;
            serializedChart.FindProperty("laneCount").intValue = 8;
            serializedChart.FindProperty("visualLeadTime").floatValue = 2.4f;

            if (created || serializedChart.FindProperty("tempoSections").arraySize == 0)
            {

                ConfigureTempo(serializedChart);

            }

            ConfigureStemParameters(serializedChart);

            SerializedProperty activationWindows = serializedChart.FindProperty("activationWindows");
            SerializedProperty notes = serializedChart.FindProperty("notes");

            if (created || (activationWindows.arraySize == 0 && notes.arraySize == 0))
            {

                ConfigureMusicalParts(serializedChart);

            }

            if (created)
            {

                activationWindows.arraySize = 0;
                notes.arraySize = 0;

            }

            serializedChart.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(chart);
            AssetDatabase.SaveAssetIfDirty(chart);
            Debug.Log(
                $"Snow chart is ready at '{ChartPath}'. FMOD duration reference: {SnowDurationSeconds:0.000}s.",
                chart);
            return chart;

        }

        private static void ConfigureTempo(SerializedObject serializedChart)
        {

            SerializedProperty tempoSections = serializedChart.FindProperty("tempoSections");
            tempoSections.arraySize = 1;
            SerializedProperty tempo = tempoSections.GetArrayElementAtIndex(0);
            tempo.FindPropertyRelative("startBar").intValue = 1;
            tempo.FindPropertyRelative("startTime").doubleValue = SnowFirstDownbeatSeconds;
            tempo.FindPropertyRelative("beatsPerMinute").doubleValue = 130d;
            tempo.FindPropertyRelative("beatsPerBar").intValue = 4;
            tempo.FindPropertyRelative("beatUnit").intValue = 4;

        }

        private static void ConfigureMusicalParts(SerializedObject serializedChart)
        {

            SerializedProperty parts = serializedChart.FindProperty("musicalParts");
            parts.arraySize = 4;
            ConfigurePart(parts.GetArrayElementAtIndex(0), "drum", "Drum", new Color(0.94f, 0.92f, 0.86f));
            ConfigurePart(parts.GetArrayElementAtIndex(1), "bass", "Bass", new Color(0.95f, 0.31f, 0.58f));
            ConfigurePart(parts.GetArrayElementAtIndex(2), "synth", "Synth", new Color(0.38f, 0.82f, 0.92f));
            ConfigurePart(parts.GetArrayElementAtIndex(3), "etc", "Etc", new Color(0.52f, 0.42f, 0.95f));

        }

        private static void ConfigureStemParameters(SerializedObject serializedChart)
        {

            SerializedProperty stems = serializedChart.FindProperty("stemParameters");
            stems.arraySize = 4;
            ConfigureStem(stems.GetArrayElementAtIndex(0), "drum", "stems_drum_volume");
            ConfigureStem(stems.GetArrayElementAtIndex(1), "bass", "stems_bass_volume");
            ConfigureStem(stems.GetArrayElementAtIndex(2), "synth", "stems_synth1_volume");
            ConfigureStem(stems.GetArrayElementAtIndex(3), "etc", "stems_etc_volume");

        }

        private static void ConfigureStem(
            SerializedProperty stem,
            string stemId,
            string parameterName)
        {

            stem.FindPropertyRelative("stemId").stringValue = stemId;
            stem.FindPropertyRelative("parameterName").stringValue = parameterName;

        }

        private static void ConfigurePart(
            SerializedProperty part,
            string id,
            string displayName,
            Color color)
        {

            part.FindPropertyRelative("id").stringValue = id;
            part.FindPropertyRelative("displayName").stringValue = displayName;
            part.FindPropertyRelative("color").colorValue = color;

        }

    }

}
