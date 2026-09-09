using System.Collections.Concurrent;

namespace NexusAuth.Extension;

/// <summary>
/// 中文：使用进程内并发字典存储 OIDC 授权流程状态。
/// English: Stores OIDC authorization flow state in an in-process concurrent dictionary.
/// </summary>
/// <remarks>
/// 中文：该实现不跨进程共享数据，应用重启后状态会丢失。
/// English: This implementation does not share data across processes, and all state is lost when the application restarts.
/// </remarks>
public class InMemoryFlowStateStore : IFlowStateStore
{
    private readonly ConcurrentDictionary<string, FlowState> _store = new();

    /// <inheritdoc />
    public Task AddAsync(string state, FlowState flowState, CancellationToken ct = default)
    {
        _store[state] = flowState;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<FlowState?> GetAsync(string state, CancellationToken ct = default)
    {
        _store.TryGetValue(state, out var flowState);
        return Task.FromResult(flowState);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string state, CancellationToken ct = default)
    {
        _store.TryRemove(state, out _);
        return Task.CompletedTask;
    }
}
