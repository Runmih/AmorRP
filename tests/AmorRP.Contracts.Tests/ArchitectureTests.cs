using AmorRP.Contracts.Common;
using AmorRP.Core.Groups;
using Xunit;

namespace AmorRP.Contracts.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void Shared_assemblies_do_not_depend_on_transport_game_or_persistence_frameworks()
    {
        var forbidden = new[] { "Dalamud", "Microsoft.EntityFrameworkCore", "Npgsql", "Microsoft.AspNetCore", "AmorRP.Server", "AmorRP.Plugin" };
        foreach (var assembly in new[] { typeof(HealthResponse).Assembly, typeof(GroupCapacityPolicy).Assembly })
        foreach (var reference in assembly.GetReferencedAssemblies())
            Assert.DoesNotContain(forbidden, name => reference.Name!.StartsWith(name, StringComparison.Ordinal));
        Assert.DoesNotContain(typeof(GroupCapacityPolicy).Assembly.GetReferencedAssemblies(),
            x => x.Name == "AmorRP.Contracts");
    }
}
