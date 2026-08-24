using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class LanePressFeedbackViewTests
    {

        [UnityTest]
        public IEnumerator FeedbackRemainsVisibleWhilePressedAndFadesAfterRelease()
        {

            Material material = new(Shader.Find("Sprites/Default"));
            GameObject feedbackObject = new("Lane Feedback Test");
            LanePressFeedbackView feedbackView = feedbackObject.AddComponent<LanePressFeedbackView>();
            feedbackView.Initialize(material, 0, 8, 8.2f, -5.4f, -2.75f, 0.18f);

            feedbackView.SetPressed(true);
            yield return null;

            MeshRenderer laneRenderer = feedbackObject.GetComponent<MeshRenderer>();
            LineRenderer lineRenderer = feedbackObject.GetComponent<LineRenderer>();
            Assert.That(laneRenderer.enabled, Is.True);
            Assert.That(lineRenderer.enabled, Is.True);

            feedbackView.SetPressed(false);
            yield return new WaitForSecondsRealtime(0.14f);

            Assert.That(laneRenderer.enabled, Is.False);
            Assert.That(lineRenderer.enabled, Is.False);
            Object.Destroy(feedbackObject);
            Object.Destroy(material);

        }

    }

}
