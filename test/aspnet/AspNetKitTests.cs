using Xunit;

namespace Mamby.AspNet.Tests;

/// <summary>
/// Tests for <see cref="AspNetKit"/>.
/// </summary>
public sealed class AspNetKitTests
{
    /// <summary>
    /// Verifies that the placeholder metadata method returns the package assembly name.
    /// </summary>
    [Fact]
    public void GetAssemblyNameReturnsAspNetAssemblyName()
    {
        Assert.Equal("Mamby.AspNet", AspNetKit.GetAssemblyName());
    }
}
