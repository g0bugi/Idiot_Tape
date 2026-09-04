#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using IdiotTape.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class GameplayStartFlowTests
    {

        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator StartWaitsForThePlayerAndZeroTimeNoteApproachesBeforeAudioStarts()
        {

            int previousTargetFrameRate = Application.targetFrameRate;
            int previousVSyncCount = QualitySettings.vSyncCount;
            bool previousRunInBackground = Application.runInBackground;
            InputSettings previousInputSettings = InputSystem.settings;
            InputSettings testInputSettings = null;
            Mouse previousMouse = Mouse.current;
            Keyboard previousKeyboard = Keyboard.current;
            Mouse testMouse = null;
            Keyboard testKeyboard = null;
            GameplaySession session = null;
            FmodSongPlayback playback = null;
            PrototypeChart originalChart = null;
            PrototypeChart fixtureChart = null;
            string originalJson = null;

            try
            {

                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 60;
                Application.runInBackground = true;
                // Batch mode has no focused Game View. Route this fixture's real queued
                // pointer/key events through the player loop without changing project settings.
                testInputSettings = Object.Instantiate(previousInputSettings);
                testInputSettings.hideFlags = HideFlags.DontSave;
                testInputSettings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings = testInputSettings;
                AsyncOperation load = SceneManager.LoadSceneAsync("Gameplay", LoadSceneMode.Single);

                while (!load.isDone)
                {

                    yield return null;

                }

                session = Object.FindAnyObjectByType<GameplaySession>();
                playback = Object.FindAnyObjectByType<FmodSongPlayback>();
                GameplayInputRouter input = Object.FindAnyObjectByType<GameplayInputRouter>();
                GameplayHud hud = Object.FindAnyObjectByType<GameplayHud>();
                PlayfieldPresenter presenter = Object.FindAnyObjectByType<PlayfieldPresenter>();
                Assert.That(session, Is.Not.Null);
                Assert.That(playback, Is.Not.Null);
                Assert.That(input, Is.Not.Null);
                Assert.That(hud, Is.Not.Null);
                Assert.That(presenter, Is.Not.Null);
                float deadline = Time.realtimeSinceStartup + 15f;

                while (!session.IsWaitingForStart && !playback.PreparationFailed &&
                    Time.realtimeSinceStartup < deadline)
                {

                    yield return null;

                }

                Assert.That(playback.IsPrepared, Is.True);
                Assert.That(session.IsWaitingForStart, Is.True, "Prepared songs must wait for the player.");
                yield return new WaitForSecondsRealtime(0.15f);
                Assert.That(playback.IsRunning, Is.False, "Preparing audio must not start the song automatically.");
                Assert.That(Object.FindObjectsByType<RuntimeNoteView>(FindObjectsSortMode.None), Is.Empty);

                originalChart = GetField<PrototypeChart>(session, "chart");
                originalJson = JsonUtility.ToJson(originalChart);
                fixtureChart = Object.Instantiate(originalChart);
                fixtureChart.name = "Start Flow Verification";
                fixtureChart.hideFlags = HideFlags.DontSave;
                ConfigureOpeningFixture(fixtureChart);
                Assert.That(fixtureChart.TryValidate(out string validationError), Is.True, validationError);
                SetField(session, "chart", fixtureChart);
                SetField(session, "preparationBars", 1);
                SetField(session, "shortPreparationBars", 1);
                Assert.That(hud.NoteSpeedMultiplier, Is.EqualTo(1f));

                Text scoreText = GameObject.Find("Score").GetComponent<Text>();
                Text judgementText = GameObject.Find("Judgement").GetComponent<Text>();
                Text comboText = GameObject.Find("Combo").GetComponent<Text>();
                CaptureIfRequested("01-ready.png");
                Canvas.ForceUpdateCanvases();
                RectTransform startButton = GameObject.Find("StartButton").GetComponent<RectTransform>();
                Vector2 startPosition = RectTransformUtility.WorldToScreenPoint(Camera.main, startButton.position);
                Assert.That(hud.IsStartButtonPress(startPosition), Is.True);
                testMouse = InputSystem.AddDevice<Mouse>("StartFlowTestMouse");
                testKeyboard = InputSystem.AddDevice<Keyboard>("StartFlowTestKeyboard");
                RectTransform readySpeedArea = GameObject.Find("NoteSpeedSlider").GetComponent<RectTransform>();
                Vector2 readySpeedMiddle = RectTransformUtility.WorldToScreenPoint(Camera.main,
                    readySpeedArea.TransformPoint(readySpeedArea.rect.center));
                Vector2 readySpeedRight = RectTransformUtility.WorldToScreenPoint(Camera.main,
                    readySpeedArea.TransformPoint(new Vector3(readySpeedArea.rect.xMax, 0f, 0f)));
                Vector2 readySpeedLeft = RectTransformUtility.WorldToScreenPoint(Camera.main,
                    readySpeedArea.TransformPoint(new Vector3(readySpeedArea.rect.xMin, 0f, 0f)));
                InputSystem.QueueStateEvent(testMouse,
                    new MouseState { position = readySpeedMiddle }.WithButton(MouseButton.Left));
                yield return null;
                Assert.That(hud.NoteSpeedMultiplier, Is.EqualTo(2.5f).Within(0.02f));
                InputSystem.QueueStateEvent(testMouse,
                    new MouseState { position = readySpeedRight + new Vector2(40f, 40f) }.WithButton(MouseButton.Left));
                yield return null;
                Assert.That(hud.NoteSpeedMultiplier, Is.EqualTo(4f));
                Assert.That(session.IsWaitingForStart, Is.True);
                Assert.That(GetField<IDictionary>(session, "contacts").Count, Is.Zero,
                    "Adjusting the existing speed slider must not acquire gameplay contacts.");
                InputSystem.QueueStateEvent(testMouse,
                    new MouseState { position = readySpeedLeft }.WithButton(MouseButton.Left));
                yield return null;
                InputSystem.QueueStateEvent(testMouse, new MouseState { position = readySpeedLeft });
                yield return null;
                Assert.That(hud.NoteSpeedMultiplier, Is.EqualTo(1f));
                InputSystem.QueueStateEvent(testMouse,
                    new MouseState { position = startPosition }.WithButton(MouseButton.Left));
                yield return null;
                InputSystem.QueueStateEvent(testMouse, new MouseState { position = startPosition });
                yield return null;

                Assert.That(session.IsCountingIn, Is.True, "The real START mouse press must enter preparation.");
                WriteClockDiagnostics(playback, "Scheduled preparation");
                Assert.That(playback.IsRunning, Is.True);
                Assert.That(playback.HasReachedScheduledStart, Is.False);
                Assert.That(playback.TimelineTime, Is.LessThan(-fixtureChart.VisualLeadTime));
                Assert.That(playback.SongTime, Is.Zero, "The compatibility audio position stays at zero during preparation.");
                Assert.That(scoreText.text, Is.EqualTo("00000000"));
                Assert.That(judgementText.text, Is.Empty);
                Assert.That(GameObject.Find("Note_start_zero_synth"), Is.Null);

                RectTransform speedArea = GameObject.Find("NoteSpeedSlider").GetComponent<RectTransform>();
                Vector2 speedPosition = RectTransformUtility.WorldToScreenPoint(Camera.main,
                    speedArea.TransformPoint(new Vector3(speedArea.rect.xMax, 0f, 0f)));
                Assert.That(hud.TrySetNoteSpeedFromScreenPosition(speedPosition), Is.False);
                Assert.That(hud.NoteSpeedMultiplier, Is.EqualTo(1f));

                GameObject openingNote = null;
                deadline = Time.realtimeSinceStartup + 5f;

                while (openingNote == null && Time.realtimeSinceStartup < deadline)
                {

                    yield return null;
                    openingNote = GameObject.Find("Note_start_zero_synth");

                }

                Assert.That(openingNote, Is.Not.Null);
                Assert.That(session.IsCountingIn, Is.True);
                Assert.That(playback.TimelineTime, Is.LessThan(-1d));
                float spawnY = GetField<float>(presenter, "spawnY");
                float lineY = PlayfieldGeometry.GetJudgementLineY(
                    PlayfieldGeometry.GetLaneCenterNormalized(3, fixtureChart.LaneCount),
                    GetField<float>(presenter, "judgementBaseY"),
                    GetField<float>(presenter, "judgementCurvature"));
                float firstY = openingNote.transform.position.y;
                Assert.That(firstY, Is.GreaterThan(lineY + (spawnY - lineY) * 0.85f),
                    "A zero-time note must first appear near the top of its full approach path.");
                CaptureIfRequested("02-opening-note-at-top.png");

                for (int press = 0; press < 8; press++)
                {

                    input.SubmitLaneInput(3, InputState.currentTime);
                    input.SubmitLaneRelease(3, InputState.currentTime);
                    yield return null;
                    Assert.That(session.IsCountingIn, Is.True);
                    Assert.That(scoreText.text, Is.EqualTo("00000000"));
                    Assert.That(judgementText.text, Is.Empty, "Preparation must produce neither hits nor misses.");
                    Assert.That(comboText.text, Is.Empty);

                }

                Assert.That(GetField<IDictionary>(session, "contacts").Count, Is.Zero);
                yield return new WaitForSecondsRealtime(0.15f);
                Assert.That(playback.TimelineTime, Is.LessThan(0d));
                Assert.That(openingNote.transform.position.y, Is.LessThan(firstY - 0.25f));
                Assert.That(openingNote.transform.position.y, Is.GreaterThan(lineY + 0.5f));
                CaptureIfRequested("03-opening-note-approaching.png");
                deadline = Time.realtimeSinceStartup + 5f;

                while (session.IsCountingIn && Time.realtimeSinceStartup < deadline)
                {

                    yield return null;

                }

                Assert.That(session.IsCountingIn, Is.False);
                Assert.That(session.IsWaitingForStart, Is.False);
                Assert.That(playback.HasReachedScheduledStart, Is.True);
                WriteClockDiagnostics(playback, "Scheduled zero reached");
                Assert.That(playback.TimelineTime, Is.InRange(0d, 0.12d),
                    "The same DSP timeline must enter play at zero without restarting the note path.");
                // Recreate the ordering boundary where InputRouter reaches the first music
                // frame before Session.Update has changed the phase, without changing DSP time.
                SetField(session, "phase", Enum.Parse(GetField<object>(session, "phase").GetType(), "CountIn"));
                input.SubmitLaneInput(3, InputState.currentTime - 0.25d);
                Assert.That(session.IsCountingIn, Is.False);
                Assert.That(GetField<int>(session, "score"), Is.Zero,
                    "A delayed preparation input must not become a zero-time hit after the phase transition.");
                Assert.That(GetField<IDictionary>(session, "contacts").Count, Is.Zero);
                input.SubmitLaneInput(3, InputState.currentTime);
                input.SubmitLaneRelease(3, InputState.currentTime);
                Assert.That(GetField<int>(session, "score"), Is.GreaterThan(0));
                Assert.That(comboText.text, Does.Contain("1"));
                Assert.That(judgementText.text, Is.EqualTo("PERFECT").Or.EqualTo("GOOD"));

                deadline = Time.realtimeSinceStartup + 3f;
                bool wroteEarlyPlaybackClock = false;

                while (playback.TimelineTime < 0.98d && Time.realtimeSinceStartup < deadline)
                {

                    if (!wroteEarlyPlaybackClock && playback.TimelineTime >= 0.3d)
                    {

                        WriteClockDiagnostics(playback, "Early playback");
                        wroteEarlyPlaybackClock = true;

                    }

                    yield return null;

                }

                Assert.That(playback.TimelineTime, Is.InRange(0.98d, 1.1d));
                input.SubmitLaneInput(1, InputState.currentTime);
                input.SubmitLaneRelease(1, InputState.currentTime);
                Assert.That(comboText.text, Does.Contain("2"));
                WriteClockDiagnostics(playback, "One-second playback");
                Assert.That(playback.PlaybackPositionSeconds, Is.GreaterThan(0d));
                // Native output keeps the same startup phase as existing Play/Restart and
                // recording. The channel-phase regression owns that comparison; Studio's
                // cached source cursor is not the gameplay timeline's origin.
                double startupAudioPhase = playback.PlaybackPositionSeconds - playback.TimelineTime;

                // Reuse the changed scheduling API at a nonzero song position. Its short
                // requested delay must still reserve enough native preparation time.
                // Both fixture notes are already judged, so this jump cannot create incidental misses.
                double laterSongTarget = playback.DurationSeconds * 0.6d;
                Assert.That(laterSongTarget, Is.GreaterThan(playback.TimelineTime));
                ulong beforeLaterScheduleClock = WriteClockDiagnostics(playback, $"Before scheduling {laterSongTarget:R}");
                Assert.That(playback.SchedulePlay(0.1d, laterSongTarget,
                    out ulong laterStartClock, out int laterSampleRate), Is.True);
                Assert.That(laterStartClock - beforeLaterScheduleClock,
                    Is.GreaterThan((ulong)Math.Ceiling(0.1d * laterSampleRate)),
                    "The native scheduling budget must retain its required preparation lead.");
                Assert.That(playback.HasReachedScheduledStart, Is.False);
                Assert.That(playback.SongTime, Is.EqualTo(laterSongTarget).Within(0.000001d));
                WriteClockDiagnostics(playback, $"Later schedule accepted at {laterSongTarget:R}");
                deadline = Time.realtimeSinceStartup + 5f;

                while ((!playback.HasReachedScheduledStart || playback.TimelineTime < laterSongTarget + 0.3d ||
                    playback.PlaybackPositionSeconds < laterSongTarget + 0.3d) &&
                    Time.realtimeSinceStartup < deadline)
                {

                    yield return null;

                }

                double laterTimeline = playback.TimelineTime;
                WriteClockDiagnostics(playback, $"Later scheduling settled for {laterSongTarget:R}");
                Assert.That(laterTimeline, Is.GreaterThanOrEqualTo(laterSongTarget + 0.3d));
                Assert.That(playback.SongTime, Is.EqualTo(laterTimeline).Within(0.025d));
                Assert.That(playback.PlaybackPositionSeconds - laterTimeline,
                    Is.EqualTo(startupAudioPhase).Within(0.04d),
                    "A nonzero scheduled start must preserve the same native phase as the opening.");
                input.RequestPause();
                yield return null;
                Assert.That(playback.IsPaused, Is.True);
                double pausedTimeline = playback.TimelineTime;
                double pausedAudioPosition = playback.PlaybackPositionSeconds;
                // Preserve the existing transport contract: the game clock freezes immediately,
                // while Studio's already scheduled audio commands settle before its raw-position baseline.
                yield return new WaitForSecondsRealtime(0.25f);
                Assert.That(playback.TimelineTime, Is.EqualTo(pausedTimeline).Within(0.005d));
                Assert.That(playback.SongTime, Is.EqualTo(pausedTimeline).Within(0.005d));
                double settledPausedAudioPosition = playback.PlaybackPositionSeconds;
                TestContext.WriteLine($"Pause transition timeline movement: " +
                    $"{settledPausedAudioPosition - pausedAudioPosition:0.000}s.");
                yield return new WaitForSecondsRealtime(0.15f);
                Assert.That(playback.TimelineTime, Is.EqualTo(pausedTimeline).Within(0.005d));
                Assert.That(playback.PlaybackPositionSeconds, Is.EqualTo(settledPausedAudioPosition).Within(0.01d));
                input.RequestPause();
                deadline = Time.realtimeSinceStartup + 3f;

                while (playback.TimelineTime < pausedTimeline + 0.15d && Time.realtimeSinceStartup < deadline)
                {

                    yield return null;

                }

                Assert.That(playback.IsPaused, Is.False);
                Assert.That(playback.TimelineTime, Is.GreaterThanOrEqualTo(pausedTimeline + 0.15d));
                Assert.That(playback.SongTime, Is.EqualTo(playback.TimelineTime).Within(0.025d));
                Assert.That(playback.PlaybackPositionSeconds, Is.GreaterThan(settledPausedAudioPosition));
                Assert.That(session.IsCountingIn, Is.False);
                TestContext.WriteLine($"Later-song scheduled target: {laterSongTarget:0.000}s; " +
                    $"settled timeline: {laterTimeline:0.000}s; resume: {playback.TimelineTime:0.000}s.");

                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.R));
                yield return null;
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                yield return null;
                Assert.That(session.IsCountingIn, Is.True, "R must restart through the same preparation flow.");
                Assert.That(playback.TimelineTime, Is.LessThan(-fixtureChart.VisualLeadTime));
                Assert.That(scoreText.text, Is.EqualTo("00000000"));
                Assert.That(judgementText.text, Is.Empty);

                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Escape));
                yield return null;
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                yield return null;
                Assert.That(session.IsWaitingForStart, Is.True, "Escape must cancel preparation and return to START.");
                Assert.That(playback.IsRunning, Is.False);
                Assert.That(Object.FindObjectsByType<RuntimeNoteView>(FindObjectsSortMode.None), Is.Empty);

                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Enter));
                yield return null;
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                yield return null;
                Assert.That(session.IsCountingIn, Is.True, "Enter must also activate the waiting start prompt.");
                deadline = Time.realtimeSinceStartup + 5f;

                while (GameObject.Find("Note_start_zero_synth") == null && Time.realtimeSinceStartup < deadline)
                {

                    yield return null;

                }

                Assert.That(session.IsCountingIn, Is.True);
                Assert.That(GameObject.Find("Note_start_zero_synth"), Is.Not.Null);
                Assert.That(Object.FindObjectsByType<RuntimeTimingGuideView>(FindObjectsSortMode.None), Is.Not.Empty);
                session.enabled = false;
                Assert.That(playback.IsRunning, Is.False, "Disabling the session must cancel scheduled audio.");
                Assert.That(GetField<object>(session, "countInMetronome"), Is.Null,
                    "Disabling must dispose every scheduled preparation click.");
                yield return null;
                Assert.That(Object.FindObjectsByType<RuntimeNoteView>(FindObjectsSortMode.None), Is.Empty,
                    "Disabling must remove notes already visible during preparation.");
                Assert.That(Object.FindObjectsByType<RuntimeTimingGuideView>(FindObjectsSortMode.None), Is.Empty);
                session.enabled = true;
                yield return null;
                Assert.That(session.IsWaitingForStart, Is.True);
                Assert.That(playback.IsRunning, Is.False);
                Assert.That(Object.FindObjectsByType<RuntimeNoteView>(FindObjectsSortMode.None), Is.Empty);

                // Exercise re-enabling a session whose initial preparation did not finish.
                // FMOD preparation itself still runs against the real song event.
                session.enabled = false;
                SetField(session, "isReady", false);
                SetField(session, "phase", Enum.Parse(GetField<object>(session, "phase").GetType(), "Preparing"));
                Assert.That(GetField<bool>(session, "startCalled"), Is.True);
                session.enabled = true;
                deadline = Time.realtimeSinceStartup + 15f;

                while (!session.IsWaitingForStart && !playback.PreparationFailed &&
                    Time.realtimeSinceStartup < deadline)
                {

                    yield return null;

                }

                Assert.That(session.IsWaitingForStart, Is.True,
                    "Re-enabling an unfinished session must resume preparation and reach START.");
                Assert.That(playback.IsRunning, Is.False);
                Assert.That(JsonUtility.ToJson(originalChart), Is.EqualTo(originalJson));
                LogAssert.NoUnexpectedReceived();

            }
            finally
            {

                if (session != null)
                {

                    session.enabled = false;

                    if (originalChart != null)
                    {

                        SetField(session, "chart", originalChart);

                    }

                }

                playback?.Stop();

                if (fixtureChart != null)
                {

                    Object.Destroy(fixtureChart);

                }

                if (testMouse != null && testMouse.added)
                {

                    InputSystem.RemoveDevice(testMouse);

                }

                if (testKeyboard != null && testKeyboard.added)
                {

                    InputSystem.RemoveDevice(testKeyboard);

                }

                if (previousMouse != null && previousMouse.added)
                {

                    previousMouse.MakeCurrent();

                }

                if (previousKeyboard != null && previousKeyboard.added)
                {

                    previousKeyboard.MakeCurrent();

                }

                InputSystem.settings = previousInputSettings;

                if (testInputSettings != null)
                {

                    Object.Destroy(testInputSettings);

                }

                Application.targetFrameRate = previousTargetFrameRate;
                QualitySettings.vSyncCount = previousVSyncCount;
                Application.runInBackground = previousRunInBackground;

            }

            yield return null;

        }

        private static void ConfigureOpeningFixture(PrototypeChart chart)
        {

            JsonUtility.FromJsonOverwrite(
                "{\"visualLeadTime\":1.25," +
                "\"tempoSections\":[{\"startBar\":1,\"startTime\":0,\"beatsPerMinute\":120," +
                "\"beatsPerBar\":4,\"beatUnit\":4}]," +
                "\"activationWindows\":[{\"musicalPartId\":\"synth\",\"startTime\":0,\"endTime\":8}," +
                "{\"musicalPartId\":\"drum\",\"startTime\":0,\"endTime\":8}]," +
                "\"notes\":[{\"id\":\"start_zero_synth\",\"hitTime\":0,\"laneIndex\":3," +
                "\"musicalPartId\":\"synth\",\"noteType\":0}," +
                "{\"id\":\"start_one_drum\",\"hitTime\":1,\"laneIndex\":1," +
                "\"musicalPartId\":\"drum\",\"noteType\":0}]}", chart);

        }

        private static ulong WriteClockDiagnostics(FmodSongPlayback playback, string stage)
        {

            Type runtimeManager = Type.GetType("FMODUnity.RuntimeManager, FMODUnity", true);
            object coreSystem = runtimeManager.GetProperty("CoreSystem").GetValue(null);
            object[] masterArguments = { null };
            coreSystem.GetType().GetMethod("getMasterChannelGroup").Invoke(coreSystem, masterArguments);
            object[] masterClocks = { 0ul, 0ul };
            masterArguments[0].GetType().GetMethod("getDSPClock").Invoke(masterArguments[0], masterClocks);
            object eventInstance = GetField<object>(playback, "songInstance");
            object[] eventArguments = { null };
            eventInstance.GetType().GetMethod("getChannelGroup").Invoke(eventInstance, eventArguments);
            object[] eventClocks = { 0ul, 0ul };
            eventArguments[0].GetType().GetMethod("getDSPClock").Invoke(eventArguments[0], eventClocks);
            TestContext.WriteLine($"{stage}: master={masterClocks[0]}, masterParent={masterClocks[1]}, " +
                $"event={eventClocks[0]}, eventParent={eventClocks[1]}, " +
                $"anchor={GetField<ulong>(playback, "anchorDspClock")}, " +
                $"rate={GetField<int>(playback, "dspSampleRate")}, " +
                $"signed={playback.TimelineTime:R}, compatibility={playback.SongTime:R}, " +
                $"FMOD={playback.PlaybackPositionSeconds:R}.");
            return (ulong)masterClocks[0];

        }

        private static T GetField<T>(object target, string name)
        {

            FieldInfo field = target.GetType().GetField(name, PrivateInstance);
            Assert.That(field, Is.Not.Null, $"Missing test boundary field: {name}");
            return (T)field.GetValue(target);

        }

        private static void SetField(object target, string name, object value)
        {

            FieldInfo field = target.GetType().GetField(name, PrivateInstance);
            Assert.That(field, Is.Not.Null, $"Missing test boundary field: {name}");
            field.SetValue(target, value);

        }

        private static void CaptureIfRequested(string filename)
        {

            string directory = Environment.GetEnvironmentVariable("IDIOT_TAPE_START_FLOW_EVIDENCE");

            if (string.IsNullOrWhiteSpace(directory))
            {

                return;

            }

            Directory.CreateDirectory(directory);
            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            RenderTexture target = new(1280, 720, 24, RenderTextureFormat.ARGB32);
            Texture2D pixels = new(1280, 720, TextureFormat.RGBA32, false);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;

            try
            {

                camera.targetTexture = target;
                RenderTexture.active = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                pixels.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
                pixels.Apply();
                string path = Path.Combine(directory, filename);
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                TestContext.WriteLine($"Start flow capture: {path}");

            }
            finally
            {

                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.Destroy(target);
                Object.Destroy(pixels);

            }

        }

    }

}
#endif
