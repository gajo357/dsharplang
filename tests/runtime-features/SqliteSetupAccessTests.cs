using System.Reflection;
using System.Runtime.CompilerServices;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class SqliteSetupAccessTests
{
    [Fact]
    public void OptionalProviderIsTheOnlyNewNamedFriend()
    {
        var names = typeof(RuntimeAssembly).Assembly.GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName).ToArray();
        Assert.Single(names, name => name == "DSharp.Runtime.Sqlite");
        Assert.DoesNotContain("DSS", names);
    }
}
