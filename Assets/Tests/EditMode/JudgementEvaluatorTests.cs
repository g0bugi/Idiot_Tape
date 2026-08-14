using System.Collections.Generic;
using NUnit.Framework;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class JudgementEvaluatorTests
    {

        private readonly JudgementSettings settings = new();

        [Test]
        public void ExactTimeAndPositionReturnsPerfect()
        {

            List<JudgementCandidate> candidates = new()
            {

                new JudgementCandidate(0, 2d, 3)

            };

            float laneCenter = PlayfieldGeometry.GetLaneCenterNormalized(3, 8);
            JudgementResult result = JudgementEvaluator.Evaluate(candidates, 2d, laneCenter, 8, settings);

            Assert.That(result.Grade, Is.EqualTo(JudgementGrade.Perfect));
            Assert.That(result.SourceIndex, Is.EqualTo(0));

        }

        [Test]
        public void InputOutsideWidePositionToleranceDoesNotHit()
        {

            List<JudgementCandidate> candidates = new()
            {

                new JudgementCandidate(0, 2d, 0)

            };

            JudgementResult result = JudgementEvaluator.Evaluate(candidates, 2d, 0.5f, 8, settings);

            Assert.That(result.Grade, Is.EqualTo(JudgementGrade.None));

        }

        [Test]
        public void OverlappingToleranceChoosesClosestLaneBeforeTiming()
        {

            List<JudgementCandidate> candidates = new()
            {

                new JudgementCandidate(0, 2.01d, 3),
                new JudgementCandidate(1, 2d, 4)

            };

            float inputX = PlayfieldGeometry.GetLaneCenterNormalized(3, 8) + 0.02f;
            JudgementResult result = JudgementEvaluator.Evaluate(candidates, 2d, inputX, 8, settings);

            Assert.That(result.SourceIndex, Is.EqualTo(0));

        }

    }

}
