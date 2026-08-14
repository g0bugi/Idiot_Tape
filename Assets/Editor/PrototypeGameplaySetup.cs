using System.Collections.Generic;
using IdiotTape.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IdiotTape.EditorTools
{

    public static class PrototypeGameplaySetup
    {

        private const string ChartPath = "Assets/Data/PrototypeChart.asset";
        private const string NoteSpritePath = "Assets/Art/Gameplay/SketchTapNote.png";
        private const string ScenePath = "Assets/Scenes/Gameplay.unity";

        [MenuItem("Tools/Idiot Tape/Rebuild Prototype Gameplay")]
        public static void Run()
        {

            ConfigureNoteTexture();
            PrototypeChart chart = CreateOrUpdateChart();
            CreateGameplayScene(chart);
            ConfigureLandscapeOrientation();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Idiot_Tape prototype gameplay created at {ScenePath}");

        }

        private static void ConfigureNoteTexture()
        {

            AssetDatabase.ImportAsset(NoteSpritePath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(NoteSpritePath) as TextureImporter;

            if (importer == null)
            {

                throw new System.InvalidOperationException($"Could not import note texture at {NoteSpritePath}.");

            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 1024f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();

        }

        private static PrototypeChart CreateOrUpdateChart()
        {

            EnsureFolder("Assets", "Data");
            PrototypeChart chart = AssetDatabase.LoadAssetAtPath<PrototypeChart>(ChartPath);

            if (chart == null)
            {

                chart = ScriptableObject.CreateInstance<PrototypeChart>();
                AssetDatabase.CreateAsset(chart, ChartPath);

            }

            SerializedObject serializedChart = new(chart);
            serializedChart.FindProperty("laneCount").intValue = 8;
            serializedChart.FindProperty("visualLeadTime").floatValue = 2.4f;

            SerializedProperty parts = serializedChart.FindProperty("musicalParts");
            parts.arraySize = 3;
            SetPart(parts.GetArrayElementAtIndex(0), "pulse", new Color(0.94f, 0.92f, 0.86f, 1f));
            SetPart(parts.GetArrayElementAtIndex(1), "blue", new Color(0.28f, 0.5f, 0.95f, 1f));
            SetPart(parts.GetArrayElementAtIndex(2), "pink", new Color(0.95f, 0.31f, 0.58f, 1f));

            int[] pattern =
            {

                0, 2, 4, 6, 7, 5, 3, 1,
                1, 4, 6, 3, 0, 3, 6, 4,
                7, 5, 2, 0, 2, 5, 7, 4,
                3, 1, 4, 6, 5, 2, 0, 7

            };

            SerializedProperty notes = serializedChart.FindProperty("notes");
            notes.arraySize = pattern.Length;

            for (int index = 0; index < pattern.Length; index++)
            {

                string partId = index % 4 == 0 ? "pulse" : index % 2 == 0 ? "pink" : "blue";
                SetNote(
                    notes.GetArrayElementAtIndex(index),
                    $"prototype_{index + 1:000}",
                    1.5d + index * 0.34d,
                    pattern[index],
                    partId);

            }

            serializedChart.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(chart);
            return chart;

        }

        private static void CreateGameplayScene(PrototypeChart chart)
        {

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new("Main Camera");
            Camera gameplayCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            gameplayCamera.orthographic = true;
            gameplayCamera.orthographicSize = 5.4f;
            gameplayCamera.clearFlags = CameraClearFlags.SolidColor;
            gameplayCamera.backgroundColor = Color.black;

            GameObject gameplayRoot = new("GameplayRoot");
            AudioSource audioSource = gameplayRoot.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            DspSongClock songClock = gameplayRoot.AddComponent<DspSongClock>();
            GameplayInputRouter inputRouter = gameplayRoot.AddComponent<GameplayInputRouter>();
            GameplaySession gameplaySession = gameplayRoot.AddComponent<GameplaySession>();

            GameObject presentationRoot = new("PresentationRoot");
            GameObject noteRoot = new("RuntimeNotes");
            noteRoot.transform.SetParent(presentationRoot.transform, false);
            PlayfieldPresenter presenter = presentationRoot.AddComponent<PlayfieldPresenter>();

            GameObject canvasObject = new("HUD Canvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            GameplayHud hud = canvasObject.AddComponent<GameplayHud>();

            Image progressBackground = CreateImage(
                "ProgressBackground",
                canvasObject.transform,
                new Color(1f, 1f, 1f, 0.12f),
                new Vector2(0.18f, 0.955f),
                new Vector2(0.82f, 0.963f));
            Image progressFill = CreateImage(
                "ProgressFill",
                progressBackground.transform,
                new Color(0.92f, 0.91f, 0.88f, 0.8f),
                Vector2.zero,
                Vector2.one);
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = 0;
            progressFill.fillAmount = 0f;

            Text scoreText = CreateText(
                "Score",
                canvasObject.transform,
                "00000000",
                34,
                TextAnchor.UpperRight,
                new Vector2(0.78f, 0.885f),
                new Vector2(0.965f, 0.95f));
            Text comboText = CreateText(
                "Combo",
                canvasObject.transform,
                string.Empty,
                32,
                TextAnchor.UpperCenter,
                new Vector2(0.38f, 0.86f),
                new Vector2(0.62f, 0.94f));
            Text judgementText = CreateText(
                "Judgement",
                canvasObject.transform,
                string.Empty,
                42,
                TextAnchor.MiddleCenter,
                new Vector2(0.35f, 0.2f),
                new Vector2(0.65f, 0.32f));
            judgementText.fontStyle = FontStyle.Bold;

            SetObjectReference(songClock, "audioSource", audioSource);
            SetObjectReference(presenter, "gameplayCamera", gameplayCamera);
            SetObjectReference(presenter, "noteRoot", noteRoot.transform);
            SetObjectReference(presenter, "noteSprite", AssetDatabase.LoadAssetAtPath<Sprite>(NoteSpritePath));
            SetObjectReference(hud, "scoreText", scoreText);
            SetObjectReference(hud, "comboText", comboText);
            SetObjectReference(hud, "judgementText", judgementText);
            SetObjectReference(hud, "progressFill", progressFill);
            SetObjectReference(gameplaySession, "chart", chart);
            SetObjectReference(gameplaySession, "songClock", songClock);
            SetObjectReference(gameplaySession, "inputRouter", inputRouter);
            SetObjectReference(gameplaySession, "presenter", presenter);
            SetObjectReference(gameplaySession, "hud", hud);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

        }

        private static void ConfigureLandscapeOrientation()
        {

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

        }

        private static void SetPart(SerializedProperty part, string id, Color color)
        {

            part.FindPropertyRelative("id").stringValue = id;
            part.FindPropertyRelative("color").colorValue = color;

        }

        private static void SetNote(SerializedProperty note, string id, double hitTime, int laneIndex, string partId)
        {

            note.FindPropertyRelative("id").stringValue = id;
            note.FindPropertyRelative("hitTime").doubleValue = hitTime;
            note.FindPropertyRelative("laneIndex").intValue = laneIndex;
            note.FindPropertyRelative("musicalPartId").stringValue = partId;

        }

        private static Image CreateImage(
            string objectName,
            Transform parent,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {

            GameObject imageObject = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            return image;

        }

        private static Text CreateText(
            string objectName,
            Transform parent,
            string value,
            int fontSize,
            TextAnchor alignment,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {

            GameObject textObject = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.92f, 0.91f, 0.88f, 0.9f);
            return text;

        }

        private static void SetObjectReference(Object target, string propertyName, Object value)
        {

            SerializedObject serializedObject = new(target);
            serializedObject.FindProperty(propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

        }

        private static void AddSceneToBuildSettings(string scenePath)
        {

            List<EditorBuildSettingsScene> scenes = new()
            {

                new EditorBuildSettingsScene(scenePath, true)

            };

            foreach (EditorBuildSettingsScene existingScene in EditorBuildSettings.scenes)
            {

                if (existingScene.path != scenePath)
                {

                    scenes.Add(existingScene);

                }

            }

            EditorBuildSettings.scenes = scenes.ToArray();

        }

        private static void EnsureFolder(string parent, string child)
        {

            string path = $"{parent}/{child}";

            if (!AssetDatabase.IsValidFolder(path))
            {

                AssetDatabase.CreateFolder(parent, child);

            }

        }

    }

}
