#if IOS || MACCATALYST
using UIKit;

namespace Mamby.Maui.Controls.Sheets;

internal static partial class BottomSheetContentDragCoordinator
{
    public static partial IBottomSheetContentDragCoordinator? Create(
        BottomSheetView sheet,
        Microsoft.Maui.Controls.ScrollView scrollView)
    {
        return new AppleBottomSheetContentDragCoordinator(sheet, scrollView);
    }
}

internal sealed class AppleBottomSheetContentDragCoordinator : IBottomSheetContentDragCoordinator
{
    private readonly BottomSheetView _sheet;
    private readonly Microsoft.Maui.Controls.ScrollView _scrollView;
    private readonly SheetPanGestureDelegate _gestureDelegate = new();
    private UIScrollView? _platformScrollView;
    private UIPanGestureRecognizer? _panGestureRecognizer;
    private bool _isPullingSheet;
    private bool _isDisposed;
    private double _pullGestureOffsetY;

    public AppleBottomSheetContentDragCoordinator(
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
        _pullGestureOffsetY = 0d;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _scrollView.HandlerChanged -= OnScrollViewHandlerChanged;
        DetachFromPlatformScrollView();
        _gestureDelegate.Dispose();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    private void OnScrollViewHandlerChanged(object? sender, EventArgs e)
    {
        DetachFromPlatformScrollView();
        AttachToPlatformScrollView();
    }

    private void AttachToPlatformScrollView()
    {
        _platformScrollView = _scrollView.Handler?.PlatformView as UIScrollView;

        if (_platformScrollView is null)
        {
            return;
        }

        _panGestureRecognizer = new UIPanGestureRecognizer(OnPanGestureUpdated)
        {
            CancelsTouchesInView = false,
            DelaysTouchesBegan = false,
            DelaysTouchesEnded = false,
            Delegate = _gestureDelegate
        };

        _platformScrollView.AddGestureRecognizer(_panGestureRecognizer);
    }

    private void DetachFromPlatformScrollView()
    {
        UIScrollView? platformScrollView = _platformScrollView;
        UIPanGestureRecognizer? panGestureRecognizer = _panGestureRecognizer;

        if (platformScrollView is not null && panGestureRecognizer is not null)
        {
            platformScrollView.RemoveGestureRecognizer(panGestureRecognizer);
        }

        _platformScrollView = null;
        _panGestureRecognizer = null;
        panGestureRecognizer?.Dispose();
        Reset();
    }

    private void OnPanGestureUpdated()
    {
        if (_panGestureRecognizer is null || _platformScrollView is null)
        {
            return;
        }

        CoreGraphics.CGPoint translation = _panGestureRecognizer.TranslationInView(_platformScrollView);

        switch (_panGestureRecognizer.State)
        {
            case UIGestureRecognizerState.Began:
                Reset();
                break;

            case UIGestureRecognizerState.Changed:
                MoveGesture(translation.X, translation.Y);
                break;

            case UIGestureRecognizerState.Ended:
                EndGesture(GestureStatus.Completed, translation.Y);
                break;

            case UIGestureRecognizerState.Cancelled:
            case UIGestureRecognizerState.Failed:
                EndGesture(GestureStatus.Canceled, translation.Y);
                break;
        }
    }

    private void MoveGesture(double deltaX, double deltaY)
    {
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

    private void EndGesture(GestureStatus status, double totalY)
    {
        if (_isPullingSheet)
        {
            _sheet.CompleteContentPull(this, status, totalY - _pullGestureOffsetY);
        }

        Reset();
    }

    private sealed class SheetPanGestureDelegate : UIGestureRecognizerDelegate
    {
        public override bool ShouldRecognizeSimultaneously(
            UIGestureRecognizer gestureRecognizer,
            UIGestureRecognizer otherGestureRecognizer)
        {
            return true;
        }
    }
}
#endif
