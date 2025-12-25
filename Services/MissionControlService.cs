using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Models;

namespace SLSKDONET.Services
{
    public class MissionControlService : IDisposable
    {
        private readonly IEventBus _eventBus;
        private readonly DownloadManager _downloadManager;
        private readonly CrashRecoveryJournal _crashJournal;
        private readonly SearchOrchestrationService _searchOrchestrator;
        private readonly LibraryEnrichmentWorker _enrichmentWorker;
        private readonly ILogger<MissionControlService> _logger;
        
        private readonly CancellationTokenSource _cts = new();
        private int _lastHash = 0;
        private Task? _monitorTask;

        // Caching for expensive stats
        private SystemHealthStats _cachedHealth;
        private int _cachedZombieCount;
        private int _tickCounter = 0;

        public MissionControlService(
            IEventBus eventBus,
            DownloadManager downloadManager,
            CrashRecoveryJournal crashJournal,
            SearchOrchestrationService searchOrchestrator,
            LibraryEnrichmentWorker enrichmentWorker,
            ILogger<MissionControlService> logger)
        {
            _eventBus = eventBus;
            _downloadManager = downloadManager;
            _crashJournal = crashJournal;
            _searchOrchestrator = searchOrchestrator;
            _enrichmentWorker = enrichmentWorker;
            _logger = logger;
        }

        public void Start()
        {
            _monitorTask = Task.Run(ProcessThrottledUpdatesAsync);
            _logger.LogInformation("Mission Control Service started");
        }

        private async Task ProcessThrottledUpdatesAsync()
        {
            // 4 FPS = 250ms
            var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
            
            // Initial load of expensive stats
            await UpdateExpensiveStatsAsync();

            while (await timer.WaitForNextTickAsync(_cts.Token))
            {
                try
                {
                    _tickCounter++;
                    
                    // Update expensive stats every 4 ticks (1 second)
                    if (_tickCounter % 4 == 0)
                    {
                        await UpdateExpensiveStatsAsync();
                    }

                    var snapshot = await GetCurrentStateAsync();
                    var currentHash = snapshot.GetHashCode();

                    if (currentHash != _lastHash)
                    {
                        _lastHash = currentHash;
                        _eventBus.Publish(snapshot);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Mission Control monitoring loop");
                }
            }
        }

        private async Task UpdateExpensiveStatsAsync()
        {
            try
            {
                _cachedHealth = await _crashJournal.GetSystemHealthAsync();
                _cachedZombieCount = GetZombieProcessCount();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update expensive stats");
            }
        }

        public async Task<DashboardSnapshot> GetCurrentStateAsync()
        {
            // Fast Path: Use cached expensive stats + real-time cheap stats
            var healthStats = _cachedHealth;
            var zombieCount = _cachedZombieCount;
            
            var activeDownloads = _downloadManager.ActiveDownloads.ToList(); 
            var downloadCount = activeDownloads.Count;
            
            // Calculate overall health
            var health = SystemHealth.Excellent;
            if (healthStats.DeadLetterCount > 0 || zombieCount > 2)
            {
                health = SystemHealth.Warning;
            }
            if (activeDownloads.Any(d => d.State == PlaylistTrackState.Failed))
            {
                health = SystemHealth.Warning;
            }

            // Build Active Operations List (Cheap memory scan)
            var operations = new List<string>();
            foreach (var dl in activeDownloads.Take(10)) 
            {
                operations.Add($"⬇️ Downloading: {dl.Model.Artist} - {dl.Model.Title} ({dl.Progress:P0})");
            }
            if (_searchOrchestrator.GetActiveSearchCount() > 0)
            {
                operations.Add($"🔍 Searching: {_searchOrchestrator.GetActiveSearchCount()} active queries");
            }

            // Resilience Log
            var resilienceLog = new List<string>();
            if (healthStats.RecoveredCount > 0)
            {
                resilienceLog.Add($"✅ Recovered {healthStats.RecoveredCount} files from previous session");
            }
            if (zombieCount > 0)
            {
                resilienceLog.Add($"🧟 Detected {zombieCount} potential zombie processes");
            }

            return new DashboardSnapshot
            {
                CapturedAt = DateTime.UtcNow,
                SystemHealth = health,
                ActiveDownloads = downloadCount,
                DeadLetterCount = healthStats.DeadLetterCount,
                RecoveredFileCount = healthStats.RecoveredCount,
                ZombieProcessCount = zombieCount,
                ActiveOperations = operations,
                ResilienceLog = resilienceLog
            };
        }

        private int GetZombieProcessCount()
        {
            try
            {
                var ffmpegs = Process.GetProcessesByName("ffmpeg");
                // Note: GetProcessesByName is relatively expensive (2-5ms), so caching it is good.
                var activeConversions = _downloadManager.ActiveDownloads.Count(d => d.State == PlaylistTrackState.Downloading);
                
                if (ffmpegs.Length > activeConversions)
                {
                    return ffmpegs.Length - activeConversions;
                }
                return 0;
            }
            catch 
            {
                return 0;
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
