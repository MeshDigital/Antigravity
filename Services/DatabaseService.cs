using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SLSKDONET.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SLSKDONET.Services;

public class DatabaseService
{
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(ILogger<DatabaseService> logger)
    {
        _logger = logger;
    }

    public async Task InitAsync()
    {
        using var context = new AppDbContext();
        await context.Database.EnsureCreatedAsync();
        _logger.LogInformation("Database initialized.");
    }

    public async Task<List<TrackEntity>> LoadTracksAsync()
    {
        using var context = new AppDbContext();
        return await context.Tracks.ToListAsync();
    }

    public async Task SaveTrackAsync(TrackEntity track)
    {
        using var context = new AppDbContext();
        var existing = await context.Tracks.FindAsync(track.GlobalId);
        
        if (existing == null)
        {
            await context.Tracks.AddAsync(track);
        }
        else
        {
            // Update fields
            existing.State = track.State;
            existing.ErrorMessage = track.ErrorMessage;
            // Should we update others? Usually just state changes.
        }
        
        await context.SaveChangesAsync();
    }

    public async Task RemoveTrackAsync(string globalId)
    {
        using var context = new AppDbContext();
        var track = await context.Tracks.FindAsync(globalId);
        if (track != null)
        {
            context.Tracks.Remove(track);
            await context.SaveChangesAsync();
        }
    }

    // Helper to bulk save if needed
    public async Task SaveAllAsync(IEnumerable<TrackEntity> tracks)
    {
        using var context = new AppDbContext();
        foreach(var t in tracks)
        {
            if (!await context.Tracks.AnyAsync(x => x.GlobalId == t.GlobalId))
            {
                await context.Tracks.AddAsync(t);
            }
        }
        await context.SaveChangesAsync();
    }
}
