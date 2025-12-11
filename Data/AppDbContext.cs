using Microsoft.EntityFrameworkCore;
using System.IO;

namespace SLSKDONET.Data;

public class AppDbContext : DbContext
{
    public DbSet<TrackEntity> Tracks { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
        var dbPath = Path.Combine(appData, "SLSKDONET", "library.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
    }
}
