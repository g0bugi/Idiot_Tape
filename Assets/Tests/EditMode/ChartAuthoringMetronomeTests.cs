using IdiotTape.Audio;
using IdiotTape.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class ChartAuthoringMetronomeTests
    {

        [Test]
        public void MetronomeContinuesFromCountInThroughDelayedFirstDownbeat()
        {

            ChartTempoSection tempo = JsonUtility.FromJson<ChartTempoSection>(
                "{\"startBar\":1,\"startTime\":0.5773379970760288,\"beatsPerMinute\":128,\"beatsPerBar\":4,\"beatUnit\":4}");
            var sections = new[] { tempo };
            var countIn = ChartAuthoringCountIn.BuildBeats(tempo, 0d, 2);
            double beat = ChartAuthoringMetronome.GetBeatTimeAtOrAfter(sections, 0d);

            Assert.That(beat, Is.EqualTo(0.1085879970760288d).Within(0.000001d));
            Assert.That(beat - countIn[^1].SongTime, Is.EqualTo(60d / 128d).Within(0.000001d));
            Assert.That(ChartAuthoringMetronome.IsDownbeat(sections, beat), Is.False);
            double firstDownbeat = ChartAuthoringMetronome.GetBeatTimeAfter(sections, beat);
            Assert.That(firstDownbeat, Is.EqualTo(tempo.StartTime).Within(0.000001d));
            Assert.That(ChartAuthoringMetronome.IsDownbeat(sections, firstDownbeat), Is.True);
            Assert.That(ChartAuthoringMetronome.GetBeatTimeAfter(sections, firstDownbeat),
                Is.EqualTo(tempo.StartTime + tempo.SecondsPerBeat).Within(0.000001d));

        }

        [Test]
        public void MetronomeBeforeLateBarOnePreservesAccentsAndLaterTempoSections()
        {

            ChartTempoSection first = JsonUtility.FromJson<ChartTempoSection>(
                "{\"startBar\":1,\"startTime\":4,\"beatsPerMinute\":120,\"beatsPerBar\":4,\"beatUnit\":4}");
            ChartTempoSection second = JsonUtility.FromJson<ChartTempoSection>(
                "{\"startBar\":3,\"startTime\":8,\"beatsPerMinute\":60,\"beatsPerBar\":4,\"beatUnit\":4}");
            var sections = new[] { first, second };

            Assert.That(ChartAuthoringMetronome.GetBeatTimeAtOrAfter(sections, 0.01d), Is.EqualTo(0.5d));
            Assert.That(ChartAuthoringMetronome.IsDownbeat(sections, 2d), Is.True);
            Assert.That(ChartAuthoringMetronome.IsDownbeat(sections, 2.5d), Is.False);
            Assert.That(ChartAuthoringMetronome.GetBeatTimeAfter(sections, 7.5d), Is.EqualTo(8d));
            Assert.That(ChartAuthoringMetronome.GetBeatTimeAfter(sections, 8d), Is.EqualTo(9d));
            Assert.That(ChartAuthoringMetronome.GetBeatTimeAtOrAfter(sections, 8.1d), Is.EqualTo(9d));

        }

        [Test]
        public void EditorMetronomeIsPlainDisposableHelperInsteadOfEditorComponent()
        {

            ChartAuthoringMetronome metronome = new();

            Assert.That(typeof(Component).IsAssignableFrom(typeof(ChartAuthoringMetronome)), Is.False);
            Assert.That(metronome.IsInitialized, Is.False);
            Assert.DoesNotThrow(metronome.Dispose);

            Assert.That(metronome.IsInitialized, Is.False);

        }

        [Test]
        public void RuntimeMetronomeDoesNotCreateAudioResourcesBeforeScheduling()
        {

            using FmodMetronome metronome = new();

            Assert.That(typeof(Component).IsAssignableFrom(typeof(FmodMetronome)), Is.False);
            Assert.That(metronome.IsInitialized, Is.False);
            Assert.That(metronome.Schedule(0d, false, 0.15f), Is.False);
            Assert.That(metronome.IsInitialized, Is.False);
            Assert.DoesNotThrow(metronome.StopAll);

        }

        [TestCase(10d, 9.9d, true, 0.1d)]
        [TestCase(10d, 9.975d, true, 0.025d)]
        [TestCase(10d, 9.98d, false, 0.02d)]
        [TestCase(10d, 10.01d, false, -0.01d)]
        public void ScheduleDelayRejectsClicksThatCannotMeetMinimumLead(
            double targetSongTime,
            double currentSongTime,
            bool expectedCanSchedule,
            double expectedDelay)
        {

            bool canSchedule = ChartAuthoringMetronome.TryGetScheduleDelay(
                targetSongTime,
                currentSongTime,
                out double delay);

            Assert.That(canSchedule, Is.EqualTo(expectedCanSchedule));
            Assert.That(delay, Is.EqualTo(expectedDelay).Within(0.000001d));

        }

        [Test]
        public void CountInExtendsTempoGridBeforeDelayedFirstDownbeat()
        {

            ChartTempoSection tempo = JsonUtility.FromJson<ChartTempoSection>(
                "{\"startBar\":1,\"startTime\":0.233,\"beatsPerMinute\":120.0," +
                "\"beatsPerBar\":4,\"beatUnit\":4}");

            var beats = ChartAuthoringCountIn.BuildBeats(tempo, 0d, 1);

            Assert.That(beats, Has.Count.EqualTo(4));
            Assert.That(beats[0].SongTime, Is.EqualTo(-1.767d).Within(0.000001d));
            Assert.That(beats[0].Accent, Is.True);
            Assert.That(beats[3].SongTime, Is.EqualTo(-0.267d).Within(0.000001d));
            Assert.That(
                tempo.StartTime - beats[3].SongTime,
                Is.EqualTo(tempo.SecondsPerBeat).Within(0.000001d));

        }

        [Test]
        public void CountInKeepsExactBarLengthWhenRecordingStartsAfterSongZero()
        {

            ChartTempoSection tempo = JsonUtility.FromJson<ChartTempoSection>(
                "{\"startBar\":1,\"startTime\":0.233,\"beatsPerMinute\":120.0," +
                "\"beatsPerBar\":4,\"beatUnit\":4}");

            var beats = ChartAuthoringCountIn.BuildBeats(tempo, 0.3d, 1);

            Assert.That(beats, Has.Count.EqualTo(4));
            Assert.That(beats[3].SongTime, Is.EqualTo(0.233d).Within(0.000001d));
            Assert.That(beats[3].Accent, Is.True);

        }

    }

}
