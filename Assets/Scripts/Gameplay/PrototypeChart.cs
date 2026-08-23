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

    [Serializable]
    public sealed class ChartNote
    {

        [SerializeField] private string id = "note";
        [SerializeField, Min(0f)] private double hitTime;
        [SerializeField, Min(0)] private int laneIndex;
        [SerializeField] private string musicalPartId = "part";

        public string Id => id;
        public double HitTime => hitTime;
        public int LaneIndex => laneIndex;
        public string MusicalPartId => musicalPartId;

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

                return notes.Count == 0 ? 0d : notes[^1].HitTime + 1d;

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

                if (string.IsNullOrWhiteSpace(note.Id) || !noteIds.Add(note.Id))
                {

                    error = $"Note at index {index} has an empty or duplicate ID.";
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

                if (!IsPartActive(note.MusicalPartId, note.HitTime))
                {

                    error = $"Note '{note.Id}' occurs while part '{note.MusicalPartId}' is inactive.";
                    return false;

                }

                previousTime = note.HitTime;

            }

            error = string.Empty;
            return true;

        }

    }

}
