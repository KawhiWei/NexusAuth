namespace NexusAuth.Extension;

/// <summary>
/// 中文：定义 OIDC 授权流程临时状态的存储操作。
/// English: Defines storage operations for temporary OIDC authorization flow state.
/// </summary>
public interface IFlowStateStore
{
    /// <summary>
    /// 中文：保存指定 state 参数对应的授权流程状态。
    /// English: Stores the authorization flow state associated with the specified state parameter.
    /// </summary>
    /// <param name="state">中文：用于关联授权请求和回调的随机 state 值。English: The random state value that correlates the authorization request and callback.</param>
    /// <param name="flowState">中文：要保存的流程安全状态。English: The flow security state to store.</param>
    /// <param name="ct">中文：用于取消异步操作的令牌。English: A token used to cancel the asynchronous operation.</param>
    /// <returns>中文：表示保存操作的任务。English: A task representing the store operation.</returns>
    Task AddAsync(string state, FlowState flowState, CancellationToken ct = default);

    /// <summary>
    /// 中文：获取指定 state 参数对应的授权流程状态。
    /// English: Gets the authorization flow state associated with the specified state parameter.
    /// </summary>
    /// <param name="state">中文：授权请求中的 state 值。English: The state value from the authorization request.</param>
    /// <param name="ct">中文：用于取消异步操作的令牌。English: A token used to cancel the asynchronous operation.</param>
    /// <returns>中文：找到的流程状态；不存在时为 <see langword="null"/>。English: The stored flow state, or <see langword="null"/> when no match exists.</returns>
    Task<FlowState?> GetAsync(string state, CancellationToken ct = default);

    /// <summary>
    /// 中文：删除指定 state 参数对应的授权流程状态。
    /// English: Removes the authorization flow state associated with the specified state parameter.
    /// </summary>
    /// <param name="state">中文：要删除的 state 值。English: The state value to remove.</param>
    /// <param name="ct">中文：用于取消异步操作的令牌。English: A token used to cancel the asynchronous operation.</param>
    /// <returns>中文：表示删除操作的任务。English: A task representing the remove operation.</returns>
    Task RemoveAsync(string state, CancellationToken ct = default);
}
