using IdiotTape.EditorTools;
using NUnit.Framework;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class ChartInteractionRecordingUtilityTests
    {

        [Test]
        public void SameLaneSequenceCreatesHold()
        {

            ChartInteractionRecordingUtility.RecordSlideOrHoldLane(
                null,
                2,
                1d,
                "part",
                out ChartNoteAuthoringData pending,
                out ChartNoteAuthoringData completed);

            Assert.That(completed, Is.Null);
            Assert.That(pending.NoteType, Is.EqualTo(ChartNoteType.Hold));

            ChartInteractionRecordingUtility.RecordSlideOrHoldLane(
                pending,
                2,
                2d,
                "part",
                out pending,
                out completed);

            Assert.That(pending, Is.Null);
            Assert.That(completed.NoteType, Is.EqualTo(ChartNoteType.Hold));
            Assert.That(completed.HitTime, Is.EqualTo(1d));
            Assert.That(completed.EndTime, Is.EqualTo(2d));
            Assert.That(completed.LaneIndex, Is.EqualTo(2));
            Assert.That(completed.EndLaneIndex, Is.EqualTo(2));

        }

        [Test]
        public void LaneSequenceCreatesNormalSlideWithExplicitCloseNode()
        {

            ChartInteractionRecordingUtility.RecordSlideOrHoldLane(
                null,
                2,
                1d,
                "part",
                out ChartNoteAuthoringData pending,
                out _);
            ChartInteractionRecordingUtility.RecordSlideOrHoldLane(
                pending,
                0,
                2d,
                "part",
                out pending,
                out ChartNoteAuthoringData completed);

            Assert.That(completed, Is.Null);

            ChartInteractionRecordingUtility.RecordSlideOrHoldLane(
                pending,
                0,
                3d,
                "part",
                out pending,
                out completed);

            Assert.That(pending, Is.Null);
            Assert.That(completed.NoteType, Is.EqualTo(ChartNoteType.Slide));
            Assert.That(completed.SlideEndBehavior, Is.EqualTo(SlideEndBehavior.Normal));
            Assert.That(completed.SlideNodes.Count, Is.EqualTo(2));
            Assert.That(completed.SlideNodes[0].Time, Is.EqualTo(2d));
            Assert.That(completed.SlideNodes[0].LaneIndex, Is.EqualTo(0));
            Assert.That(completed.SlideNodes[1].Time, Is.EqualTo(3d));
            Assert.That(completed.SlideNodes[1].LaneIndex, Is.EqualTo(0));

        }

        [Test]
        public void TerminalFlickUsesFinalTransitionWithoutAddingTimeOrNode()
        {

            ChartInteractionRecordingUtility.RecordSlideOrHoldLane(
                null,
                2,
                1d,
                "part",
                out ChartNoteAuthoringData pending,
                out _);
            ChartInteractionRecordingUtility.RecordSlideOrHoldLane(
                pending,
                0,
                2d,
                "part",
                out pending,
                out _);
            int nodeCountBeforeTerminalCommand = pending.SlideNodes.Count;
            double endTimeBeforeTerminalCommand = pending.EndTime;

            bool succeeded = ChartInteractionRecordingUtility.TryCompleteTerminalFlick(
                pending,
                out ChartNoteAuthoringData completed,
                out string error);

            Assert.That(succeeded, Is.True, error);
            Assert.That(completed.SlideEndBehavior, Is.EqualTo(SlideEndBehavior.Flick));
            Assert.That(completed.SlideNodes.Count, Is.EqualTo(nodeCountBeforeTerminalCommand));
            Assert.That(completed.EndTime, Is.EqualTo(endTimeBeforeTerminalCommand));

        }

        [Test]
        public void FlickUsesAdjacentLaneAndRejectsOutwardBoundaryDirection()
        {

            bool succeeded = ChartInteractionRecordingUtility.TryCreateFlick(
                3,
                1,
                8,
                1d,
                "part",
                out ChartNoteAuthoringData flick,
                out string error);

            Assert.That(succeeded, Is.True, error);
            Assert.That(flick.NoteType, Is.EqualTo(ChartNoteType.Flick));
            Assert.That(flick.LaneIndex, Is.EqualTo(3));
            Assert.That(flick.EndLaneIndex, Is.EqualTo(4));

            succeeded = ChartInteractionRecordingUtility.TryCreateFlick(
                7,
                1,
                8,
                2d,
                "part",
                out flick,
                out error);

            Assert.That(succeeded, Is.False);
            Assert.That(flick, Is.Null);
            Assert.That(error, Does.Contain("만들 수 없습니다"));

        }

    }

}
