using System;
using System.Collections;
using IdiotTape.Audio;
using UnityEngine;

namespace IdiotTape.Gameplay
{

    public sealed class SongPreviewPlayer : MonoBehaviour
    {

        private FmodSongPlayback playback;
        private Coroutine routine;
        public SongDefinition SelectedSong { get; private set; }
        public event Action<SongDefinition, double, string> StateChanged;

        public void Select(SongDefinition song)
        {

            StopPreview();
            SelectedSong = song;
            if (song != null)
            {

                routine = StartCoroutine(Preview(song));

            }

        }

        public void StopPreview()
        {

            if (routine != null)
            {

                StopCoroutine(routine);

            }
            routine = null;
            SelectedSong = null;
            if (playback != null)
            {

                playback.Stop();
                // Configure releases the old event and its sample-data reference.
                playback.ConfigureEventPath(string.Empty, false);

            }

        }

        private IEnumerator Preview(SongDefinition song)
        {

            yield return new WaitForSecondsRealtime(0.3f);
            if (playback == null)
            {

                playback = gameObject.AddComponent<FmodSongPlayback>();

            }
            playback.ConfigureEventPath(song.EventPath, false);
            playback.ConfigureStemParameters(song.Stems);
            playback.Prepare();
            double deadline = Time.realtimeSinceStartupAsDouble + 15d;
            while (!playback.IsPrepared && !playback.PreparationFailed && Time.realtimeSinceStartupAsDouble < deadline)
            {

                yield return null;

            }

            if (!playback.IsPrepared)
            {

                StateChanged?.Invoke(song, 0d, "미리듣기를 준비하지 못했습니다.");
                playback.ConfigureEventPath(string.Empty, false);
                routine = null;
                yield break;

            }

            double duration = playback.DurationSeconds;
            double start = Math.Min(song.PreviewStart, Math.Max(0d, duration - 1d));
            double end = Math.Min(duration, start + song.PreviewDuration);
            StateChanged?.Invoke(song, duration, "미리듣기");
            // Use scheduled nonzero starts, never live Seek or the gameplay instance.
            while (SelectedSong == song && end > start)
            {

                if (!playback.SchedulePlay(0.1d, start, out _, out _))
                {

                    StateChanged?.Invoke(song, duration, "미리듣기를 시작하지 못했습니다.");
                    break;

                }

                while (SelectedSong == song && playback.TimelineTime < end)
                {

                    double position = playback.TimelineTime;
                    float gain = Mathf.Clamp01((float)Math.Min((position - start) / 0.25d, (end - position) / 0.35d)) * 0.65f;
                    for (int index = 0; index < song.Stems.Count; index++)
                    {

                        playback.SetStemVolume(song.Stems[index].StemId, gain);

                    }
                    yield return null;

                }

                playback.Stop();

            }

            routine = null;

        }

        private void OnDisable() => StopPreview();

    }

}
