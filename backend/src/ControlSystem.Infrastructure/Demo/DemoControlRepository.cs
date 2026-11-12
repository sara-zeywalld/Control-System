using ControlSystem.Core.Controls;
using ControlSystem.Core.Definitions;

namespace ControlSystem.Infrastructure.Demo;

/// <summary>מאגר בקרות בזיכרון (מצב Demo). הנתונים אובדים בעת כיבוי השרת.</summary>
public sealed class DemoControlRepository : IControlRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<int, ControlRecord> _items = new();
    private int _nextId = 1;

    public DemoControlRepository()
    {
        foreach (var (name, description, json) in DemoData.SampleControls())
            Add(new ControlInput(name, description, ControlJson.Parse(json), "manual"));
    }

    private int Add(ControlInput input)
    {
        var now = DateTime.UtcNow;
        var id = _nextId++;
        _items[id] = new ControlRecord(id, input.Name, input.Description, input.Definition, input.Source ?? "manual", now, now, null);
        return id;
    }

    public Task<IReadOnlyList<ControlRecord>> ListAsync(CancellationToken ct)
    {
        lock (_gate) return Task.FromResult<IReadOnlyList<ControlRecord>>(_items.Values.OrderBy(c => c.Id).ToList());
    }

    public Task<ControlRecord?> GetAsync(int id, CancellationToken ct)
    {
        lock (_gate) return Task.FromResult(_items.GetValueOrDefault(id));
    }

    public Task<int> CreateAsync(ControlInput input, CancellationToken ct)
    {
        lock (_gate) return Task.FromResult(Add(input));
    }

    public Task<bool> UpdateAsync(int id, ControlInput input, CancellationToken ct)
    {
        lock (_gate)
        {
            if (!_items.TryGetValue(id, out var old)) return Task.FromResult(false);
            _items[id] = old with
            {
                Name = input.Name, Description = input.Description, Definition = input.Definition,
                Source = input.Source ?? old.Source, UpdatedAt = DateTime.UtcNow
            };
            return Task.FromResult(true);
        }
    }

    public Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        lock (_gate) return Task.FromResult(_items.Remove(id));
    }

    public Task RecordRunAsync(int controlId, int? count, long durationMs, bool succeeded, string? error, CancellationToken ct)
    {
        lock (_gate)
        {
            if (_items.TryGetValue(controlId, out var c))
                _items[controlId] = c with { LastRun = new LastRunInfo(DateTime.UtcNow, count, succeeded, error) };
        }
        return Task.CompletedTask;
    }
}
