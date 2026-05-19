namespace Mamby.Maui.Tests.Controls.Sheets;

/// <summary>
/// Tests the reusable bottom sheet control contract.
/// </summary>
public sealed class BottomSheetViewTests
{
    /// <summary>
    /// Verifies the default closed-state values.
    /// </summary>
    [Xunit.Fact]
    public void ConstructorInitializesClosedSheetDefaults()
    {
        using Mamby.Maui.Controls.Sheets.BottomSheetView sheet = new();

        Xunit.Assert.False(sheet.IsVisible);
        Xunit.Assert.False(sheet.IsOpen);
        Xunit.Assert.Equal(0.75d, sheet.MaximumHeightRatio);
        Xunit.Assert.True(sheet.IsContentPullToDismissEnabled);
        Xunit.Assert.Null(sheet.SheetContent);
    }

    /// <summary>
    /// Verifies that assigned sheet content is configured for sheet hosting.
    /// </summary>
    [Xunit.Fact]
    public void SheetContentAssignmentConfiguresHostedViewLayout()
    {
        using Mamby.Maui.Controls.Sheets.BottomSheetView sheet = new();
        Microsoft.Maui.Controls.Label content = new();

        sheet.SheetContent = content;

        Xunit.Assert.Same(content, sheet.SheetContent);
        Xunit.Assert.Equal(Microsoft.Maui.Controls.LayoutOptions.Fill, content.HorizontalOptions);
        Xunit.Assert.Equal(Microsoft.Maui.Controls.LayoutOptions.Start, content.VerticalOptions);
    }

    /// <summary>
    /// Verifies that assigned sheet content can be cleared.
    /// </summary>
    [Xunit.Fact]
    public void SheetContentCanBeCleared()
    {
        using Mamby.Maui.Controls.Sheets.BottomSheetView sheet = new();
        Microsoft.Maui.Controls.Label content = new();

        sheet.SheetContent = content;
        sheet.SheetContent = null;

        Xunit.Assert.Null(sheet.SheetContent);
    }
}
