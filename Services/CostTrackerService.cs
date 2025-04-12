using System.Text.Json;

namespace DiscordPA.Services;

public class CostTrackerService
{
    private const string CostFile = "total_cost.json";
    private double _total = 0;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public CostTrackerService()
    {
        Load();
    }

    public void Add(double amount)
    {
        _lock.Wait();
        try
        {
            _total += amount;
            Save();
        }
        finally
        {
            _lock.Release();
        }
    }

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

    private void Load()
    {
        try
        {
            if (File.Exists(CostFile))
            {
                var json = File.ReadAllText(CostFile);
                _total = JsonSerializer.Deserialize<double>(json);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CostTracker] Failed to load saved cost: {ex.Message}");
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_total);
            File.WriteAllText(CostFile, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CostTracker] Failed to save total cost: {ex.Message}");
        }
    }
}
