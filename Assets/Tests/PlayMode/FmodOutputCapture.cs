#if UNITY_EDITOR
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using FMOD;
using FMODUnity;
using UnityEngine;

namespace IdiotTape.Gameplay.Tests
{

    // Test-only, main-thread lifetime. The mixer callbacks neither allocate capture buffers
    // nor call Unity, logging, or file APIs. Dispose before shutting down FMOD or changing scenes.
    // Usage: construct before SchedulePlay; call SetTimelineReference with its returned clock;
    // wait for the observation interval; Dispose writes prefix.f32 and prefix.json.
    // Official getclock semantics: the current DSP's clock and the input signal's offset/length.
    // Keep front L/R for every callback frame, including frames outside that signal subset. Do not add offset
    // blindly to the whole buffer's timestamps. Preserve raw fields for reference-click checks.
    internal sealed class FmodOutputCapture : IDisposable
    {

        [Serializable]
        public struct Block
        {

            public int firstCapturedFrame;
            public int capturedFrames;
            public uint callbackFrames;
            public int inputChannels;
            public ulong dspClock;
            public uint signalOffset;
            public uint signalLength;
            public int clockResult;

        }

        [Serializable]
        private sealed class Metadata
        {

            public string format = "float32-le interleaved stereo";
            public string insertionPoint = "FMOD master ChannelGroup HEAD";
            public string channelSelection = "Front L/R taps (input channels 0/1) before final output downmix; mono is duplicated. Every input channel passes through unchanged.";
            public string clockSemantics = "Raw DSP_STATE.getclock clock plus its signal offset/length; block PCM includes the whole callback buffer, including silence. This is mixer output, not measured hardware presentation time.";
            public string clockReference = "https://www.fmod.com/docs/2.03/api/plugin-api-dsp.html#fmod_dsp_getclock_func";
            public int sampleRate;
            public int channels = 2;
            public int maximumFrames;
            public int capturedFrames;
            public bool reachedCapacity;
            public int callbackFaults;
            public int clockFailures;
            public int unexpectedChannelCount;
            public bool hasTimelineReference;
            public ulong scheduledAudioStartDspClock;
            public double targetSongTime;
            public string label;
            public Block[] blocks;

        }

        private static readonly DSP_CREATE_CALLBACK CreateDelegate = CreateCallback;
        private static readonly DSP_READ_CALLBACK ReadDelegate = ReadCallback;
        private static readonly DSP_SHOULDIPROCESS_CALLBACK ShouldProcessDelegate = ShouldProcessCallback;

        private readonly string outputPathPrefix;
        private readonly int maximumFrames;
        private readonly float[] samples;
        private readonly float[] scratch = new float[8192];
        private readonly Block[] blocks;
        private FMOD.System core;
        private ChannelGroup master;
        private DSP dsp;
        private GCHandle selfHandle;
        private DSP_GETCLOCK_FUNC getClock;
        private int capturedFrames;
        private int blockCount;
        private int callbackFaults;
        private int clockFailures;
        private int unexpectedChannelCount;
        private bool attached;
        private bool disposed;
        private bool hasTimelineReference;
        private ulong scheduledAudioStartDspClock;
        private double targetSongTime;
        private string label = string.Empty;

        public int SampleRate { get; }
        public int CapturedFrames => Volatile.Read(ref capturedFrames);
        public bool HasFault => Volatile.Read(ref callbackFaults) != 0 ||
            Volatile.Read(ref clockFailures) != 0 || Volatile.Read(ref unexpectedChannelCount) != 0;

        public FmodOutputCapture(string outputPathPrefix, double maximumSeconds = 12d)
        {

            if (string.IsNullOrWhiteSpace(outputPathPrefix))
            {

                throw new ArgumentException("An artifact output path is required.", nameof(outputPathPrefix));

            }

            if (double.IsNaN(maximumSeconds) || maximumSeconds <= 0d || maximumSeconds > 12d)
            {

                throw new ArgumentOutOfRangeException(nameof(maximumSeconds));

            }

            this.outputPathPrefix = Path.GetFullPath(outputPathPrefix);
            core = RuntimeManager.CoreSystem;
            Check(core.getSoftwareFormat(out int sampleRate, out _, out _));

            if (sampleRate <= 0 || sampleRate > 48000)
            {

                throw new InvalidOperationException("This capture fixture requires output at at most 48 kHz.");

            }

            SampleRate = sampleRate;
            maximumFrames = (int)Math.Ceiling(sampleRate * maximumSeconds);
            samples = new float[maximumFrames * 2];
            Check(core.getDSPBufferSize(out uint mixerFrames, out _));
            // Size diagnostic metadata from the actual mixer block, with subdivision room.
            // Allocating one entry per sample causes needless GC pauses during audio tests.
            blocks = new Block[(int)Math.Ceiling(maximumFrames / (double)Math.Max(1u, mixerFrames)) * 2 + 8];
            selfHandle = GCHandle.Alloc(this, GCHandleType.Normal);

            try
            {

                DSP_DESCRIPTION description = new()
                {

                    name = new byte[32],
                    numinputbuffers = 1,
                    numoutputbuffers = 1,
                    create = CreateDelegate,
                    read = ReadDelegate,
                    shouldiprocess = ShouldProcessDelegate,
                    userdata = GCHandle.ToIntPtr(selfHandle)

                };
                byte[] name = System.Text.Encoding.UTF8.GetBytes("Test output capture");
                Array.Copy(name, description.name, name.Length);
                Check(core.createDSP(ref description, out dsp));
                Check(core.getMasterChannelGroup(out master));
                Check(master.addDSP(CHANNELCONTROL_DSP_INDEX.HEAD, dsp));
                attached = true;

            }
            catch
            {

                if (dsp.hasHandle())
                {

                    dsp.release();

                }

                selfHandle.Free();
                throw;

            }

        }

        public void SetTimelineReference(ulong audioStartDspClock, double songTime, string captureLabel)
        {

            if (disposed)
            {

                throw new ObjectDisposedException(nameof(FmodOutputCapture));

            }

            scheduledAudioStartDspClock = audioStartDspClock;
            targetSongTime = songTime;
            label = captureLabel ?? string.Empty;
            hasTimelineReference = true;

        }

        public void Dispose()
        {

            if (disposed)
            {

                return;

            }

            if (attached)
            {

                // Exclude an in-flight read before detaching. Do not flush Studio while locked.
                Check(core.lockDSP());

                try
                {

                    Check(master.removeDSP(dsp));
                    attached = false;

                }
                finally
                {

                    Check(core.unlockDSP());

                }

            }

            if (dsp.hasHandle())
            {

                Check(dsp.release());
                dsp.clearHandle();

            }

            // A failed detach/release throws before this point, retaining the handle rather
            // than letting an uncertain native callback target a freed managed object.
            if (selfHandle.IsAllocated)
            {

                selfHandle.Free();

            }

            disposed = true;
            WriteFiles();

        }

        [AOT.MonoPInvokeCallback(typeof(DSP_CREATE_CALLBACK))]
        private static RESULT CreateCallback(ref DSP_STATE state)
        {

            DSP_STATE_FUNCTIONS functions = state.functions;
            RESULT result = functions.getuserdata(ref state, out IntPtr userdata);

            if (result != RESULT.OK)
            {

                return result;

            }

            state.plugindata = userdata;
            FmodOutputCapture capture = (FmodOutputCapture)GCHandle.FromIntPtr(userdata).Target;
            capture.getClock = functions.getclock;
            return RESULT.OK;

        }

        [AOT.MonoPInvokeCallback(typeof(DSP_SHOULDIPROCESS_CALLBACK))]
        private static RESULT ShouldProcessCallback(
            ref DSP_STATE state, bool inputsIdle, uint length, CHANNELMASK inputMask,
            int inputChannels, SPEAKERMODE mode)
        {

            // Retain silent preparation blocks instead of letting the capture node go idle.
            return RESULT.OK;

        }

        [AOT.MonoPInvokeCallback(typeof(DSP_READ_CALLBACK))]
        private static RESULT ReadCallback(
            ref DSP_STATE state, IntPtr input, IntPtr output, uint length,
            int inputChannels, ref int outputChannels)
        {

            FmodOutputCapture capture = (FmodOutputCapture)GCHandle.FromIntPtr(state.plugindata).Target;

            try
            {

                int channels = inputChannels > 0 ? inputChannels : Math.Max(1, outputChannels);
                outputChannels = channels;
                int frames = (int)length;
                int firstFrame = capture.capturedFrames;
                int framesToCapture = Math.Min(frames, capture.maximumFrames - firstFrame);
                int maximumChunkFrames = capture.scratch.Length / channels;

                if (framesToCapture > 0 && capture.blockCount >= capture.blocks.Length)
                {

                    capture.callbackFaults++;
                    framesToCapture = 0;

                }

                if (maximumChunkFrames == 0)
                {

                    capture.unexpectedChannelCount = channels;
                    return RESULT.ERR_DSP_DONTPROCESS;

                }

                if (framesToCapture > 0)
                {

                    RESULT clockResult = capture.getClock(
                        ref state, out ulong clock, out uint signalOffset, out uint signalLength);
                    capture.blocks[capture.blockCount++] = new Block
                    {

                        firstCapturedFrame = firstFrame,
                        capturedFrames = framesToCapture,
                        callbackFrames = length,
                        inputChannels = inputChannels,
                        dspClock = clock,
                        signalOffset = signalOffset,
                        signalLength = signalLength,
                        clockResult = (int)clockResult

                    };

                    if (clockResult != RESULT.OK)
                    {

                        capture.clockFailures++;

                    }

                }

                for (int copiedFrames = 0; copiedFrames < frames;)
                {

                    int chunkFrames = Math.Min(maximumChunkFrames, frames - copiedFrames);
                    int count = chunkFrames * channels;
                    int byteOffset = copiedFrames * channels * sizeof(float);

                    if (input != IntPtr.Zero && inputChannels > 0)
                    {

                        Marshal.Copy(IntPtr.Add(input, byteOffset), capture.scratch, 0, count);

                    }
                    else
                    {

                        Array.Clear(capture.scratch, 0, count);

                    }

                    int chunkCaptureFrames = Math.Min(chunkFrames, framesToCapture - copiedFrames);

                    for (int frame = 0; frame < chunkCaptureFrames; frame++)
                    {

                        int source = frame * channels;
                        int destination = (firstFrame + copiedFrames + frame) * 2;
                        capture.samples[destination] = capture.scratch[source];
                        capture.samples[destination + 1] = capture.scratch[source + (channels > 1 ? 1 : 0)];

                    }

                    Marshal.Copy(capture.scratch, 0, IntPtr.Add(output, byteOffset), count);
                    copiedFrames += chunkFrames;

                }

                Volatile.Write(ref capture.capturedFrames, firstFrame + framesToCapture);
                return RESULT.OK;

            }
            catch
            {

                Interlocked.Increment(ref capture.callbackFaults);
                return RESULT.ERR_DSP_DONTPROCESS;

            }

        }

        private void WriteFiles()
        {

            if (!BitConverter.IsLittleEndian)
            {

                throw new PlatformNotSupportedException("The diagnostic f32 format expects a little-endian host.");

            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPathPrefix));
            byte[] bytes = new byte[capturedFrames * 2 * sizeof(float)];
            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
            File.WriteAllBytes(outputPathPrefix + ".f32", bytes);
            Block[] capturedBlocks = new Block[blockCount];
            Array.Copy(blocks, capturedBlocks, blockCount);
            Metadata metadata = new()
            {

                sampleRate = SampleRate,
                maximumFrames = maximumFrames,
                capturedFrames = capturedFrames,
                reachedCapacity = capturedFrames == maximumFrames,
                callbackFaults = callbackFaults,
                clockFailures = clockFailures,
                unexpectedChannelCount = unexpectedChannelCount,
                hasTimelineReference = hasTimelineReference,
                scheduledAudioStartDspClock = scheduledAudioStartDspClock,
                targetSongTime = targetSongTime,
                label = label,
                blocks = capturedBlocks

            };
            File.WriteAllText(outputPathPrefix + ".json", JsonUtility.ToJson(metadata, true));

        }

        private static void Check(RESULT result)
        {

            if (result != RESULT.OK)
            {

                throw new InvalidOperationException("FMOD output capture: " + result);

            }

        }

    }

}
#endif
