namespace Mamby.DotNet;

/// <summary>
/// Provides package-level metadata for Mamby.DotNet.
/// </summary>
public static class DotNetKit
{
    /// <summary>
    /// Gets the assembly name for the Mamby.DotNet package.
    /// </summary>
    /// <returns>The package assembly name.</returns>
    public static string GetAssemblyName()
    {
        return typeof(DotNetKit).Assembly.GetName().Name
            ?? throw new InvalidOperationException("Assembly name is unavailable.");
    }
}
