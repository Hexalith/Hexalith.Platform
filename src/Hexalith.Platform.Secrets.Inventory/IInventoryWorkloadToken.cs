using System.Net.Http.Headers;
using System.Text;

using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Secrets.Inventory;

/// <summary>A separately enrolled workload credential; registry administration establishes no secrets trust.</summary>
public interface IInventoryWorkloadToken
{
    /// <summary>Whether independently supplied credential references are complete.</summary>
    bool Configured { get; }
    /// <summary>Obtains a fresh bearer for one current authority read.</summary>
    Task<string?> GetAsync(CancellationToken cancellationToken);
}
