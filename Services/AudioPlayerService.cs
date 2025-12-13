using System;
using System.IO;
using LibVLCSharp.Shared;

namespace SLSKDONET.Services
{
    public class AudioPlayerService : IAudioPlayerService
    {
        private LibVLC? _libVLC;
        private MediaPlayer? _mediaPlayer;
        private bool _isInitialized;

        public event EventHandler<long>? TimeChanged;
        public event EventHandler<float>? PositionChanged;
        public event EventHandler<long>? LengthChanged;
        public event EventHandler? EndReached;
        public event EventHandler? PausableChanged;

        public AudioPlayerService()
        {
            // Lazy initialization or explicit? 
            // We'll initialize in constructor for now, assuming Core.Initialize is safe.
            Initialize();
        }

        private void Initialize()
        {
            if (_isInitialized) return;

            try 
            {
                Core.Initialize(); // Defines native library path
                _libVLC = new LibVLC();
                _mediaPlayer = new MediaPlayer(_libVLC);

                _mediaPlayer.TimeChanged += (s, e) => TimeChanged?.Invoke(this, e.Time);
                _mediaPlayer.PositionChanged += (s, e) => PositionChanged?.Invoke(this, e.Position);
                _mediaPlayer.LengthChanged += (s, e) => LengthChanged?.Invoke(this, e.Length);
                _mediaPlayer.EndReached += (s, e) => EndReached?.Invoke(this, EventArgs.Empty);
                _mediaPlayer.PausableChanged += (s, e) => PausableChanged?.Invoke(this, EventArgs.Empty);

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AudioPlayerService] Initialization Failed: {ex.Message}");
                // In production, log this properly.
            }
        }

        public bool IsInitialized => _isInitialized;
        
        public bool IsPlaying => _mediaPlayer?.IsPlaying ?? false;
        
        public long Length => _mediaPlayer?.Length ?? 0;
        
        public long Time => _mediaPlayer?.Time ?? 0;

        public float Position
        {
            get => _mediaPlayer?.Position ?? 0f;
            set
            {
                if (_mediaPlayer != null) 
                    _mediaPlayer.Position = value;
            }
        }

        public int Volume
        {
            get => _mediaPlayer?.Volume ?? 100;
            set
            {
                if (_mediaPlayer != null) 
                    _mediaPlayer.Volume = value;
            }
        }

        public void Play(string uri)
        {
            if (!_isInitialized || _libVLC == null || _mediaPlayer == null) return;

            // Stop current if playing? MediaPlayer handles this if we set new media.
            // But good practice to clean up Media object.
            
            var media = new Media(_libVLC, uri, FromType.FromPath);
            media.Parse(MediaParseOptions.ParseLocal); // Optional: Parse metadata immediately
            
            _mediaPlayer.Play(media);
            
            // media object ownership is transferred to MediaPlayer? No.
            // But we don't need to keep a reference to it unless we want to access it later.
            // Ideally we dispose it when done, but MediaPlayer play is async.
            // Actually LibVLC C# wrapper handles reference counting mostly, but generally we should keep 'media' alive?
            // "You can dispose the media immediately after calling Play".
            // Let's verify documentation. LibVLCSharp examples often keep it locally.
        }

        public void Pause()
        {
            _mediaPlayer?.Pause();
        }

        public void Stop()
        {
            _mediaPlayer?.Stop();
        }

        public void Dispose()
        {
            _mediaPlayer?.Dispose();
            _libVLC?.Dispose();
        }
    }
}
