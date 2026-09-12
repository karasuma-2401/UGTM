using System.Reflection;

namespace UGTM.Architecture.Tests;

public class ModuleBoundaryTests
{
    private static readonly string[] Modules =
    [
        "AcademicPeriods",
        "DefenseCouncil",
        "FinalReportEvaluation",
        "GradingConsolidation",
        "Identity",
        "MidtermProgress",
        "Notifications",
        "ThesisManagement",
        "TopicCatalog",
    ];

    public static IEnumerable<object[]> ModuleNames() => Modules.Select(m => new object[] { m });

    public static IEnumerable<object[]> ModulePairs() =>
        from module in Modules
        from otherModule in Modules
        where module != otherModule
        select new object[] { module, otherModule };

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Domain_should_not_reference_its_own_application_or_infrastructure(string module)
    {
        var referenced = ReferencedAssemblyNames($"UGTM.Modules.{module}.Domain");

        Assert.DoesNotContain($"UGTM.Modules.{module}.Application", referenced);
        Assert.DoesNotContain($"UGTM.Modules.{module}.Infrastructure", referenced);
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Application_should_not_reference_its_own_infrastructure(string module)
    {
        var referenced = ReferencedAssemblyNames($"UGTM.Modules.{module}.Application");

        Assert.DoesNotContain($"UGTM.Modules.{module}.Infrastructure", referenced);
    }

    [Theory]
    [MemberData(nameof(ModulePairs))]
    public void Module_layers_should_only_reach_other_modules_through_contracts(string module, string otherModule)
    {
        string[] forbidden =
        [
            $"UGTM.Modules.{otherModule}.Domain",
            $"UGTM.Modules.{otherModule}.Application",
            $"UGTM.Modules.{otherModule}.Infrastructure",
        ];

        foreach (var layer in new[] { "Domain", "Application", "Infrastructure" })
        {
            var referenced = ReferencedAssemblyNames($"UGTM.Modules.{module}.{layer}");

            foreach (var forbiddenAssembly in forbidden)
            {
                Assert.DoesNotContain(forbiddenAssembly, referenced);
            }
        }
    }

    [Fact]
    public void BuildingBlocks_domain_should_not_reference_application_or_infrastructure()
    {
        var referenced = ReferencedAssemblyNames("UGTM.BuildingBlocks.Domain");

        Assert.DoesNotContain("UGTM.BuildingBlocks.Application", referenced);
        Assert.DoesNotContain("UGTM.BuildingBlocks.Infrastructure", referenced);
    }

    private static string?[] ReferencedAssemblyNames(string assemblyName) =>
        Assembly.Load(assemblyName).GetReferencedAssemblies().Select(a => a.Name).ToArray();
}
