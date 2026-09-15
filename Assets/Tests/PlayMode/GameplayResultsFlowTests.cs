using System;
using System.Collections;
using System.IO;
using System.Reflection;
using IdiotTape.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class GameplayResultsFlowTests
    {

        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator SongEndFreezesResultsAndRoutesPartSelectionRetryAndBack()
        {

            GameplaySession session = null;
            FmodSongPlayback playback = null;
            PrototypeChart original = null;
            PrototypeChart fixture = null;
            int oldFrameRate = Application.targetFrameRate;
            int oldVSync = QualitySettings.vSyncCount;
            bool oldRunInBackground = Application.runInBackground;

            try
            {

                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 60;
                Application.runInBackground = true;
                yield return SceneManager.LoadSceneAsync("Gameplay");
                session = Object.FindAnyObjectByType<GameplaySession>();
                playback = Object.FindAnyObjectByType<FmodSongPlayback>();
                GameplayHud hud = Object.FindAnyObjectByType<GameplayHud>();
                GameplayInputRouter input = Object.FindAnyObjectByType<GameplayInputRouter>();
                yield return WaitFor(() => session.IsWaitingForStart, 15f, "audio preparation");
                Assert.That(hud.transform.Find("StartPrompt/SongTitle").GetComponent<Text>().text, Is.EqualTo("Snow"));
                Font font = hud.transform.Find("StartPrompt/SongTitle").GetComponent<Text>().font;
                Assert.That(font.HasCharacter('연'), Is.True, "The mobile build must include Korean glyphs.");
                Capture("01-start", hud);
                original = Get<PrototypeChart>(session, "chart");
                fixture = Object.Instantiate(original);
                fixture.hideFlags = HideFlags.DontSave;
                JsonUtility.FromJsonOverwrite("{\"visualLeadTime\":0.5," +
                    "\"tempoSections\":[{\"startBar\":1,\"startTime\":0,\"beatsPerMinute\":120,\"beatsPerBar\":4,\"beatUnit\":4}]," +
                    "\"activationWindows\":[{\"musicalPartId\":\"drum\",\"startTime\":0,\"endTime\":3}]," +
                    "\"notes\":[{\"id\":\"hit\",\"hitTime\":0.3,\"laneIndex\":0,\"musicalPartId\":\"drum\",\"noteType\":0}," +
                    "{\"id\":\"miss\",\"hitTime\":0.6,\"laneIndex\":7,\"musicalPartId\":\"drum\",\"noteType\":0}," +
                    "{\"id\":\"inactive\",\"hitTime\":0.8,\"laneIndex\":4,\"musicalPartId\":\"bass\",\"noteType\":0}]}", fixture);
                Assert.That(fixture.TryValidate(out string error), Is.True, error);
                Set(session, "chart", fixture);
                Set(session, "preparationBars", 1);
                session.RequestStart();
                yield return WaitFor(() => !session.IsCountingIn && playback.TimelineTime >= 0.3d, 8f,
                    "count-in", () => $"time={playback.TimelineTime}, phase={Get<object>(session, "phase")}, running={playback.IsRunning}");
                Text partLabel = Get<Text>(hud, "instrumentText");
                Assert.That(partLabel.text, Is.EqualTo("Drum"), "Section appears before any judgement.");
                input.SubmitLaneInput(0, InputState.currentTime);
                input.SubmitLaneRelease(0, InputState.currentTime);
                yield return WaitFor(() => playback.TimelineTime >= 2d, 4f, "opening notes");
                Assert.That(partLabel.text, Is.EqualTo("Drum"));
                Assert.That(partLabel.color.a, Is.GreaterThan(0.9f), "Hit feedback must not fade the section label.");
                GameplayPerformance performance = Get<GameplayPerformance>(session, "performance");
                Assert.That(performance.Total.Perfect + performance.Total.Good, Is.EqualTo(1));
                Assert.That(performance.Total.Miss, Is.EqualTo(1));
                Assert.That(performance.GetPart("bass"), Is.Null, "Inactive notes cannot appear as misses.");
                Assert.That(performance.MaximumCombo, Is.EqualTo(1));
                Assert.That(session.IsShowingResults, Is.False, "The uncharted audio outro still plays.");
                input.RequestPause();
                Invoke(session, "TryCompleteSession", double.MaxValue);
                Assert.That(session.IsShowingResults, Is.False, "Pause cannot finish an attempt.");
                input.RequestPause();
                // Use the verified scheduled-target path to reach the event tail. Live seek has
                // a separately documented FMOD anchor issue and is not part of the result flow.
                Assert.That(playback.SchedulePlay(0.2d, playback.DurationSeconds - 0.25d, out _, out _), Is.True);
                yield return WaitFor(() => session.IsShowingResults, 5f, "audio end",
                    () => $"time={playback.TimelineTime}, FMOD={playback.PlaybackPositionSeconds}, end={Get<double>(session, "sessionEndTime")}, " +
                        $"phase={Get<object>(session, "phase")}, active={Get<IList>(session, "activeNotes").Count}, next={Get<int>(session, "nextNoteIndex")}");
                Assert.That(playback.IsRunning, Is.False);
                Assert.That(hud.transform.Find("ResultsPrompt").gameObject.activeSelf, Is.True);
                int finalScore = Get<int>(session, "score");
                int finalJudgements = performance.Total.Total;
                input.SubmitLaneInput(7, InputState.currentTime);
                input.SubmitFlickAssist(InputState.currentTime);
                yield return null;
                Assert.That(Get<int>(session, "score"), Is.EqualTo(finalScore));
                Assert.That(performance.Total.Total, Is.EqualTo(finalJudgements));
                Assert.That(hud.TrySetNoteSpeedFromScreenPosition(Vector2.zero, false), Is.False);
                RectTransform row = Area(hud, "ResultsPrompt/ResultPart0");
                Assert.That(hud.TrySelectResultPart(Point(row)), Is.True);
                Assert.That(hud.transform.Find("ResultsPrompt/ResultScope").GetComponent<Text>().text, Is.EqualTo("Drum 판정"));
                Press(session, Point(Area(hud, "ResultsPrompt/AllPartsButton")));
                Assert.That(hud.transform.Find("ResultsPrompt/ResultScope").GetComponent<Text>().text, Is.EqualTo("전체 판정"));
                Press(session, Point(Area(hud, "ResultsPrompt/ResultRetryButton")));
                Assert.That(session.IsCountingIn, Is.True);
                Assert.That(partLabel.text, Is.Empty, "Retry clears the preceding section.");
                Assert.That(performance.Total.Total, Is.Zero);
                Assert.That(performance.MaximumCombo, Is.Zero);
                Assert.That(Get<int>(session, "score"), Is.Zero);
                Assert.That(hud.transform.Find("ResultsPrompt").gameObject.activeSelf, Is.False);
                input.RequestPause();
                Assert.That(session.IsWaitingForStart, Is.True);

                // A richer deterministic display fixture exercises row paging, long names,
                // screen sizes and the supplied visual design without persisting any chart edit.
                JsonUtility.FromJsonOverwrite("{\"musicalParts\":[" +
                    "{\"id\":\"drum\",\"displayName\":\"Drum\",\"color\":{\"r\":0.91,\"g\":0.88,\"b\":0.76,\"a\":1}}," +
                    "{\"id\":\"synth\",\"displayName\":\"Synth\",\"color\":{\"r\":0.39,\"g\":0.8,\"b\":0.87,\"a\":1}}," +
                    "{\"id\":\"bass\",\"displayName\":\"Bass\",\"color\":{\"r\":0.95,\"g\":0.48,\"b\":0.67,\"a\":1}}," +
                    "{\"id\":\"other\",\"displayName\":\"Additional musical layer\",\"color\":{\"r\":1,\"g\":1,\"b\":1,\"a\":1}}]}", fixture);
                AddCounts(performance, "drum", 91, 3, 2);
                AddCounts(performance, "synth", 130, 10, 4);
                AddCounts(performance, "bass", 177, 31, 3);
                performance.ObserveCombo(186);
                hud.ShowResults(fixture, 428800, performance);
                Capture("02-results", hud);
                performance.Record("other", JudgementGrade.Perfect, 1);
                hud.ShowResults(fixture, 429800, performance);
                Assert.That(hud.TrySelectResultPart(Point(Area(hud, "ResultsPrompt/NextParts"))), Is.True);
                Assert.That(hud.transform.Find("ResultsPrompt/ResultPart0/PartName").GetComponent<Text>().text,
                    Is.EqualTo("Additional musical layer"));
                // Restore the actual session result phase to exercise the back button's routing.
                Set(session, "phase", Enum.Parse(typeof(GameplaySession).GetNestedType("SessionPhase", BindingFlags.NonPublic), "Results"));
                Press(session, Point(Area(hud, "ResultsPrompt/ResultBackButton")));
                Assert.That(session.IsWaitingForStart, Is.True);
                Assert.That(hud.transform.Find("ResultsPrompt").gameObject.activeSelf, Is.False);
                Assert.That(performance.Total.Total, Is.Zero);
                hud.ShowResults(fixture, 0, performance);
                Assert.That(hud.transform.Find("ResultsPrompt/EmptyResults").gameObject.activeSelf, Is.True);
                Assert.That(hud.transform.Find("ResultsPrompt/ResultPart0").gameObject.activeSelf, Is.False);

            }
            finally
            {

                if (playback != null)
                {

                    playback.Stop();

                }

                if (session != null && original != null)
                {

                    Set(session, "chart", original);

                }

                if (fixture != null)
                {

                    Object.Destroy(fixture);

                }
                Application.targetFrameRate = oldFrameRate;
                QualitySettings.vSyncCount = oldVSync;
                Application.runInBackground = oldRunInBackground;

            }

        }

        private static void AddCounts(GameplayPerformance performance, string part, int perfect, int good, int miss)
        {

            for (int index = 0; index < perfect; index++)
            {

                performance.Record(part, JudgementGrade.Perfect, 1);

            }

            for (int index = 0; index < good; index++)
            {

                performance.Record(part, JudgementGrade.Good, 1);

            }

            for (int index = 0; index < miss; index++)
            {

                performance.Record(part, JudgementGrade.Miss, 0);

            }

        }

        private static IEnumerator WaitFor(Func<bool> condition, float seconds, string stage, Func<string> diagnostics = null)
        {

            float deadline = Time.realtimeSinceStartup + seconds;

            while (!condition() && Time.realtimeSinceStartup < deadline)
            {

                yield return null;

            }

            Assert.That(condition(), Is.True, $"Timed out at {stage}. {diagnostics?.Invoke()}");

        }

        private static RectTransform Area(GameplayHud hud, string path)
        {

            Canvas.ForceUpdateCanvases();
            return (RectTransform)hud.transform.Find(path);

        }

        private static Vector2 Point(RectTransform area)
        {

            return RectTransformUtility.WorldToScreenPoint(Camera.main, area.TransformPoint(area.rect.center));

        }

        private static void Press(GameplaySession session, Vector2 point)
        {

            Invoke(session, "HandleContactPressed", 99, point, InputState.currentTime);
            Invoke(session, "HandleContactReleased", 99, InputState.currentTime);

        }

        private static void Invoke(object target, string method, params object[] args)
        {

            target.GetType().GetMethod(method, PrivateInstance).Invoke(target, args);

        }

        private static T Get<T>(object target, string field)
        {

            return (T)target.GetType().GetField(field, PrivateInstance).GetValue(target);

        }

        private static void Set(object target, string field, object value)
        {

            target.GetType().GetField(field, PrivateInstance).SetValue(target, value);

        }

        private static void Capture(string name, GameplayHud hud)
        {

            string directory = Environment.GetEnvironmentVariable("IDIOT_TAPE_RESULTS_EVIDENCE");

            if (string.IsNullOrWhiteSpace(directory))
            {

                return;

            }

            Directory.CreateDirectory(directory);
            Camera camera = Camera.main;
            RenderTexture oldTarget = camera.targetTexture;
            RenderTexture oldActive = RenderTexture.active;
            int[,] sizes = { { 1280, 720 }, { 1920, 1080 }, { 2340, 1080 }, { 1024, 768 } };

            for (int index = 0; index < sizes.GetLength(0); index++)
            {

                int width = sizes[index, 0];
                int height = sizes[index, 1];
                RenderTexture target = new(width, height, 24);
                Texture2D pixels = new(width, height, TextureFormat.RGBA32, false);

                try
                {

                    camera.targetTexture = target;
                    RenderTexture.active = target;
                    Canvas.ForceUpdateCanvases();
                    camera.Render();
                    pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    pixels.Apply();
                    File.WriteAllBytes(Path.Combine(directory, $"{name}-{width}x{height}.png"), pixels.EncodeToPNG());

                }
                finally
                {

                    camera.targetTexture = oldTarget;
                    RenderTexture.active = oldActive;
                    Object.Destroy(target);
                    Object.Destroy(pixels);

                }

            }

        }

    }

}
