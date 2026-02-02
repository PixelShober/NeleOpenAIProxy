using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;

namespace NeleDesktop.Services;

public sealed class AudioCaptureService
{
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;
    private MemoryStream? _stream;
    private TaskCompletionSource<byte[]>? _stopTcs;

    public bool IsRecording { get; private set; }

    public void Start()
    {
        if (IsRecording)
        {
            return;
        }

        _stopTcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        _stream = new MemoryStream();
        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 1)
        };
        _writer = new WaveFileWriter(_stream, _waveIn.WaveFormat);

        _waveIn.DataAvailable += (_, args) =>
        {
            _writer?.Write(args.Buffer, 0, args.BytesRecorded);
        };
        _waveIn.RecordingStopped += (_, _) =>
        {
            byte[] data;
            try
            {
                _writer?.Flush();
                data = _stream?.ToArray() ?? Array.Empty<byte>();
            }
            finally
            {
                _writer?.Dispose();
                _waveIn?.Dispose();
                _stream?.Dispose();
                _writer = null;
                _waveIn = null;
                _stream = null;
                IsRecording = false;
            }

            _stopTcs?.TrySetResult(data);
        };

        _waveIn.StartRecording();
        IsRecording = true;
    }

    public Task<byte[]> StopAsync()
    {
        if (!IsRecording || _waveIn is null || _stopTcs is null)
        {
            return Task.FromResult(Array.Empty<byte>());
        }

        _waveIn.StopRecording();
        return _stopTcs.Task;
    }
}
