using S5.Recs.Models;

namespace S5.Recs.Services;

public interface ISeedService
{
    /// <summary>
    /// Checks if an import is currently in progress.
    /// </summary>
    bool IsImportInProgress { get; }

    Task<string> StartAsync(string source, int? limit, bool overwrite, CancellationToken ct);
    Task<string> StartAsync(string source, string mediaTypeName, int? limit, bool overwrite, CancellationToken ct);
    Task<string> StartVectorUpsertAsync(IEnumerable<Media> items, CancellationToken ct);
    Task<object> GetStatusAsync(string jobId, CancellationToken ct);
    Task<(int media, int contentPieces, int vectors)> GetStatsAsync(CancellationToken ct);
    Task<int> RebuildTagCatalogAsync(CancellationToken ct);
    Task<int> RebuildGenreCatalogAsync(CancellationToken ct);
}