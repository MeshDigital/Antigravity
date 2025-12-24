![ORBIT Banner](assets/orbit_banner.png)

# 🛰️ ORBIT – Organized Retrieval & Batch Integration Tool

> **"Intelligent music discovery meets DJ-grade metadata management."**  
> *A Soulseek client designed for reliability and musical intelligence*

[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-blue)](https://github.com/MeshDigital/ORBIT)
[![.NET](https://img.shields.io/badge/. NET-8.0-purple)](https://dotnet.microsoft.com/)
[![UI](https://img.shields.io/badge/UI-Avalonia-orange)](https://avaloniaui.net/)
[![License](https://img.shields.io/badge/license-GPL--3.0-green)](LICENSE)
[![Status](https://img.shields.io/badge/status-Active%20Development-brightgreen)](https://github.com/MeshDigital/ORBIT)

---

## 🚀 What Is ORBIT?

ORBIT is a Soulseek client built for DJs and music enthusiasts who demand both quality and reliability. It combines intelligent search ranking, automated metadata enrichment, and crash-resilient downloads into a professional tool.

Where traditional P2P clients download the first available file, ORBIT analyzes search results to find:
- ✅ Highest quality files (FLAC > 320kbps > 128kbps)
- ✅ Correct versions (Radio Edit vs Extended Mix)
- ✅ Musically compatible tracks (BPM/Key matching for DJs)
- ✅ Authentic files (VBR validation detects fakes)

---

## ✨ Core Features

### 🎯 Intelligent Search Ranking
- **Quality-First Scoring**: Bitrate is the primary factor, musical attributes act as tiebreakers
- **VBR Validation**: Detects upconverted files (128→320, MP3→FLAC)
- **Filename Cleanup**: Ignores noise like `[uploader-tag]`, `(Remastered)`, `[Official Video]`
- **Path-Based Discovery**: Extracts BPM/Key from directory names when files lack tags
- **Duration Matching**: Ensures you get the version you're searching for

### 🛡️ Crash Recovery (Phase 2A)
- **Automatic Resume**: Downloads and tag writes resume after unexpected closures
- **Atomic Operations**: File operations complete fully or not at all
- **Progress Tracking**: 15-second heartbeats monitor active downloads
- **Stall Detection**: Warns when transfers haven't progressed in 1 minute
- **Zero Data Loss**: SQLite WAL mode prevents database corruption

### 🎧 Spotify Integration
- **Playlist Import**: Paste a Spotify URL to queue downloads
- **Metadata Enrichment**: Automatic BPM, Key, Album Art, and Genre tagging
- **Duration Validation**: Uses Spotify's canonical duration to verify file versions
- **Liked Songs Support**: Import your entire Spotify library

### 💿 DJ-Ready Metadata
- **Camelot Key Notation**: Automatic detection and tagging (e.g., "8A")
- **BPM Persistence**: Writes tempo to file tags (ID3v2.4, Vorbis)
- **Custom Tags**: Spotify IDs embedded for library maintenance
- **DJ Software Compatible**: Works with Rekordbox, Serato, Traktor

### 🎨 Modern UI
- **Dark Theme**: Clean, Spotify-inspired interface
- **Real-Time Progress**: Live download tracking with queue management
- **Library Organization**: Drag-and-drop playlist management
- **Built-in Player**: Preview tracks before committing to downloads

---

## 🧠 The Brain: Ranking Algorithm

ORBIT uses a multi-tiered scoring system that prioritizes quality while respecting musical context:

### Tier 0: Availability
- Free upload slot: +2000 pts
- Queue length penalty: -10 pts per waiting item
- Overloaded peer penalty: -500 pts for >50 queued downloads

### Tier 1: Quality (Primary)
- **Lossless (FLAC)**: 450 pts
- **High (320kbps)**: 300 pts
- **Medium (192kbps)**: 150 pts
- **Low (128kbps)**: 64 pts (proportional scaling)

### Tier 2: Musical Intelligence (Tiebreaker)
- BPM match: +100 pts
- Key match: +75 pts
- Harmonic key: +50 pts

### Tier 3: Guard Clauses
- Duration mismatch: Hidden from results
- Fake file detected: Hidden from results
- VBR validation failed: Hidden from results

**Example Scoring:**
```
Search: "Deadmau5 - Strobe" (128 BPM, 10:37)

File A: FLAC, 1411kbps, "Strobe (128bpm).flac"
→ Quality: 450 + BPM: 100 = 550 pts ✅ SELECTED

File B: MP3, 320kbps, "Strobe.mp3"
→ Quality: 300 + BPM: 50 = 350 pts

File C: MP3, 128kbps, "Strobe (128bpm).mp3"
→ Quality: 64 + BPM: 100 = 164 pts

File D: "FLAC", 1411kbps, "Strobe.flac" (9 MB - FAKE)
→ VBR Validation: FAIL = Hidden
```

---

## 🏗️ Architecture

### Tech Stack
- **UI Framework**: Avalonia (cross-platform XAML)
- **Backend**: .NET 8.0 (C#)
- **Database**: SQLite + Entity Framework Core
- **Audio Playback**: LibVLC
- **P2P Network**: Soulseek.NET
- **Metadata**: TagLib# (audio tagging)

### Design Patterns
- **Strategy Pattern**: Swappable ranking algorithms
- **Observer Pattern**: Event-driven UI updates
- **Journal-First Pattern**: Crash recovery with prepare-log-execute-commit flow
- **Connection Pooling**: Optimized SQLite access for recovery journal
- **Atomic Operations**: SafeWrite pattern for file operations

### Project Structure
```
ORBIT/
├── Views/Avalonia/          # UI components (XAML + code-behind)
├── ViewModels/              # Business logic & state management
├── Services/                # Core engines
│   ├── DownloadManager.cs       # Queue orchestration + heartbeat
│   ├── SearchResultMatcher.cs   # Ranking algorithm
│   ├── CrashRecoveryJournal.cs  # Recovery checkpoint logging
│   └── SonicIntegrityService.cs # Spectral analysis (Phase 8)
├── Models/                  # Data models & events
├── Configuration/           # Scoring constants, app settings
├── Utils/                   # String matching, filename normalization
└── DOCS/                    # Technical documentation
```

---

## 📊 Development Status

### ✅ Phase 0: Foundation
- Cross-platform UI (Avalonia)
- Spotify playlist import
- Soulseek download manager
- SQLite library database
- Built-in audio player
- Metadata enrichment (BPM, Key, Album Art)

### ✅ Phase 1: Intelligent Ranking
- Quality-gated scoring
- Filename noise stripping
- Path-based token search
- VBR fraud detection
- Duration gating

### ✅ Phase 1A: Atomic File Operations
- SafeWrite pattern for crash-safe tag writes
- Disk space checking
- Timestamp preservation
- File verification helpers

### ✅ Phase 1B: Database Optimization
- SQLite WAL mode for concurrency
- Index audit and recommendations
- 10MB cache configuration
- Auto-checkpoint at 1000 pages

### ✅ Phase 2A: Crash Recovery (December 2024)
- Recovery journal with connection pooling
- Monotonic heartbeat tracking
- Download resume capability
- Stall detection (4-heartbeat threshold)
- Idempotent recovery logic
- Dead-letter handling (3-strike limit)
- Priority-based startup recovery
- Non-intrusive UX notifications

### 🚧 Phase 2B: Code Quality (In Progress)
- Strategy Pattern for ranking modes
- Parameter Object refactoring
- Observer Pattern for events
- Null Object Pattern for metadata

### 🔥 Phase 8: Sonic Integrity (40% Complete)
- FFmpeg integration for spectral analysis
- Producer-Consumer batch processing
- Database maintenance automation
- Smart dependency validation

### 🔮 Future Phases
- **Phase 3**: USB/Local import with duplicate detection
- **Phase 4**: Performance optimization (multi-core, hardware acceleration)
- **Phase 5**: Self-healing library (automatic quality upgrades)

---

## 🚀 Quick Start

### Prerequisites
- **Windows 10/11** (macOS/Linux support in progress)
- .NET 8.0 SDK ([Download](https://dotnet.microsoft.com/download))
- Soulseek account (Free at [slsknet.org](https://www.slsknet.org))
- **Optional**: FFmpeg (for Phase 8 spectral analysis features)

### Installation
```bash
git clone https://github.com/MeshDigital/ORBIT.git
cd ORBIT
dotnet restore
dotnet build
dotnet run
```

### First-Time Setup
1. Launch ORBIT
2. Navigate to **Settings**
3. Enter your Soulseek credentials
4. **Optional**: Connect Spotify (PKCE auth - no API keys required)
5. Import a playlist via URL or search directly

### FFmpeg Setup (Optional - for Sonic Integrity)
- **Windows**: Download from [ffmpeg.org](https://ffmpeg.org), add to PATH
- **macOS**: `brew install ffmpeg`
- **Linux**: `sudo apt install ffmpeg` or equivalent

---

## 📖 Documentation

### Core Documentation
- [**Architecture Overview**](DOCS/ARCHITECTURE.md) - Design decisions and patterns
- [**The Brain: Smart Gating**](DOCS/THE_BRAIN_SMART_GATING.md) - Duration validation logic
- [**Metadata Persistence**](DOCS/METADATA_PERSISTENCE.md) - DJ-ready tagging explained
- [**Ranking Examples**](DOCS/RANKING_EXAMPLES.md) - Real-world scoring scenarios
- [**Spotify Auth**](DOCS/SPOTIFY_AUTH.md) - PKCE implementation details

### Technical Artifacts
- [**TODO.md**](TODO.md) - Active development tasks
- [**ROADMAP.md**](ROADMAP.md) - Long-term vision and priorities
- [**CHANGELOG.md**](CHANGELOG.md) - Version history

---

## 🤝 Contributing

Contributions are welcome! Whether you're fixing bugs, adding features, or improving documentation, your help is appreciated.

### Development Workflow
1. Fork the repository
2. Create a feature branch (`git checkout -b feature/your-feature`)
3. Commit your changes (`git commit -m 'feat: add your feature'`)
4. Push to your branch (`git push origin feature/your-feature`)
5. Open a Pull Request

### Code Standards
- Follow C# naming conventions
- Write XML documentation for public APIs
- Include unit tests for new features
- Keep commits atomic and well-described

---

## 🔧 Built With

- [Avalonia UI](https://avaloniaui.net/) - Cross-platform XAML framework
- [Entity Framework Core](https://docs.microsoft.com/ef/) - Object-relational mapping
- [Soulseek.NET](https://github.com/jpdillingham/Soulseek.NET) - P2P networking
- [TagLib#](https://github.com/mono/taglib-sharp) - Audio metadata
- [LibVLCSharp](https://code.videolan.org/videolan/LibVLCSharp) - Media playback
- [Xabe.FFmpeg](https://ffmpeg.xabe.net/) - Audio analysis

---

## 📜 License

GPL-3.0 - See [LICENSE](LICENSE) for details.

---

## 💬 Contact

- **Issues**: [Report bugs or request features](https://github.com/MeshDigital/ORBIT/issues)
- **Discussions**: [Join the community](https://github.com/MeshDigital/ORBIT/discussions)

---

**Built for music enthusiasts who demand quality and reliability** | **Since 2024**
