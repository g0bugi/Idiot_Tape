using System.Collections.Generic;

namespace IdiotTape.Gameplay
{

    public sealed class JudgementCounts
    {

        public int Perfect { get; private set; }
        public int Good { get; private set; }
        public int Miss { get; private set; }
        public int Total => Perfect + Good + Miss;

        internal void Record(JudgementGrade grade)
        {

            switch (grade)
            {

                case JudgementGrade.Perfect: Perfect++; break;
                case JudgementGrade.Good: Good++; break;
                case JudgementGrade.Miss: Miss++; break;

            }

        }

    }

    // Records emitted judgement events, not authored note counts or banana charge ticks.
    // Score and the current combo remain owned by GameplaySession.
    public sealed class GameplayPerformance
    {

        private readonly Dictionary<string, JudgementCounts> parts = new();

        public JudgementCounts Total { get; private set; } = new();
        public int MaximumCombo { get; private set; }

        public void Reset()
        {

            Total = new JudgementCounts();
            parts.Clear();
            MaximumCombo = 0;

        }

        public void Record(string partId, JudgementGrade grade, int combo)
        {

            if (grade == JudgementGrade.None)
            {

                return;

            }

            if (!parts.TryGetValue(partId, out JudgementCounts counts))
            {

                counts = new JudgementCounts();
                parts.Add(partId, counts);

            }

            counts.Record(grade);
            Total.Record(grade);
            ObserveCombo(combo);

        }

        public void ObserveCombo(int combo)
        {

            if (combo > MaximumCombo)
            {

                MaximumCombo = combo;

            }

        }

        public JudgementCounts GetPart(string partId)
        {

            return parts.TryGetValue(partId, out JudgementCounts counts) ? counts : null;

        }

    }

}
