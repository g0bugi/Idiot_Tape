using System;
using System.Collections.Generic;
using IdiotTape.Gameplay;
using UnityEditor;
using UnityEngine;

namespace IdiotTape.EditorTools
{

    public static class ChartActivationWindowUtility
    {

        private readonly struct EditableWindow
        {

            public EditableWindow(string partId, double startTime, double endTime)
            {

                PartId = partId;
                StartTime = startTime;
                EndTime = endTime;

            }

            public string PartId { get; }
            public double StartTime { get; }
            public double EndTime { get; }

        }

        public static int Normalize(PrototypeChart chart, bool recordUndo = true)
        {

            if (chart == null || chart.ActivationWindows.Count == 0)
            {

                return 0;

            }

            List<EditableWindow> source = new(chart.ActivationWindows.Count);

            for (int index = 0; index < chart.ActivationWindows.Count; index++)
            {

                MusicalPartActivationWindow window = chart.ActivationWindows[index];

                if (string.IsNullOrWhiteSpace(window.MusicalPartId) || window.EndTime <= window.StartTime)
                {

                    continue;

                }

                source.Add(new EditableWindow(window.MusicalPartId, window.StartTime, window.EndTime));

            }

            source.Sort((left, right) =>
            {

                int partComparison = string.CompareOrdinal(left.PartId, right.PartId);

                if (partComparison != 0)
                {

                    return partComparison;

                }

                int startComparison = left.StartTime.CompareTo(right.StartTime);
                return startComparison != 0 ? startComparison : left.EndTime.CompareTo(right.EndTime);

            });

            List<EditableWindow> normalized = new(source.Count);

            for (int index = 0; index < source.Count; index++)
            {

                EditableWindow current = source[index];

                if (normalized.Count == 0)
                {

                    normalized.Add(current);
                    continue;

                }

                EditableWindow previous = normalized[^1];

                if (previous.PartId == current.PartId && current.StartTime <= previous.EndTime + 0.000001d)
                {

                    normalized[^1] = new EditableWindow(
                        previous.PartId,
                        previous.StartTime,
                        Math.Max(previous.EndTime, current.EndTime));

                }
                else
                {

                    normalized.Add(current);

                }

            }

            int removedCount = chart.ActivationWindows.Count - normalized.Count;

            if (removedCount == 0)
            {

                return 0;

            }

            if (recordUndo)
            {

                Undo.RecordObject(chart, "활성 구간 중복 정리");

            }

            SerializedObject serializedChart = new(chart);
            SerializedProperty windows = serializedChart.FindProperty("activationWindows");
            windows.arraySize = normalized.Count;

            for (int index = 0; index < normalized.Count; index++)
            {

                EditableWindow window = normalized[index];
                SerializedProperty property = windows.GetArrayElementAtIndex(index);
                property.FindPropertyRelative("musicalPartId").stringValue = window.PartId;
                property.FindPropertyRelative("startTime").doubleValue = window.StartTime;
                property.FindPropertyRelative("endTime").doubleValue = window.EndTime;

            }

            serializedChart.ApplyModifiedPropertiesWithoutUndo();

            if (recordUndo)
            {

                EditorUtility.SetDirty(chart);

            }

            return removedCount;

        }

        public static int RemovePart(PrototypeChart chart, string partId)
        {

            if (chart == null || string.IsNullOrWhiteSpace(partId))
            {

                return 0;

            }

            int removedCount = 0;

            for (int index = 0; index < chart.ActivationWindows.Count; index++)
            {

                if (chart.ActivationWindows[index].MusicalPartId == partId)
                {

                    removedCount++;

                }

            }

            if (removedCount == 0)
            {

                return 0;

            }

            Undo.RecordObject(chart, "선택 파트 활성 구간 지우기");
            SerializedObject serializedChart = new(chart);
            SerializedProperty windows = serializedChart.FindProperty("activationWindows");

            for (int index = windows.arraySize - 1; index >= 0; index--)
            {

                SerializedProperty window = windows.GetArrayElementAtIndex(index);

                if (window.FindPropertyRelative("musicalPartId").stringValue == partId)
                {

                    windows.DeleteArrayElementAtIndex(index);

                }

            }

            serializedChart.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(chart);
            return removedCount;

        }

    }

}
