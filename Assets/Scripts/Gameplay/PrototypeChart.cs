using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdiotTape.Gameplay
{

    [Serializable]
    public sealed class MusicalPartDefinition
    {

        [SerializeField] private string id = "part";
        [SerializeField] private Color color = Color.white;

        public string Id => id;
        public Color Color => color;

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

        [SerializeField] private AudioClip audioClip;
        [SerializeField, Min(2)] private int laneCount = 8;
        [SerializeField, Min(0.1f)] private float visualLeadTime = 2.4f;
        [SerializeField] private List<MusicalPartDefinition> musicalParts = new();
        [SerializeField] private List<ChartNote> notes = new();

        public AudioClip AudioClip => audioClip;
        public int LaneCount => laneCount;
        public float VisualLeadTime => visualLeadTime;
        public IReadOnlyList<ChartNote> Notes => notes;

        public double Duration
        {

            get
            {

                if (audioClip != null)
                {

                    return audioClip.length;

                }

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

        public bool TryValidate(out string error)
        {

            if (laneCount < 2)
            {

                error = "Lane count must be at least 2.";
                return false;

            }

            double previousTime = double.NegativeInfinity;
            HashSet<string> noteIds = new();
            HashSet<string> partIds = new();

            for (int index = 0; index < musicalParts.Count; index++)
            {

                if (string.IsNullOrWhiteSpace(musicalParts[index].Id) || !partIds.Add(musicalParts[index].Id))
                {

                    error = $"Musical part at index {index} has an empty or duplicate ID.";
                    return false;

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

                previousTime = note.HitTime;

            }

            error = string.Empty;
            return true;

        }

    }

}
