using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace IdiotTape.Gameplay
{

    public sealed class SongLibraryView : MonoBehaviour
    {

        private const string PreferenceKey = "IdiotTape.Library.v1";
        private const float RowHeight = 116f;
        private static readonly Color Background = new(0.063f, 0.086f, 0.098f);
        private static readonly Color Surface = new(0.098f, 0.137f, 0.153f);
        private static readonly Color Ink = new(0.95f, 0.937f, 0.894f);
        private static readonly Color Muted = new(0.63f, 0.68f, 0.68f);
        private static readonly Color Accent = new(0.514f, 0.85f, 0.875f);

        [Serializable]
        private sealed class Preferences
        {

            public string songId;
            public string chartId;
            public float speed = 1f;
            public bool shortPreparation;

        }

        private sealed class Row
        {

            public RectTransform Rect;
            public Button Button;
            public Text Title;
            public Text Artist;
            public Image Cover;
            public Text Initial;
            public SongDefinition Song;

        }

        private SongCatalog catalog;
        private SongPreviewPlayer preview;
        private Action<PlayRequest> start;
        private Preferences preferences;
        private readonly List<SongDefinition> songs = new();
        private readonly List<CatalogChart> choices = new();
        private readonly List<string> difficulties = new() { "" };
        private readonly List<Row> rows = new();
        private readonly List<GameObject> partBars = new();
        private readonly Dictionary<string, double> durations = new();
        private readonly SongArtworkLoader artworkLoader = new();
        private readonly List<SongDefinition> artworkTargets = new();
        private SongDefinition selected;
        private CatalogChart selectedChart;
        private GameObject canvasObject;
        private RectTransform safeRoot;
        private Font font;
        private ScrollRect scroll;
        private RectTransform content;
        private Text countText;
        private Text titleText;
        private Text artistText;
        private Text lengthText;
        private Text partsText;
        private Text statusText;
        private Text speedText;
        private Text preparationText;
        private Text chartText;
        private Text difficultyText;
        private Text sortText;
        private Text emptyText;
        private Text previewText;
        private Image coverImage;
        private Text coverInitial;
        private Button playButton;
        private RectTransform partArea;
        private CanvasGroup controls;
        private GameObject loadingPanel;
        private Text loadingText;
        private InputField search;
        private int difficultyIndex;
        private bool sortByArtist;
        private bool loading;
        private bool previewEnabled = true;
        private Rect previousSafeArea;
        private Vector2 previousSize;
        private int firstRow = -1;

        public SongDefinition SelectedSong => selected;
        public CatalogChart SelectedChart => selectedChart;
        public int VisibleRowCount => rows.Count;

        public void Build(SongCatalog source, SongPreviewPlayer player, Action<PlayRequest> onStart, Action onCancel)
        {

            catalog = source;
            preview = player;
            start = onStart;
            preferences = new Preferences();
            try
            {

                JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString(PreferenceKey, "{}"), preferences);

            }
            catch (ArgumentException) { preferences = new Preferences(); }
            preferences.speed = float.IsNaN(preferences.speed) || float.IsInfinity(preferences.speed)
                ? 1f : NoteSpeedMath.ClampMultiplier(preferences.speed);
            font = Resources.Load<Font>("Gameplay/NanumGothic-Regular");
            if (font == null)
            {

                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            }
            canvasObject = new GameObject("Song Library Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            Panel("Background", canvasObject.transform, Background, 0, 0, 1, 1);
            safeRoot = Rect("SafeArea", canvasObject.transform, 0, 0, 1, 1);
            controls = safeRoot.gameObject.AddComponent<CanvasGroup>();
            if (EventSystem.current == null)
            {

                GameObject events = new("Library EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);

            }

            Label("Brand", safeRoot, "IDIOT_TAPE", 27, Ink, .045f, .90f, .40f, .96f);
            Label("Page", safeRoot, "MUSIC LIBRARY  /  곡 선택", 18, Muted, .65f, .90f, .95f, .96f, TextAnchor.MiddleRight);
            Panel("Divider", safeRoot, new Color(.2f, .25f, .267f), .465f, .10f, .466f, .86f);
            BuildSearch();
            MakeButton("Sort", safeRoot, "제목순", .045f, .747f, .17f, .804f, () =>
            {

                sortByArtist = !sortByArtist;
                sortText.text = sortByArtist ? "아티스트순" : "제목순";
                RefreshSongs();

            }, out sortText);
            MakeButton("Difficulty", safeRoot, "난이도 · 전체", .18f, .747f, .335f, .804f, () =>
            {

                difficultyIndex = (difficultyIndex + 1) % difficulties.Count;
                difficultyText.text = "난이도 · " + (difficultyIndex == 0 ? "전체" : difficulties[difficultyIndex]);
                RefreshSongs();

            }, out difficultyText);
            countText = Label("Count", safeRoot, "", 19, Muted, .34f, .747f, .43f, .804f, TextAnchor.MiddleRight);
            RectTransform viewport = Rect("Viewport", safeRoot, .045f, .155f, .435f, .73f);
            viewport.gameObject.AddComponent<Image>().color = Background;
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;
            scroll.viewport = viewport;
            content = Rect("Content", viewport, 0, 1, 1, 1);
            content.pivot = new Vector2(.5f, 1);
            scroll.content = content;
            scroll.onValueChanged.AddListener(_ => BindRows());
            emptyText = Label("Empty", safeRoot, "조건에 맞는 곡이 없습니다.\n검색어나 난이도를 바꿔 보세요.", 24, Muted,
                .065f, .35f, .42f, .55f, TextAnchor.MiddleCenter);
            Label("BrowseHint", safeRoot, "음악을 고르고, 리듬 속으로.", 21, Muted, .045f, .06f, .435f, .12f);

            Image artwork = Panel("Artwork", safeRoot, Surface, .51f, .57f, .95f, .855f);
            coverImage = Panel("Cover", artwork.transform, Color.white, 0, 0, 1, 1);
            coverImage.preserveAspect = true;
            coverInitial = Label("CoverInitial", artwork.transform, "", 110, Accent, .06f, .10f, .62f, .90f);
            RectTransform curve = Rect("MusicCurve", artwork.transform, .04f, .13f, .96f, .55f);
            curve.gameObject.AddComponent<GameplayMenuCurve>().color = new Color(.51f, .85f, .875f, .3f);
            Label("ArtworkCaption", artwork.transform, "SELECTED TRACK", 17, Muted, .68f, .72f, .96f, .90f, TextAnchor.MiddleRight);
            titleText = Label("SongTitle", safeRoot, "", 51, Ink, .51f, .483f, .95f, .563f);
            artistText = Label("Artist", safeRoot, "", 24, Muted, .51f, .435f, .78f, .49f);
            lengthText = Label("Length", safeRoot, "", 20, Muted, .78f, .435f, .95f, .49f, TextAnchor.MiddleRight);
            MakeButton("Chart", safeRoot, "", .51f, .354f, .77f, .416f, CycleChart, out chartText);
            MakeButton("Preview", safeRoot, "미리듣기 켜짐", .79f, .354f, .95f, .416f, () =>
            {

                previewEnabled = !previewEnabled;
                ResumePreview();

            }, out previewText);
            partsText = Label("Parts", safeRoot, "", 20, Muted, .51f, .295f, .95f, .346f);
            partArea = Rect("PartTimeline", safeRoot, .51f, .276f, .95f, .289f);
            MakeButton("SpeedMinus", safeRoot, "−", .51f, .185f, .565f, .25f, () => ChangeSpeed(-.1f), out _);
            speedText = Label("Speed", safeRoot, "", 22, Ink, .57f, .185f, .685f, .25f, TextAnchor.MiddleCenter);
            MakeButton("SpeedPlus", safeRoot, "+", .69f, .185f, .745f, .25f, () => ChangeSpeed(.1f), out _);
            MakeButton("Preparation", safeRoot, "", .77f, .185f, .95f, .25f, () =>
            {

                preferences.shortPreparation = !preferences.shortPreparation;
                UpdateSettings();

            }, out preparationText);
            playButton = MakeButton("Play", safeRoot, "플레이 시작  →", .51f, .076f, .95f, .161f, Launch, out _);
            playButton.image.color = Accent;
            playButton.GetComponentInChildren<Text>().color = Background;
            statusText = Label("Status", safeRoot, "", 17, Muted, .51f, .022f, .95f, .07f);
            loadingPanel = Panel("Loading", canvasObject.transform, new Color(.063f, .086f, .098f, .97f), 0, 0, 1, 1).gameObject;
            loadingText = Label("Message", loadingPanel.transform, "", 36, Ink, .15f, .48f, .85f, .62f, TextAnchor.MiddleCenter);
            MakeButton("Cancel", loadingPanel.transform, "곡 선택으로 돌아가기", .34f, .32f, .66f, .42f, onCancel, out _);
            loadingPanel.SetActive(false);
            preview.StateChanged += PreviewChanged;
            artworkLoader.Changed += RefreshArtwork;
            UpdateSettings();
            UpdateSafeArea();
            if (catalog == null || !catalog.TryValidate(out _))
            {

                statusText.text = "곡 목록을 읽을 수 없습니다. 카탈로그를 확인해 주세요.";
                playButton.interactable = false;
                return;

            }

            foreach (CatalogChart entry in catalog.Charts)
            {

                if (!difficulties.Contains(entry.Chart.DifficultyLabel))
                {

                    difficulties.Add(entry.Chart.DifficultyLabel);

                }

            }

            RefreshSongs();

        }

        private void BuildSearch()
        {

            Image background = Panel("Search", safeRoot, Surface, .045f, .819f, .435f, .879f);
            search = background.gameObject.AddComponent<InputField>();
            background.raycastTarget = true;
            search.targetGraphic = background;
            search.textComponent = Label("Value", background.transform, "", 23, Ink, .035f, .08f, .965f, .92f);
            search.placeholder = Label("Placeholder", background.transform, "제목 또는 아티스트 검색", 23, Muted, .035f, .08f, .965f, .92f);
            search.lineType = InputField.LineType.SingleLine;
            search.characterLimit = 100;
            search.onValueChanged.AddListener(_ => RefreshSongs());

        }

        private void RefreshSongs()
        {

            if (catalog == null)
            {

                return;

            }
            catalog.FindSongs(search.text, difficulties[difficultyIndex], sortByArtist, songs);
            countText.text = $"{songs.Count}곡";
            emptyText.gameObject.SetActive(songs.Count == 0);
            content.sizeDelta = new Vector2(0, songs.Count * RowHeight);
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1;
            firstRow = -1;
            SongDefinition next = selected != null && songs.Contains(selected) ? selected :
                songs.Find(song => song.Id == preferences.songId);
            if (next == null)
            {

                foreach (SongDefinition song in songs)
                {

                    foreach (CatalogChart entry in catalog.Charts)
                    {

                        if (entry.Chart.Song == song && entry.IsPlayable &&
                            (difficultyIndex == 0 || entry.Chart.DifficultyLabel == difficulties[difficultyIndex]))
                        {

                            next = song;
                            break;

                        }

                    }

                    if (next != null)
                    {

                        break;

                    }

                }

            }

            SelectSong(next != null ? next : songs.Count > 0 ? songs[0] : null);
            BindRows();

        }

        public void SelectSong(SongDefinition song)
        {

            bool changed = selected != song;
            selected = song;
            choices.Clear();
            if (song != null)
            {

                foreach (CatalogChart entry in catalog.Charts)
                {

                    if (entry.Chart.Song == song && (difficultyIndex == 0 || entry.Chart.DifficultyLabel == difficulties[difficultyIndex]))
                    {

                        choices.Add(entry);

                    }

                }

            }

            selectedChart = choices.Find(entry => entry.Chart.ChartId == preferences.chartId);
            if (selectedChart == null && choices.Count > 0)
            {

                selectedChart = choices[0];

            }
            titleText.text = song != null ? song.Title : "선택된 곡이 없습니다";
            artistText.text = song != null ? song.Artist : "";
            RefreshArtwork();
            UpdateChart();
            if (changed)
            {

                ResumePreview();

            }
            firstRow = -1;
            BindRows();

        }

        private void CycleChart()
        {

            if (choices.Count == 0)
            {

                return;

            }
            selectedChart = choices[(choices.IndexOf(selectedChart) + 1) % choices.Count];
            UpdateChart();

        }

        private void UpdateChart()
        {

            chartText.text = selectedChart != null ? $"채보 · {selectedChart.Chart.DifficultyLabel}   {choices.IndexOf(selectedChart) + 1}/{choices.Count}" : "채보 없음";
            partsText.text = selectedChart != null ? selectedChart.PartSummary : "";
            playButton.interactable = selectedChart != null && selectedChart.IsPlayable && !loading;
            statusText.text = selectedChart != null && !selectedChart.IsPlayable ? "플레이 가능한 노트가 없는 준비 중인 채보입니다." : "";
            foreach (GameObject bar in partBars)
            {

                Destroy(bar);

            }
            partBars.Clear();
            double extent = selectedChart?.Chart.Duration ?? 0d;
            if (selectedChart != null && extent > 0d)
            {

                foreach (MusicalPartActivationWindow window in selectedChart.Chart.ActivationWindows)
                {

                    float left = Mathf.Clamp01((float)(window.StartTime / extent));
                    float right = Mathf.Clamp01((float)(window.EndTime / extent));
                    if (right > left)
                    {

                        partBars.Add(Panel("Part", partArea, selectedChart.Chart.GetPartColor(window.MusicalPartId), left, 0, right, 1).gameObject);

                    }

                }

            }

            UpdateLength();
            if (selected != null && selectedChart != null)
            {

                preferences.songId = selected.Id;
                preferences.chartId = selectedChart.Chart.ChartId;
                SavePreferences();

            }

        }

        private void UpdateLength()
        {

            if (selected == null) { lengthText.text = ""; return; }
            if (!durations.TryGetValue(selected.Id, out double duration)) { lengthText.text = "길이 확인 전"; return; }
            duration = Math.Max(duration, selectedChart?.Chart.Duration ?? 0d);
            int seconds = (int)Math.Ceiling(duration);
            lengthText.text = $"플레이 {seconds / 60}:{seconds % 60:00}";

        }

        private void PreviewChanged(SongDefinition song, double duration, string message)
        {

            if (duration > 0)
            {

                durations[song.Id] = duration;

            }
            if (song != selected)
            {

                return;

            }
            UpdateLength();
            if (selectedChart != null && selectedChart.IsPlayable)
            {

                statusText.text = message;

            }

        }

        public void ResumePreview()
        {

            previewText.text = previewEnabled ? "미리듣기 켜짐" : "미리듣기 꺼짐";
            if (previewEnabled && !loading)
            {

                preview.Select(selected);

            }
            else preview.StopPreview();

        }

        private void ChangeSpeed(float delta)
        {

            preferences.speed = Mathf.Round(NoteSpeedMath.ClampMultiplier(preferences.speed + delta) * 10f) / 10f;
            UpdateSettings();

        }

        private void UpdateSettings()
        {

            speedText.text = $"속도 ×{preferences.speed:0.0}";
            preparationText.text = preferences.shortPreparation ? "카운트인 · 짧게" : "카운트인 · 기본";
            SavePreferences();

        }

        private void SavePreferences() => PlayerPrefs.SetString(PreferenceKey, JsonUtility.ToJson(preferences));
        private void OnApplicationPause(bool paused)
        {

            if (paused)
            {

                PlayerPrefs.Save();
                preview?.StopPreview();

            }
            else if (canvasObject != null && canvasObject.activeSelf)
            {

                ResumePreview();

            }

        }
        private void OnApplicationQuit() => PlayerPrefs.Save();

        public void Launch()
        {

            if (loading || selectedChart == null || !selectedChart.IsPlayable)
            {

                return;

            }
            PlayerPrefs.Save();
            start(new PlayRequest(selectedChart.Chart, preferences.speed, preferences.shortPreparation));

        }

        public void SetLoading(bool value, string message)
        {

            loading = value;
            controls.interactable = !value;
            controls.blocksRaycasts = !value;
            loadingPanel.SetActive(value);
            loadingText.text = message;
            if (!value)
            {

                statusText.text = message;

            }

        }

        public void SetVisible(bool visible)
        {

            canvasObject.SetActive(visible);
            if (visible)
            {

                SetLoading(false, "");

            }

        }

        private void LateUpdate()
        {

            if (canvasObject == null || !canvasObject.activeSelf)
            {

                return;

            }
            UpdateSafeArea();
            BindRows();

        }

        private void UpdateSafeArea()
        {

            Vector2 size = new(Screen.width, Screen.height);
            if (size == previousSize && Screen.safeArea == previousSafeArea)
            {

                return;

            }
            previousSize = size;
            previousSafeArea = Screen.safeArea;
            safeRoot.anchorMin = Screen.safeArea.min / size;
            safeRoot.anchorMax = Screen.safeArea.max / size;
            firstRow = -1;

        }

        private void BindRows()
        {

            if (scroll == null)
            {

                return;

            }
            int needed = Mathf.CeilToInt(scroll.viewport.rect.height / RowHeight) + 2;
            int first = Mathf.Clamp(Mathf.FloorToInt(content.anchoredPosition.y / RowHeight), 0, Mathf.Max(0, songs.Count - 1));
            if (firstRow == first && rows.Count == needed)
            {

                return;

            }
            while (rows.Count < needed)
            {

                Row row = new();
                row.Button = MakeButton("SongRow", content, "", 0, 1, 1, 1, () => SelectSong(row.Song), out _);
                row.Rect = (RectTransform)row.Button.transform;
                row.Rect.pivot = new Vector2(.5f, 1);
                row.Cover = Panel("Cover", row.Rect, Accent, .025f, .14f, .15f, .86f);
                row.Cover.preserveAspect = true;
                row.Initial = Label("Initial", row.Cover.transform, "", 36, Background, 0, 0, 1, 1, TextAnchor.MiddleCenter);
                row.Title = Label("Title", row.Rect, "", 29, Ink, .19f, .42f, .96f, .86f);
                row.Artist = Label("Artist", row.Rect, "", 20, Muted, .19f, .12f, .96f, .44f);
                rows.Add(row);

            }

            // Keep the pool bounded when the viewport shrinks.
            while (rows.Count > needed)
            {

                Destroy(rows[^1].Rect.gameObject);
                rows.RemoveAt(rows.Count - 1);

            }

            firstRow = first;
            for (int i = 0; i < rows.Count; i++)
            {

                Row row = rows[i];
                int index = first + i;
                row.Rect.gameObject.SetActive(index < songs.Count);
                if (index >= songs.Count) { row.Song = null; continue; }
                row.Song = songs[index];
                row.Rect.sizeDelta = new Vector2(0, RowHeight - 10);
                row.Rect.anchoredPosition = new Vector2(0, -index * RowHeight);
                row.Title.text = row.Song.Title;
                row.Artist.text = row.Song.Artist;
                row.Button.image.color = row.Song == selected ? new Color(.16f, .26f, .28f) : Surface;

            }

            RefreshArtwork();
            artworkTargets.Clear();
            if (selected != null)
            {

                artworkTargets.Add(selected);

            }
            foreach (Row row in rows)
            {

                if (row.Song != null)
                {

                    artworkTargets.Add(row.Song);

                }

            }
            artworkLoader.SetTargets(artworkTargets);

        }

        private void RefreshArtwork()
        {

            if (coverImage == null)
            {

                return;

            }
            coverImage.sprite = artworkLoader.Get(selected);
            coverImage.enabled = coverImage.sprite != null;
            coverInitial.text = selected != null && coverImage.sprite == null ? selected.Title.Substring(0, 1).ToUpperInvariant() : "";
            foreach (Row row in rows)
            {

                if (row.Song == null)
                {

                    continue;

                }
                row.Cover.sprite = artworkLoader.Get(row.Song);
                row.Cover.color = row.Cover.sprite != null ? Color.white : Accent;
                row.Initial.text = row.Cover.sprite == null ? row.Song.Title.Substring(0, 1).ToUpperInvariant() : "";

            }

        }

        private RectTransform Rect(string name, Transform parent, float x0, float y0, float x1, float y1)
        {

            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;

        }

        private Image Panel(string name, Transform parent, Color color, float x0, float y0, float x1, float y1)
        {

            Image image = Rect(name, parent, x0, y0, x1, y1).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;

        }

        private Text Label(string name, Transform parent, string value, int size, Color color,
            float x0, float y0, float x1, float y1, TextAnchor alignment = TextAnchor.MiddleLeft)
        {

            Text text = Rect(name, parent, x0, y0, x1, y1).gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Min(16, size);
            text.resizeTextMaxSize = size;
            return text;

        }

        private Button MakeButton(string name, Transform parent, string value, float x0, float y0, float x1, float y1,
            Action action, out Text label)
        {

            Image image = Panel(name, parent, Surface, x0, y0, x1, y1);
            image.sprite = GameplayVisualAssets.RoundedRectangleSprite;
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action?.Invoke());
            label = Label("Label", image.transform, value, 24, Ink, .04f, .08f, .96f, .92f, TextAnchor.MiddleCenter);
            return button;

        }

        private void OnDestroy()
        {

            artworkLoader.Dispose();
            if (preview != null)
            {

                preview.StateChanged -= PreviewChanged;

            }

        }

    }

}
