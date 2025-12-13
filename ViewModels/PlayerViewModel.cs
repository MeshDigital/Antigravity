using System;
using System.Windows.Input; // For ICommand
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SLSKDONET.Services;

namespace SLSKDONET.ViewModels
{
    public partial class PlayerViewModel : ObservableObject
    {
        private readonly IAudioPlayerService _playerService;
        
        [ObservableProperty]
        private string _trackTitle = "No Track Playing";

        [ObservableProperty]
        private string _trackArtist = "";

        [ObservableProperty]
        private bool _isPlaying;

        [ObservableProperty]
        private float _position; // 0.0 to 1.0

        [ObservableProperty]
        private string _currentTimeStr = "0:00";

        [ObservableProperty]
        private string _totalTimeStr = "0:00";
        
        [ObservableProperty]
        private int _volume = 100;

        public PlayerViewModel(IAudioPlayerService playerService)
        {
            _playerService = playerService;
            
            _playerService.PausableChanged += (s, e) => IsPlaying = _playerService.IsPlaying;
            _playerService.EndReached += (s, e) => IsPlaying = false;
            
            // Throttle position updates? LibVLC updates frequently.
            _playerService.PositionChanged += (s, pos) => 
            {
                 // Check if user is dragging?
                 Position = pos;
            };
            
            _playerService.TimeChanged += (s, timeMs) =>
            {
                CurrentTimeStr = TimeSpan.FromMilliseconds(timeMs).ToString(@"m\:ss");
            };
            
            _playerService.LengthChanged += (s, lenMs) =>
            {
                TotalTimeStr = TimeSpan.FromMilliseconds(lenMs).ToString(@"m\:ss");
            };
        }

        [RelayCommand]
        private void TogglePlayPause()
        {
            if (IsPlaying)
                _playerService.Pause();
            else
            {
                // If nothing loaded, maybe play current?
                // For now, Pause works as toggle if media is loaded.
                _playerService.Pause(); // LibVLC Pause toggles.
            }
            IsPlaying = _playerService.IsPlaying;
        }

        [RelayCommand]
        private void Stop()
        {
            _playerService.Stop();
            IsPlaying = false;
            Position = 0;
            CurrentTimeStr = "0:00";
        }
        
        // Volume Change
        partial void OnVolumeChanged(int value)
        {
            _playerService.Volume = value;
        }

        // Seek (User Drag)
        public void Seek(float position)
        {
            _playerService.Position = position;
        }
        
        // Helper to load track
        public void PlayTrack(string filePath, string title, string artist)
        {
            TrackTitle = title;
            TrackArtist = artist;
            _playerService.Play(filePath);
            IsPlaying = true;
        }
    }
}
