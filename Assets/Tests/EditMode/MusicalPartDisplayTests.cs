using NUnit.Framework;
using UnityEngine;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class MusicalPartDisplayTests
    {

        [Test]
        public void BoundariesOverlapGapsHitchesAndRestartResolveWithoutJudgements()
        {

            PrototypeChart chart = ScriptableObject.CreateInstance<PrototypeChart>();
            try
            {

                JsonUtility.FromJsonOverwrite("{\"musicalParts\":[{\"id\":\"drum\",\"displayName\":\"Drum\"},{\"id\":\"synth\",\"displayName\":\"Synth\"}],\"activationWindows\":[{\"musicalPartId\":\"synth\",\"startTime\":2,\"endTime\":4},{\"musicalPartId\":\"drum\",\"startTime\":1,\"endTime\":3},{\"musicalPartId\":\"drum\",\"startTime\":6,\"endTime\":7},{\"musicalPartId\":\"drum\",\"startTime\":7,\"endTime\":8}]}", chart);
                MusicalPartDisplayState state = new();
                Assert.That(state.Update(chart, 0), Is.False);
                Assert.That(state.DisplayName, Is.Empty);
                Assert.That(state.Update(chart, 1), Is.True);
                Assert.That(state.DisplayName, Is.EqualTo("Drum"));
                Assert.That(state.Update(chart, 1.9), Is.False);
                Assert.That(state.Update(chart, 2), Is.True);
                Assert.That(state.DisplayName, Is.EqualTo("Drum + Synth"));
                Assert.That(state.Update(chart, 3), Is.True);
                Assert.That(state.DisplayName, Is.EqualTo("Synth"));
                Assert.That(state.Update(chart, 4), Is.False);
                Assert.That(state.Update(chart, 5), Is.False);
                state.Reset();
                Assert.That(state.Update(chart, 5), Is.True, "A hitch into a gap reconstructs the previous section.");
                Assert.That(state.DisplayName, Is.EqualTo("Synth"));
                Assert.That(state.Update(chart, 6), Is.True);
                Assert.That(state.Update(chart, 7), Is.False, "Adjacent same-part windows must not animate again.");
                Assert.That(state.Update(chart, 100), Is.False);
                state.Reset();
                Assert.That(state.DisplayName, Is.Empty);
                Assert.That(state.Update(chart, -1), Is.False);
                Assert.That(state.Update(chart, 1), Is.True);

            }
            finally
            {

                Object.DestroyImmediate(chart);

            }

        }

    }

}
