using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdiotTape.Gameplay
{

    // Only the selected cover and visible thumbnails are retained. A single native request
    // can finish after its owner is disposed; its completion still releases the unused asset.
    public sealed class SongArtworkLoader : IDisposable
    {

        private readonly List<string> desired = new();
        private readonly Dictionary<string, Sprite> loaded = new();
        private readonly List<string> obsolete = new();
        private ResourceRequest pending;
        private bool disposed;
        public event Action Changed;

        public Sprite Get(SongDefinition song)
        {

            return song != null && loaded.TryGetValue(song.CoverResourcePath, out Sprite sprite) ? sprite : null;

        }

        public void SetTargets(IReadOnlyList<SongDefinition> songs)
        {

            if (disposed)
            {

                return;

            }
            desired.Clear();
            foreach (SongDefinition song in songs)
            {

                if (song != null && !string.IsNullOrWhiteSpace(song.CoverResourcePath) && !desired.Contains(song.CoverResourcePath))
                {

                    desired.Add(song.CoverResourcePath);

                }

            }

            obsolete.Clear();
            foreach (var pair in loaded)
            {

                if (!desired.Contains(pair.Key))
                {

                    obsolete.Add(pair.Key);

                }

            }
            foreach (string path in obsolete)
            {

                if (loaded[path] != null)
                {

                    Resources.UnloadAsset(loaded[path]);

                }
                loaded.Remove(path);

            }

            RequestNext();

        }

        private void RequestNext()
        {

            if (disposed || pending != null)
            {

                return;

            }
            foreach (string path in desired)
            {

                if (loaded.ContainsKey(path))
                {

                    continue;

                }
                ResourceRequest request = Resources.LoadAsync<Sprite>(path);
                pending = request;
                request.completed += _ =>
                {

                    Sprite sprite = request.asset as Sprite;
                    pending = null;
                    if (disposed || !desired.Contains(path))
                    {

                        if (sprite != null)
                        {

                            Resources.UnloadAsset(sprite);

                        }

                    }
                    else
                    {

                        loaded[path] = sprite;
                        Changed?.Invoke();

                    }

                    RequestNext();

                };
                return;

            }

        }

        public void Dispose()
        {

            disposed = true;
            Changed = null;
            desired.Clear();
            foreach (var pair in loaded)
            {

                if (pair.Value != null)
                {

                    Resources.UnloadAsset(pair.Value);

                }

            }
            loaded.Clear();

        }

    }

}
