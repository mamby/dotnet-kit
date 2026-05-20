namespace Mamby.AspNet;

/// <summary>
/// Provides package-level metadata for Mamby.AspNet.
/// </summary>
public static class AspNetKit
{
    /// <summary>
    /// Gets the assembly name for the Mamby.AspNet package.
    /// </summary>
    /// <returns>The package assembly name.</returns>
    public static string GetAssemblyName()
    {
        return typeof(AspNetKit).Assembly.GetName().Name
            ?? throw new InvalidOperationException("Assembly name is unavailable.");
    }
}
