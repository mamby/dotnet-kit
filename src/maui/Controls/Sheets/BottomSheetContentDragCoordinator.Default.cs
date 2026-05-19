#if !ANDROID && !IOS && !MACCATALYST && !WINDOWS
namespace Mamby.Maui.Controls.Sheets;

internal static partial class BottomSheetContentDragCoordinator
{
    public static partial IBottomSheetContentDragCoordinator? Create(
        BottomSheetView sheet,
        Microsoft.Maui.Controls.ScrollView scrollView)
    {
        return null;
    }
}
#endif
