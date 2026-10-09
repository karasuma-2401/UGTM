using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UGTM.Api.Modules;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.Notifications.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Infrastructure.Persistence;
using UGTM.Database.Migrations;

namespace UGTM.Architecture.Tests;

public class PersistenceBoundaryTests
{
    public static IEnumerable<object[]> Contexts() => new[]
    {
        typeof(IdentityDbContext), typeof(AcademicPeriodsDbContext), typeof(TopicCatalogDbContext),
        typeof(ThesisManagementDbContext), typeof(DefenseCouncilDbContext), typeof(NotificationsDbContext)
    }.Select(type => new object[] { type });

    [Theory]
    [MemberData(nameof(Contexts))]
    public void Runtime_context_contains_only_its_own_domain_and_schema(Type contextType)
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var context = (ModuleDbContext)scope.ServiceProvider.GetRequiredService(contextType);
        var module = contextType.Namespace!.Split('.')[2];

        Assert.NotEmpty(context.Model.GetEntityTypes());
        Assert.All(context.Model.GetEntityTypes(), entity =>
        {
            Assert.Equal(context.Schema, entity.GetSchema());
            Assert.Equal($"UGTM.Modules.{module}.Domain.Entities", entity.ClrType.Namespace);
            Assert.All(entity.GetNavigations(), navigation =>
                Assert.Equal(entity.ClrType.Assembly, navigation.TargetEntityType.ClrType.Assembly));
        });
    }

    [Fact]
    public void Runtime_DI_does_not_expose_the_whole_database_migration_context()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        Assert.Null(scope.ServiceProvider.GetService<UGTMDbContext>());
    }

    [Fact]
    public void Thesis_repository_and_unit_of_work_share_the_scoped_module_context()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ThesisManagementDbContext>();
        Assert.Same(context, scope.ServiceProvider.GetRequiredService<IThesisUnitOfWork>());
        Assert.Same(scope.ServiceProvider.GetRequiredService<IStudentGroupRepository>(),
            scope.ServiceProvider.GetRequiredService<IStudentGroupRepository>());
    }

    [Fact]
    public async Task Migration_context_rejects_application_writes()
    {
        var options = new DbContextOptionsBuilder<UGTMDbContext>();
        DatabaseOptions.Configure(options, "Host=localhost;Database=test;Username=test", "UGTM.Database.Migrations");
        using var context = new UGTMDbContext(options.Options);
        Assert.Throws<NotSupportedException>(() => context.SaveChanges());
        await Assert.ThrowsAsync<NotSupportedException>(() => context.SaveChangesAsync());
    }

    private static ServiceProvider CreateProvider()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DATABASE_CONNECTION_STRING"] = "Host=localhost;Database=test;Username=test"
        }).Build();
        return new ServiceCollection().AddUgtmModules(configuration)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
}
