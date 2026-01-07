using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DiscordPA.Services;

public class CostTrackerService
{
    private const string CostFile = "total_cost.json";
    private double _total = 0;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<CostTrackerService> _logger;

    public CostTrackerService(ILogger<CostTrackerService> logger)
    {
        _logger = logger;
        LoadAsync().GetAwaiter().GetResult(); // Sync load on startup only
    }

    public async Task AddAsync(double amount)
    {
        await _lock.WaitAsync();
        try
        {
            _total += amount;
            await SaveAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<double> GetTotalAsync()
    {
        await _lock.WaitAsync();
        try
        {
            return _total;
        }
        finally
        {
            _lock.Release();
        }
    }

    // Synchronous version for non-async contexts (e.g., embed footer)
    public double GetTotal()
    {
        _lock.Wait();
        try
        {
            return _total;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task LoadAsync()
    {
        try
        {
            if (File.Exists(CostFile))
            {
                var json = await File.ReadAllTextAsync(CostFile);
                _total = JsonSerializer.Deserialize<double>(json);
                _logger.LogInformation("Loaded saved cost: ${Total:F4}", _total);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load saved cost from {CostFile}", CostFile);
        }
    }

    private async Task SaveAsync()
    {
        var tempFile = CostFile + ".tmp";
        try
        {
            // Write to temp file first
            var json = JsonSerializer.Serialize(_total);
            await File.WriteAllTextAsync(tempFile, json);

            // Atomic rename - replaces target file if it exists
            File.Move(tempFile, CostFile, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save total cost to {CostFile}", CostFile);

            // Clean up temp file if it exists
            try { File.Delete(tempFile); } catch { /* ignore cleanup failures */ }
        }
    }
}
