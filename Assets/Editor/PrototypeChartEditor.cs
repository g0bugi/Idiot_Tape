using System.Collections.Generic;
using IdiotTape.Gameplay;
using UnityEditor;
using UnityEngine;

namespace IdiotTape.EditorTools
{

    [CustomEditor(typeof(PrototypeChart))]
    public sealed class PrototypeChartEditor : Editor
    {

        public override void OnInspectorGUI()
        {

            DrawDefaultInspector();
            EditorGUILayout.Space(12f);
            DrawLayerOverview((PrototypeChart)target);

        }

        private static void DrawLayerOverview(PrototypeChart chart)
        {

            EditorGUILayout.LabelField("Musical Layer Overview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Colors are presentation. Part IDs and activation windows are the chart's authoritative musical-layer structure.",
                MessageType.Info);

            IReadOnlyList<MusicalPartDefinition> parts = chart.MusicalParts;

            for (int partIndex = 0; partIndex < parts.Count; partIndex++)
            {

                MusicalPartDefinition part = parts[partIndex];
                int noteCount = CountNotes(chart.Notes, part.Id);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {

                    Rect headerRect = EditorGUILayout.GetControlRect();
                    Rect colorRect = new(headerRect.x, headerRect.y + 2f, 14f, 14f);
                    EditorGUI.DrawRect(colorRect, part.Color);
                    EditorGUI.LabelField(
                        new Rect(headerRect.x + 20f, headerRect.y, headerRect.width - 20f, headerRect.height),
                        $"{part.DisplayName}  [{part.Id}]  ·  {noteCount} notes",
                        EditorStyles.boldLabel);

                    bool hasWindow = false;

                    for (int windowIndex = 0; windowIndex < chart.ActivationWindows.Count; windowIndex++)
                    {

                        MusicalPartActivationWindow window = chart.ActivationWindows[windowIndex];

                        if (window.MusicalPartId != part.Id)
                        {

                            continue;

                        }

                        hasWindow = true;
                        EditorGUILayout.LabelField($"Active  {window.StartTime:0.00}s  →  {window.EndTime:0.00}s");

                    }

                    if (!hasWindow)
                    {

                        EditorGUILayout.LabelField("No activation window", EditorStyles.miniLabel);

                    }

                }

            }

        }

        private static int CountNotes(IReadOnlyList<ChartNote> notes, string partId)
        {

            int count = 0;

            for (int index = 0; index < notes.Count; index++)
            {

                if (notes[index].MusicalPartId == partId)
                {

                    count++;

                }

            }

            return count;

        }

    }

}
