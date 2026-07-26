using System.Management;
using RobloxOptimizer.Core.Interfaces;

namespace RobloxOptimizer.Monitoring;

/// <summary>Enumerates display adapters via WMI so the app can detect multi-GPU (e.g. laptop hybrid graphics) systems.</summary>
public sealed class GpuInventory : IGpuInventory
{
    public Task<IReadOnlyList<string>> GetDetectedGpuNamesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            using var results = searcher.Get();

            var names = new List<string>();
            foreach (ManagementBaseObject obj in results)
            {
                using (obj)
                {
                    if (obj["Name"] is string name && !string.IsNullOrWhiteSpace(name))
                    {
                        names.Add(name);
                    }
                }
            }

            return Task.FromResult<IReadOnlyList<string>>(names);
        }
        catch (Exception)
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }
    }
}
