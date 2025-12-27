using System;
using System.Threading.Tasks;
using SLSKDONET.Data.Entities;

namespace SLSKDONET.Services.Repositories;

public interface IEnrichmentTaskRepository
{
    Task QueueTaskAsync(Guid trackId, Guid? albumId = null);
    Task<EnrichmentTaskEntity?> GetNextPendingTaskAsync();
    Task MarkProcessingAsync(Guid taskId);
    Task MarkCompletedAsync(Guid taskId);
    Task MarkFailedAsync(Guid taskId, string error);
}
