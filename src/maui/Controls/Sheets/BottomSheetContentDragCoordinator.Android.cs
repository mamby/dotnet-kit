#if ANDROID
using Android.Views;

namespace Mamby.Maui.Controls.Sheets;

internal static partial class BottomSheetContentDragCoordinator
{
    public static partial IBottomSheetContentDragCoordinator? Create(
        BottomSheetView sheet,
        Microsoft.Maui.Controls.ScrollView scrollView)
    {
        return new AndroidBottomSheetContentDragCoordinator(sheet, scrollView);
    }
}

internal sealed class AndroidBottomSheetContentDragCoordinator : IBottomSheetContentDragCoordinator
{
    private readonly BottomSheetView _sheet;
    private readonly Microsoft.Maui.Controls.ScrollView _scrollView;
    private Android.Views.View? _platformScrollView;
    private bool _isPullingSheet;
    private double _gestureStartX;
    private double _gestureStartY;
    private double _pullGestureOffsetY;

    public AndroidBottomSheetContentDragCoordinator(
        BottomSheetView sheet,
        Microsoft.Maui.Controls.ScrollView scrollView)
    {
        _sheet = sheet;
        _scrollView = scrollView;
        _scrollView.HandlerChanged += OnScrollViewHandlerChanged;
        AttachToPlatformScrollView();
    }

    public void Reset()
    {
        _isPullingSheet = false;
        _gestureStartX = 0d;
        _gestureStartY = 0d;
        _pullGestureOffsetY = 0d;
    }

    public void Dispose()
    {
        _scrollView.HandlerChanged -= OnScrollViewHandlerChanged;
        DetachFromPlatformScrollView();
        GC.SuppressFinalize(this);
    }

    private void OnScrollViewHandlerChanged(object? sender, EventArgs e)
    {
        DetachFromPlatformScrollView();
        AttachToPlatformScrollView();
    }

    private void AttachToPlatformScrollView()
    {
        _platformScrollView = _scrollView.Handler?.PlatformView as Android.Views.View;

        if (_platformScrollView is Android.Views.View platformScrollView)
        {
            platformScrollView.Touch += OnPlatformScrollViewTouched;
        }
    }

    private void DetachFromPlatformScrollView()
    {
        if (_platformScrollView is Android.Views.View platformScrollView)
        {
            platformScrollView.Touch -= OnPlatformScrollViewTouched;
            _platformScrollView = null;
        }
    }

    private void OnPlatformScrollViewTouched(object? sender, Android.Views.View.TouchEventArgs e)
    {
        e.Handled = false;

        if (e.Event is not MotionEvent motionEvent)
        {
            return;
        }

        double density = Microsoft.Maui.Devices.DeviceDisplay.Current.MainDisplayInfo.Density;
        double x = motionEvent.RawX / density;
        double y = motionEvent.RawY / density;

        switch (motionEvent.ActionMasked)
        {
            case MotionEventActions.Down:
                BeginGesture(x, y);
                break;

            case MotionEventActions.Move:
                MoveGesture(x, y);
                break;

            case MotionEventActions.Up:
                EndGesture(GestureStatus.Completed, y);
                break;

            case MotionEventActions.Cancel:
                EndGesture(GestureStatus.Canceled, y);
                break;
        }
    }

    private void BeginGesture(double x, double y)
    {
        Reset();
        _gestureStartX = x;
        _gestureStartY = y;
    }

    private void MoveGesture(double x, double y)
    {
        double deltaX = x - _gestureStartX;
        double deltaY = y - _gestureStartY;

        if (_isPullingSheet)
        {
            _sheet.UpdateContentPull(this, deltaY - _pullGestureOffsetY);
            return;
        }

        if (!_sheet.CanStartContentPull ||
            deltaY <= BottomSheetView.ContentPullStartThreshold ||
            deltaY < Math.Abs(deltaX))
        {
            return;
        }

        _isPullingSheet = true;
        _pullGestureOffsetY = deltaY - BottomSheetView.ContentPullStartThreshold;
        _sheet.StartContentPull(this);
        _sheet.UpdateContentPull(this, deltaY - _pullGestureOffsetY);
    }

    private void EndGesture(GestureStatus status, double y)
    {
        if (_isPullingSheet)
        {
            _sheet.CompleteContentPull(this, status, y - _gestureStartY - _pullGestureOffsetY);
        }

        Reset();
    }
}
#endif
