using System.Reflection;
using System.Xml.Linq;

namespace UGTM.Architecture.Tests;

public class ModuleBoundaryTests
{
    private static readonly string[] Modules =
    [
        "AcademicPeriods",
        "DefenseCouncil",
        "Identity",
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
    public void Contracts_have_no_project_dependencies_and_domain_does_not_reference_contracts_or_persistence(string module)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "UGTM.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var moduleDirectory = Path.Combine(directory.FullName, "backend", "Modules", module);
        var contracts = XDocument.Load(Path.Combine(moduleDirectory, "Contracts", $"UGTM.Modules.{module}.Contracts.csproj"));
        var domain = XDocument.Load(Path.Combine(moduleDirectory, "Domain", $"UGTM.Modules.{module}.Domain.csproj"));
        foreach (var layer in new[] { "Domain", "Application", "Infrastructure", "Contracts" })
        {
            var project = XDocument.Load(Path.Combine(moduleDirectory, layer, $"UGTM.Modules.{module}.{layer}.csproj"));
            Assert.DoesNotContain(project.Descendants("ProjectReference"), reference =>
                Path.GetFileName(reference.Attribute("Include")!.Value.Replace('\\', '/')) == "UGTM.Database.Migrations.csproj");
        }

        Assert.Empty(contracts.Descendants("ProjectReference"));
        Assert.Empty(contracts.Descendants("PackageReference"));
        Assert.DoesNotContain(domain.Descendants("ProjectReference"), reference =>
            reference.Attribute("Include")!.Value.Contains("Contracts", StringComparison.Ordinal)
            || reference.Attribute("Include")!.Value.Contains("Persistence", StringComparison.Ordinal)
            || reference.Attribute("Include")!.Value.Contains("Database.Migrations", StringComparison.Ordinal));
        Assert.DoesNotContain(domain.Descendants("PackageReference"), reference =>
            reference.Attribute("Include")!.Value.Contains("EntityFrameworkCore", StringComparison.Ordinal));
    }

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
