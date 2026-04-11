namespace NexusAuth.Extension;

public interface IFlowStateStore
{
    Task AddAsync(string state, FlowState flowState, CancellationToken ct = default);
    Task<FlowState?> GetAsync(string state, CancellationToken ct = default);
    Task RemoveAsync(string state, CancellationToken ct = default);
}