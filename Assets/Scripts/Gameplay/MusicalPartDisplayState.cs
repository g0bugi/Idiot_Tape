using System.Collections.Generic;
using UnityEngine;

namespace IdiotTape.Gameplay
{

    public sealed class MusicalPartDisplayState
    {

        private readonly List<string> displayedIds = new();
        private readonly List<string> nextIds = new();
        public string DisplayName { get; private set; } = string.Empty;
        public Color Color { get; private set; } = Color.white;

        public void Reset()
        {

            displayedIds.Clear();
            nextIds.Clear();
            DisplayName = string.Empty;
            Color = Color.white;

        }

        public bool Update(PrototypeChart chart, double songTime)
        {

            bool hasActiveWindow = false;
            double lastEnd = double.NegativeInfinity;
            for (int windowIndex = 0; windowIndex < chart.ActivationWindows.Count; windowIndex++)
            {

                MusicalPartActivationWindow window = chart.ActivationWindows[windowIndex];

                hasActiveWindow |= window.Contains(songTime);
                if (window.EndTime <= songTime && window.EndTime > lastEnd)
                {

                    lastEnd = window.EndTime;

                }

            }

            nextIds.Clear();
            for (int partIndex = 0; partIndex < chart.MusicalParts.Count; partIndex++)
            {

                MusicalPartDefinition part = chart.MusicalParts[partIndex];

                for (int windowIndex = 0; windowIndex < chart.ActivationWindows.Count; windowIndex++)
                {

                    MusicalPartActivationWindow window = chart.ActivationWindows[windowIndex];

                    // In a gap, reconstruct the state immediately before the last end.
                    // This also works when a frame hitch skips the entire preceding section.
                    bool visible = hasActiveWindow ? window.Contains(songTime)
                        : window.StartTime < lastEnd && window.EndTime >= lastEnd;
                    if (window.MusicalPartId == part.Id && visible)
                    {

                        nextIds.Add(part.Id);
                        break;

                    }

                }

            }

            bool changed = displayedIds.Count != nextIds.Count;
            for (int index = 0; !changed && index < nextIds.Count; index++)
            {

                changed = displayedIds[index] != nextIds[index];

            }

            if (!changed)
            {

                return false;

            }

            displayedIds.Clear();
            displayedIds.AddRange(nextIds);
            DisplayName = string.Empty;
            Color = nextIds.Count == 1 ? chart.GetPartColor(nextIds[0]) : Color.white;
            foreach (string id in nextIds)
            {

                DisplayName += (DisplayName.Length == 0 ? "" : " + ") + chart.GetPartDisplayName(id);

            }

            return true;

        }

    }

}
