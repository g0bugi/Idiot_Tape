using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdiotTape.Gameplay
{

    [Serializable]
    public sealed class CatalogChart
    {

        [SerializeField] private PrototypeChart chart;
        [SerializeField] private string partSummary;
        [SerializeField] private int playableNotes;

        // Local charts are lightweight data. This reference is the local loader boundary;
        // audio event instances and runtime note objects are never catalog assets.
        public PrototypeChart Chart => chart;
        public string PartSummary => partSummary ?? string.Empty;
        public int PlayableNotes => playableNotes;
        public bool IsPlayable => chart != null && playableNotes > 0;

        public CatalogChart(PrototypeChart source, string parts, int count)
        {

            chart = source;
            partSummary = parts;
            playableNotes = count;

        }

    }

    [CreateAssetMenu(menuName = "Idiot Tape/Song Catalog")]
    public sealed class SongCatalog : ScriptableObject
    {

        [SerializeField] private List<CatalogChart> charts = new();
        public IReadOnlyList<CatalogChart> Charts => charts;

        public bool TryValidate(out string error)
        {

            HashSet<string> chartIds = new();
            Dictionary<string, SongDefinition> songs = new();
            foreach (CatalogChart entry in charts)
            {

                PrototypeChart chart = entry?.Chart;
                if (chart == null || chart.Song == null || string.IsNullOrWhiteSpace(chart.ChartId) ||
                    string.IsNullOrWhiteSpace(chart.Song.Id) || !chartIds.Add(chart.ChartId))
                {

                    error = "곡·채보 참조 또는 고유 ID가 없거나 중복되었습니다.";
                    return false;

                }

                if (songs.TryGetValue(chart.Song.Id, out SongDefinition previous) && previous != chart.Song)
                {

                    error = "서로 다른 곡 에셋이 같은 ID를 사용합니다.";
                    return false;

                }

                songs[chart.Song.Id] = chart.Song;
                if (!chart.TryValidate(out error))
                {

                    return false;

                }

            }

            error = null;
            return true;

        }

        public void FindSongs(string query, string difficulty, bool sortByArtist, List<SongDefinition> output)
        {

            output.Clear();
            HashSet<SongDefinition> included = new();
            string search = (query ?? string.Empty).Trim();
            foreach (CatalogChart entry in charts)
            {

                PrototypeChart chart = entry?.Chart;
                SongDefinition song = chart != null ? chart.Song : null;
                if (song == null || (!string.IsNullOrEmpty(difficulty) && chart.DifficultyLabel != difficulty) ||
                    (search.Length > 0 && song.Title.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 &&
                     song.Artist.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) || !included.Add(song))
                {

                    continue;

                }

                output.Add(song);

            }

            output.Sort((a, b) =>
            {

                int comparison = sortByArtist ? string.Compare(a.Artist, b.Artist, StringComparison.OrdinalIgnoreCase) : 0;
                if (comparison == 0)
                {

                    comparison = string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);

                }
                return comparison != 0 ? comparison : string.CompareOrdinal(a.Id, b.Id);

            });

        }

    }

    public sealed class PlayRequest
    {

        public PrototypeChart Chart { get; }
        public string SongId { get; }
        public string ChartId { get; }
        public int ChartRevision { get; }
        public float Speed { get; }
        public bool ShortPreparation { get; }

        public PlayRequest(PrototypeChart chart, float speed, bool shortPreparation)
        {

            Chart = chart != null ? chart : throw new ArgumentNullException(nameof(chart));
            SongId = chart.Song != null ? chart.Song.Id : chart.SongEventPath;
            ChartId = chart.ChartId;
            ChartRevision = chart.Revision;
            Speed = float.IsNaN(speed) || float.IsInfinity(speed) ? 1f : NoteSpeedMath.ClampMultiplier(speed);
            ShortPreparation = shortPreparation;

        }

    }

}
