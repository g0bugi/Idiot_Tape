using System.Collections.Generic;
using System.Reflection;
using IdiotTape.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class ChartWorkspaceCanvasTests
    {

        private PrototypeChartRecorderWindow window;
        private PrototypeChart chart;

        [SetUp]
        public void SetUp()
        {

            chart = ScriptableObject.CreateInstance<PrototypeChart>();
            JsonUtility.FromJsonOverwrite(
                "{\"laneCount\":8,\"tempoSections\":[{\"startTime\":0,\"startBar\":1," +
                "\"beatsPerMinute\":120,\"beatsPerBar\":4,\"beatUnit\":4}]," +
                "\"musicalParts\":[{\"id\":\"synth\",\"displayName\":\"Synth\"}]," +
                "\"notes\":[{\"id\":\"slide\",\"hitTime\":1,\"laneIndex\":2," +
                "\"musicalPartId\":\"synth\",\"noteType\":2,\"endTime\":6," +
                "\"endLaneIndex\":5,\"slideNodes\":[{\"time\":3,\"laneIndex\":6}," +
                "{\"time\":5,\"laneIndex\":5},{\"time\":6,\"laneIndex\":5}]}]}", chart);
            window = ScriptableObject.CreateInstance<PrototypeChartRecorderWindow>();
            SetField("chart", chart);
            SetField("timelineStartTime", 2d);
            SetField("timelineVisibleDuration", 4f);

        }

        [TearDown]
        public void TearDown()
        {

            Object.DestroyImmediate(window);
            Object.DestroyImmediate(chart);

        }

        [TestCase(true, 250f, 50f)]
        [TestCase(false, 100f, 128.375f)]
        public void OffscreenSlideStartStillAllowsSelectingItsVisibleHoldBody(bool vertical, float x, float y)
        {

            float distance = (float)Invoke("GetWorkspaceNoteDistance", new Rect(0f, 0f, 800f, 400f),
                new Vector2(x, y), ChartNoteAuthoringData.FromChartNote(chart.Notes[0]), vertical);

            Assert.That(distance, Is.LessThan(0.001f));

        }

        [TestCase(true, 450f, 100f)]
        [TestCase(false, 200f, 220f)]
        public void SlideTransitionConnectorIsSelectableAcrossItsWholeSpan(bool vertical, float x, float y)
        {

            float distance = (float)Invoke("GetWorkspaceNoteDistance", new Rect(0f, 0f, 800f, 400f),
                new Vector2(x, y), ChartNoteAuthoringData.FromChartNote(chart.Notes[0]), vertical);

            Assert.That(distance, Is.LessThan(0.001f));

        }

        [TestCase(true)]
        [TestCase(false)]
        public void SlideRenderingAddsDisplayCornersWithoutChangingChartOrAuthoringNodes(bool vertical)
        {

            string before = JsonUtility.ToJson(chart);
            ChartNoteAuthoringData data = ChartNoteAuthoringData.FromChartNote(chart.Notes[0]);
            List<Vector3> points = new();

            Invoke("BuildWorkspaceNotePath", new Rect(0f, 0f, 800f, 400f), data, vertical, points);

            Assert.That(points.Count, Is.EqualTo(7));
            Assert.That(data.SlideNodes.Count, Is.EqualTo(3));
            Assert.That(data.SlideNodes[0].Time, Is.EqualTo(3d));
            Assert.That(JsonUtility.ToJson(chart), Is.EqualTo(before));

            for (int index = 1; index < points.Count; index++)
            {

                Vector3 difference = points[index] - points[index - 1];
                Assert.That(Mathf.Abs(difference.x) < 0.001f || Mathf.Abs(difference.y) < 0.001f,
                    Is.True, "Every step segment must keep either chart time or lane position constant.");

            }

        }

        [TestCase(2, 0)]
        [TestCase(8, 1)]
        [TestCase(12, 2)]
        public void BananaPreviewMatchesRuntimeCurveIncludingNonuniformControlTimes(int laneCount, int handleCount)
        {

            string handles = handleCount == 0 ? "[]"
                : handleCount == 1 ? "[{\"normalizedTime\":0.2,\"normalizedX\":0.9}]"
                : "[{\"normalizedTime\":0.2,\"normalizedX\":0.9},{\"normalizedTime\":0.8,\"normalizedX\":0.1}]";
            ChartNote note = JsonUtility.FromJson<ChartNote>(
                "{\"hitTime\":1,\"endTime\":5,\"laneIndex\":0,\"endLaneIndex\":" +
                (laneCount - 1) + ",\"noteType\":4,\"bananaCurveHandles\":" + handles + "}");
            ChartNoteAuthoringData data = ChartNoteAuthoringData.FromChartNote(note);

            foreach (float parameter in new[] { 0f, 0.15f, 0.55f, 0.9f, 1f })
            {

                Vector2 point = (Vector2)InvokeStatic("EvaluateWorkspaceBananaPoint", data, parameter, laneCount);
                float runtimeX = NotePathMath.GetBananaNormalizedX(note, 1d + point.x * 4d, laneCount);
                Assert.That(point.y, Is.EqualTo(runtimeX).Within(0.00003f));

            }

            Vector2 first = (Vector2)InvokeStatic("EvaluateWorkspaceBananaPoint", data, 0f, laneCount);
            Vector2 last = (Vector2)InvokeStatic("EvaluateWorkspaceBananaPoint", data, 1f, laneCount);
            Assert.That(first.x, Is.Zero);
            Assert.That(first.y, Is.EqualTo(0.5f / laneCount));
            Assert.That(last.x, Is.EqualTo(1f));
            Assert.That(last.y, Is.EqualTo((laneCount - 0.5f) / laneCount));

        }

        [Test]
        public void SharedSheetLaneCentersFollowChartLaneCount()
        {

            JsonUtility.FromJsonOverwrite("{\"laneCount\":12}", chart);
            Vector2 first = (Vector2)Invoke("WorkspaceNotePosition", new Rect(0f, 0f, 600f, 400f), 2d, 0, true);
            Vector2 last = (Vector2)Invoke("WorkspaceNotePosition", new Rect(0f, 0f, 600f, 400f), 2d, 11, true);

            Assert.That(first.x, Is.EqualTo(25f).Within(0.001f));
            Assert.That(last.x, Is.EqualTo(575f).Within(0.001f));

        }

        [TestCase(0, 1d, 5d)]
        [TestCase(1, 1d, 5d)]
        [TestCase(2, 1d, 5d)]
        [TestCase(2, 1.01d, 1.02d)]
        public void GeneratedBananaCheckpointsMatchRuntimeCurveAndShortNoteFallback(int handleCount, double start, double end)
        {

            string handles = handleCount == 0 ? "[]"
                : handleCount == 1 ? "[{\"normalizedTime\":0.2,\"normalizedX\":0.9}]"
                : "[{\"normalizedTime\":0.2,\"normalizedX\":0.9},{\"normalizedTime\":0.8,\"normalizedX\":0.1}]";
            ChartNote note = JsonUtility.FromJson<ChartNote>(
                "{\"hitTime\":" + start.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                ",\"endTime\":" + end.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                ",\"laneIndex\":0,\"endLaneIndex\":7,\"noteType\":4,\"bananaCurveHandles\":" + handles + "}");
            ChartNoteAuthoringData data = ChartNoteAuthoringData.FromChartNote(note);

            Invoke("GenerateBananaCheckpoints", data, 4);

            Assert.That(data.BananaCheckpoints.Count, Is.GreaterThan(0));

            foreach (ChartNoteAuthoringData.CheckpointData checkpoint in data.BananaCheckpoints)
            {

                Assert.That(checkpoint.Time, Is.GreaterThan(start).And.LessThan(end));
                Assert.That(checkpoint.NormalizedX,
                    Is.EqualTo(NotePathMath.GetBananaNormalizedX(note, checkpoint.Time, 8)).Within(0.00003f));

            }

            if (end - start < 0.02d)
            {

                Assert.That(data.BananaCheckpoints.Count, Is.EqualTo(1));
                Assert.That(data.BananaCheckpoints[0].Time, Is.EqualTo((start + end) * 0.5d));

            }

        }

        [Test]
        public void LongTemporaryBodyIsClippedBeforeDashDrawing()
        {

            object[] arguments = { new Rect(0f, 0f, 800f, 400f), new Vector2(250f, -1000000f), new Vector2(250f, 1000000f) };
            bool visible = (bool)InvokeStatic("ClipWorkspaceSegment", arguments);

            Assert.That(visible, Is.True);
            Assert.That(((Vector2)arguments[1]).y, Is.EqualTo(0f).Within(0.1f));
            Assert.That(((Vector2)arguments[2]).y, Is.EqualTo(400f).Within(0.1f));

        }

        [Test]
        public void PatternGhostUsesMusicalTimeForEndpointsAcrossTempoChanges()
        {

            JsonUtility.FromJsonOverwrite(
                "{\"tempoSections\":[{\"startTime\":0,\"startBar\":1,\"beatsPerMinute\":120," +
                "\"beatsPerBar\":4,\"beatUnit\":4},{\"startTime\":8,\"startBar\":5," +
                "\"beatsPerMinute\":60,\"beatsPerBar\":4,\"beatUnit\":4}]}", chart);
            ChartNoteAuthoringData source = ChartNoteAuthoringData.CreateTap("source", 1d, 2, "synth");
            source.NoteType = ChartNoteType.Slide;
            source.SlideNodes.Add(new ChartNoteAuthoringData.PathNodeData { Time = 3d, LaneIndex = 6 });
            ChartNoteAuthoringData ghost = ChartPatternDuplication.CloneAtMusicalBarOffset(chart, source, 4, "ghost");

            Assert.That(ghost.HitTime, Is.EqualTo(10d));
            Assert.That(ghost.SlideNodes[0].Time, Is.EqualTo(14d));
            Assert.That(source.HitTime, Is.EqualTo(1d));
            Assert.That(source.SlideNodes[0].Time, Is.EqualTo(3d));

        }

        private void SetField(string name, object value)
        {

            typeof(PrototypeChartRecorderWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(window, value);

        }

        private object Invoke(string name, params object[] arguments)
        {

            return typeof(PrototypeChartRecorderWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(window, arguments);

        }

        private static object InvokeStatic(string name, params object[] arguments)
        {

            return typeof(PrototypeChartRecorderWindow).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, arguments);

        }

    }

}
