using System;
using System.Collections.Generic;
using IdiotTape.Audio;
using UnityEngine;

namespace IdiotTape.Gameplay
{

    [Serializable]
    public sealed class MusicalPartDefinition
    {

        [SerializeField] private string id = "part";
        [SerializeField] private string displayName = "Part";
        [SerializeField] private Color color = Color.white;

        public string Id => id;
        public string DisplayName => displayName;
        public Color Color => color;

    }

    [Serializable]
    public sealed class MusicalPartActivationWindow
    {

        [SerializeField] private string musicalPartId = "part";
        [SerializeField, Min(0f)] private double startTime;
        [SerializeField, Min(0f)] private double endTime = 1d;

        public string MusicalPartId => musicalPartId;
        public double StartTime => startTime;
        public double EndTime => endTime;

        public bool Contains(double songTime)
        {

            return songTime >= startTime && songTime < endTime;

        }

    }

    public enum ChartNoteType
    {

        Tap = 0,
        Hold = 1,
        Slide = 2,
        Flick = 3,
        Banana = 4

    }

    public enum SlideEndBehavior
    {

        Normal = 0,
        Flick = 1

    }

    [Serializable]
    public sealed class ChartPathNode
    {

        [SerializeField, Min(0f)] private double time;
        [SerializeField, Min(0)] private int laneIndex;

        public double Time => time;
        public int LaneIndex => laneIndex;

    }

    [Serializable]
    public sealed class BananaCurveHandle
    {

        [SerializeField, Range(0f, 1f)] private float normalizedTime = 0.5f;
        [SerializeField, Range(0f, 1f)] private float normalizedX = 0.5f;

        public float NormalizedTime => normalizedTime;
        public float NormalizedX => normalizedX;

    }

    [Serializable]
    public sealed class BananaCheckpoint
    {

        [SerializeField, Min(0f)] private double time;
        [SerializeField, Range(0f, 1f)] private float normalizedX = 0.5f;

        public double Time => time;
        public float NormalizedX => normalizedX;

    }

    [Serializable]
    public sealed class ChartNote
    {

        [SerializeField] private string id = "note";
        [SerializeField, Min(0f)] private double hitTime;
        [SerializeField, Min(0)] private int laneIndex;
        [SerializeField] private string musicalPartId = "part";
        [SerializeField] private ChartNoteType noteType;
        [SerializeField, Min(0f)] private double endTime = 1d;
        [SerializeField, Min(0)] private int endLaneIndex;
        [SerializeField] private SlideEndBehavior slideEndBehavior;
        [SerializeField] private List<ChartPathNode> slideNodes = new();
        [SerializeField] private List<BananaCurveHandle> bananaCurveHandles = new();
        [SerializeField] private List<BananaCheckpoint> bananaCheckpoints = new();
        [SerializeField, Min(0)] private int bananaMaximumBonusCombo = 4;

        public string Id => id;
        public double HitTime => hitTime;
        public int LaneIndex => laneIndex;
        public string MusicalPartId => musicalPartId;
        public ChartNoteType NoteType => noteType;
        public double AuthoredEndTime => endTime;
        public int AuthoredEndLaneIndex => endLaneIndex;
        public SlideEndBehavior SlideEndBehavior => slideEndBehavior;
        public IReadOnlyList<ChartPathNode> SlideNodes => slideNodes;
        public IReadOnlyList<BananaCurveHandle> BananaCurveHandles => bananaCurveHandles;
        public IReadOnlyList<BananaCheckpoint> BananaCheckpoints => bananaCheckpoints;
        public int BananaMaximumBonusCombo => bananaMaximumBonusCombo;

        public double EndTime
        {

            get
            {

                if (noteType == ChartNoteType.Slide && slideNodes != null && slideNodes.Count > 0)
                {

                    return slideNodes[^1].Time;

                }

                return noteType == ChartNoteType.Hold || noteType == ChartNoteType.Banana
                    ? endTime
                    : hitTime;

            }

        }

        public int EndLaneIndex
        {

            get
            {

                if (noteType == ChartNoteType.Slide && slideNodes != null && slideNodes.Count > 0)
                {

                    return slideNodes[^1].LaneIndex;

                }

                if (noteType == ChartNoteType.Flick || noteType == ChartNoteType.Banana)
                {

                    return endLaneIndex;

                }

                return laneIndex;

            }

        }

    }

    [CreateAssetMenu(fileName = "PrototypeChart", menuName = "Idiot Tape/Prototype Chart")]
    public sealed class PrototypeChart : ScriptableObject
    {

        [SerializeField] private string songEventPath = "event:/Music/Idiotape/Pluto";
        [SerializeField] private List<FmodStemDefinition> stemParameters = new();
        [SerializeField] private List<ChartTempoSection> tempoSections = new();
        [SerializeField, Min(2)] private int laneCount = 8;
        [SerializeField, Min(0.1f)] private float visualLeadTime = 2.4f;
        [SerializeField] private List<MusicalPartDefinition> musicalParts = new();
        [SerializeField] private List<MusicalPartActivationWindow> activationWindows = new();
        [SerializeField] private List<ChartNote> notes = new();

        public string SongEventPath => songEventPath;
        public IReadOnlyList<FmodStemDefinition> StemParameters => stemParameters;
        public IReadOnlyList<ChartTempoSection> TempoSections => tempoSections;
        public int LaneCount => laneCount;
        public float VisualLeadTime => visualLeadTime;
        public IReadOnlyList<MusicalPartDefinition> MusicalParts => musicalParts;
        public IReadOnlyList<MusicalPartActivationWindow> ActivationWindows => activationWindows;
        public IReadOnlyList<ChartNote> Notes => notes;

        public double Duration
        {

            get
            {

                double latestEndTime = 0d;

                for (int index = 0; index < notes.Count; index++)
                {

                    if (notes[index] != null)
                    {

                        latestEndTime = Math.Max(latestEndTime, notes[index].EndTime);

                    }

                }

                return notes.Count == 0 ? 0d : latestEndTime + 1d;

            }

        }

        public Color GetPartColor(string partId)
        {

            for (int index = 0; index < musicalParts.Count; index++)
            {

                if (musicalParts[index].Id == partId)
                {

                    return musicalParts[index].Color;

                }

            }

            return Color.white;

        }

        public string GetPartDisplayName(string partId)
        {

            for (int index = 0; index < musicalParts.Count; index++)
            {

                if (musicalParts[index].Id == partId)
                {

                    return musicalParts[index].DisplayName;

                }

            }

            return partId;

        }

        public bool IsPartActive(string partId, double songTime)
        {

            for (int index = 0; index < activationWindows.Count; index++)
            {

                MusicalPartActivationWindow window = activationWindows[index];

                if (window.MusicalPartId == partId && window.Contains(songTime))
                {

                    return true;

                }

            }

            return false;

        }

        public bool IsNotePlayable(ChartNote note)
        {

            return note != null && IsPartActive(note.MusicalPartId, note.HitTime);

        }

        public bool TryValidate(out string error)
        {

            if (string.IsNullOrWhiteSpace(songEventPath))
            {

                error = "An FMOD song event path is required.";
                return false;

            }

            HashSet<string> stemIds = new();

            for (int index = 0; index < stemParameters.Count; index++)
            {

                FmodStemDefinition stem = stemParameters[index];

                if (stem == null || string.IsNullOrWhiteSpace(stem.StemId) || !stemIds.Add(stem.StemId))
                {

                    error = $"Stem parameter at index {index} has an empty or duplicate ID.";
                    return false;

                }

                if (string.IsNullOrWhiteSpace(stem.ParameterName))
                {

                    error = $"Stem '{stem.StemId}' has an empty FMOD parameter name.";
                    return false;

                }

            }

            if (laneCount < 2)
            {

                error = "Lane count must be at least 2.";
                return false;

            }

            int previousStartBar = 0;
            double previousStartTime = -1d;

            for (int index = 0; tempoSections != null && index < tempoSections.Count; index++)
            {

                ChartTempoSection section = tempoSections[index];

                if (section == null ||
                    section.StartBar < 1 ||
                    section.StartTime < 0d ||
                    section.BeatsPerMinute <= 0d ||
                    section.BeatsPerBar < 1 ||
                    section.BeatUnit < 1)
                {

                    error = $"Tempo section {index} has invalid values.";
                    return false;

                }

                if (section.StartBar <= previousStartBar || section.StartTime <= previousStartTime)
                {

                    error = $"Tempo section {index} is out of order.";
                    return false;

                }

                previousStartBar = section.StartBar;
                previousStartTime = section.StartTime;

            }

            double previousTime = double.NegativeInfinity;
            HashSet<string> noteIds = new();
            HashSet<string> partIds = new();

            for (int index = 0; index < musicalParts.Count; index++)
            {

                MusicalPartDefinition part = musicalParts[index];

                if (string.IsNullOrWhiteSpace(part.Id) || !partIds.Add(part.Id))
                {

                    error = $"Musical part at index {index} has an empty or duplicate ID.";
                    return false;

                }

                if (string.IsNullOrWhiteSpace(part.DisplayName))
                {

                    error = $"Musical part '{part.Id}' has an empty display name.";
                    return false;

                }

            }

            for (int index = 0; index < activationWindows.Count; index++)
            {

                MusicalPartActivationWindow window = activationWindows[index];

                if (!partIds.Contains(window.MusicalPartId))
                {

                    error = $"Activation window {index} references unknown part '{window.MusicalPartId}'.";
                    return false;

                }

                if (window.StartTime < 0d || window.EndTime <= window.StartTime)
                {

                    error = $"Activation window {index} has an invalid time range.";
                    return false;

                }

                for (int previousIndex = 0; previousIndex < index; previousIndex++)
                {

                    MusicalPartActivationWindow previousWindow = activationWindows[previousIndex];

                    if (previousWindow.MusicalPartId == window.MusicalPartId &&
                        previousWindow.StartTime < window.EndTime &&
                        window.StartTime < previousWindow.EndTime)
                    {

                        error =
                            $"Activation window {index} overlaps window {previousIndex} " +
                            $"for part '{window.MusicalPartId}'.";
                        return false;

                    }

                }

            }

            for (int index = 0; index < notes.Count; index++)
            {

                ChartNote note = notes[index];

                if (note == null)
                {

                    error = $"Note at index {index} is null.";
                    return false;

                }

                if (string.IsNullOrWhiteSpace(note.Id) || !noteIds.Add(note.Id))
                {

                    error = $"Note at index {index} has an empty or duplicate ID.";
                    return false;

                }

                if (!IsFinite(note.HitTime) || note.HitTime < 0d)
                {

                    error = $"Note '{note.Id}' has an invalid hit time.";
                    return false;

                }

                if (note.HitTime < previousTime)
                {

                    error = $"Note '{note.Id}' is out of time order.";
                    return false;

                }

                if (note.LaneIndex < 0 || note.LaneIndex >= laneCount)
                {

                    error = $"Note '{note.Id}' has lane {note.LaneIndex}, outside 0..{laneCount - 1}.";
                    return false;

                }

                if (!partIds.Contains(note.MusicalPartId))
                {

                    error = $"Note '{note.Id}' references unknown part '{note.MusicalPartId}'.";
                    return false;

                }

                if (!Enum.IsDefined(typeof(ChartNoteType), note.NoteType))
                {

                    error = $"Note '{note.Id}' has unsupported type '{note.NoteType}'.";
                    return false;

                }

                if (!TryValidateInteraction(note, laneCount, tempoSections.Count > 0, out error))
                {

                    return false;

                }

                previousTime = note.HitTime;

            }

            error = string.Empty;
            return true;

        }

        private static bool TryValidateInteraction(
            ChartNote note,
            int chartLaneCount,
            bool hasTempoMap,
            out string error)
        {

            switch (note.NoteType)
            {

                case ChartNoteType.Tap:
                    error = string.Empty;
                    return true;

                case ChartNoteType.Hold:
                    if (!hasTempoMap)
                    {

                        error = $"Hold note '{note.Id}' requires a tempo map.";
                        return false;

                    }

                    if (!IsFinite(note.AuthoredEndTime) || note.AuthoredEndTime <= note.HitTime)
                    {

                        error = $"Hold note '{note.Id}' has an invalid end time.";
                        return false;

                    }

                    error = string.Empty;
                    return true;

                case ChartNoteType.Slide:
                    return TryValidateSlide(note, chartLaneCount, hasTempoMap, out error);

                case ChartNoteType.Flick:
                    if (note.AuthoredEndLaneIndex < 0 || note.AuthoredEndLaneIndex >= chartLaneCount)
                    {

                        error =
                            $"Flick note '{note.Id}' has end lane {note.AuthoredEndLaneIndex}, " +
                            $"outside 0..{chartLaneCount - 1}.";
                        return false;

                    }

                    if (note.AuthoredEndLaneIndex == note.LaneIndex)
                    {

                        error = $"Flick note '{note.Id}' must end in a different lane.";
                        return false;

                    }

                    error = string.Empty;
                    return true;

                case ChartNoteType.Banana:
                    return TryValidateBanana(note, chartLaneCount, out error);

                default:
                    error = $"Note '{note.Id}' has unsupported type '{note.NoteType}'.";
                    return false;

            }

        }

        private static bool TryValidateSlide(
            ChartNote note,
            int chartLaneCount,
            bool hasTempoMap,
            out string error)
        {

            if (!hasTempoMap)
            {

                error = $"Slide note '{note.Id}' requires a tempo map.";
                return false;

            }

            if (!Enum.IsDefined(typeof(SlideEndBehavior), note.SlideEndBehavior))
            {

                error = $"Slide note '{note.Id}' has an unsupported end behavior.";
                return false;

            }

            if (note.SlideNodes == null || note.SlideNodes.Count == 0)
            {

                error = $"Slide note '{note.Id}' requires at least one path node.";
                return false;

            }

            double previousTime = note.HitTime;
            int previousLane = note.LaneIndex;
            bool hasLaneChange = false;

            for (int index = 0; index < note.SlideNodes.Count; index++)
            {

                ChartPathNode node = note.SlideNodes[index];

                if (node == null || !IsFinite(node.Time) || node.Time <= previousTime)
                {

                    error = $"Slide note '{note.Id}' has an invalid node at index {index}.";
                    return false;

                }

                if (node.LaneIndex < 0 || node.LaneIndex >= chartLaneCount)
                {

                    error =
                        $"Slide note '{note.Id}' node {index} has lane {node.LaneIndex}, " +
                        $"outside 0..{chartLaneCount - 1}.";
                    return false;

                }

                hasLaneChange |= node.LaneIndex != previousLane;
                previousTime = node.Time;
                previousLane = node.LaneIndex;

            }

            if (!hasLaneChange)
            {

                error = $"Slide note '{note.Id}' has no lane transition; author it as a hold.";
                return false;

            }

            if (note.SlideEndBehavior == SlideEndBehavior.Flick)
            {

                int flickStartLane = note.SlideNodes.Count > 1
                    ? note.SlideNodes[^2].LaneIndex
                    : note.LaneIndex;

                if (flickStartLane == note.SlideNodes[^1].LaneIndex)
                {

                    error = $"Slide note '{note.Id}' terminal flick requires a lane transition.";
                    return false;

                }

            }

            error = string.Empty;
            return true;

        }

        private static bool TryValidateBanana(
            ChartNote note,
            int chartLaneCount,
            out string error)
        {

            if (!IsFinite(note.AuthoredEndTime) || note.AuthoredEndTime <= note.HitTime)
            {

                error = $"Banana note '{note.Id}' has an invalid end time.";
                return false;

            }

            if (note.AuthoredEndLaneIndex < 0 || note.AuthoredEndLaneIndex >= chartLaneCount)
            {

                error =
                    $"Banana note '{note.Id}' has end lane {note.AuthoredEndLaneIndex}, " +
                    $"outside 0..{chartLaneCount - 1}.";
                return false;

            }

            if (note.BananaCurveHandles == null ||
                note.BananaCurveHandles.Count < 1 ||
                note.BananaCurveHandles.Count > 2)
            {

                error = $"Banana note '{note.Id}' requires one or two curve handles.";
                return false;

            }

            float previousNormalizedTime = 0f;

            for (int index = 0; index < note.BananaCurveHandles.Count; index++)
            {

                BananaCurveHandle handle = note.BananaCurveHandles[index];

                if (handle == null ||
                    handle.NormalizedTime <= previousNormalizedTime ||
                    handle.NormalizedTime >= 1f ||
                    handle.NormalizedX < 0f ||
                    handle.NormalizedX > 1f)
                {

                    error = $"Banana note '{note.Id}' has an invalid curve handle at index {index}.";
                    return false;

                }

                previousNormalizedTime = handle.NormalizedTime;

            }

            if (note.BananaCheckpoints == null || note.BananaCheckpoints.Count == 0)
            {

                error = $"Banana note '{note.Id}' requires at least one checkpoint.";
                return false;

            }

            double previousCheckpointTime = note.HitTime;

            for (int index = 0; index < note.BananaCheckpoints.Count; index++)
            {

                BananaCheckpoint checkpoint = note.BananaCheckpoints[index];

                if (checkpoint == null ||
                    !IsFinite(checkpoint.Time) ||
                    checkpoint.Time <= previousCheckpointTime ||
                    checkpoint.Time >= note.AuthoredEndTime ||
                    checkpoint.NormalizedX < 0f ||
                    checkpoint.NormalizedX > 1f)
                {

                    error = $"Banana note '{note.Id}' has an invalid checkpoint at index {index}.";
                    return false;

                }

                previousCheckpointTime = checkpoint.Time;

            }

            if (note.BananaMaximumBonusCombo < 0)
            {

                error = $"Banana note '{note.Id}' has a negative maximum bonus combo.";
                return false;

            }

            error = string.Empty;
            return true;

        }

        private static bool IsFinite(double value)
        {

            return !double.IsNaN(value) && !double.IsInfinity(value);

        }

    }

}
