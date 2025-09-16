using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Koan.Flow.Materialization;

public sealed record MaterializedDecision(string? Value, string Policy, IReadOnlyDictionary<string, string>? Meta = null);

public interface IRecordMaterializationTransformer
{
    Task<(IReadOnlyDictionary<string, string?> values, IReadOnlyDictionary<string, string> policies)> MaterializeAsync(
        string modelName,
        IReadOnlyDictionary<string, IReadOnlyCollection<string?>> canonical,
        CancellationToken ct);
}

public interface IPropertyMaterializationTransformer
{
    Task<MaterializedDecision> MaterializeAsync(
        string modelName,
        string path,
        IReadOnlyCollection<string?> values,
        IReadOnlyDictionary<string, IReadOnlyCollection<string?>> canonical,
        CancellationToken ct);
}
