using System;
using UnityEngine;

namespace IdiotTape.Gameplay
{

    public sealed class DspSongClock : MonoBehaviour
    {

        private const double ScheduleLeadSeconds = 0.1d;

        [SerializeField] private AudioSource audioSource;

        private double songStartDspTime;
        private double pausedSongTime;

        public bool IsRunning { get; private set; }
        public bool IsPaused { get; private set; }

        public double SongTime
        {

            get
            {

                if (!IsRunning)
                {

                    return 0d;

                }

                return IsPaused ? pausedSongTime : AudioSettings.dspTime - songStartDspTime;

            }

        }

        public void StartSong(AudioClip clip)
        {

            audioSource.Stop();
            audioSource.clip = clip;
            songStartDspTime = AudioSettings.dspTime + ScheduleLeadSeconds;
            pausedSongTime = 0d;
            IsPaused = false;
            IsRunning = true;

            if (clip != null)
            {

                audioSource.PlayScheduled(songStartDspTime);

            }

        }

        public double GetSongTimeForExternalTimestamp(double eventTimestamp, double externalNow)
        {

            double eventAge = Math.Max(0d, externalNow - eventTimestamp);
            return SongTime - eventAge;

        }

        public void Pause()
        {

            if (!IsRunning || IsPaused)
            {

                return;

            }

            pausedSongTime = SongTime;
            audioSource.Pause();
            IsPaused = true;

        }

        public void Resume()
        {

            if (!IsRunning || !IsPaused)
            {

                return;

            }

            songStartDspTime = AudioSettings.dspTime - pausedSongTime;
            audioSource.UnPause();
            IsPaused = false;

        }

    }

}
