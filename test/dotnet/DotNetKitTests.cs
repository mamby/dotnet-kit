using Xunit;

namespace Mamby.DotNet.Tests;

/// <summary>
/// Tests for <see cref="DotNetKit"/>.
/// </summary>
public sealed class DotNetKitTests
{
    /// <summary>
    /// Verifies that the placeholder metadata method returns the package assembly name.
    /// </summary>
    [Fact]
    public void GetAssemblyNameReturnsDotNetAssemblyName()
    {
        Assert.Equal("Mamby.DotNet", DotNetKit.GetAssemblyName());
    }
}
