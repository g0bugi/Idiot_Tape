using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class SnowNoteTestChartTests
    {

        private const string ChartPath = "Assets/Data/SnowNoteTestChart.asset";

        [Test]
        public void ImportedChartIsValidAndEveryInteractionIsPlayable()
        {

            AssetDatabase.ImportAsset(ChartPath, ImportAssetOptions.ForceSynchronousImport);
            PrototypeChart chart = AssetDatabase.LoadAssetAtPath<PrototypeChart>(ChartPath);
            Assert.That(chart, Is.Not.Null);
            Assert.That(chart.TryValidate(out string error), Is.True, error);
            int[] counts = new int[5];
            bool normalSlide = false;
            bool flickSlide = false;

            foreach (ChartNote note in chart.Notes)
            {

                Assert.That(chart.IsNotePlayable(note), Is.True, note.Id);
                counts[(int)note.NoteType]++;

                if (note.NoteType == ChartNoteType.Slide)
                {

                    normalSlide |= note.SlideEndBehavior == SlideEndBehavior.Normal;
                    flickSlide |= note.SlideEndBehavior == SlideEndBehavior.Flick;

                }

                // Sustained paths plus a second hand must never require a third contact.
                int concurrent = 0;

                foreach (ChartNote other in chart.Notes)
                {

                    if (other.HitTime <= note.HitTime && other.EndTime >= note.HitTime)
                    {

                        concurrent++;

                    }

                }

                Assert.That(concurrent, Is.LessThanOrEqualTo(2), note.Id);

            }

            Assert.That(counts, Has.All.GreaterThan(0));
            Assert.That(normalSlide && flickSlide, Is.True);

        }

        [Test]
        public void SerializedRoundTripPreservesTimingAndBananaCheckpointsMatchVisibleCurve()
        {

            PrototypeChart chart = AssetDatabase.LoadAssetAtPath<PrototypeChart>(ChartPath);
            Assert.That(chart, Is.Not.Null);
            PrototypeChart copy = ScriptableObject.CreateInstance<PrototypeChart>();

            try
            {

                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(chart), copy);
                Assert.That(copy.TryValidate(out string error), Is.True, error);
                Assert.That(copy.Notes.Count, Is.EqualTo(chart.Notes.Count));

                for (int index = 0; index < copy.Notes.Count; index++)
                {

                    ChartNote note = copy.Notes[index];
                    Assert.That(note.Id, Is.EqualTo(chart.Notes[index].Id));
                    Assert.That(note.HitTime, Is.EqualTo(chart.Notes[index].HitTime));
                    Assert.That(note.EndTime, Is.EqualTo(chart.Notes[index].EndTime));

                    foreach (BananaCheckpoint checkpoint in note.BananaCheckpoints)
                    {

                        float visibleX = NotePathMath.GetBananaNormalizedX(
                            note, checkpoint.Time, copy.LaneCount);
                        Assert.That(checkpoint.NormalizedX, Is.EqualTo(visibleX).Within(0.0001f), note.Id);

                    }

                }

            }
            finally
            {

                Object.DestroyImmediate(copy);

            }

        }

    }

}
