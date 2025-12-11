# SLSKDONET Architecture & Data Flow

## System Architecture

```
┌───────────────────────────────────────────────────────────────┐
│                        WPF GUI Layer                          │
│  ┌────────────────────────┐    ┌───────────────────────────┐  │
│  │ LibraryPage (Project)  │    │ DownloadsPage (Global)    │  │
│  │ - "Conveyor Belt" View │    │ - "Air Traffic Control"   │  │
│  │ - Local Project Filter │    │ - Global Actions & Monitor│  │
│  └───────────┬────────────┘    └─────────────┬─────────────┘  │
│              │                               │                │
│              ▼                               ▼                │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │               PlaylistTrackViewModel (State Machine)    │  │
│  │ - States: Pending, Searching, Downloading, Paused, etc. │  │
│  │ - Commands: Pause, Resume, Cancel, HardRetry            │  │
│  └──────────────────────────┬──────────────────────────────┘  │
└─────────────────────────────┼─────────────────────────────────┘
                              │
                    ┌─────────▼────────┐
                    │ DownloadManager  │ (Singleton)
                    │ - Concurrency (4)│
                    │ - Background Loop│
                    │ - Global List    │
                    └─────────┬────────┘
                              │
           ┌──────────────────┼─────────────────────┐
           ▼                  ▼                     ▼
    ┌──────────────┐   ┌──────────────┐    ┌────────────────┐
    │ ResultSorter │   │ Soulseek     │    │ FileSystem     │
    │ - Ranking    │   │ Adapter      │    │ - Write Files  │
    │ - Intelligence│  │ - Network    │    │ - Delete .part │
    └──────────────┘   └──────────────┘    └────────────────┘
```

## Input Processing Flow

```
User Input (Search Query)
        │
        ▼
┌─────────────────────┐
│ InputType Detection │
├─────────────────────┤
│ - CSV file?         │
│ - Spotify URL?      │
│ - YouTube URL?      │
│ - Direct string?    │
└────────┬────────────┘
         │
    ┌────┴────────────────────┬──────────────┬─────────────┐
    │                         │              │             │
    ▼                         ▼              ▼             ▼
┌─────────┐           ┌──────────┐    ┌────────────┐  ┌────────┐
│ CSV     │           │ String   │    │ Spotify    │  │ YouTube│
│ Source  │           │ Source   │    │ Source     │  │ Source │
└────┬────┘           └────┬─────┘    └────┬───────┘  └───┬────┘
     │                     │               │             │
     ├─ Auto-detect cols   └─ Parse        ├─ OAuth      └─ yt-dlp
     └─ Build queries         format       └─ Playlist

         │
         ▼
    ┌──────────────┐
    │ SearchQuery  │
    │ Objects      │
    │ - title      │
    │ - artist     │
    │ - mode       │
    └──────┬───────┘
           │
           ▼
    ┌─────────────────────┐
    │ SearchQueryNormalizer│
    ├─────────────────────┤
    │ - RemoveFeatArtists()│
    │ - RemoveYoutubeMarkers
    │ - ApplyRegex()      │
    └──────┬──────────────┘
           │
           ▼
    ┌──────────────┐
    │ Search Ready │
    └──────────────┘
```

## Search & Filter Flow

```
Search Query
    │
    ▼
SoulseekAdapter.SearchAsync()
    │
    ├─ Query: "Daft Punk - Get Lucky"
    ├─ Timeout: 6000ms
    ├─ Max Results: 100
    │
    ▼
Soulseek Network
    │
    ├─ Returns: List<SearchResponse>
    │  - Username, Files[], etc.
    │
    ▼
┌──────────────────────────┐
│ Parse Results to Track[] │
├──────────────────────────┤
│ - Extract filename       │
│ - Parse bitrate          │
│ - Get size, sample rate  │
└──────┬───────────────────┘
       │
       ▼
┌──────────────────────────────────────┐
│ FileConditionEvaluator.FilterAndRank()
├──────────────────────────────────────┤
│ 1. Apply Required Conditions        │
│    ├─ Format in [mp3, flac]?        │
│    ├─ Bitrate >= 128?               │
│    └─ Pass: true/false              │
│                                      │
│ 2. Score Preferred Conditions       │
│    ├─ Format = mp3? (+1 point)      │
│    ├─ Bitrate in 200-2500? (+1)     │
│    ├─ Length within 3s? (+1)        │
│    └─ Score: 0.0 to 1.0             │
│                                      │
│ 3. Sort Results                     │
│    ├─ By preference score DESC      │
│    ├─ By bitrate DESC               │
│    └─ Final ranking                 │
└──────┬───────────────────────────────┘
       │
       ▼
┌────────────────────┐
│ UI: SearchResults  │
│ (Top results first)│
└────────────────────┘
```

## Download Flow (The Engine)

### 1. Queue Phase
- User adds track -> `DownloadManager.QueueProject`
- Creates `PlaylistTrackViewModel` (State: `Pending`)
- Added to `AllGlobalTracks` collection

### 2. Processing Loop
- `ProcessQueueLoop` runs continuously in background
- Finds next `Pending` track
- Acquires `SemaphoreSlim` slot (Max 4 active)

### 3. Execution Phase
- **Search**: `SoulseekAdapter.SearchAsync`
    - `ResultSorter` ranks results (Priority: Free Slot > Queue Length > Quality)
- **Select**: Best match chosen automatically
- **Download**: `SoulseekAdapter.DownloadAsync`
    - Monitors for "Stalled" vs "Queued" timeout
    - Updates Progress/Speed on ViewModel
- **Completion**: 
    - Success -> State: `Completed` -> Release Slot
    - Failure -> State: `Failed` -> Release Slot

## Smart Search Ranking

The `ResultSorter` uses a weighted scoring system to select the best source:

| Criteria | Score Impact | Reason |
|----------|--------------|--------|
| **Has Free Slot** | **+2000** | Immediate download start. Essential. |
| **Queue Length** | -1 per user | Penalize wait times. |
| Required Conditions | +1000 | Format/Bitrate match. |
| Preferred Conditions | +500 | Perfect metadata match. |
| String Similarity | 0-350 | Fuzzy match on Artist/Title. |


## Configuration & State Flow

```
Application Start
    │
    ▼
┌────────────────────────────────────┐
│ ConfigManager.Load()               │
├────────────────────────────────────┤
│ Check paths in order:              │
│ 1. %AppData%\SLSKDONET\config.ini │
│ 2. Local directory                 │
│ 3. Create default if missing       │
└──────┬─────────────────────────────┘
       │
       ▼
┌────────────────────────────────────┐
│ Parse INI File                     │
├────────────────────────────────────┤
│ [Soulseek]                         │
│ - Username                         │
│ - Password                         │
│ - ListenPort                       │
│ - Timeouts, etc.                   │
│                                    │
│ [Download]                         │
│ - Directory                        │
│ - MaxConcurrent                    │
│ - NameFormat                       │
│ - Conditions (preferred)           │
└──────┬─────────────────────────────┘
       │
       ▼
┌────────────────────────────────────┐
│ AppConfig Object                   │
│ (Injected to services)             │
├────────────────────────────────────┤
│ SoulseekAdapter uses:              │
│ - Username, Password               │
│ - Timeouts                         │
│                                    │
│ DownloadManager uses:              │
│ - DownloadDirectory                │
│ - MaxConcurrentDownloads           │
│ - NameFormat                       │
│                                    │
│ UI uses:                           │
│ - For display defaults             │
└────────────────────────────────────┘
```

## Service Dependency Graph

```
App.xaml.cs (Bootstrapper)
    │
    ├─ Configures Services
    │
    ▼
IServiceProvider (DI Container)
    │
    ├─ Creates Singletons:
    │  ├─ AppConfig (from ConfigManager)
    │  ├─ ConfigManager
    │  ├─ SoulseekAdapter (uses AppConfig)
    │  ├─ DownloadManager (uses AppConfig, SoulseekAdapter)
    │  └─ ILogger
    │
    ├─ Creates Transients:
    │  ├─ MainViewModel (uses logger, config, adapter, manager)
    │  └─ MainWindow (uses MainViewModel)
    │
    ▼
MainWindow (Resolved on app start)
    │
    ├─ DataContext = MainViewModel
    │
    └─ MainViewModel connects everything:
       ├─ Calls adapter.ConnectAsync()
       ├─ Calls adapter.SearchAsync()
       ├─ Calls downloadManager.EnqueueDownload()
       ├─ Calls downloadManager.StartAsync()
       └─ Updates UI via INotifyPropertyChanged
```

## Event Flow Architecture

```
SoulseekAdapter.EventBus (Subject<(string, object)>)
    │
    ├─ connection_status → UI updates login
    ├─ search_results → UI updates grid
    ├─ transfer_added → UI adds row
    ├─ transfer_progress → UI updates progress bar
    ├─ transfer_finished → UI shows complete
    └─ transfer_failed → UI shows error

Also:

DownloadManager Events:
    ├─ JobUpdated → UI progress
    └─ JobCompleted → UI status

MainViewModel.PropertyChanged:
    ├─ IsConnected
    ├─ StatusText
    ├─ SearchResults (ObservableCollection)
    └─ Downloads (ObservableCollection)
```

## File Format Examples

### SearchQuery Parsing
```
Input: "Daft Punk - Get Lucky"
    ↓
Parse as shorthand (contains " - ")
    ↓
Output:
  Artist: "Daft Punk"
  Title: "Get Lucky"
  Mode: Normal
  
Input: "artist=Daft Punk,title=Get Lucky,length=244"
    ↓
Parse as properties (contains "=")
    ↓
Output:
  Artist: "Daft Punk"
  Title: "Get Lucky"
  Length: 244
  Mode: Normal
```

### File Naming Templates
```
Template: "{artist}/{album}/{track}. {title}"
Track data:
  - artist: "Metallica"
  - album: "Master of Puppets"
  - track: 1
  - title: "Battery"

Result: "Metallica/Master of Puppets/1. Battery"

Template: "{artist( - ){title|filename}"
Without artist:
  Result: "Unknown.mp3"
With artist:
  Result: "Artist - Song Title"
```

### CSV Processing
```
CSV File:
  Artist,Title,Album,Length
  Daft Punk,Get Lucky,Random Access Memories,244
  The Weeknd,Blinding Lights,After Hours,200

Processing:
  1. Detect column names (auto)
  2. For each row → SearchQuery
  3. Convert Length to int (seconds)
  4. Create DownloadMode based on Title presence

Output:
  [
    SearchQuery { Artist: "Daft Punk", Title: "Get Lucky", ... },
    SearchQuery { Artist: "The Weeknd", Title: "Blinding Lights", ... }
  ]
```

---

## Design Principles

1. **Separation of Concerns**
   - UI doesn't talk to Soulseek directly
   - Business logic in services
   - Configuration separate from runtime

2. **Dependency Injection**
   - Services injected, not created
   - Loose coupling between components
   - Easy to test and extend

3. **Async Throughout**
   - No blocking calls on UI thread
   - Long operations in background
   - Responsive UI at all times

4. **Reactive Updates**
   - Event bus for major events
   - ObservableCollection for lists
   - INotifyPropertyChanged for properties

5. **Extensibility**
   - IInputSource for new input types
   - FileCondition for new filters
   - Services can be swapped

This architecture makes the codebase maintainable, testable, and ready for future enhancements.
