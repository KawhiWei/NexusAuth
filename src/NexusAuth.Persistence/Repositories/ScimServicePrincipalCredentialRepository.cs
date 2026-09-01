namespace NexusAuth.Persistence.Repositories;

public class ScimServicePrincipalCredentialRepository(IUnitOfWork unitOfWork)
    : EfCoreEntityRepository<ScimServicePrincipalCredential, Guid>(unitOfWork),
        IScimServicePrincipalCredentialRepository
{
    private readonly LuckDbContextBase _dbContext = unitOfWork.GetLuckDbContext() as LuckDbContextBase
                                                    ?? throw new InvalidOperationException("Failed to resolve LuckDbContext.");
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<ScimServicePrincipalCredential?> FindByTokenHashAsync(
        string tokenHash,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            return null;

        return await FindAll(credential => credential.TokenHash == tokenHash.Trim())
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ScimServicePrincipalCredential?> FindByNameAsync(
        string name,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return await FindAll(credential => credential.Name == name.Trim())
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ScimServicePrincipalCredential?> FindByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await FindAsync(id);
    }

    public async Task<IReadOnlyList<ScimServicePrincipalCredential>> GetAllAsync(CancellationToken ct = default)
    {
        return await FindAll().OrderByDescending(credential => credential.CreatedAt).ToListAsync(ct);
    }

    public async Task AddAsync(
        ScimServicePrincipalCredential credential,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(credential);

        _dbContext.Add(credential);
        await _unitOfWork.CommitAsync(ct);
    }

    public async Task UpdateAsync(
        ScimServicePrincipalCredential credential,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(credential);

        _dbContext.Set<ScimServicePrincipalCredential>().Update(credential);
        await _unitOfWork.CommitAsync(ct);
    }
}
