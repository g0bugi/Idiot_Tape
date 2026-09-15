using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace IdiotTape.Gameplay
{

    public sealed partial class GameplayHud
    {

        private static readonly Color MenuBackground = new(0.063f, 0.086f, 0.098f, 1f);
        private static readonly Color MenuForeground = new(0.95f, 0.937f, 0.894f, 1f);
        private static readonly Color MenuMuted = new(0.63f, 0.68f, 0.68f, 1f);
        private static readonly Color MenuSurface = new(0.098f, 0.137f, 0.153f, 1f);
        private static readonly Color MenuRule = new(0.2f, 0.25f, 0.267f, 1f);
        private static readonly Color MenuAccent = new(0.514f, 0.85f, 0.875f, 1f);
        private static readonly Color MenuMiss = new(0.922f, 0.439f, 0.569f, 1f);

        private sealed class ResultPartRow
        {

            public RectTransform Area;
            public Image Background;
            public Image Dot;
            public Text Name;
            public Text Miss;
            public Text Counts;
            public Image[] Bars;
            public MusicalPartDefinition Part;

        }

        private readonly List<MusicalPartDefinition> resultParts = new();
        private readonly List<ResultPartRow> resultRows = new();
        private GameObject menuBackdrop;
        private GameObject resultsPrompt;
        private Font menuFont;
        private Text startArtistText;
        private Text resultSongText;
        private Text resultArtistText;
        private Text resultScoreText;
        private Text resultComboText;
        private Text resultScopeText;
        private Text resultSpeedText;
        private Text resultPageText;
        private Text emptyResultsText;
        private Text[] resultGradeTexts;
        private RectTransform resultRetryArea;
        private RectTransform resultBackArea;
        private RectTransform resultAllArea;
        private RectTransform previousPartsArea;
        private RectTransform nextPartsArea;
        private GameplayPerformance displayedPerformance;
        private string selectedResultPart;
        private int resultPage;
        private int defaultPreparationBarCount = 2;
        private int shortPreparationBarCount = 1;
        private bool menuShowingStart;
        private bool menuShowingResults;
        private Rect previousSafeArea;
        private Vector2 previousScreenSize;
        private int gameplayCanvasOrder;

        public void ConfigurePreparation(int defaultBars, int shortBars)
        {

            defaultPreparationBarCount = defaultBars;
            shortPreparationBarCount = shortBars;
            UpdatePreparationText();

        }

        private void BuildStartPrompt()
        {

            menuFont = Resources.Load<Font>("Gameplay/NanumGothic-Regular");
            gameplayCanvasOrder = hudCanvas != null ? hudCanvas.sortingOrder : 0;
            menuBackdrop = CreateImageObject("MenuBackdrop", transform, MenuBackground, Vector2.zero, Vector2.one);
            menuBackdrop.SetActive(false);
            startPrompt = BuildMenuRoot("StartPrompt", "READY TO PLAY");
            Transform parent = startPrompt.transform;
            MenuText("ReadyLabel", parent, "READY TO PLAY", 27, TextAnchor.MiddleCenter,
                new Vector2(0.2f, 0.73f), new Vector2(0.8f, 0.78f), MenuMuted);
            songTitleText = MenuText("SongTitle", parent, "", 120, TextAnchor.MiddleCenter,
                new Vector2(0.1f, 0.555f), new Vector2(0.9f, 0.71f), MenuForeground);
            startArtistText = MenuText("Artist", parent, "", 30, TextAnchor.MiddleCenter,
                new Vector2(0.2f, 0.49f), new Vector2(0.8f, 0.545f), MenuMuted);
            preparationButtonArea = MenuButton("PreparationButton", parent, "", MenuSurface,
                new Vector2(0.3f, 0.285f), new Vector2(0.7f, 0.37f), out preparationText);
            UpdatePreparationText();
            startButtonArea = MenuButton("StartButton", parent, "연주 시작   /   START", MenuAccent,
                new Vector2(0.34f, 0.13f), new Vector2(0.66f, 0.225f), out _);
            MenuText("StartHint", parent, "속도와 준비 시간을 정하고 시작하세요", 24, TextAnchor.MiddleCenter,
                new Vector2(0.15f, 0.04f), new Vector2(0.85f, 0.09f), MenuMuted);
            BuildResultsPrompt();

        }

        private GameObject BuildMenuRoot(string objectName, string status)
        {

            GameObject root = CreateImageObject(objectName, transform, MenuBackground, Vector2.zero, Vector2.one);
            MenuText("Brand", root.transform, "IDIOT_TAPE", 29, TextAnchor.MiddleLeft,
                new Vector2(0.055f, 0.89f), new Vector2(0.46f, 0.96f), MenuForeground);
            MenuText("Status", root.transform, status, 24, TextAnchor.MiddleRight,
                new Vector2(0.52f, 0.89f), new Vector2(0.945f, 0.96f), MenuMuted);
            CreateImageObject("HeaderRule", root.transform, MenuRule,
                new Vector2(0.055f, 0.866f), new Vector2(0.945f, 0.868f));
            GameObject curve = new("MenuCurve", typeof(RectTransform), typeof(CanvasRenderer), typeof(GameplayMenuCurve));
            curve.transform.SetParent(root.transform, false);
            SetAnchors(curve.GetComponent<RectTransform>(), new Vector2(0.02f, 0.105f), new Vector2(0.98f, 0.14f));
            GameplayMenuCurve graphic = curve.GetComponent<GameplayMenuCurve>();
            graphic.color = MenuRule;
            graphic.raycastTarget = false;
            root.SetActive(false);
            return root;

        }

        private void BuildResultsPrompt()
        {

            resultsPrompt = BuildMenuRoot("ResultsPrompt", "SESSION COMPLETE");
            Transform parent = resultsPrompt.transform;
            resultSongText = MenuText("ResultSong", parent, "", 70, TextAnchor.MiddleLeft,
                new Vector2(0.14f, 0.71f), new Vector2(0.5f, 0.815f), MenuForeground);
            resultArtistText = MenuText("ResultArtist", parent, "", 25, TextAnchor.MiddleLeft,
                new Vector2(0.14f, 0.66f), new Vector2(0.5f, 0.71f), MenuMuted);
            BuildCoverMark(parent);
            MenuText("ScoreLabel", parent, "TOTAL SCORE", 25, TextAnchor.MiddleLeft,
                new Vector2(0.055f, 0.58f), new Vector2(0.49f, 0.63f), MenuMuted);
            resultScoreText = MenuText("ResultScore", parent, "0", 135, TextAnchor.MiddleLeft,
                new Vector2(0.05f, 0.44f), new Vector2(0.5f, 0.585f), MenuForeground);
            CreateImageObject("ComboRule", parent, MenuRule, new Vector2(0.055f, 0.425f), new Vector2(0.49f, 0.427f));
            MenuText("ComboLabel", parent, "MAX COMBO", 25, TextAnchor.MiddleLeft,
                new Vector2(0.055f, 0.335f), new Vector2(0.27f, 0.42f), MenuMuted);
            resultComboText = MenuText("ResultMaximumCombo", parent, "0", 56, TextAnchor.MiddleLeft,
                new Vector2(0.29f, 0.335f), new Vector2(0.49f, 0.42f), MenuForeground);
            resultScopeText = MenuText("ResultScope", parent, "전체 판정", 25, TextAnchor.MiddleLeft,
                new Vector2(0.055f, 0.28f), new Vector2(0.32f, 0.33f), MenuMuted);
            resultAllArea = MenuButton("AllPartsButton", parent, "전체 보기", MenuSurface,
                new Vector2(0.35f, 0.275f), new Vector2(0.49f, 0.335f), out _);
            string[] grades = { "PERFECT", "GOOD", "MISS" };
            Color[] colors = { new(0.91f, 0.88f, 0.76f), MenuAccent, MenuMiss };
            resultGradeTexts = new Text[3];

            for (int index = 0; index < grades.Length; index++)
            {

                float x = 0.055f + index * 0.15f;
                MenuText(grades[index] + "Label", parent, grades[index], 23, TextAnchor.MiddleLeft,
                    new Vector2(x, 0.233f), new Vector2(x + 0.14f, 0.273f), colors[index]);
                resultGradeTexts[index] = MenuText("Result" + grades[index], parent, "0", 48, TextAnchor.MiddleLeft,
                    new Vector2(x, 0.16f), new Vector2(x + 0.14f, 0.233f), MenuForeground);

            }

            MenuText("PartsTitle", parent, "파트별 연주", 34, TextAnchor.MiddleLeft,
                new Vector2(0.57f, 0.735f), new Vector2(0.945f, 0.815f), MenuForeground);
            MenuText("PartsHint", parent, "파트를 누르면 판정 집계를 볼 수 있어요", 22, TextAnchor.MiddleLeft,
                new Vector2(0.57f, 0.68f), new Vector2(0.945f, 0.735f), MenuMuted);

            for (int index = 0; index < 3; index++)
            {

                float top = 0.66f - index * 0.155f;
                GameObject rowObject = CreateImageObject("ResultPart" + index, parent, MenuBackground,
                    new Vector2(0.56f, top - 0.14f), new Vector2(0.945f, top));
                Transform rowParent = rowObject.transform;
                ResultPartRow row = new()
                {
                    Area = rowObject.GetComponent<RectTransform>(),
                    Background = rowObject.GetComponent<Image>(),
                    Name = MenuText("PartName", rowParent, "", 32, TextAnchor.MiddleLeft,
                        new Vector2(0.065f, 0.54f), new Vector2(0.74f, 0.98f), MenuForeground),
                    Miss = MenuText("MissCount", rowParent, "", 22, TextAnchor.MiddleRight,
                        new Vector2(0.76f, 0.54f), new Vector2(0.97f, 0.98f), MenuMuted),
                    Counts = MenuText("PartCounts", rowParent, "", 21, TextAnchor.MiddleLeft,
                        new Vector2(0.025f, 0.05f), new Vector2(0.975f, 0.35f), MenuMuted),
                    Bars = new Image[3]
                };
                row.Dot = CreateImageObject("PartColor", rowParent, MenuForeground,
                    new Vector2(0.025f, 0.69f), new Vector2(0.04f, 0.79f)).GetComponent<Image>();
                row.Dot.sprite = GameplayVisualAssets.CircleSprite;

                for (int bar = 0; bar < 3; bar++)
                {

                    row.Bars[bar] = CreateImageObject("Bar" + bar, rowParent, MenuMuted,
                        new Vector2(0.025f, 0.41f), new Vector2(0.975f, 0.45f)).GetComponent<Image>();

                }

                resultRows.Add(row);

            }

            emptyResultsText = MenuText("EmptyResults", parent, "이번 곡에는 연주한 노트가 없습니다", 25,
                TextAnchor.MiddleCenter, new Vector2(0.56f, 0.36f), new Vector2(0.945f, 0.59f), MenuMuted);
            previousPartsArea = MenuButton("PreviousParts", parent, "이전", MenuSurface,
                new Vector2(0.57f, 0.145f), new Vector2(0.68f, 0.205f), out _);
            nextPartsArea = MenuButton("NextParts", parent, "다음", MenuSurface,
                new Vector2(0.835f, 0.145f), new Vector2(0.945f, 0.205f), out _);
            resultPageText = MenuText("PartsPage", parent, "", 22, TextAnchor.MiddleCenter,
                new Vector2(0.69f, 0.145f), new Vector2(0.825f, 0.205f), MenuMuted);
            resultSpeedText = MenuText("ResultSpeed", parent, "", 23, TextAnchor.MiddleLeft,
                new Vector2(0.055f, 0.035f), new Vector2(0.44f, 0.105f), MenuMuted);
            resultBackArea = MenuButton("ResultBackButton", parent, "시작 화면", MenuSurface,
                new Vector2(0.575f, 0.025f), new Vector2(0.745f, 0.112f), out _);
            resultRetryArea = MenuButton("ResultRetryButton", parent, "다시 하기", MenuAccent,
                new Vector2(0.765f, 0.025f), new Vector2(0.945f, 0.112f), out _);

        }

        public void ShowResults(PrototypeChart chart, int score, GameplayPerformance performance)
        {

            displayedPerformance = performance;
            resultPage = 0;
            selectedResultPart = null;
            resultParts.Clear();

            for (int index = 0; index < chart.MusicalParts.Count; index++)
            {

                MusicalPartDefinition part = chart.MusicalParts[index];

                if (performance.GetPart(part.Id) != null)
                {

                    resultParts.Add(part);

                }

            }

            resultSongText.text = chart.SongTitle;
            resultArtistText.text = chart.ArtistName;
            resultScoreText.text = score.ToString("N0", CultureInfo.InvariantCulture);
            resultComboText.text = performance.MaximumCombo.ToString();
            resultSpeedText.text = $"NOTE SPEED  x{noteSpeedMultiplier:0.0}";
            SetMenuVisibility(false, true);
            countInDisplay.SetActive(false);
            SetPreparationControlsLocked(true);
            SetPauseButtonVisible(false);
            comboAnimation.Stop();
            judgementAnimation.Stop();
            instrumentAnimation.Stop();
            RefreshResultParts();
            ShowResultCounts(performance.Total, "전체");

        }

        public bool IsResultRetryPress(Vector2 position)
        {

            return ContainsScreenPoint(resultRetryArea, position);

        }

        public bool IsResultBackPress(Vector2 position)
        {

            return ContainsScreenPoint(resultBackArea, position);

        }

        public bool TrySelectResultPart(Vector2 position)
        {

            if (!menuShowingResults)
            {

                return false;

            }

            if (ContainsScreenPoint(previousPartsArea, position) || ContainsScreenPoint(nextPartsArea, position))
            {

                resultPage += ContainsScreenPoint(previousPartsArea, position) ? -1 : 1;
                RefreshResultParts();
                return true;

            }

            if (ContainsScreenPoint(resultAllArea, position))
            {

                selectedResultPart = null;
                ShowResultCounts(displayedPerformance.Total, "전체");
                RefreshResultParts();
                return true;

            }

            for (int index = 0; index < resultRows.Count; index++)
            {

                ResultPartRow row = resultRows[index];

                if (!ContainsScreenPoint(row.Area, position))
                {

                    continue;

                }

                selectedResultPart = selectedResultPart == row.Part.Id ? null : row.Part.Id;
                ShowResultCounts(selectedResultPart == null ? displayedPerformance.Total : displayedPerformance.GetPart(row.Part.Id),
                    selectedResultPart == null ? "전체" : row.Part.DisplayName);
                RefreshResultParts();
                return true;

            }

            return false;

        }

        private void ShowResultCounts(JudgementCounts counts, string scope)
        {

            resultScopeText.text = scope + " 판정";
            resultGradeTexts[0].text = counts.Perfect.ToString();
            resultGradeTexts[1].text = counts.Good.ToString();
            resultGradeTexts[2].text = counts.Miss.ToString();
            resultAllArea.gameObject.SetActive(selectedResultPart != null);

        }

        private void RefreshResultParts()
        {

            int pages = Mathf.Max(1, (resultParts.Count + 2) / 3);
            resultPage = Mathf.Clamp(resultPage, 0, pages - 1);
            previousPartsArea.gameObject.SetActive(resultPage > 0);
            nextPartsArea.gameObject.SetActive(resultPage + 1 < pages);
            resultPageText.text = pages > 1 ? $"{resultPage + 1} / {pages}" : "";
            emptyResultsText.gameObject.SetActive(resultParts.Count == 0);

            for (int index = 0; index < resultRows.Count; index++)
            {

                ResultPartRow row = resultRows[index];
                int partIndex = resultPage * 3 + index;
                row.Area.gameObject.SetActive(partIndex < resultParts.Count);

                if (partIndex >= resultParts.Count)
                {

                    continue;

                }

                row.Part = resultParts[partIndex];
                JudgementCounts counts = displayedPerformance.GetPart(row.Part.Id);
                row.Name.text = row.Part.DisplayName;
                row.Miss.text = $"MISS  {counts.Miss}";
                row.Counts.text = $"P {counts.Perfect}       G {counts.Good}       M {counts.Miss}";
                row.Dot.color = row.Part.Color;
                row.Background.color = selectedResultPart == row.Part.Id ? MenuSurface : MenuBackground;
                int[] values = { counts.Perfect, counts.Good, counts.Miss };
                float x = 0.025f;

                for (int bar = 0; bar < 3; bar++)
                {

                    float width = 0.95f * values[bar] / Mathf.Max(1, counts.Total);
                    row.Bars[bar].gameObject.SetActive(values[bar] > 0);
                    SetAnchors(row.Bars[bar].rectTransform, new Vector2(x, 0.41f), new Vector2(x + width, 0.45f));
                    Color color = bar == 2 ? MenuMiss : row.Part.Color;
                    row.Bars[bar].color = bar == 1 ? Color.Lerp(MenuBackground, color, 0.4f) : color;
                    x += width;

                }

            }

        }

        private void SetMenuVisibility(bool showStart, bool showResults)
        {

            bool changed = menuShowingStart != showStart || menuShowingResults != showResults;
            menuShowingStart = showStart;
            menuShowingResults = showResults;
            menuBackdrop.SetActive(showStart || showResults);
            startPrompt.SetActive(showStart);
            resultsPrompt.SetActive(showResults);
            RectTransform speedPanel = (RectTransform)noteSpeedSliderArea.parent;
            speedPanel.gameObject.SetActive(!showResults);

            if (changed)
            {

                // Screen-space-camera UI otherwise shares the playfield's transparent sort.
                // Menus cover note/line renderers; play keeps its serialized canvas order.
                if (hudCanvas != null)
                {

                    hudCanvas.sortingOrder = showStart || showResults ? Mathf.Max(100, gameplayCanvasOrder) : gameplayCanvasOrder;

                }

                menuBackdrop.transform.SetAsLastSibling();
                startPrompt.transform.SetAsLastSibling();
                resultsPrompt.transform.SetAsLastSibling();
                speedPanel.SetAsLastSibling();
                UpdateMenuLayout(true);

            }

        }

        private void UpdateMenuLayout(bool force = false)
        {

            Vector2 size = new(Screen.width, Screen.height);
            Rect safeArea = Screen.safeArea;

            if (size.x <= 0f || size.y <= 0f || (!force && size == previousScreenSize && safeArea == previousSafeArea))
            {

                return;

            }

            previousScreenSize = size;
            previousSafeArea = safeArea;
            Vector2 min = safeArea.position / size;
            Vector2 max = (safeArea.position + safeArea.size) / size;
            SetAnchors((RectTransform)startPrompt.transform, min, max);
            SetAnchors((RectTransform)resultsPrompt.transform, min, max);
            RectTransform speedPanel = (RectTransform)noteSpeedSliderArea.parent;

            speedPanel.Find("NoteSpeedCaption").GetComponent<Text>().fontSize = menuShowingStart ? 28 : 18;
            noteSpeedValueText.fontSize = menuShowingStart ? 28 : 20;
            Image speedBackground = speedPanel.GetComponent<Image>();
            speedBackground.sprite = menuShowingStart ? GameplayVisualAssets.RoundedRectangleSprite : null;
            speedBackground.type = Image.Type.Sliced;

            if (menuShowingStart)
            {

                SetAnchors(speedPanel, min + (max - min) * new Vector2(0.3f, 0.385f),
                    min + (max - min) * new Vector2(0.7f, 0.47f));
                speedPanel.GetComponent<Image>().color = MenuSurface;

            }
            else
            {

                SetAnchors(speedPanel, new Vector2(0.035f, 0.785f), new Vector2(0.195f, 0.835f));

            }

        }

        private Text MenuText(string objectName, Transform parent, string value, int size,
            TextAnchor alignment, Vector2 min, Vector2 max, Color color)
        {

            Text text = CreateTextObject(objectName, parent, value, size, alignment, min, max);
            text.font = menuFont != null ? menuFont : text.font;
            text.color = color;
            text.supportRichText = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Min(size, 20);
            text.resizeTextMaxSize = size;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            return text;

        }

        private RectTransform MenuButton(string objectName, Transform parent, string caption,
            Color color, Vector2 min, Vector2 max, out Text text)
        {

            GameObject button = CreateImageObject(objectName, parent, color, min, max);
            Image background = button.GetComponent<Image>();
            background.sprite = GameplayVisualAssets.RoundedRectangleSprite;
            background.type = Image.Type.Sliced;
            text = MenuText(objectName + "Caption", button.transform, caption, 29, TextAnchor.MiddleCenter,
                new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f), color == MenuAccent ? MenuBackground : MenuForeground);
            return button.GetComponent<RectTransform>();

        }

        private void BuildCoverMark(Transform parent)
        {

            GameObject cover = CreateImageObject("CoverMark", parent, MenuSurface,
                new Vector2(0.055f, 0.69f), new Vector2(0.12f, 0.805f));

            for (int index = 0; index < 4; index++)
            {

                float bottom = index == 2 ? 0.13f : 0.23f;
                CreateImageObject("Stripe" + index, cover.transform, index == 2 ? MenuMiss : MenuAccent,
                    new Vector2(0.16f + index * 0.18f, bottom), new Vector2(0.28f + index * 0.18f, bottom + 0.54f));

            }

        }

    }

}
