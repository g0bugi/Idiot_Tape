using System;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using Debug = UnityEngine.Debug;
using FmodStopMode = FMOD.Studio.STOP_MODE;

namespace IdiotTape.Audio
{

    public sealed class FmodSongPlayback : MonoBehaviour
    {

        [Serializable]
        private sealed class StemVolumeControl
        {

            [SerializeField]
            private string stemId;

            [SerializeField]
            private string parameterName;

            [SerializeField, Range(0f, 1f)]
            private float volume = 1f;

            [NonSerialized]
            private PARAMETER_ID parameterId;

            [NonSerialized]
            private bool hasParameterId;

            [NonSerialized]
            private float appliedVolume = float.NaN;

            public StemVolumeControl(string stemId, string parameterName)
            {

                this.stemId = stemId;
                this.parameterName = parameterName;

            }

            public string StemId => stemId;

            public string ParameterName => parameterName;

            public float Volume
            {

                get => volume;
                set => volume = Mathf.Clamp01(value);

            }

            public bool NeedsApply => hasParameterId && !Mathf.Approximately(appliedVolume, volume);

            public void Resolve(EventDescription eventDescription, UnityEngine.Object logContext)
            {

                hasParameterId = false;
                appliedVolume = float.NaN;

                if (string.IsNullOrWhiteSpace(stemId) || string.IsNullOrWhiteSpace(parameterName))
                {

                    Debug.LogError("A stem volume entry needs both a stem ID and an FMOD parameter name.", logContext);
                    return;

                }

                RESULT result = eventDescription.getParameterDescriptionByName(
                    parameterName,
                    out PARAMETER_DESCRIPTION parameterDescription);

                if (result != RESULT.OK)
                {

                    Debug.LogError(
                        $"FMOD parameter '{parameterName}' for stem '{stemId}' was not found: " +
                        $"{result} ({Error.String(result)}).",
                        logContext);
                    return;

                }

                parameterId = parameterDescription.id;
                hasParameterId = true;

            }

            public void Apply(EventInstance eventInstance, UnityEngine.Object logContext)
            {

                if (!NeedsApply)
                {

                    return;

                }

                RESULT result = eventInstance.setParameterByID(parameterId, volume);

                if (result != RESULT.OK)
                {

                    Debug.LogError(
                        $"Could not set FMOD parameter '{parameterName}': " +
                        $"{result} ({Error.String(result)}).",
                        logContext);
                }

                appliedVolume = volume;

            }

        }

        [Header("Song")]
        [SerializeField]
        private EventReference songEvent;

        [SerializeField]
        private bool playOnStart = true;

        [SerializeField]
        private bool preloadSampleData = true;

        [SerializeField]
        private bool stopOnDisable = true;

        [Header("Stem Volumes")]
        [SerializeField]
        private StemVolumeControl[] stemVolumes =
        {

            new StemVolumeControl("synth", "stems_synth1_volume"),
            new StemVolumeControl("bass", "stems_bass_volume"),
            new StemVolumeControl("drum", "stems_drum_volume"),
            new StemVolumeControl("etc", "stems_etc_volume")

        };

        private EventDescription songDescription;
        private EventInstance songInstance;
        private bool sampleLoadRequested;
        private bool playWhenPrepared;
        private bool preparationFailed;

        public bool IsPrepared => songInstance.isValid();

        public bool IsPlaying
        {

            get
            {

                if (!songInstance.isValid())
                {

                    return false;

                }

                RESULT result = songInstance.getPlaybackState(out PLAYBACK_STATE playbackState);
                return result == RESULT.OK && playbackState == PLAYBACK_STATE.PLAYING && !IsPaused;

            }

        }

        public bool IsPaused
        {

            get
            {

                if (!songInstance.isValid())
                {

                    return false;

                }

                RESULT result = songInstance.getPaused(out bool isPaused);
                return result == RESULT.OK && isPaused;

            }

        }

        // This millisecond FMOD timeline position is suitable for audition UI and seeking.
        // Rhythm judgement will require a dedicated DSP-clock-backed song clock.
        public double PlaybackPositionSeconds
        {

            get
            {

                if (!songInstance.isValid())
                {

                    return 0d;

                }

                RESULT result = songInstance.getTimelinePosition(out int positionMilliseconds);
                return result == RESULT.OK ? positionMilliseconds / 1000d : 0d;

            }

        }

        public void Configure(EventReference eventReference, bool autoPlay)
        {

            if (Application.isPlaying && songInstance.isValid())
            {

                Release();

            }

            songEvent = eventReference;
            playOnStart = autoPlay;
            preparationFailed = false;

        }

        private void Awake()
        {

            Prepare();

        }

        private void Start()
        {

            if (playOnStart)
            {

                Play();

            }

        }

        private void Update()
        {

            if (sampleLoadRequested)
            {

                PollSampleLoading();

            }

            if (songInstance.isValid())
            {

                ApplyStemVolumes();

            }

        }

        private void OnDisable()
        {

            if (stopOnDisable && Application.isPlaying)
            {

                Stop();

            }

        }

        private void OnDestroy()
        {

            Release();

        }

        public void Prepare()
        {

            if (songInstance.isValid() || sampleLoadRequested || preparationFailed)
            {

                return;

            }

            if (songEvent.IsNull)
            {

                preparationFailed = true;
                Debug.LogError("Assign an FMOD song event before preparing playback.", this);
                return;

            }

            try
            {

                songDescription = RuntimeManager.GetEventDescription(songEvent);

            }
            catch (EventNotFoundException exception)
            {

                preparationFailed = true;
                Debug.LogException(exception, this);
                return;

            }

            if (!preloadSampleData)
            {

                CreateInstance();
                return;

            }

            RESULT result = songDescription.loadSampleData();

            if (result != RESULT.OK)
            {

                preparationFailed = true;
                LogFmodError("load song sample data", result);
                return;

            }

            sampleLoadRequested = true;
            PollSampleLoading();

        }

        public void Play()
        {

            if (!songInstance.isValid())
            {

                playWhenPrepared = true;
                Prepare();
                return;

            }

            if (IsPaused)
            {

                Resume();
                return;

            }

            RESULT stateResult = songInstance.getPlaybackState(out PLAYBACK_STATE playbackState);

            if (stateResult != RESULT.OK)
            {

                LogFmodError("read song playback state", stateResult);
                return;

            }

            if (playbackState == PLAYBACK_STATE.PLAYING || playbackState == PLAYBACK_STATE.STARTING)
            {

                return;

            }

            ApplyStemVolumes();
            RESULT result = songInstance.start();

            if (result != RESULT.OK)
            {

                LogFmodError("start song", result);

            }

        }

        public void Pause()
        {

            SetPaused(true);

        }

        public void Resume()
        {

            SetPaused(false);

        }

        public void Stop()
        {

            Stop(false);

        }

        public void Stop(bool allowFadeOut)
        {

            playWhenPrepared = false;

            if (!songInstance.isValid())
            {

                return;

            }

            FmodStopMode stopMode = allowFadeOut ? FmodStopMode.ALLOWFADEOUT : FmodStopMode.IMMEDIATE;
            RESULT result = songInstance.stop(stopMode);

            if (result != RESULT.OK)
            {

                LogFmodError("stop song", result);

            }

        }

        public void Restart()
        {

            if (!songInstance.isValid())
            {

                playWhenPrepared = true;
                Prepare();
                return;

            }

            RESULT stopResult = songInstance.stop(FmodStopMode.IMMEDIATE);

            if (stopResult != RESULT.OK)
            {

                LogFmodError("stop song for restart", stopResult);
                return;

            }

            RESULT seekResult = songInstance.setTimelinePosition(0);

            if (seekResult != RESULT.OK)
            {

                LogFmodError("rewind song", seekResult);
                return;

            }

            ApplyStemVolumes();
            RESULT startResult = songInstance.start();

            if (startResult != RESULT.OK)
            {

                LogFmodError("restart song", startResult);

            }

        }

        public bool Seek(double songTimeSeconds)
        {

            if (!songInstance.isValid())
            {

                return false;

            }

            double clampedSeconds = Math.Max(0d, songTimeSeconds);
            int positionMilliseconds = (int)Math.Min(int.MaxValue, clampedSeconds * 1000d);
            RESULT result = songInstance.setTimelinePosition(positionMilliseconds);

            if (result != RESULT.OK)
            {

                LogFmodError("seek song", result);
                return false;

            }

            RESULT flushResult = RuntimeManager.StudioSystem.flushCommands();

            if (flushResult != RESULT.OK)
            {

                LogFmodError("synchronize song seek", flushResult);
                return false;

            }

            return true;

        }

        public bool SetStemVolume(string stemId, float volume)
        {

            if (stemVolumes == null || string.IsNullOrWhiteSpace(stemId))
            {

                return false;

            }

            for (int index = 0; index < stemVolumes.Length; index++)
            {

                StemVolumeControl stemVolume = stemVolumes[index];

                if (stemVolume == null || !string.Equals(stemVolume.StemId, stemId, StringComparison.Ordinal))
                {

                    continue;

                }

                stemVolume.Volume = volume;

                if (songInstance.isValid())
                {

                    stemVolume.Apply(songInstance, this);

                }

                return true;

            }

            return false;

        }

        public bool TryGetStemVolume(string stemId, out float volume)
        {

            if (stemVolumes != null && !string.IsNullOrWhiteSpace(stemId))
            {

                for (int index = 0; index < stemVolumes.Length; index++)
                {

                    StemVolumeControl stemVolume = stemVolumes[index];

                    if (stemVolume != null && string.Equals(stemVolume.StemId, stemId, StringComparison.Ordinal))
                    {

                        volume = stemVolume.Volume;
                        return true;

                    }

                }

            }

            volume = 0f;
            return false;

        }

        private void PollSampleLoading()
        {

            RESULT result = songDescription.getSampleLoadingState(out LOADING_STATE loadingState);

            if (result != RESULT.OK)
            {

                sampleLoadRequested = false;
                preparationFailed = true;
                LogFmodError("read song sample loading state", result);
                return;

            }

            if (loadingState == LOADING_STATE.ERROR)
            {

                sampleLoadRequested = false;
                preparationFailed = true;
                Debug.LogError("FMOD reported an error while loading the song sample data.", this);
                return;

            }

            if (loadingState != LOADING_STATE.LOADED)
            {

                return;

            }

            sampleLoadRequested = false;
            CreateInstance();

        }

        private void CreateInstance()
        {

            RESULT result = songDescription.createInstance(out songInstance);

            if (result != RESULT.OK)
            {

                preparationFailed = true;
                LogFmodError("create the song event instance", result);
                return;

            }

            if (!songInstance.isValid())
            {

                preparationFailed = true;
                Debug.LogError("FMOD could not create the song event instance.", this);
                return;

            }

            ResolveStemParameters();
            ApplyStemVolumes();

            if (playWhenPrepared)
            {

                playWhenPrepared = false;
                Play();

            }

        }

        private void ResolveStemParameters()
        {

            if (stemVolumes == null)
            {

                return;

            }

            for (int index = 0; index < stemVolumes.Length; index++)
            {

                stemVolumes[index]?.Resolve(songDescription, this);

            }

        }

        private void ApplyStemVolumes()
        {

            if (stemVolumes == null || !songInstance.isValid())
            {

                return;

            }

            for (int index = 0; index < stemVolumes.Length; index++)
            {

                stemVolumes[index]?.Apply(songInstance, this);

            }

        }

        private void SetPaused(bool paused)
        {

            if (!songInstance.isValid())
            {

                return;

            }

            RESULT result = songInstance.setPaused(paused);

            if (result != RESULT.OK)
            {

                LogFmodError(paused ? "pause song" : "resume song", result);
                return;

            }

            // Pause and resume are session boundaries. Flush the Studio command queue so
            // callers never observe a paused flag while the mixer timeline is still moving.
            RESULT flushResult = RuntimeManager.StudioSystem.flushCommands();

            if (flushResult != RESULT.OK)
            {

                LogFmodError(paused ? "synchronize song pause" : "synchronize song resume", flushResult);

            }

        }

        private void Release()
        {

            playWhenPrepared = false;
            sampleLoadRequested = false;

            if (songInstance.isValid())
            {

                songInstance.stop(FmodStopMode.IMMEDIATE);
                songInstance.release();
                songInstance = default;

            }

            if (preloadSampleData && songDescription.isValid())
            {

                songDescription.unloadSampleData();

            }

        }

        private void LogFmodError(string operation, RESULT result)
        {

            Debug.LogError(
                $"Could not {operation}: {result} ({Error.String(result)}).",
                this);

        }

    }

}
