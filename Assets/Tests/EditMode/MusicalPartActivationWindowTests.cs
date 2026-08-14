using NUnit.Framework;
using UnityEngine;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class MusicalPartActivationWindowTests
    {

        [Test]
        public void WindowUsesInclusiveStartAndExclusiveEnd()
        {

            MusicalPartActivationWindow window = JsonUtility.FromJson<MusicalPartActivationWindow>(
                "{\"musicalPartId\":\"drum\",\"startTime\":2.0,\"endTime\":4.0}");

            Assert.That(window.Contains(2d), Is.True);
            Assert.That(window.Contains(3.999d), Is.True);
            Assert.That(window.Contains(4d), Is.False);

        }

    }

}
