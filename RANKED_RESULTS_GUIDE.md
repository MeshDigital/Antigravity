# Using Ranked Search Results - Quick Start

## What You'll See

When you search for a track, results now appear in **ranked order** with two new columns:

```
┌────┬────────┬──────────────┬──────────────┬────────┬─────────┐
│ #  │ Rank   │ Artist       │ Title        │ Bitrate│ User    │
├────┼────────┼──────────────┼──────────────┼────────┼─────────┤
│ 0  │ 2156.4 │ Daft Punk    │ Get Lucky    │ 192k   │ user1   │
│ 1  │ 1834.2 │ Daft Punk    │ Get Luckty   │ 128k   │ user2   │
│ 3  │ 1205.0 │ -            │ Get_Lucky_Re │ 320k   │ user3   │
│ 2  │ 512.3  │ -            │ lucky        │ 64k    │ user4   │
└────┴────────┴──────────────┴──────────────┴────────┴─────────┘
```

## Column Meanings

### **#** Column (Original Index)
- Shows the **original position** from the search results before ranking
- Useful for: "I want the one from position #7 in the original results"
- Click the header to sort by original order
- Ranges from 0 (first result) upward

### **Rank** Column (Ranking Score)
- Shows the **quality score** from 0-9999
- Higher number = Better match for your search
- Reflects how well it matches:
  - Your query (title/artist/album)
  - Your preferences (format, bitrate)
  - File quality (bitrate, length accuracy)
- Click the header to sort by rank

## How Ranking Works

Your search is ranked on 6 criteria:

| Criteria | Weight | What It Means |
|----------|--------|---------------|
| **1. Required** | +1000 pts | Must match format, not banned user, etc. |
| **2. Preferred** | +0-500 pts | Matches bitrate range, length tolerance |
| **3. Bitrate** | +0-50 pts | Higher bitrate preferred (320k > 128k) |
| **4. Length** | +0-100 pts | Track duration matches expected length |
| **5. Similarity** | +0-200 pts | Title/artist/album text similarity |
| **6. Tiebreaker** | +0-1 pt | Random (for truly identical results) |

## Common Tasks

### ✅ Get the Best Match
Just use the **first result**! It's already ranked as the best match.

```
Top result = Best match for your search
```

### 🔄 Go Back to Original Order
Click the **"#"** column header. Results sort by original search position.

```
# Header Click → Original Order
```

### 📊 See All Results by Rank
Click the **"Rank"** column header. Results sort from highest to lowest score.

```
Rank Header Click → Sorted by Quality Score
```

### 📌 Remember a Specific Result
The **"#"** column tells you the original position:

```
"I want the #7 from the original search"
Look for the row with # = 6 (0-based indexing)
```

### 🎯 Analyze Why a Result Ranked High/Low

**High Rank (2000+)**
- Good title match
- Artist/album included
- Acceptable bitrate
- Correct duration

**Medium Rank (800-2000)**
- Partial title match
- Standalone title (no artist)
- Lower bitrate but acceptable
- Close duration

**Low Rank (0-800)**
- Poor text match
- Typos in filename
- Low bitrate (64-128k)
- Wrong duration
- All of the above

## Examples

### Example 1: Searching "Get Lucky"

**You Search For:** "Get Lucky"  
**Your Filters:** 128-320 kbps, MP3 format

**Results Ranked As:**

```
Rank 1: 2156.4 | "Daft Punk - Get Lucky.mp3" 192k
   Why: Perfect match, good bitrate, correct duration
   
Rank 2: 1834.2 | "get lucky.mp3" 192k
   Why: Good match, correct bitrate, but no artist
   
Rank 3: 1205.0 | "get lucky remix.mp3" 320k
   Why: Partial match (remix), good bitrate, but longer than expected
   
Rank 4: 512.3  | "lucky.mp3" 64k
   Why: Poor match (missing "Get"), low bitrate
```

### Example 2: Searching "Bohemian Rhapsody"

**You Search For:** "Bohemian Rhapsody"  
**Your Filters:** 256-320 kbps, FLAC format

**Results Ranked As:**

```
Rank 1: 2876.5 | "Queen - Bohemian Rhapsody.flac" 320k
   Why: Perfect text match, preferred format, high bitrate
   
Rank 2: 2543.2 | "Bohemian Rhapsody - Queen.flac" 320k
   Why: Perfect match but different order, still excellent
   
Rank 3: 1742.0 | "Bohemian Rhapsody.mp3" 256k
   Why: Good text match, wrong format but acceptable quality
   
Rank 4: 898.3  | "bohemian_rhapsody_edit.mp3" 192k
   Why: Good match but edited version, lower bitrate, wrong format
```

## Filtering + Ranking

**Format Filter:** MP3 only
→ All results that passed (bitrate + format requirements)

**Bitrate Filter:** 192-320 kbps
→ Results within range score higher

**Combined Effect:**
Results must pass format filters, then ranked by how well they match your preferences.

## Sorting Tips

| Click Header | Result |
|--|--|
| **Artist** | Alphabetical A→Z or Z→A |
| **Title** | Alphabetical A→Z or Z→A |
| **Rank** ⬆️ | Best match first (highest score) |
| **#** ⬆️ | Original search order (position 0 first) |
| **Bitrate** | Higher bitrate first |
| **User** | Alphabetical |

## FAQ

**Q: Why is result #7 not at position 7?**
A: Results are ranked. The "#" column shows it was originally position 7, but now displays higher in rank.

**Q: Can I see the original order?**
A: Yes! Click the "#" column header to sort by original index.

**Q: Why did my filter change the ranking?**
A: Your format/bitrate filters are part of the ranking algorithm. Results that match your preferences score higher.

**Q: What if two results have the same rank?**
A: They tie. Order between them is random (tiebreaker is randomized for diversity).

**Q: Can I download results in ranked order?**
A: Yes! Select items in rank order and "Add to Downloads" uses them in the order displayed.

**Q: Does ranking affect download queue?**
A: No. Ranking affects display order only. Downloads happen based on selection order.

## Pro Tips

💡 **Tip 1:** The first result is usually your best bet - it's already ranked best.

💡 **Tip 2:** If results seem off, adjust your filters (format, bitrate range).

💡 **Tip 3:** Click multiple columns to multi-sort (e.g., sort by Rank, then by Bitrate within each rank).

💡 **Tip 4:** Use the "#" column when recommending results to others ("Use #14").

💡 **Tip 5:** High bitrate doesn't always mean best - ranking also considers title accuracy.

## What's Ranking?

**Ranking** = Ordering results by how good a match they are.

Instead of random results from the P2P network, you get:
- Titles matching your search first
- Best bitrate/format combinations
- Proper album/artist information
- Correct duration estimates

All automatically, using multiple criteria, so you get the best match without clicking through pages of results.

## See Also

- `RANKING_QUICK_REFERENCE.md` - Technical reference
- `SEARCH_RANKING_OPTIMIZATION.md` - Algorithm details
- `RANKING_IMPLEMENTATION.md` - How it works internally
