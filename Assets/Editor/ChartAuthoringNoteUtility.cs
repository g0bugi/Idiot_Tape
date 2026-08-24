using IdiotTape.Gameplay;
using UnityEditor;

namespace IdiotTape.EditorTools
{

    public static class ChartAuthoringNoteUtility
    {

        public static int CountNotesInRange(
            PrototypeChart chart,
            string partId,
            double startTime,
            double endTime)
        {

            if (chart == null ||
                string.IsNullOrWhiteSpace(partId) ||
                endTime <= startTime)
            {

                return 0;

            }

            int count = 0;

            for (int index = 0; index < chart.Notes.Count; index++)
            {

                ChartNote note = chart.Notes[index];

                if (note.MusicalPartId == partId &&
                    note.HitTime >= startTime &&
                    note.HitTime < endTime)
                {

                    count++;

                }

            }

            return count;

        }

        public static bool DeleteNote(PrototypeChart chart, string noteId)
        {

            if (chart == null || string.IsNullOrWhiteSpace(noteId))
            {

                return false;

            }

            for (int index = 0; index < chart.Notes.Count; index++)
            {

                if (chart.Notes[index].Id != noteId)
                {

                    continue;

                }

                Undo.RecordObject(chart, "Delete applied chart note");
                SerializedObject serializedChart = new(chart);
                SerializedProperty notes = serializedChart.FindProperty("notes");
                notes.DeleteArrayElementAtIndex(index);
                serializedChart.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(chart);
                return true;

            }

            return false;

        }

        public static int DeleteNotesInRange(
            PrototypeChart chart,
            string partId,
            double startTime,
            double endTime)
        {

            int deleteCount = CountNotesInRange(chart, partId, startTime, endTime);

            if (deleteCount == 0)
            {

                return 0;

            }

            Undo.RecordObject(chart, "Delete applied chart notes in range");
            SerializedObject serializedChart = new(chart);
            SerializedProperty notes = serializedChart.FindProperty("notes");

            for (int index = chart.Notes.Count - 1; index >= 0; index--)
            {

                ChartNote note = chart.Notes[index];

                if (note.MusicalPartId == partId &&
                    note.HitTime >= startTime &&
                    note.HitTime < endTime)
                {

                    notes.DeleteArrayElementAtIndex(index);

                }

            }

            serializedChart.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(chart);
            return deleteCount;

        }

    }

}
