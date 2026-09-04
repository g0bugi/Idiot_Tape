using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class RuntimeSlideViewTests
    {

        private GameObject noteObject;

        [UnityTearDown]
        public IEnumerator RemoveNote()
        {

            Object.Destroy(noteObject);
            yield return null;

        }

        [UnityTest]
        public IEnumerator SlideBodyContainsLaneHoldsAndSameTimeTransitionsAfterTimelineJumps()
        {

            RuntimeNoteView view = CreateSlide(0, 0f);
            view.UpdatePresentation(0d);
            LineRenderer body = noteObject.transform.Find("BodyFill").GetComponent<LineRenderer>();
            Vector3[] initialPositions = new Vector3[body.positionCount];
            body.GetPositions(initialPositions);
            bool sawRightTransition = false;
            bool sawLeftTransition = false;
            bool sawHold = false;

            for (int index = 1; index < initialPositions.Length; index++)
            {

                Vector3 delta = initialPositions[index] - initialPositions[index - 1];
                bool sameLane = Mathf.Abs(delta.x) < 0.00001f;
                bool sameTime = Mathf.Abs(delta.y) < 0.00001f;
                Assert.That(sameLane || sameTime, Is.True, $"Body segment {index} is diagonal.");
                sawHold |= sameLane && !sameTime;
                sawRightTransition |= delta.x > 0.00001f;
                sawLeftTransition |= delta.x < -0.00001f;

            }

            Assert.That(sawHold && sawRightTransition && sawLeftTransition, Is.True);
            view.UpdatePresentation(2.75d);
            view.UpdatePresentation(0d);
            yield return null;

            for (int index = 0; index < initialPositions.Length; index++)
            {

                Assert.That(
                    Vector3.Distance(body.GetPosition(index), initialPositions[index]),
                    Is.LessThan(0.00001f),
                    $"Point {index} changed after returning to the same song time.");

            }

        }

        [UnityTest]
        public IEnumerator SlideConnectorsFollowOneChartTimeAcrossTheCurvedPlayfield()
        {

            const float curvature = -2f;
            RuntimeNoteView view = CreateSlide(0, curvature);
            view.UpdatePresentation(0d);
            yield return null;
            LineRenderer body = noteObject.transform.Find("BodyFill").GetComponent<LineRenderer>();

            for (int index = 1; index < body.positionCount; index++)
            {

                Vector3 previous = body.GetPosition(index - 1);
                Vector3 current = body.GetPosition(index);

                if (Mathf.Abs(current.x - previous.x) < 0.00001f)
                {

                    continue;

                }

                double transitionTime = current.x > previous.x ? 2d : 3d;
                AssertPointHasChartTime(previous, transitionTime, curvature);
                AssertPointHasChartTime(current, transitionTime, curvature);

            }

        }

        [UnityTest]
        public IEnumerator TerminalFlickKeepsItsEndpointAndUsesTheFinalTransitionsDirection()
        {

            RuntimeNoteView view = CreateSlide(1, -2f);
            view.UpdatePresentation(0d);
            yield return null;
            LineRenderer body = noteObject.transform.Find("BodyFill").GetComponent<LineRenderer>();
            Transform endMarker = noteObject.transform.Find("End");
            Assert.That(
                Vector3.Distance(body.GetPosition(body.positionCount - 1), endMarker.position),
                Is.LessThan(0.00001f));
            Assert.That(endMarker.Find("Direction").localPosition.x, Is.LessThan(0f));

        }

        private RuntimeNoteView CreateSlide(int endBehavior, float curvature)
        {

            ChartNote note = JsonUtility.FromJson<ChartNote>(
                "{\"hitTime\":1.0,\"laneIndex\":2,\"noteType\":2," +
                $"\"slideEndBehavior\":{endBehavior}," +
                "\"slideNodes\":[{\"time\":2.0,\"laneIndex\":6},{\"time\":3.0,\"laneIndex\":5}]} ");
            noteObject = new GameObject("Stepped Slide View Test");
            RuntimeNoteView view = noteObject.AddComponent<RuntimeNoteView>();
            view.Initialize(note, null, Color.cyan, 8, 8f, 5f, -5f, curvature, 4f, 1f, null, true);
            return view;

        }

        private static void AssertPointHasChartTime(Vector3 point, double chartTime, float curvature)
        {

            float normalizedX = (point.x + 8f) / 16f;
            float targetY = PlayfieldGeometry.GetJudgementLineY(normalizedX, -5f, curvature);
            float expectedY = Mathf.Lerp(5f, targetY, 1f - (float)chartTime / 4f);
            Assert.That(point.y, Is.EqualTo(expectedY).Within(0.00001f));

        }

    }

}
