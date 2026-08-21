using IdiotTape.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class ChartAuthoringMetronomeTests
    {

        [Test]
        public void EditorMetronomeIsPlainDisposableHelperInsteadOfEditorComponent()
        {

            ChartAuthoringMetronome metronome = new();

            Assert.That(typeof(Component).IsAssignableFrom(typeof(ChartAuthoringMetronome)), Is.False);
            Assert.That(metronome.IsInitialized, Is.False);
            Assert.DoesNotThrow(metronome.Dispose);

            Assert.That(metronome.IsInitialized, Is.False);

        }

    }

}
