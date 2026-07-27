using System.IO;
using System.Windows;
using System.Windows.Threading;
using NAudio.Wave;

namespace RobloxOptimizer;

/// <summary>
/// A "for fun" voice-effects tab: records a few seconds from the microphone and
/// plays it back through the speakers with a simple effect. This never
/// impersonates a real person's voice, and it does not feed into Roblox's (or
/// any other app's) voice chat - actually routing a changed voice into a game
/// would require a separate virtual audio cable app, which this project does
/// not install or bundle.
///
/// The "Deep"/"High" effects work by simply telling the audio engine the
/// recording was made at a different sample rate than it actually was - a
/// classic, simple way to shift pitch (and tempo) without any real DSP
/// library. "Robot" applies basic ring modulation (multiplying the waveform
/// by a fixed-frequency tone), a well-understood, simple effect.
/// </summary>
public partial class MainWindow
{
    private const int VoiceSampleRate = 44100;
    private const int VoiceBitsPerSample = 16;
    private const int VoiceChannels = 1;
    private static readonly TimeSpan RecordingDuration = TimeSpan.FromSeconds(5);

    private WaveInEvent? _waveIn;
    private WaveOutEvent? _waveOut;
    private DispatcherTimer? _recordingStopTimer;
    private MemoryStream _recordedBuffer = new();
    private byte[]? _recordedPcm;
    private bool _isRecording;

    private enum VoiceEffectMode { Normal, Deep, High, Robot }

    private WaveInEvent? _liveWaveIn;
    private WaveOutEvent? _liveWaveOut;
    private BufferedWaveProvider? _liveOutputBuffer;
    private VoiceEffectMode _selectedLiveEffect = VoiceEffectMode.Normal;
    private long _liveSampleIndex;
    private bool _isLiveModeRunning;

    private void RecordButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRecording)
        {
            return;
        }

        try
        {
            if (WaveInEvent.DeviceCount == 0)
            {
                VoiceStatusText.Text = "No microphone was found on this PC.";
                return;
            }

            _recordedBuffer = new MemoryStream();
            _recordedPcm = null;
            SetPlaybackButtonsEnabled(false);

            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(VoiceSampleRate, VoiceBitsPerSample, VoiceChannels)
            };
            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;

            _isRecording = true;
            RecordButton.IsEnabled = false;
            VoiceStatusText.Text = "Recording... speak now (5 seconds).";

            _waveIn.StartRecording();

            _recordingStopTimer = new DispatcherTimer { Interval = RecordingDuration };
            _recordingStopTimer.Tick += (_, _) =>
            {
                _recordingStopTimer?.Stop();
                try
                {
                    _waveIn?.StopRecording();
                }
                catch (Exception)
                {
                    // Any failure here still surfaces through RecordingStopped's StoppedEventArgs.Exception.
                }
            };
            _recordingStopTimer.Start();
        }
        catch (Exception ex)
        {
            _isRecording = false;
            RecordButton.IsEnabled = true;
            VoiceStatusText.Text = "Couldn't start recording: " + ex.Message +
                " (check Windows Settings > Privacy & security > Microphone.)";
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        _recordedBuffer.Write(e.Buffer, 0, e.BytesRecorded);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _isRecording = false;
            RecordButton.IsEnabled = true;

            _recordedPcm = _recordedBuffer.ToArray();
            _waveIn?.Dispose();
            _waveIn = null;

            if (e.Exception is not null)
            {
                VoiceStatusText.Text = "Recording failed: " + e.Exception.Message;
                return;
            }

            if (_recordedPcm.Length == 0)
            {
                VoiceStatusText.Text = "Didn't capture any audio - check your microphone and try again.";
                return;
            }

            SetPlaybackButtonsEnabled(true);
            VoiceStatusText.Text = "Got it! Pick an effect below to play it back.";
        });
    }

    private void SetPlaybackButtonsEnabled(bool enabled)
    {
        PlayNormalButton.IsEnabled = enabled;
        PlayDeepButton.IsEnabled = enabled;
        PlayHighButton.IsEnabled = enabled;
        PlayRobotButton.IsEnabled = enabled;
    }

    private void PlayNormalButton_Click(object sender, RoutedEventArgs e) => PlayAtDeclaredRate(VoiceSampleRate, "Normal");

    private void PlayDeepButton_Click(object sender, RoutedEventArgs e) => PlayAtDeclaredRate((int)(VoiceSampleRate * 0.75), "Deep Voice");

    private void PlayHighButton_Click(object sender, RoutedEventArgs e) => PlayAtDeclaredRate((int)(VoiceSampleRate * 1.4), "High Voice");

    private void PlayRobotButton_Click(object sender, RoutedEventArgs e)
    {
        if (_recordedPcm is null)
        {
            return;
        }

        try
        {
            StopPlayback();
            var robotPcm = ApplyRingModulation(_recordedPcm, VoiceSampleRate, carrierHz: 60);
            PlayRawPcm(robotPcm, VoiceSampleRate, "Robot");
        }
        catch (Exception ex)
        {
            VoiceStatusText.Text = "Couldn't play that back: " + ex.Message;
        }
    }

    private void PlayAtDeclaredRate(int declaredSampleRate, string label)
    {
        if (_recordedPcm is null)
        {
            return;
        }

        try
        {
            StopPlayback();
            PlayRawPcm(_recordedPcm, declaredSampleRate, label);
        }
        catch (Exception ex)
        {
            VoiceStatusText.Text = "Couldn't play that back: " + ex.Message;
        }
    }

    private void PlayRawPcm(byte[] pcm, int declaredSampleRate, string label)
    {
        var stream = new MemoryStream(pcm);
        var waveFormat = new WaveFormat(declaredSampleRate, VoiceBitsPerSample, VoiceChannels);
        var rawStream = new RawSourceWaveStream(stream, waveFormat);

        _waveOut = new WaveOutEvent();
        _waveOut.Init(rawStream);
        _waveOut.PlaybackStopped += (_, _) =>
        {
            rawStream.Dispose();
            stream.Dispose();
        };
        _waveOut.Play();

        VoiceStatusText.Text = "Playing back as: " + label;
    }

    private void StopPlayback()
    {
        if (_waveOut is null)
        {
            return;
        }

        try
        {
            _waveOut.Stop();
            _waveOut.Dispose();
        }
        catch (Exception)
        {
            // Best effort.
        }
        finally
        {
            _waveOut = null;
        }
    }

    /// <summary>Stops any in-progress recording/playback/live mode - called when the window closes.</summary>
    private void StopVoiceActivity()
    {
        try
        {
            _recordingStopTimer?.Stop();
            _waveIn?.StopRecording();
            _waveIn?.Dispose();
        }
        catch (Exception)
        {
            // Best effort on the way out.
        }
        finally
        {
            _waveIn = null;
        }

        StopPlayback();
        StopLiveVoiceChanger();
    }

    /// <summary>
    /// A classic "robot voice" effect: multiplies each sample by a fixed-frequency
    /// carrier tone (ring modulation). Simple, well-understood DSP - no pitch/tempo change.
    /// </summary>
    private static byte[] ApplyRingModulation(byte[] pcm16Mono, int sampleRate, double carrierHz)
    {
        var result = new byte[pcm16Mono.Length];
        var sampleCount = pcm16Mono.Length / 2;

        for (var i = 0; i < sampleCount; i++)
        {
            var sample = BitConverter.ToInt16(pcm16Mono, i * 2);
            var modulator = Math.Sin(2 * Math.PI * carrierHz * i / sampleRate);
            var modulated = (short)Math.Clamp(sample * modulator, short.MinValue, short.MaxValue);
            var bytes = BitConverter.GetBytes(modulated);
            result[i * 2] = bytes[0];
            result[i * 2 + 1] = bytes[1];
        }

        return result;
    }

    // ================= Live Voice Changer =================
    //
    // Unlike the record-then-playback feature above, this continuously reads the
    // microphone and writes processed audio to a chosen output device while running.
    // "Deep"/"High" can't use the simple "declare a different sample rate" trick here
    // (that only works for a fixed-length clip - for a live stream it would drift out
    // of sync or run out of buffered audio within seconds), so instead each small
    // incoming chunk is resampled to the SAME length via linear interpolation, which
    // keeps real-time sync perfectly but can sound slightly grainy - an accepted
    // trade-off for a simple, dependency-free implementation. "Robot" (ring
    // modulation) has no such issue and sounds clean live, using a running phase
    // counter so the effect doesn't click at chunk boundaries.

    private void RefreshDevicesButton_Click(object sender, RoutedEventArgs e) => PopulateOutputDevices();

    private void PopulateOutputDevices()
    {
        var previousSelection = LiveOutputDeviceCombo.SelectedIndex;

        LiveOutputDeviceCombo.Items.Clear();
        LiveOutputDeviceCombo.Items.Add("Default output device");

        try
        {
            for (var i = 0; i < WaveOut.DeviceCount; i++)
            {
                var capabilities = WaveOut.GetCapabilities(i);
                LiveOutputDeviceCombo.Items.Add(capabilities.ProductName);
            }
        }
        catch (Exception)
        {
            // Fall back to just the default device entry - still usable.
        }

        LiveOutputDeviceCombo.SelectedIndex = previousSelection >= 0 && previousSelection < LiveOutputDeviceCombo.Items.Count
            ? previousSelection
            : 0;
    }

    private void LiveToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isLiveModeRunning)
        {
            StopLiveVoiceChanger();
        }
        else
        {
            StartLiveVoiceChanger();
        }
    }

    private void StartLiveVoiceChanger()
    {
        if (_isRecording)
        {
            LiveStatusText.Text = "Finish the current recording first.";
            return;
        }

        try
        {
            if (WaveInEvent.DeviceCount == 0)
            {
                LiveStatusText.Text = "No microphone was found on this PC.";
                return;
            }

            _selectedLiveEffect = LiveDeepRadio.IsChecked == true ? VoiceEffectMode.Deep
                : LiveHighRadio.IsChecked == true ? VoiceEffectMode.High
                : LiveRobotRadio.IsChecked == true ? VoiceEffectMode.Robot
                : VoiceEffectMode.Normal;

            // Index 0 in the combo is "Default output device" (NAudio's DeviceNumber -1);
            // every entry after that lines up with WaveOut.GetCapabilities(index - 1).
            var outputDeviceNumber = LiveOutputDeviceCombo.SelectedIndex <= 0 ? -1 : LiveOutputDeviceCombo.SelectedIndex - 1;

            _liveOutputBuffer = new BufferedWaveProvider(new WaveFormat(VoiceSampleRate, VoiceBitsPerSample, VoiceChannels))
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromSeconds(2)
            };

            _liveWaveOut = new WaveOutEvent { DeviceNumber = outputDeviceNumber };
            _liveWaveOut.Init(_liveOutputBuffer);
            _liveWaveOut.Play();

            _liveSampleIndex = 0;
            _liveWaveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(VoiceSampleRate, VoiceBitsPerSample, VoiceChannels)
            };
            _liveWaveIn.DataAvailable += OnLiveDataAvailable;
            _liveWaveIn.StartRecording();

            _isLiveModeRunning = true;
            LiveToggleButton.Content = "⏹ _Stop Live Voice Changer";
            SetLiveControlsEnabled(false);
            LiveStatusText.Text = $"Live voice changer is running ({_selectedLiveEffect} effect).";
        }
        catch (Exception ex)
        {
            StopLiveVoiceChanger();
            LiveStatusText.Text = "Couldn't start the live voice changer: " + ex.Message +
                " (check Windows Settings > Privacy & security > Microphone.)";
        }
    }

    private void StopLiveVoiceChanger()
    {
        try
        {
            _liveWaveIn?.StopRecording();
            _liveWaveIn?.Dispose();
        }
        catch (Exception)
        {
            // Best effort.
        }
        finally
        {
            _liveWaveIn = null;
        }

        try
        {
            _liveWaveOut?.Stop();
            _liveWaveOut?.Dispose();
        }
        catch (Exception)
        {
            // Best effort.
        }
        finally
        {
            _liveWaveOut = null;
        }

        _liveOutputBuffer = null;

        if (_isLiveModeRunning)
        {
            LiveStatusText.Text = "Live voice changer is off.";
        }

        _isLiveModeRunning = false;
        LiveToggleButton.Content = "▶ _Start Live Voice Changer";
        SetLiveControlsEnabled(true);
    }

    private void SetLiveControlsEnabled(bool enabled)
    {
        LiveNormalRadio.IsEnabled = enabled;
        LiveDeepRadio.IsEnabled = enabled;
        LiveHighRadio.IsEnabled = enabled;
        LiveRobotRadio.IsEnabled = enabled;
        LiveOutputDeviceCombo.IsEnabled = enabled;
        RefreshDevicesButton.IsEnabled = enabled;

        // A second WaveInEvent on the same mic while live mode owns it would fail - keep Record disabled meanwhile.
        RecordButton.IsEnabled = enabled;
    }

    private void OnLiveDataAvailable(object? sender, WaveInEventArgs e)
    {
        var buffer = _liveOutputBuffer;
        if (buffer is null || e.BytesRecorded == 0)
        {
            return;
        }

        switch (_selectedLiveEffect)
        {
            case VoiceEffectMode.Robot:
                var robotBytes = ApplyRingModulationContinuous(e.Buffer, e.BytesRecorded, VoiceSampleRate);
                buffer.AddSamples(robotBytes, 0, robotBytes.Length);
                break;
            case VoiceEffectMode.Deep:
                var deepBytes = ApplyBlockPitchShift(e.Buffer, e.BytesRecorded, 0.75);
                buffer.AddSamples(deepBytes, 0, deepBytes.Length);
                break;
            case VoiceEffectMode.High:
                var highBytes = ApplyBlockPitchShift(e.Buffer, e.BytesRecorded, 1.4);
                buffer.AddSamples(highBytes, 0, highBytes.Length);
                break;
            default:
                buffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
                break;
        }
    }

    /// <summary>Ring modulation with a phase counter that keeps advancing across calls, so consecutive chunks don't click at the seams.</summary>
    private byte[] ApplyRingModulationContinuous(byte[] buffer, int bytesRecorded, int sampleRate)
    {
        var result = new byte[bytesRecorded];
        var sampleCount = bytesRecorded / 2;

        for (var i = 0; i < sampleCount; i++)
        {
            var sample = BitConverter.ToInt16(buffer, i * 2);
            var modulator = Math.Sin(2 * Math.PI * 60.0 * _liveSampleIndex / sampleRate);
            var modulated = (short)Math.Clamp(sample * modulator, short.MinValue, short.MaxValue);
            var bytes = BitConverter.GetBytes(modulated);
            result[i * 2] = bytes[0];
            result[i * 2 + 1] = bytes[1];
            _liveSampleIndex++;
        }

        return result;
    }

    /// <summary>
    /// Resamples one chunk via linear interpolation, but forces the output back to the
    /// same length as the input - a simple way to shift pitch on a live stream without
    /// the timing drift a straight sample-rate trick would cause. pitchRatio &lt; 1 = lower/deeper, &gt; 1 = higher.
    /// </summary>
    private static byte[] ApplyBlockPitchShift(byte[] buffer, int bytesRecorded, double pitchRatio)
    {
        var sampleCount = bytesRecorded / 2;
        var result = new byte[bytesRecorded];

        for (var i = 0; i < sampleCount; i++)
        {
            var sourcePosition = i * pitchRatio;
            var sourceIndex = (int)sourcePosition;
            var frac = sourcePosition - sourceIndex;

            var sampleA = sourceIndex < sampleCount ? BitConverter.ToInt16(buffer, sourceIndex * 2) : (short)0;
            var nextIndex = sourceIndex + 1;
            var sampleB = nextIndex < sampleCount ? BitConverter.ToInt16(buffer, nextIndex * 2) : sampleA;

            var interpolated = (short)(sampleA * (1 - frac) + sampleB * frac);
            var bytes = BitConverter.GetBytes(interpolated);
            result[i * 2] = bytes[0];
            result[i * 2 + 1] = bytes[1];
        }

        return result;
    }
}
