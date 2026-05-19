#if WINDOWS
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Mamby.Maui.Controls.Sheets;

internal static partial class BottomSheetContentDragCoordinator
{
    public static partial IBottomSheetContentDragCoordinator? Create(
        BottomSheetView sheet,
        Microsoft.Maui.Controls.ScrollView scrollView)
    {
        return new WindowsBottomSheetContentDragCoordinator(sheet, scrollView);
    }
}

internal sealed class WindowsBottomSheetContentDragCoordinator : IBottomSheetContentDragCoordinator
{
    private readonly BottomSheetView _sheet;
    private readonly Microsoft.Maui.Controls.ScrollView _scrollView;
    private readonly PointerEventHandler _pointerPressedHandler;
    private readonly PointerEventHandler _pointerMovedHandler;
    private readonly PointerEventHandler _pointerReleasedHandler;
    private readonly PointerEventHandler _pointerCanceledHandler;
    private readonly PointerEventHandler _pointerCaptureLostHandler;
    private ScrollViewer? _platformScrollView;
    private uint? _activePointerId;
    private bool _isPullingSheet;
    private double _gestureStartX;
    private double _gestureStartY;
    private double _pullGestureOffsetY;

    public WindowsBottomSheetContentDragCoordinator(BottomSheetView sheet, Microsoft.Maui.Controls.ScrollView scrollView)
    {
        _sheet = sheet;
        _scrollView = scrollView;
        _pointerPressedHandler = OnPointerPressed;
        _pointerMovedHandler = OnPointerMoved;
        _pointerReleasedHandler = OnPointerReleased;
        _pointerCanceledHandler = OnPointerCanceled;
        _pointerCaptureLostHandler = OnPointerCaptureLost;
        _scrollView.HandlerChanged += OnScrollViewHandlerChanged;
        AttachToPlatformScrollView();
    }

    public void Reset()
    {
        _activePointerId = null;
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
        _platformScrollView = _scrollView.Handler?.PlatformView as ScrollViewer;

        if (_platformScrollView is null)
        {
            return;
        }

        _platformScrollView.AddHandler(UIElement.PointerPressedEvent, _pointerPressedHandler, true);
        _platformScrollView.AddHandler(UIElement.PointerMovedEvent, _pointerMovedHandler, true);
        _platformScrollView.AddHandler(UIElement.PointerReleasedEvent, _pointerReleasedHandler, true);
        _platformScrollView.AddHandler(UIElement.PointerCanceledEvent, _pointerCanceledHandler, true);
        _platformScrollView.AddHandler(UIElement.PointerCaptureLostEvent, _pointerCaptureLostHandler, true);
    }

    private void DetachFromPlatformScrollView()
    {
        if (_platformScrollView is ScrollViewer platformScrollView)
        {
            platformScrollView.RemoveHandler(UIElement.PointerPressedEvent, _pointerPressedHandler);
            platformScrollView.RemoveHandler(UIElement.PointerMovedEvent, _pointerMovedHandler);
            platformScrollView.RemoveHandler(UIElement.PointerReleasedEvent, _pointerReleasedHandler);
            platformScrollView.RemoveHandler(UIElement.PointerCanceledEvent, _pointerCanceledHandler);
            platformScrollView.RemoveHandler(UIElement.PointerCaptureLostEvent, _pointerCaptureLostHandler);
            _platformScrollView = null;
        }

        Reset();
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_platformScrollView is null)
        {
            return;
        }

        PointerPoint pointerPoint = e.GetCurrentPoint(_platformScrollView);

        if (!pointerPoint.IsInContact)
        {
            return;
        }

        Reset();
        _activePointerId = pointerPoint.PointerId;
        _gestureStartX = pointerPoint.Position.X;
        _gestureStartY = pointerPoint.Position.Y;
        _platformScrollView.CapturePointer(e.Pointer);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_platformScrollView is null || _activePointerId is null)
        {
            return;
        }

        PointerPoint pointerPoint = e.GetCurrentPoint(_platformScrollView);

        if (pointerPoint.PointerId != _activePointerId)
        {
            return;
        }

        if (!pointerPoint.IsInContact)
        {
            EndGesture(GestureStatus.Completed, pointerPoint.Position.Y);
            ReleasePointerCapture(e);
            return;
        }

        MoveGesture(pointerPoint.Position.X, pointerPoint.Position.Y);
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        EndPointerGesture(e, GestureStatus.Completed);
    }

    private void OnPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        EndPointerGesture(e, GestureStatus.Canceled);
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        EndPointerGesture(e, GestureStatus.Canceled);
    }

    private void EndPointerGesture(PointerRoutedEventArgs e, GestureStatus status)
    {
        if (_platformScrollView is null || _activePointerId is null)
        {
            return;
        }

        PointerPoint pointerPoint = e.GetCurrentPoint(_platformScrollView);

        if (pointerPoint.PointerId != _activePointerId)
        {
            return;
        }

        EndGesture(status, pointerPoint.Position.Y);
        ReleasePointerCapture(e);
    }

    private void ReleasePointerCapture(PointerRoutedEventArgs e)
    {
        if (_platformScrollView is ScrollViewer platformScrollView)
        {
            platformScrollView.ReleasePointerCapture(e.Pointer);
        }
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
