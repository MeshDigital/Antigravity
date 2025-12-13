using System;

namespace SLSKDONET.Services
{
    public interface IAudioPlayerService : IDisposable
    {
        bool IsPlaying { get; }
        bool IsInitialized { get; } // Check if LibVLC native libraries loaded successfully
        long Length { get; } // Duration in ms
        long Time { get; }   // Current time in ms
        float Position { get; set; } // 0.0 to 1.0
        int Volume { get; set; }     // 0 to 100

        event EventHandler<long> TimeChanged;
        event EventHandler<float> PositionChanged;
        event EventHandler<long> LengthChanged;
        event EventHandler EndReached;
        event EventHandler PausableChanged;

        void Play(string uri);
        void Pause();
        void Stop();
    }
}
