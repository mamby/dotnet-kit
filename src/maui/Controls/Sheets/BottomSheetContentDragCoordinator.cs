namespace Mamby.Maui.Controls.Sheets;

internal interface IBottomSheetContentDragCoordinator : IDisposable
{
    void Reset();
}

internal static partial class BottomSheetContentDragCoordinator
{
    public static partial IBottomSheetContentDragCoordinator? Create(
        BottomSheetView sheet,
        Microsoft.Maui.Controls.ScrollView scrollView);
}
