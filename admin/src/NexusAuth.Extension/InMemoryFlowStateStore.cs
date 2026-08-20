using System.Collections.Concurrent;

namespace NexusAuth.Extension;

public class InMemoryFlowStateStore : IFlowStateStore
{
    private readonly ConcurrentDictionary<string, FlowState> _store = new();

    public Task AddAsync(string state, FlowState flowState, CancellationToken ct = default)
    {
        _store[state] = flowState;
        return Task.CompletedTask;
    }

    public Task<FlowState?> GetAsync(string state, CancellationToken ct = default)
    {
        _store.TryGetValue(state, out var flowState);
        return Task.FromResult(flowState);
    }

    public Task RemoveAsync(string state, CancellationToken ct = default)
    {
        _store.TryRemove(state, out _);
        return Task.CompletedTask;
    }
}