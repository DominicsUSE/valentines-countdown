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

    /// <summary>Stops any in-progress recording/playback - called when the window closes.</summary>
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
}
