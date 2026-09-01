using System;
using System.Collections.Generic;
using IdiotTape.Gameplay;
using UnityEditor;

namespace IdiotTape.EditorTools
{

    [Serializable]
    internal sealed class ChartNoteAuthoringData
    {

        [Serializable]
        internal sealed class PathNodeData
        {

            public double Time;
            public int LaneIndex;

        }

        [Serializable]
        internal sealed class CurveHandleData
        {

            public float NormalizedTime;
            public float NormalizedX;

        }

        [Serializable]
        internal sealed class CheckpointData
        {

            public double Time;
            public float NormalizedX;

        }

        public string Id;
        public double HitTime;
        public int LaneIndex;
        public string MusicalPartId;
        public ChartNoteType NoteType;
        public double EndTime;
        public int EndLaneIndex;
        public SlideEndBehavior SlideEndBehavior;
        public List<PathNodeData> SlideNodes = new();
        public List<CurveHandleData> BananaCurveHandles = new();
        public List<CheckpointData> BananaCheckpoints = new();
        public int BananaMaximumBonusCombo;

        public static ChartNoteAuthoringData FromChartNote(ChartNote note)
        {

            ChartNoteAuthoringData data = new()
            {

                Id = note.Id,
                HitTime = note.HitTime,
                LaneIndex = note.LaneIndex,
                MusicalPartId = note.MusicalPartId,
                NoteType = note.NoteType,
                EndTime = note.AuthoredEndTime,
                EndLaneIndex = note.AuthoredEndLaneIndex,
                SlideEndBehavior = note.SlideEndBehavior,
                BananaMaximumBonusCombo = note.BananaMaximumBonusCombo

            };

            if (note.SlideNodes != null)
            {

                for (int index = 0; index < note.SlideNodes.Count; index++)
                {

                    data.SlideNodes.Add(new PathNodeData
                    {

                        Time = note.SlideNodes[index].Time,
                        LaneIndex = note.SlideNodes[index].LaneIndex

                    });

                }

            }

            if (note.BananaCurveHandles != null)
            {

                for (int index = 0; index < note.BananaCurveHandles.Count; index++)
                {

                    data.BananaCurveHandles.Add(new CurveHandleData
                    {

                        NormalizedTime = note.BananaCurveHandles[index].NormalizedTime,
                        NormalizedX = note.BananaCurveHandles[index].NormalizedX

                    });

                }

            }

            if (note.BananaCheckpoints != null)
            {

                for (int index = 0; index < note.BananaCheckpoints.Count; index++)
                {

                    data.BananaCheckpoints.Add(new CheckpointData
                    {

                        Time = note.BananaCheckpoints[index].Time,
                        NormalizedX = note.BananaCheckpoints[index].NormalizedX

                    });

                }

            }

            return data;

        }

        public static ChartNoteAuthoringData CreateTap(
            string id,
            double hitTime,
            int laneIndex,
            string musicalPartId)
        {

            return new ChartNoteAuthoringData
            {

                Id = id,
                HitTime = hitTime,
                LaneIndex = laneIndex,
                MusicalPartId = musicalPartId,
                NoteType = ChartNoteType.Tap,
                EndTime = hitTime,
                EndLaneIndex = laneIndex,
                SlideEndBehavior = SlideEndBehavior.Normal,
                BananaMaximumBonusCombo = 4

            };

        }

        public ChartNoteAuthoringData CloneWithOffset(double offsetSeconds, string id = null)
        {

            ChartNoteAuthoringData clone = new()
            {

                Id = id ?? Id,
                HitTime = HitTime + offsetSeconds,
                LaneIndex = LaneIndex,
                MusicalPartId = MusicalPartId,
                NoteType = NoteType,
                EndTime = EndTime + offsetSeconds,
                EndLaneIndex = EndLaneIndex,
                SlideEndBehavior = SlideEndBehavior,
                BananaMaximumBonusCombo = BananaMaximumBonusCombo

            };

            for (int index = 0; index < SlideNodes.Count; index++)
            {

                clone.SlideNodes.Add(new PathNodeData
                {

                    Time = SlideNodes[index].Time + offsetSeconds,
                    LaneIndex = SlideNodes[index].LaneIndex

                });

            }

            for (int index = 0; index < BananaCurveHandles.Count; index++)
            {

                clone.BananaCurveHandles.Add(new CurveHandleData
                {

                    NormalizedTime = BananaCurveHandles[index].NormalizedTime,
                    NormalizedX = BananaCurveHandles[index].NormalizedX

                });

            }

            for (int index = 0; index < BananaCheckpoints.Count; index++)
            {

                clone.BananaCheckpoints.Add(new CheckpointData
                {

                    Time = BananaCheckpoints[index].Time + offsetSeconds,
                    NormalizedX = BananaCheckpoints[index].NormalizedX

                });

            }

            return clone;

        }

        public void ShiftTimes(double offsetSeconds)
        {

            HitTime += offsetSeconds;
            EndTime += offsetSeconds;

            for (int index = 0; index < SlideNodes.Count; index++)
            {

                SlideNodes[index].Time += offsetSeconds;

            }

            for (int index = 0; index < BananaCheckpoints.Count; index++)
            {

                BananaCheckpoints[index].Time += offsetSeconds;

            }

        }

        public void WriteTo(SerializedProperty property)
        {

            property.FindPropertyRelative("id").stringValue = Id;
            property.FindPropertyRelative("hitTime").doubleValue = HitTime;
            property.FindPropertyRelative("laneIndex").intValue = LaneIndex;
            property.FindPropertyRelative("musicalPartId").stringValue = MusicalPartId;
            property.FindPropertyRelative("noteType").enumValueIndex = (int)NoteType;
            property.FindPropertyRelative("endTime").doubleValue = EndTime;
            property.FindPropertyRelative("endLaneIndex").intValue = EndLaneIndex;
            property.FindPropertyRelative("slideEndBehavior").enumValueIndex = (int)SlideEndBehavior;

            SerializedProperty nodes = property.FindPropertyRelative("slideNodes");
            nodes.arraySize = SlideNodes.Count;

            for (int index = 0; index < SlideNodes.Count; index++)
            {

                SerializedProperty node = nodes.GetArrayElementAtIndex(index);
                node.FindPropertyRelative("time").doubleValue = SlideNodes[index].Time;
                node.FindPropertyRelative("laneIndex").intValue = SlideNodes[index].LaneIndex;

            }

            SerializedProperty handles = property.FindPropertyRelative("bananaCurveHandles");
            handles.arraySize = BananaCurveHandles.Count;

            for (int index = 0; index < BananaCurveHandles.Count; index++)
            {

                SerializedProperty handle = handles.GetArrayElementAtIndex(index);
                handle.FindPropertyRelative("normalizedTime").floatValue =
                    BananaCurveHandles[index].NormalizedTime;
                handle.FindPropertyRelative("normalizedX").floatValue =
                    BananaCurveHandles[index].NormalizedX;

            }

            SerializedProperty checkpoints = property.FindPropertyRelative("bananaCheckpoints");
            checkpoints.arraySize = BananaCheckpoints.Count;

            for (int index = 0; index < BananaCheckpoints.Count; index++)
            {

                SerializedProperty checkpoint = checkpoints.GetArrayElementAtIndex(index);
                checkpoint.FindPropertyRelative("time").doubleValue = BananaCheckpoints[index].Time;
                checkpoint.FindPropertyRelative("normalizedX").floatValue =
                    BananaCheckpoints[index].NormalizedX;

            }

            property.FindPropertyRelative("bananaMaximumBonusCombo").intValue =
                BananaMaximumBonusCombo;

        }

    }

}
