Stabilization: Fix Spotify 403 Loops & DB Race Conditions

- **Spotify**: Added `force` token refresh logic to bypass throttling on 403 Forbidden errors. Implemented auto-retry in `GetAudioFeaturesBatchAsync`.
- **Database**: Implemented "Try-Insert-Catch-Update" pattern in `SavePlaylistJobWithTracksAsync` to handle `UNIQUE constraint failed` race conditions robustly.
- **DownloadManager**: Removed error swallowing in `QueueProject`; DB persistence errors now propagate.
- **Search**: Added strict filename matching and regex-based word boundary checks in `SearchResultMatcher.cs`.
- **Docs**: Updated `task.md` to reflect new "Stabilization Phase" directive.
