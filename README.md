# SLSKDONET - Soulseek.NET Batch Downloader

A modern WPF desktop application for batch downloading music from Soulseek using the Soulseek.NET library.

## Features

- **Soulseek Integration**: Robust Soulseek.NET implementation.
- **Smart Queue Management**: 
    - **State Machine**: Tracks manage their own state (Pending, Downloading, Paused, etc.).
    - **Drag & Drop**: Reorder your pending downloads effortlessly.
    - **Global Control**: Pause, Resume, and Cancel tracks from a central dashboard.
- **Intelligent Search**: 
    - **Ranking**: Prioritizes users with **free upload slots** (+2000 score).
    - **Metrics**: Displays Queue Length and Upload Speed.
- **Dual-View UI**:
    - **Library**: "Conveyor Belt" view for active projects.
    - **Global Monitor**: "Air Traffic Control" view for all network activity.
- **Resilience**: 
    - **Smart Timeouts**: Distinguishes between "Queued" and "Stalled".
    - **Hard Retry**: One-click wipe and re-queue for failed downloads.

## Architecture

### Project Structure

```
SLSKDONET/
├── Models/                 # Data entities (Track, PlaylistTrack)
├── Services/              
│   ├── SoulseekAdapter.cs  # Network communication
│   ├── DownloadManager.cs  # Singleton Orchestrator
│   └── ResultSorter.cs     # Intelligence & Ranking
├── ViewModels/            
│   ├── MainViewModel.cs    
│   ├── LibraryViewModel.cs # Project/Conveyor View
│   └── PlaylistTrackViewModel.cs # Core State Machine
├── Views/                 
│   ├── LibraryPage.xaml    # Split Active/Warehouse UI
│   └── DownloadsPage.xaml  # Global Dashboard
└── Configuration/          # App settings
```

## Usage

### 1. Search & Queue
Enter a query. Results are auto-ranked by availability (Free Slots first). Click "Download" to add to the **Library Warehouse**.

### 2. Manage in Library
- **Top Row (Active)**: Watch files currently downloading.
- **Bottom Row (Warehouse)**: Drag & Drop pending items to reorder.
- **Context Actions**: Right-click or use buttons to Cancel/Retry.

### 3. Global Dashboard
Switch to the **Downloads** tab to see *everything* happening across the network.
- **Pause/Resume**: Control bandwidth usage globally.
- **Find New Version**: One-click "Orange Button" to find a better source for failed tracks.

## Development

### Adding Features

1. **New Models**: Add to `Models/`
2. **New Services**: Add to `Services/`
3. **New UI Views**: Add to `Views/`
4. **Configuration Options**: Extend `AppConfig.cs` and `ConfigManager.cs`

### Testing

```bash
# Build
dotnet build

# Run tests (when added)
dotnet test
```

## Dependencies

- **Soulseek**: NuGet package for Soulseek.NET protocol
- **CsvHelper**: CSV file parsing
- **TagLibSharp**: Audio metadata reading
- **Microsoft.Extensions.*** : Logging, DI, Configuration
- **System.Reactive**: Reactive extensions

## Related Projects

- **Python Version**: See `Python/` folder for aioslsk-based implementation
- **Reference**: https://github.com/fiso64/slsk-batchdl (C# inspiration)

## Troubleshooting

### Connection Issues

- Verify firewall allows the listening port
- Check Soulseek server status
- Increase `ConnectTimeout` if network is slow

### Search Returns No Results

- Try simpler search queries
- Add artist name explicitly
- Check if content exists on the network

### Downloads Failing

- Verify peer is still online
- Check disk space
- Increase `SearchTimeout` for larger files

## License

GPL-3.0

## Contributing

Contributions welcome! Please follow the existing code style and patterns.
