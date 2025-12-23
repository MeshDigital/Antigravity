# Fix: Download count, emoji parsing, and app shutdown cleanup

## 🐛 Bug Fixes

### Download Count Excludes Deleted Albums
**Problem:** Download count showed all in-progress downloads, including those from soft-deleted albums.

**Root Cause:** `DownloadManager.InitAsync()` was loading from legacy `Tracks` table (TrackEntity) which lacks `PlaylistId` field and couldn't filter deleted playlists.

**Solution:** Updated DownloadManager to load from `PlaylistTracks` table (PlaylistTrackEntity) via `GetAllPlaylistTracksAsync()`:
- ✅ Has `PlaylistId` field for filtering
- ✅ Automatically excludes soft-deleted playlists (`IsDeleted = true`)
- ✅ Provides richer metadata (Album, Bitrate, Format)

**Impact:** Download counts now accurately reflect only active album downloads.

### Emoji Removal in Tracklist Parser
**Problem:** Emojis in YouTube/SoundCloud tracklists caused parsing failures (e.g., `❎`, `❌`, `‼️`).

**Solution:** Added `RemoveEmojis()` method to `CommentTracklistParser` that strips emoji characters before timestamp parsing.

**Impact:** Tracklists with emojis now parse correctly.

## 🔧 Improvements

### Application Shutdown Cleanup
**Problem:** When app crashed or closed, orphaned `.NET Host` processes remained in Task Manager due to background services not cleaning up.

**Solution:** Enhanced `App.axaml.cs` Exit event handler to gracefully shutdown:
- Disconnect Soulseek client
- Close database connections
- Clear Spotify credentials (if configured)
- **Flush Serilog logs** (critical - prevents log loss)

**Impact:** Clean process termination, no orphaned processes.

## 📝 Files Changed

### Modified
- `Services/DownloadManager.cs` - Load from PlaylistTracks instead of Tracks table
- `Services/InputParsers/CommentTracklistParser.cs` - Add emoji removal
- `Services/DatabaseService.cs` - Add CloseConnectionsAsync() for shutdown
- `App.axaml.cs` - Enhanced Exit handler with comprehensive cleanup

### Database Schema
- Using `PlaylistTracks` table (has PlaylistId) instead of legacy `Tracks` table
- No schema changes required
