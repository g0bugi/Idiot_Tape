using NUnit.Framework;
using UnityEngine;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class GameplayPerformanceTests
    {

        [Test]
        public void PartTotalsIncludeInheritedRewardsButBonusComboDoesNotInventJudgements()
        {

            GameplayPerformance performance = new();
            performance.Record("drum", JudgementGrade.Perfect, 1);
            performance.Record("bass", JudgementGrade.Good, 2);
            performance.Record("bass", JudgementGrade.Good, 3);
            performance.Record("bass", JudgementGrade.Good, 4);
            performance.ObserveCombo(8);
            performance.Record("drum", JudgementGrade.Miss, 0);
            performance.Record("bass", JudgementGrade.Perfect, 1);
            Assert.That(performance.Total.Perfect, Is.EqualTo(2));
            Assert.That(performance.Total.Good, Is.EqualTo(3));
            Assert.That(performance.Total.Miss, Is.EqualTo(1));
            Assert.That(performance.Total.Total, Is.EqualTo(6));
            Assert.That(performance.GetPart("drum").Total, Is.EqualTo(2));
            Assert.That(performance.GetPart("bass").Total, Is.EqualTo(4));
            Assert.That(performance.GetPart("inactive"), Is.Null);
            Assert.That(performance.MaximumCombo, Is.EqualTo(8));

        }

        [Test]
        public void RetryClearsPartsMaximumAndCountsAndNoneDoesNotCreateAPart()
        {

            GameplayPerformance performance = new();
            performance.Record("synth", JudgementGrade.Perfect, 42);
            performance.Reset();
            performance.Record("inactive", JudgementGrade.None, 100);
            Assert.That(performance.Total.Total, Is.Zero);
            Assert.That(performance.MaximumCombo, Is.Zero);
            Assert.That(performance.GetPart("synth"), Is.Null);
            Assert.That(performance.GetPart("inactive"), Is.Null);

        }

        [Test]
        public void OlderChartsKeepTheirNameWhenPresentationMetadataIsAbsent()
        {

            PrototypeChart chart = ScriptableObject.CreateInstance<PrototypeChart>();

            try
            {

                chart.name = "Older Chart";
                Assert.That(chart.SongTitle, Is.EqualTo("Older Chart"));
                Assert.That(chart.ArtistName, Is.Empty);
                JsonUtility.FromJsonOverwrite("{\"songTitle\":\"Snow\",\"artistName\":\"KIRARA\"}", chart);
                Assert.That(chart.SongTitle, Is.EqualTo("Snow"));
                Assert.That(chart.ArtistName, Is.EqualTo("KIRARA"));

            }
            finally
            {

                Object.DestroyImmediate(chart);

            }

        }

    }

}
