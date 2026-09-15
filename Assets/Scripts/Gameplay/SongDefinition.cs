using System.Collections.Generic;
using IdiotTape.Audio;
using UnityEngine;

namespace IdiotTape.Gameplay
{

    [CreateAssetMenu(menuName = "Idiot Tape/Song")]
    public sealed class SongDefinition : ScriptableObject
    {

        [SerializeField] private string id;
        [SerializeField] private string title;
        [SerializeField] private string artist;
        [SerializeField, Tooltip("Sprite path relative to a Resources folder, without extension.")]
        private string coverResourcePath;
        [SerializeField] private string eventPath;
        [SerializeField] private List<FmodStemDefinition> stems = new();
        [SerializeField, Min(0f)] private float previewStart;
        [SerializeField, Min(1f)] private float previewDuration = 15f;

        public string Id => id;
        public string Title => string.IsNullOrWhiteSpace(title) ? name : title;
        public string Artist => artist ?? string.Empty;
        public string CoverResourcePath => coverResourcePath ?? string.Empty;
        public string EventPath => eventPath;
        public IReadOnlyList<FmodStemDefinition> Stems => stems;
        public float PreviewStart => Mathf.Max(0f, previewStart);
        public float PreviewDuration => Mathf.Max(1f, previewDuration);

    }

}
