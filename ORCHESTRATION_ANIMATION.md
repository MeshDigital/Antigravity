# Orchestration Animation Implementation

## Overview
Real-time visual feedback system for track orchestration during batch Spotify/CSV imports. Shows which tracks are being searched, ranked, and matched as the process happens.

## Components Added

### 1. **OrchestratedQueryProgress Model** (`Models/OrchestratedQueryProgress.cs`)
Tracks the state and progress of each imported track through the orchestration pipeline:

```csharp
public class OrchestratedQueryProgress
{
    public string QueryId { get; set; }          // Unique identifier
    public string Query { get; set; }             // "Artist - Title" format
    public string State { get; set; }             // Queued | Searching | Ranking | Matched | Failed
    public int TotalResults { get; set; }         // Updated as search completes
    public string MatchedTrack { get; set; }      // Best match result
    public double MatchScore { get; set; }        // Rank score (0-100)
    public bool IsProcessing { get; set; }        // Animates during search/rank
    public bool IsComplete { get; set; }          // True when matched/failed
    public string ErrorMessage { get; set; }      // Error details if failed
    
    public string GetStatusEmoji()                // Returns current state emoji
}
```

### 2. **StateToEmojiConverter** (`Converters/StateToEmojiConverter.cs`)
Converts track state to animated emoji for visual feedback:
- **⏳ Queued** - Waiting to start
- **🔍 Searching** (animated 🔍 ↔ 🔎) - Active search in progress
- **⭐ Ranking** (animated ⭐ ↔ ✨) - Evaluating results
- **✅ Matched** - Best match found and ready to download
- **❌ Failed** - No results found

### 3. **MainViewModel Updates**
- Added `ObservableCollection<OrchestratedQueryProgress> OrchestratedQueries` for real-time binding
- Updated `SearchAllImportedAsync()` to populate progress items as they process:
  1. Initialize all queries as "Queued" before starting
  2. Update each to "Searching" when Soulseek search begins
  3. Update to "Ranking" when evaluating results
  4. Update to "Matched" with matched track info when best match found
  5. Update to "Failed" if no results found

### 4. **SearchPage UI Enhancement** (`Views/SearchPage.xaml`)
New orchestration progress panel between filter panel and results:

**Layout (Row 2):**
```
🎵 Orchestrating Batch Import

[Scrollable List of Tracks]
┌─ ⏳ Track 1: Spotify URL        Results: 0      [Progress Bar]
├─ 🔍 Track 2: Another Song       Results: 47     [Progress Bar]
├─ ⭐ Track 3: Third Track        Results: 23     [Progress Bar]
├─ ✅ Track 4: Found Result       Artist - Song   [████████░░]
└─ ❌ Track 5: No Results                         [░░░░░░░░░░]

Status: Processed 4/10 queries. Found 3 best matches.
```

**Features:**
- **Emoji Status Indicator** - Shows current state with animation for Searching/Ranking
- **Query Display** - Shows original query string (trimmed if long)
- **Result Count** - During processing, shows how many results found
- **Matched Track** - When complete, shows the selected best match
- **Match Score Bar** - Visual progress bar showing rank score (0-100)
- **Scrollable Container** - Max height 200px with scrollbar for large imports
- **Status Text** - Real-time status updates at bottom

**Visibility:**
- Only shows when `OrchestratedQueries` collection has items
- Auto-hides when collection is cleared (after orchestration completes)

## Data Flow

```
ImportFromSpotifyAsync / SearchAllImportedAsync
    │
    ├─ Clear OrchestratedQueries collection
    ├─ Initialize each imported query as "Queued"
    │   └─ Add to OrchestratedQueries (triggers UI panel visibility)
    │
    ├─ Parallel.ForEachAsync(ImportedQueries, MaxDegreeOfParallelism=4)
    │   │
    │   ├─ Update progress state: "Searching" + IsProcessing=true
    │   ├─ Execute SoulseekAdapter.SearchAsync()
    │   │
    │   ├─ Update progress: TotalResults = count
    │   ├─ Update progress state: "Ranking"
    │   ├─ Execute ResultSorter.OrderResults()
    │   │
    │   ├─ Update progress:
    │   │   ├─ State = "Matched"
    │   │   ├─ MatchedTrack = "Artist - Title"
    │   │   ├─ MatchScore = RankValue (0-100)
    │   │   ├─ IsProcessing = false
    │   │   └─ IsComplete = true
    │   │
    │   └─ If no results:
    │       ├─ State = "Failed"
    │       ├─ ErrorMessage = "No results found"
    │       └─ IsComplete = true
    │
    ├─ UI displays all progress items with animations
    │   └─ Emojis animate while IsProcessing=true
    │   └─ Result bars fill as MatchScore is set
    │
    ├─ Auto-queue best matches for download
    └─ Clear OrchestratedQueries (hides panel)
```

## Visual Timeline

**Before Import:**
- Panel is hidden (no OrchestratedQueries)
- SearchPage shows search box, filters, empty results

**During Import - Phase 1 (Initial):**
```
🎵 Orchestrating Batch Import
⏳ The Night We Met - Lord Huron
⏳ Blinding Lights - The Weeknd
⏳ good 4 u - Olivia Rodrigo
```

**During Import - Phase 2 (Searching):**
```
🎵 Orchestrating Batch Import
✅ The Night We Met - Lord Huron     Lord Huron - The...  [████████░░]
🔍 Blinding Lights - The Weeknd      Results: 156         
⭐ good 4 u - Olivia Rodrigo         Results: 89
⏳ vampire - Olivia Rodrigo
⏳ drivers license - Olivia Rodrigo

Status: Processed 2/5 queries. Found 1 best match.
```

**During Import - Phase 3 (Complete):**
```
🎵 Orchestrating Batch Import
✅ The Night We Met - Lord Huron     Lord Huron - The...  [████████░░]
✅ Blinding Lights - The Weeknd      The Weeknd - Blin... [█████████░]
✅ good 4 u - Olivia Rodrigo         Olivia Rodrigo -...  [████████░░]
✅ vampire - Olivia Rodrigo          Olivia Rodrigo -...  [██████░░░░]
✅ drivers license - Olivia Rodrigo  Olivia Rodrigo -...  [█████████░]

Status: ✓ Orchestration complete: 5 best matches selected from 5 queries.
```

**After Import:**
- Panel hides
- Results displayed in main DataGrid
- Auto-download begins

## Key Features

1. **Real-Time Updates** - Progress updates as each track is processed
2. **Parallel Processing Visible** - Shows multiple tracks in different states simultaneously
3. **Visual Feedback** - Emoji animations + progress bars + status text
4. **State Machine** - Clear progression: Queued → Searching → Ranking → Matched/Failed
5. **Error Handling** - Shows "Failed" state for tracks with no results
6. **Auto-Hide** - Panel automatically hidden after orchestration completes
7. **Responsive** - Scrollable for large imports (100+ tracks)
8. **Status Text** - Real-time summary of progress

## Integration Points

### MainViewModel
- `OrchestratedQueries` collection (ObservableCollection<OrchestratedQueryProgress>)
- Updated `SearchAllImportedAsync()` with progress reporting
- Preserves existing logic (search, rank, auto-download, etc.)

### SearchPage.xaml
- New row in grid (Row 2) for orchestration panel
- Grid row definitions updated from 4 to 5 rows
- All subsequent row indices incremented
- ItemsControl with custom DataTemplate for progress display

### Converters
- `StateToEmojiConverter` - New converter for visual feedback
- Registered in `App.xaml` resources

### No Changes Required To
- SoulseekAdapter, ResultSorter, DownloadManager
- Spotify integration, CSV import
- Search/Download/Library functionality
- Existing converters or services

## Testing Checklist

- [ ] Manually run batch import (Spotify or CSV)
- [ ] Verify panel appears during orchestration
- [ ] Check emoji animations update smoothly
- [ ] Confirm progress bar fills as matches found
- [ ] Verify panel disappears after completion
- [ ] Test with various import sizes (5, 50, 500+ tracks)
- [ ] Monitor parallel processing (4 concurrent queries)
- [ ] Verify error handling (failed queries show ❌)
- [ ] Check scrolling with large imports
- [ ] Confirm auto-download proceeds after panel hides
