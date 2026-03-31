using Luck.EntityFrameworkCore;
using Luck.EntityFrameworkCore.DbContextDrivenProvides;
using Luck.EntityFrameworkCore.PostgreSQL;
using Microsoft.Extensions.DependencyInjection;

namespace NexusAuth.Persistence;

public class EntityFrameworkCoreModule : EntityFrameworkCoreBaseModule
{
    protected override void AddDbContextWithUnitOfWork(IServiceCollection services)
    {
        var configuration = services.GetConfiguration();
        var connectionString = configuration["ConnectionStrings:Default"]
            ?? "User ID=postgres;Password=wzw0126..;Host=localhost;Port=5432;Database=nexusauth;Search Path=nexusauth";

        services.AddLuckDbContext<DbContext>(x =>
        {
            x.ConnectionString = connectionString;
            x.Type = DataBaseType.PostgreSql;
        });
    }

    protected override void AddDbDriven(IServiceCollection service)
    {
        service.AddPostgreSQLDriven();
    }
}
