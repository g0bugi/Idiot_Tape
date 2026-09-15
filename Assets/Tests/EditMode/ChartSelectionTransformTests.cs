using System;
using System.Collections.Generic;
using IdiotTape.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class ChartSelectionTransformTests
    {

        private static readonly ChartTempoSection[] ConstantTempo =
        {

            CreateTempo(1, 0d, 120d, 4, 4)

        };

        [TestCase(ChartNoteType.Tap)]
        [TestCase(ChartNoteType.Hold)]
        [TestCase(ChartNoteType.Slide)]
        [TestCase(ChartNoteType.Flick)]
        [TestCase(ChartNoteType.Banana)]
        public void EveryInteractionMovesItsEntireAuthoredShapeWithoutChangingSource(ChartNoteType type)
        {

            ChartNoteAuthoringData note = CreateNote(type);
            string before = JsonUtility.ToJson(note);

            Assert.That(ChartSelectionTransform.TryTransform(
                new[] { note }, ConstantTempo, 8, 0.125d, 1d, 1, "bass",
                out List<ChartNoteAuthoringData> result, out string error), Is.True, error);

            ChartNoteAuthoringData moved = result[0];
            Assert.That(moved.Id, Is.EqualTo(note.Id));
            Assert.That(moved.NoteType, Is.EqualTo(type));
            Assert.That(moved.MusicalPartId, Is.EqualTo("bass"));
            Assert.That(moved.HitTime, Is.EqualTo(note.HitTime + 0.625d).Within(1e-9d));
            Assert.That(moved.EndTime, Is.EqualTo(note.EndTime + 0.625d).Within(1e-9d));
            Assert.That(moved.LaneIndex, Is.EqualTo(note.LaneIndex + 1));
            Assert.That(moved.EndLaneIndex, Is.EqualTo(note.EndLaneIndex + 1));
            Assert.That(moved.SlideEndBehavior, Is.EqualTo(note.SlideEndBehavior));
            Assert.That(moved.BananaMaximumBonusCombo, Is.EqualTo(note.BananaMaximumBonusCombo));
            Assert.That(moved.SlideNodes.Count, Is.EqualTo(note.SlideNodes.Count));
            Assert.That(moved.BananaCurveHandles.Count, Is.EqualTo(note.BananaCurveHandles.Count));
            Assert.That(moved.BananaCheckpoints.Count, Is.EqualTo(note.BananaCheckpoints.Count));

            for (int index = 0; index < note.SlideNodes.Count; index++)
            {

                Assert.That(moved.SlideNodes[index], Is.Not.SameAs(note.SlideNodes[index]));
                Assert.That(moved.SlideNodes[index].Time,
                    Is.EqualTo(note.SlideNodes[index].Time + 0.625d).Within(1e-9d));
                Assert.That(moved.SlideNodes[index].LaneIndex, Is.EqualTo(note.SlideNodes[index].LaneIndex + 1));

            }

            for (int index = 0; index < note.BananaCurveHandles.Count; index++)
            {

                Assert.That(moved.BananaCurveHandles[index], Is.Not.SameAs(note.BananaCurveHandles[index]));
                Assert.That(moved.BananaCurveHandles[index].NormalizedTime,
                    Is.EqualTo(note.BananaCurveHandles[index].NormalizedTime));
                Assert.That(moved.BananaCurveHandles[index].NormalizedX,
                    Is.EqualTo(note.BananaCurveHandles[index].NormalizedX + 0.125f).Within(1e-6f));

            }

            for (int index = 0; index < note.BananaCheckpoints.Count; index++)
            {

                Assert.That(moved.BananaCheckpoints[index], Is.Not.SameAs(note.BananaCheckpoints[index]));
                Assert.That(moved.BananaCheckpoints[index].Time,
                    Is.EqualTo(note.BananaCheckpoints[index].Time + 0.625d).Within(1e-9d));
                Assert.That(moved.BananaCheckpoints[index].NormalizedX,
                    Is.EqualTo(note.BananaCheckpoints[index].NormalizedX + 0.125f).Within(1e-6f));

            }

            Assert.That(JsonUtility.ToJson(note), Is.EqualTo(before));

        }

        [TestCase(1.75d, 1d, 2.5d)]
        [TestCase(2.5d, -1d, 1.75d)]
        [TestCase(2d, -1d, 1.5d)]
        public void MusicalMovementAcrossTempoBoundaryUsesEarliestAnchorAndPreservesAbsoluteSpacing(
            double anchor,
            double quarterBeatOffset,
            double expectedAnchor)
        {

            ChartTempoSection[] tempo =
            {

                CreateTempo(1, 0d, 120d, 4, 4),
                CreateTempo(2, 2d, 60d, 4, 4)

            };
            ChartNoteAuthoringData early = CreateNote(ChartNoteType.Banana);
            early.ShiftTimes(anchor - early.HitTime);
            ChartNoteAuthoringData late = ChartNoteAuthoringData.CreateTap("late", anchor + 0.75d, 2, "synth");

            Assert.That(ChartSelectionTransform.TryTransform(
                new[] { late, early }, tempo, 8, 0d, quarterBeatOffset, 0, null,
                out List<ChartNoteAuthoringData> result, out string error), Is.True, error);

            Assert.That(result[1].HitTime, Is.EqualTo(expectedAnchor).Within(1e-9d));
            Assert.That(result[0].HitTime, Is.EqualTo(expectedAnchor + 0.75d).Within(1e-9d));
            Assert.That(result[1].EndTime - result[1].HitTime, Is.EqualTo(2d).Within(1e-9d));
            Assert.That(result[1].BananaCheckpoints[0].Time - result[1].HitTime,
                Is.EqualTo(0.5d).Within(1e-9d));
            Assert.That(result[0].Id, Is.EqualTo("late"));
            Assert.That(result[1].MusicalPartId, Is.EqualTo("synth"));

        }

        [TestCase(4, 4)]
        [TestCase(6, 8)]
        [TestCase(2, 2)]
        public void QuarterBeatOffsetUsesQuarterNotesAcrossDifferentBeatUnits(int beatsPerBar, int beatUnit)
        {

            ChartTempoSection[] tempo = { CreateTempo(1, 0.5d, 120d, beatsPerBar, beatUnit) };
            ChartNoteAuthoringData note = ChartNoteAuthoringData.CreateTap("tap", 0.125d, 2, "synth");

            Assert.That(ChartSelectionTransform.TryTransform(
                new[] { note }, tempo, 8, 0d, 0.25d, 0, null,
                out List<ChartNoteAuthoringData> result, out string error), Is.True, error);

            Assert.That(result[0].HitTime, Is.EqualTo(0.25d).Within(1e-9d),
                "An eighth-note beat unit must not halve a quarter-note-based nudge.");

        }

        [Test]
        public void MusicalMovementTraversesTempoAndMeterChangesWithoutAddingBarGaps()
        {

            ChartTempoSection[] tempo =
            {

                CreateTempo(1, 0d, 120d, 4, 4),
                CreateTempo(2, 2d, 60d, 6, 8),
                CreateTempo(3, 5d, 240d, 2, 2)

            };
            ChartNoteAuthoringData note = ChartNoteAuthoringData.CreateTap("tap", 1.75d, 2, "synth");

            Assert.That(ChartSelectionTransform.TryTransform(
                new[] { note }, tempo, 8, 0d, 4d, 0, null,
                out List<ChartNoteAuthoringData> result, out string error), Is.True, error);

            Assert.That(result[0].HitTime, Is.EqualTo(5.125d).Within(1e-9d));

        }

        [TestCase("start-lane")]
        [TestCase("flick-end")]
        [TestCase("slide-node")]
        [TestCase("banana-end")]
        [TestCase("banana-handle")]
        [TestCase("banana-checkpoint")]
        public void OneOutOfBoundsInteractionRejectsTheWholeBatchWithoutClamping(string boundary)
        {

            ChartNoteAuthoringData valid = CreateNote(ChartNoteType.Hold);
            ChartNoteAuthoringData invalid = CreateNote(ChartNoteType.Banana);

            switch (boundary)
            {

                case "start-lane":
                    invalid.LaneIndex = 7;
                    break;
                case "flick-end":
                    invalid = CreateNote(ChartNoteType.Flick);
                    invalid.EndLaneIndex = 7;
                    break;
                case "slide-node":
                    invalid = CreateNote(ChartNoteType.Slide);
                    invalid.SlideNodes[0].LaneIndex = 7;
                    break;
                case "banana-end":
                    invalid.EndLaneIndex = 7;
                    break;
                case "banana-handle":
                    invalid.BananaCurveHandles[0].NormalizedX = 0.95f;
                    break;
                case "banana-checkpoint":
                    invalid.BananaCheckpoints[0].NormalizedX = 0.95f;
                    break;

            }

            string validBefore = JsonUtility.ToJson(valid);
            string invalidBefore = JsonUtility.ToJson(invalid);

            Assert.That(ChartSelectionTransform.TryTransform(
                new[] { valid, invalid }, ConstantTempo, 8, 0.5d, 0d, 1, "bass",
                out List<ChartNoteAuthoringData> result, out string error), Is.False);

            Assert.That(result, Is.Null);
            Assert.That(error, Does.Contain("선택 노트 2"));
            Assert.That(error, Does.Contain(invalid.Id));
            Assert.That(JsonUtility.ToJson(valid), Is.EqualTo(validBefore));
            Assert.That(JsonUtility.ToJson(invalid), Is.EqualTo(invalidBefore));

        }

        [TestCase(-2.01d, 0d)]
        [TestCase(0d, -4.01d)]
        [TestCase(double.NaN, 0d)]
        [TestCase(0d, double.PositiveInfinity)]
        public void NegativeOrNonFiniteTimingFailsWithoutChangingSource(double seconds, double quarterBeats)
        {

            ChartNoteAuthoringData note = CreateNote(ChartNoteType.Banana);
            string before = JsonUtility.ToJson(note);

            Assert.That(ChartSelectionTransform.TryTransform(
                new[] { note }, ConstantTempo, 8, seconds, quarterBeats, 0, null,
                out List<ChartNoteAuthoringData> result, out string error), Is.False);

            Assert.That(result, Is.Null);
            Assert.That(error, Is.Not.Empty);
            Assert.That(JsonUtility.ToJson(note), Is.EqualTo(before));

        }

        [Test]
        public void ExactTimeAndLaneBoundariesAreAllowedAndLargeOffsetsCannotOverflow()
        {

            ChartNoteAuthoringData note = CreateNote(ChartNoteType.Hold);

            Assert.That(ChartSelectionTransform.TryTransform(
                new[] { note }, ConstantTempo, 8, -2d, 0d, -2, null,
                out List<ChartNoteAuthoringData> result, out string error), Is.True, error);
            Assert.That(result[0].HitTime, Is.Zero);
            Assert.That(result[0].LaneIndex, Is.Zero);

            Assert.That(ChartSelectionTransform.TryTransform(
                new[] { note }, ConstantTempo, 8, 0d, 0d, int.MaxValue, null,
                out result, out error), Is.False);
            Assert.That(result, Is.Null);

        }

        [Test]
        public void LegacyTapUnusedEndFieldsDoNotPreventAnEarlierMoveWithoutTempo()
        {

            ChartNoteAuthoringData note = ChartNoteAuthoringData.CreateTap("legacy", 2d, 2, "unknown-part");
            note.EndTime = 0d;
            note.EndLaneIndex = 0;

            Assert.That(ChartSelectionTransform.TryTransform(
                new[] { note }, null, 8, -0.5d, 0d, -1, null,
                out List<ChartNoteAuthoringData> result, out string error), Is.True, error);

            Assert.That(result[0].HitTime, Is.EqualTo(1.5d));
            Assert.That(result[0].LaneIndex, Is.EqualTo(1));
            Assert.That(result[0].MusicalPartId, Is.EqualTo("unknown-part"));
            Assert.That(note.HitTime, Is.EqualTo(2d));

        }

        [Test]
        public void PartOnlyEditPreservesAllTimesAndClonesNestedPathData()
        {

            ChartNoteAuthoringData note = CreateNote(ChartNoteType.Banana);

            Assert.That(ChartSelectionTransform.TryTransform(
                new[] { note }, null, 8, 0d, 0d, 0, "drums",
                out List<ChartNoteAuthoringData> result, out string error), Is.True, error);

            Assert.That(result[0].HitTime, Is.EqualTo(note.HitTime));
            Assert.That(result[0].EndTime, Is.EqualTo(note.EndTime));
            Assert.That(result[0].BananaCheckpoints[1].Time, Is.EqualTo(note.BananaCheckpoints[1].Time));
            result[0].BananaCheckpoints[0].Time = 3d;
            result[0].BananaCurveHandles[0].NormalizedX = 0.1f;
            Assert.That(note.BananaCheckpoints[0].Time, Is.EqualTo(2.5d));
            Assert.That(note.BananaCurveHandles[0].NormalizedX, Is.EqualTo(0.5f));
            Assert.That(note.MusicalPartId, Is.EqualTo("synth"));

        }

        [Test]
        public void MissingTempoRejectsMusicalMovementAndSustainedInteractions()
        {

            ChartNoteAuthoringData tap = CreateNote(ChartNoteType.Tap);
            ChartNoteAuthoringData hold = CreateNote(ChartNoteType.Hold);

            Assert.That(ChartSelectionTransform.TryTransform(
                new[] { tap }, null, 8, 0d, 1d, 0, null, out _, out string error), Is.False);
            Assert.That(error, Does.Contain("템포"));
            Assert.That(ChartSelectionTransform.TryTransform(
                new[] { hold }, null, 8, 0.5d, 0d, 0, null, out _, out error), Is.False);
            Assert.That(error, Does.Contain("템포"));

        }

        [Test]
        public void InvalidTempoAndDamagedPathAreRejectedBeforeCloning()
        {

            ChartNoteAuthoringData note = CreateNote(ChartNoteType.Banana);
            ChartTempoSection[] invalidTempo = { CreateTempo(1, 0d, 0d, 4, 4) };

            Assert.That(ChartSelectionTransform.TryTransform(
                new[] { note }, invalidTempo, 8, 0d, 1d, 0, null, out _, out string error), Is.False);
            Assert.That(error, Does.Contain("템포"));

            note.BananaCheckpoints.Add(null);
            Assert.That(ChartSelectionTransform.TryTransform(
                new[] { note }, ConstantTempo, 8, 0d, 0d, 0, null, out _, out error), Is.False);
            Assert.That(error, Does.Contain("체크포인트"));

        }

        private static ChartNoteAuthoringData CreateNote(ChartNoteType type)
        {

            ChartNoteAuthoringData note = ChartNoteAuthoringData.CreateTap(type.ToString(), 2d, 2, "synth");
            note.NoteType = type;

            if (type == ChartNoteType.Hold || type == ChartNoteType.Slide || type == ChartNoteType.Banana)
            {

                note.EndTime = 4d;

            }

            if (type == ChartNoteType.Flick || type == ChartNoteType.Slide || type == ChartNoteType.Banana)
            {

                note.EndLaneIndex = 4;

            }

            if (type == ChartNoteType.Slide)
            {

                note.SlideEndBehavior = SlideEndBehavior.Flick;
                note.SlideNodes.Add(new ChartNoteAuthoringData.PathNodeData { Time = 3d, LaneIndex = 3 });
                note.SlideNodes.Add(new ChartNoteAuthoringData.PathNodeData { Time = 4d, LaneIndex = 4 });

            }

            if (type == ChartNoteType.Banana)
            {

                note.BananaMaximumBonusCombo = 7;
                note.BananaCurveHandles.Add(new ChartNoteAuthoringData.CurveHandleData
                {

                    NormalizedTime = 0.5f,
                    NormalizedX = 0.5f

                });
                note.BananaCheckpoints.Add(new ChartNoteAuthoringData.CheckpointData
                {

                    Time = 2.5d,
                    NormalizedX = 0.35f

                });
                note.BananaCheckpoints.Add(new ChartNoteAuthoringData.CheckpointData
                {

                    Time = 3.5d,
                    NormalizedX = 0.55f

                });

            }

            return note;

        }

        private static ChartTempoSection CreateTempo(
            int startBar,
            double startTime,
            double beatsPerMinute,
            int beatsPerBar,
            int beatUnit)
        {

            string json = FormattableString.Invariant(
                $"{{\"startBar\":{startBar},\"startTime\":{startTime},\"beatsPerMinute\":{beatsPerMinute},\"beatsPerBar\":{beatsPerBar},\"beatUnit\":{beatUnit}}}");
            return JsonUtility.FromJson<ChartTempoSection>(json);

        }

    }

}
