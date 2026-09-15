using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class SongCatalogTests
    {

        private readonly List<Object> assets = new();

        [TearDown]
        public void TearDown()
        {

            foreach (Object asset in assets)
            {

                Object.DestroyImmediate(asset);

            }
            assets.Clear();

        }

        private T Asset<T>() where T : ScriptableObject
        {

            T asset = ScriptableObject.CreateInstance<T>();
            assets.Add(asset);
            return asset;

        }

        private SongDefinition Song(string id, string title, string artist)
        {

            SongDefinition song = Asset<SongDefinition>();
            JsonUtility.FromJsonOverwrite($"{{\"id\":\"{id}\",\"title\":\"{title}\",\"artist\":\"{artist}\",\"eventPath\":\"event:/Music/Test\"}}", song);
            return song;

        }

        private PrototypeChart Chart(SongDefinition song, string id, string difficulty)
        {

            PrototypeChart chart = Asset<PrototypeChart>();
            JsonUtility.FromJsonOverwrite($"{{\"chartId\":\"{id}\",\"difficultyLabel\":\"{difficulty}\"}}", chart);
            Set(chart, "song", song);
            return chart;

        }

        private SongCatalog Catalog(params PrototypeChart[] charts)
        {

            SongCatalog catalog = Asset<SongCatalog>();
            List<CatalogChart> entries = new();
            foreach (PrototypeChart chart in charts)
            {

                entries.Add(new CatalogChart(chart, "", 1));

            }
            Set(catalog, "charts", entries);
            return catalog;

        }

        [Test]
        public void DifficultyFilterMatchesChartsAndDeduplicatesSongs()
        {

            SongDefinition snow = Song("snow", "Snow", "KIRARA");
            SongDefinition pluto = Song("pluto", "Pluto", "IDIOTAPE");
            SongCatalog catalog = Catalog(Chart(snow, "snow-easy", "Easy"), Chart(snow, "snow-hard", "Hard"), Chart(pluto, "pluto-easy", "Easy"));
            List<SongDefinition> results = new();
            catalog.FindSongs("", "Hard", false, results);
            Assert.That(results, Is.EqualTo(new[] { snow }));
            catalog.FindSongs(" KIRARA ", "", false, results);
            Assert.That(results, Is.EqualTo(new[] { snow }));
            catalog.FindSongs("", "", false, results);
            Assert.That(results, Is.EqualTo(new[] { pluto, snow }));
            catalog.FindSongs("missing", "", false, results);
            Assert.That(results, Is.Empty);

        }

        [Test]
        public void CatalogRejectsDuplicateIdsAndDifferentSongAssetsSharingAnId()
        {

            SongDefinition song = Song("song", "Track", "Artist");
            PrototypeChart a = Chart(song, "a", "Easy");
            PrototypeChart b = Chart(song, "b", "Hard");
            SongCatalog catalog = Catalog(a, b);
            Assert.That(catalog.TryValidate(out string error), Is.True, error);
            Set(b, "chartId", "a");
            Assert.That(catalog.TryValidate(out _), Is.False);
            Set(b, "chartId", "b");
            Set(b, "song", Song("song", "Other", "Artist"));
            Assert.That(catalog.TryValidate(out _), Is.False);

        }

        [Test]
        public void SongOwnsMetadataAndAudioWhileLegacyChartsRemainReadable()
        {

            PrototypeChart chart = Chart(Song("song", "New Title", "New Artist"), "chart", "");
            JsonUtility.FromJsonOverwrite("{\"songTitle\":\"Legacy\",\"songEventPath\":\"event:/Legacy\"}", chart);
            Assert.That(chart.SongTitle, Is.EqualTo("New Title"));
            Assert.That(chart.SongEventPath, Is.EqualTo("event:/Music/Test"));
            Assert.That(chart.DifficultyLabel, Is.EqualTo("미지정"));
            Set(chart, "song", null);
            Assert.That(chart.SongTitle, Is.EqualTo("Legacy"));
            Assert.That(chart.SongEventPath, Is.EqualTo("event:/Legacy"));

        }

        [Test]
        public void PlayRequestSnapshotsIdentityRevisionAndValidSettings()
        {

            PrototypeChart chart = Chart(Song("song", "Track", "Artist"), "chart", "Easy");
            PlayRequest request = new(chart, float.NaN, true);
            Set(chart, "revision", 2);
            Set(chart, "chartId", "edited");
            Assert.That(request.ChartRevision, Is.EqualTo(1));
            Assert.That(request.ChartId, Is.EqualTo("chart"));
            Assert.That(request.Speed, Is.EqualTo(1f));
            Assert.That(request.ShortPreparation, Is.True);

        }

        [TestCase(3)]
        [TestCase(30)]
        [TestCase(300)]
        public void CatalogQueriesRemainCorrectAtDifferentLibrarySizes(int count)
        {

            PrototypeChart[] charts = new PrototypeChart[count];
            for (int i = 0; i < count; i++) charts[i] = Chart(Song($"song-{i}", $"Track {i:000}", "Artist"), $"chart-{i}", "");
            SongCatalog catalog = Catalog(charts);
            List<SongDefinition> results = new();
            catalog.FindSongs("", "", true, results);
            Assert.That(results.Count, Is.EqualTo(count));
            catalog.FindSongs($"Track {count - 1:000}", "", false, results);
            Assert.That(results.Count, Is.EqualTo(1));

        }

        private static void Set(object target, string field, object value)
        {

            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        }

    }

}
