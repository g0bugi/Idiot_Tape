using IdiotTape.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class ChartSlideEditorPathTests
    {

        [TestCase(false)]
        [TestCase(true)]
        public void SlidePathHoldsPositionUntilEachTransitionTime(bool verticalTimeline)
        {

            Vector3[] authoredPoints = verticalTimeline
                ? new[]
                {
                    new Vector3(30f, 10f),
                    new Vector3(70f, 20f),
                    new Vector3(60f, 30f),
                    new Vector3(60f, 40f)
                }
                : new[]
                {
                    new Vector3(10f, 30f),
                    new Vector3(20f, 70f),
                    new Vector3(30f, 60f),
                    new Vector3(40f, 60f)
                };

            Vector3[] path = PrototypeChartRecorderWindow.BuildSteppedSlidePath(
                authoredPoints,
                verticalTimeline);

            Vector3[] expected = verticalTimeline
                ? new[]
                {
                    new Vector3(30f, 10f),
                    new Vector3(30f, 20f),
                    new Vector3(70f, 20f),
                    new Vector3(70f, 30f),
                    new Vector3(60f, 30f),
                    new Vector3(60f, 40f),
                    new Vector3(60f, 40f)
                }
                : new[]
                {
                    new Vector3(10f, 30f),
                    new Vector3(20f, 30f),
                    new Vector3(20f, 70f),
                    new Vector3(30f, 70f),
                    new Vector3(30f, 60f),
                    new Vector3(40f, 60f),
                    new Vector3(40f, 60f)
                };

            Assert.That(path, Is.EqualTo(expected));
            Assert.That(authoredPoints.Length, Is.EqualTo(4));

        }

        [TestCase(1.5d, 0, 2)]
        [TestCase(2.5d, 1, 6)]
        [TestCase(3.5d, 2, 5)]
        public void BodyInsertionKeepsTheHeldLane(
            double time,
            int expectedIndex,
            int expectedLane)
        {

            ChartNoteAuthoringData data = CreateSlide();

            bool canInsert = PrototypeChartRecorderWindow.TryGetSlideBodyInsertion(
                data,
                time,
                out int insertionIndex,
                out int heldLane);

            Assert.That(canInsert, Is.True);
            Assert.That(insertionIndex, Is.EqualTo(expectedIndex));
            Assert.That(heldLane, Is.EqualTo(expectedLane));
            Assert.That(data.SlideNodes.Count, Is.EqualTo(3));

        }

        [TestCase(1d)]
        [TestCase(1.005d)]
        [TestCase(1.995d)]
        [TestCase(2d)]
        [TestCase(3d)]
        [TestCase(4d)]
        [TestCase(5d)]
        public void BodyInsertionRejectsNodeCollisionsAndOutsideTimes(double time)
        {

            Assert.That(
                PrototypeChartRecorderWindow.TryGetSlideBodyInsertion(
                    CreateSlide(),
                    time,
                    out _,
                    out _),
                Is.False);

        }

        [TestCase(0d, 2)]
        [TestCase(1.5d, 2)]
        [TestCase(1.999d, 2)]
        [TestCase(2d, 6)]
        [TestCase(2.999d, 6)]
        [TestCase(3d, 5)]
        [TestCase(3.5d, 5)]
        [TestCase(4d, 5)]
        public void TimingPreviewUsesTheHeldLaneAndTransitionsAtTheNode(
            double time,
            int expectedLane)
        {

            Assert.That(
                PrototypeChartRecorderWindow.EvaluateSlideLane(CreateSlide(), time),
                Is.EqualTo(expectedLane));

        }

        [Test]
        public void TerminalFlickPreviewHoldsItsOriginUntilTheFinalTime()
        {

            ChartNoteAuthoringData data = CreateSlide();
            data.SlideNodes.RemoveAt(2);
            data.EndTime = 3d;
            data.SlideEndBehavior = SlideEndBehavior.Flick;

            Assert.That(PrototypeChartRecorderWindow.EvaluateSlideLane(data, 2.75d), Is.EqualTo(6));
            Assert.That(PrototypeChartRecorderWindow.EvaluateSlideLane(data, 3d), Is.EqualTo(5));

        }

        private static ChartNoteAuthoringData CreateSlide()
        {

            ChartNoteAuthoringData data = ChartNoteAuthoringData.CreateTap("slide", 1d, 2, "part");
            data.NoteType = ChartNoteType.Slide;
            data.EndTime = 4d;
            data.EndLaneIndex = 5;
            data.SlideNodes.Add(new ChartNoteAuthoringData.PathNodeData { Time = 2d, LaneIndex = 6 });
            data.SlideNodes.Add(new ChartNoteAuthoringData.PathNodeData { Time = 3d, LaneIndex = 5 });
            data.SlideNodes.Add(new ChartNoteAuthoringData.PathNodeData { Time = 4d, LaneIndex = 5 });
            return data;

        }

    }

}
