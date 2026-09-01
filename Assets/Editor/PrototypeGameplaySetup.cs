using System.Collections.Generic;
using FMODUnity;
using IdiotTape.Audio;
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
            serializedChart.FindProperty("songEventPath").stringValue = "event:/Music/Idiotape/Pluto";

            SerializedProperty stemParameters = serializedChart.FindProperty("stemParameters");
            stemParameters.arraySize = 4;
            SetStemParameter(stemParameters.GetArrayElementAtIndex(0), "synth", "stems_synth1_volume");
            SetStemParameter(stemParameters.GetArrayElementAtIndex(1), "bass", "stems_bass_volume");
            SetStemParameter(stemParameters.GetArrayElementAtIndex(2), "drum", "stems_drum_volume");
            SetStemParameter(stemParameters.GetArrayElementAtIndex(3), "etc", "stems_etc_volume");

            SerializedProperty parts = serializedChart.FindProperty("musicalParts");
            parts.arraySize = 3;
            SetPart(parts.GetArrayElementAtIndex(0), "drum", "Drum", new Color(0.94f, 0.92f, 0.86f, 1f));
            SetPart(parts.GetArrayElementAtIndex(1), "synth", "Synth", new Color(0.28f, 0.5f, 0.95f, 1f));
            SetPart(parts.GetArrayElementAtIndex(2), "bass", "Bass", new Color(0.95f, 0.31f, 0.58f, 1f));

            SerializedProperty activationWindows = serializedChart.FindProperty("activationWindows");
            activationWindows.arraySize = 5;
            SetActivationWindow(activationWindows.GetArrayElementAtIndex(0), "drum", 0d, 4.5d);
            SetActivationWindow(activationWindows.GetArrayElementAtIndex(1), "bass", 4.5d, 7.2d);
            SetActivationWindow(activationWindows.GetArrayElementAtIndex(2), "drum", 7.2d, 10d);
            SetActivationWindow(activationWindows.GetArrayElementAtIndex(3), "synth", 7.2d, 13.5d);
            SetActivationWindow(activationWindows.GetArrayElementAtIndex(4), "bass", 10d, 13.5d);

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

                string partId;

                if (index <= 8)
                {

                    partId = "drum";

                }
                else if (index <= 16)
                {

                    partId = "bass";

                }
                else if (index <= 24)
                {

                    partId = index % 2 == 0 ? "drum" : "synth";

                }
                else
                {

                    partId = index % 2 == 0 ? "bass" : "synth";

                }

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
            cameraObject.AddComponent<StudioListener>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            gameplayCamera.orthographic = true;
            gameplayCamera.orthographicSize = 5.4f;
            gameplayCamera.clearFlags = CameraClearFlags.SolidColor;
            gameplayCamera.backgroundColor = Color.black;

            GameObject gameplayRoot = new("GameplayRoot");
            FmodSongPlayback songPlayback = gameplayRoot.AddComponent<FmodSongPlayback>();
            songPlayback.ConfigureEventPath(chart.SongEventPath, false);
            GameplayInputRouter inputRouter = gameplayRoot.AddComponent<GameplayInputRouter>();
            GameplaySession gameplaySession = gameplayRoot.AddComponent<GameplaySession>();

            GameObject presentationRoot = new("PresentationRoot");
            GameObject noteRoot = new("RuntimeNotes");
            noteRoot.transform.SetParent(presentationRoot.transform, false);
            PlayfieldPresenter presenter = presentationRoot.AddComponent<PlayfieldPresenter>();

            GameObject canvasObject = new("HUD Canvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = gameplayCamera;
            canvas.planeDistance = 1f;
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
                new Vector2(0.74f, 0.885f),
                new Vector2(0.92f, 0.95f));
            Text instrumentText = CreateText(
                "Instrument",
                canvasObject.transform,
                string.Empty,
                34,
                TextAnchor.UpperLeft,
                new Vector2(0.035f, 0.86f),
                new Vector2(0.25f, 0.94f));
            Text comboText = CreateText(
                "Combo",
                canvasObject.transform,
                string.Empty,
                88,
                TextAnchor.MiddleCenter,
                new Vector2(0.32f, 0.52f),
                new Vector2(0.68f, 0.74f));
            comboText.fontStyle = FontStyle.Bold;
            AddOutline(comboText, new Vector2(3f, -3f));
            Text judgementText = CreateText(
                "Judgement",
                canvasObject.transform,
                string.Empty,
                72,
                TextAnchor.MiddleCenter,
                new Vector2(0.28f, 0.38f),
                new Vector2(0.72f, 0.53f));
            judgementText.fontStyle = FontStyle.Bold;
            AddOutline(judgementText, new Vector2(3f, -3f));

            Image pauseButtonBackground = CreateImage(
                "PauseButton",
                canvasObject.transform,
                new Color(1f, 1f, 1f, 0.08f),
                new Vector2(0.945f, 0.875f),
                new Vector2(0.985f, 0.95f));
            Text pauseButtonText = CreateText(
                "PauseIcon",
                pauseButtonBackground.transform,
                "II",
                25,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.one);

            SetObjectReference(presenter, "gameplayCamera", gameplayCamera);
            SetObjectReference(presenter, "noteRoot", noteRoot.transform);
            SetObjectReference(presenter, "noteSprite", AssetDatabase.LoadAssetAtPath<Sprite>(NoteSpritePath));
            SetObjectReference(hud, "scoreText", scoreText);
            SetObjectReference(hud, "comboText", comboText);
            SetObjectReference(hud, "judgementText", judgementText);
            SetObjectReference(hud, "instrumentText", instrumentText);
            SetObjectReference(hud, "progressFill", progressFill);
            SetObjectReference(hud, "pauseButtonArea", pauseButtonBackground.rectTransform);
            SetObjectReference(hud, "pauseButtonText", pauseButtonText);
            SetObjectReference(gameplaySession, "chart", chart);
            SetObjectReference(gameplaySession, "songPlayback", songPlayback);
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

        private static void SetPart(SerializedProperty part, string id, string displayName, Color color)
        {

            part.FindPropertyRelative("id").stringValue = id;
            part.FindPropertyRelative("displayName").stringValue = displayName;
            part.FindPropertyRelative("color").colorValue = color;

        }

        private static void SetStemParameter(SerializedProperty stem, string stemId, string parameterName)
        {

            stem.FindPropertyRelative("stemId").stringValue = stemId;
            stem.FindPropertyRelative("parameterName").stringValue = parameterName;

        }

        private static void SetActivationWindow(
            SerializedProperty window,
            string partId,
            double startTime,
            double endTime)
        {

            window.FindPropertyRelative("musicalPartId").stringValue = partId;
            window.FindPropertyRelative("startTime").doubleValue = startTime;
            window.FindPropertyRelative("endTime").doubleValue = endTime;

        }

        private static void SetNote(SerializedProperty note, string id, double hitTime, int laneIndex, string partId)
        {

            note.FindPropertyRelative("id").stringValue = id;
            note.FindPropertyRelative("hitTime").doubleValue = hitTime;
            note.FindPropertyRelative("laneIndex").intValue = laneIndex;
            note.FindPropertyRelative("musicalPartId").stringValue = partId;
            note.FindPropertyRelative("noteType").enumValueIndex = (int)ChartNoteType.Tap;
            note.FindPropertyRelative("endTime").doubleValue = hitTime;
            note.FindPropertyRelative("endLaneIndex").intValue = laneIndex;
            note.FindPropertyRelative("slideEndBehavior").enumValueIndex =
                (int)SlideEndBehavior.Normal;
            note.FindPropertyRelative("slideNodes").arraySize = 0;
            note.FindPropertyRelative("bananaCurveHandles").arraySize = 0;
            note.FindPropertyRelative("bananaCheckpoints").arraySize = 0;
            note.FindPropertyRelative("bananaMaximumBonusCombo").intValue = 4;

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

        private static void AddOutline(Text text, Vector2 effectDistance)
        {

            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.82f);
            outline.effectDistance = effectDistance;
            outline.useGraphicAlpha = true;

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
