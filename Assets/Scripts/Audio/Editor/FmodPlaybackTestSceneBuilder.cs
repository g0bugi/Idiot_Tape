using FMODUnity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdiotTape.Audio.Editor
{

    public static class FmodPlaybackTestSceneBuilder
    {

        public const string TestScenePath = "Assets/Scenes/FmodPlaybackTest.unity";
        private const string PlutoEventPath = "event:/Music/Idiotape/Pluto";

        [MenuItem("Idiot_Tape/Audio/Create or Refresh FMOD Playback Test Scene")]
        public static void CreateOrRefreshTestScene()
        {

            EventManager.Startup();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject listenerObject = new GameObject("FMOD Listener");
            listenerObject.tag = "MainCamera";
            listenerObject.transform.position = new Vector3(0f, 0f, -10f);
            listenerObject.AddComponent<Camera>();
            listenerObject.AddComponent<StudioListener>();

            GameObject playbackObject = new GameObject("Song Playback");
            FmodSongPlayback playback = playbackObject.AddComponent<FmodSongPlayback>();
            playback.Configure(EventReference.Find(PlutoEventPath), false);

            GameObject harnessObject = new GameObject("Playback Test Harness");
            FmodSongPlaybackTestHarness harness = harnessObject.AddComponent<FmodSongPlaybackTestHarness>();
            harness.Configure(playback, true);

            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene, TestScenePath))
            {

                throw new System.InvalidOperationException($"Could not save test scene at {TestScenePath}.");

            }

            AssetDatabase.SaveAssets();
            Selection.activeGameObject = harnessObject;
            Debug.Log($"Created FMOD playback test scene at {TestScenePath}.");

        }

        [MenuItem("Idiot_Tape/Audio/Run FMOD Playback Smoke Test")]
        public static void RunFromMenu()
        {

            CreateOrRefreshTestScene();
            EditorApplication.EnterPlaymode();

        }

        public static void RunBatchSmokeTest()
        {

            CreateOrRefreshTestScene();
            EditorApplication.EnterPlaymode();

        }

    }

}
