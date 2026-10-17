using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UGTM.Api.Modules;
using UGTM.BuildingBlocks.Application;
using UGTM.Modules.AcademicPeriods.Application.Auditing;
using UGTM.Modules.DefenseCouncil.Application.Auditing;
using UGTM.Modules.DefenseCouncil.Contracts.Grading;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.ThesisManagement.Application.Auditing;

namespace UGTM.Architecture.Tests;

public class ModuleCompositionTests
{
    [Fact]
    public void Composition_root_registers_six_distinct_modules_and_resolves_grading_contract()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DATABASE_CONNECTION_STRING"] = "Host=localhost;Database=composition_test;Username=test"
        }).Build();
        var services = new ServiceCollection().AddUgtmModules(configuration);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();

        Assert.Equal(
            ["AcademicPeriods", "DefenseCouncil", "Identity", "Notifications", "ThesisManagement", "TopicCatalog"],
            provider.GetServices<IModule>().Select(x => x.Name).Order());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IGradeCalculationService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAcademicPeriodsAuditOutbox>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDefenseCouncilAuditOutbox>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IThesisManagementAuditOutbox>());
        Assert.Equal(
            ["AcademicPeriods", "DefenseCouncil", "ThesisManagement"],
            scope.ServiceProvider
                .GetServices<IAuditIntegrationEventSource>()
                .Select(x => x.SourceName)
                .Order());
    }
}
