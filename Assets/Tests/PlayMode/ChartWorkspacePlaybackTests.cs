#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using IdiotTape.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class ChartWorkspacePlaybackTests
    {

        [UnityTest]
        public IEnumerator SpaceRestartsStoppedAudioAndPreservesTextEntry()
        {

            GameObject playbackObject = new("Workspace Playback Test");
            playbackObject.AddComponent(Type.GetType("FMODUnity.StudioListener, FMODUnity", true));
            FmodSongPlayback playback = playbackObject.AddComponent<FmodSongPlayback>();
            ScriptableObject window = null;
            bool previousTextEditing = EditorGUIUtility.editingTextField;
            GameplaySession session = Object.FindAnyObjectByType<GameplaySession>();
            bool sessionWasEnabled = session != null && session.enabled;

            try
            {

                playback.ConfigureEventPath("event:/Music/KIRARA/Snow", false);
                playback.Prepare();
                float deadline = Time.realtimeSinceStartup + 15f;

                while (!playback.IsPrepared && !playback.PreparationFailed && Time.realtimeSinceStartup < deadline)
                {

                    yield return null;

                }

                Assert.That(playback.IsPrepared, Is.True, "The FMOD song must prepare before testing authoring transport.");
                Type windowType = Type.GetType(
                    "IdiotTape.EditorTools.PrototypeChartRecorderWindow, IdiotTape.Gameplay.Editor", true);
                window = ScriptableObject.CreateInstance(windowType);
                window.hideFlags = HideFlags.DontSave;
                const BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
                windowType.GetField("songPlayback", members).SetValue(window, playback);
                MethodInfo keyboardHandler = windowType.GetMethod("HandleRecorderWindowKeyboardEvent", members);
                EditorGUIUtility.editingTextField = false;

                // Keep these input events in one frame so an unrelated scene's playback cannot replace this fixture.
                for (int repetition = 0; repetition < 2; repetition++)
                {

                    playback.Stop();
                    Assert.That(playback.IsRunning, Is.False);
                    PressSpace(keyboardHandler, window);
                    Assert.That(playback.IsRunning, Is.True, "Space must start the stopped song, not pause an inactive event.");
                    Assert.That(playback.IsPaused, Is.False);
                    PressSpace(keyboardHandler, window);
                    Assert.That(playback.IsPaused, Is.True);
                    PressSpace(keyboardHandler, window);
                    Assert.That(playback.IsPaused, Is.False);

                }

                EditorGUIUtility.editingTextField = true;
                Event textSpace = new() { type = EventType.KeyDown, keyCode = KeyCode.Space };
                keyboardHandler.Invoke(window, new object[] { textSpace });
                Assert.That(playback.IsPaused, Is.False, "Typing a space in a field must not pause the song.");
                Assert.That(textSpace.type, Is.EqualTo(EventType.KeyDown));
                LogAssert.NoUnexpectedReceived();

            }
            finally
            {

                EditorGUIUtility.editingTextField = previousTextEditing;

                if (window != null)
                {

                    Object.DestroyImmediate(window);

                }

                playback.Stop();
                Object.Destroy(playbackObject);

                if (session != null)
                {

                    session.enabled = sessionWasEnabled;

                }

            }

            yield return null;

        }

        private static void PressSpace(MethodInfo keyboardHandler, ScriptableObject window)
        {

            Event key = new() { type = EventType.KeyDown, keyCode = KeyCode.Space };
            keyboardHandler.Invoke(window, new object[] { key });
            Assert.That(key.type, Is.EqualTo(EventType.Used));

        }

    }

}
#endif
