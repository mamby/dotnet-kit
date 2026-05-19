namespace Mamby.Maui.Controls.Sheets;

/// <summary>
/// Displays modal content anchored to the bottom of its parent with a scrim and drag-to-dismiss interactions.
/// </summary>
public partial class BottomSheetView : ContentView, IDisposable
{
    private const double DefaultMaximumHeightRatio = 0.75d;
    private const double DismissPullDistance = 96d;
    private const double PullStartThreshold = 4d;
    private const double ContentPullActivationThreshold = 10d;
    private const double TopScrollTolerance = 0.5d;
    private const int ContentPullGestureId = 1;
    private const uint EntranceAnimationDuration = 320u;
    private const uint ExitAnimationDuration = 240u;
    private const uint RestoreAnimationDuration = 180u;

    /// <summary>
    /// Identifies the <see cref="SheetContent" /> bindable property.
    /// </summary>
    public static readonly BindableProperty SheetContentProperty =
        BindableProperty.Create(
            nameof(SheetContent),
            typeof(View),
            typeof(BottomSheetView),
            default(View),
            propertyChanged: OnSheetContentChanged);

    /// <summary>
    /// Identifies the <see cref="MaximumHeightRatio" /> bindable property.
    /// </summary>
    public static readonly BindableProperty MaximumHeightRatioProperty =
        BindableProperty.Create(
            nameof(MaximumHeightRatio),
            typeof(double),
            typeof(BottomSheetView),
            DefaultMaximumHeightRatio,
            propertyChanged: OnMaximumHeightRatioChanged);

    /// <summary>
    /// Identifies the <see cref="IsContentPullToDismissEnabled" /> bindable property.
    /// </summary>
    public static readonly BindableProperty IsContentPullToDismissEnabledProperty =
        BindableProperty.Create(
            nameof(IsContentPullToDismissEnabled),
            typeof(bool),
            typeof(BottomSheetView),
            true);

    private readonly IBottomSheetContentDragCoordinator? _contentDragCoordinator;
    private readonly SemaphoreSlim _transitionLock = new(1, 1);
    private object? _activePullPanSource;
    private bool _isSheetPulling;
    private SheetLifecycleState _lifecycleState = SheetLifecycleState.Closed;
    private bool _isDisposed;
    private double _stableViewportHeight;
    private double _stableViewportWidth;
    private double _sheetPullStartTotalY;

    /// <summary>
    /// Initializes a new instance of the <see cref="BottomSheetView" /> class.
    /// </summary>
    public BottomSheetView()
    {
        InitializeComponent();

        _contentDragCoordinator = BottomSheetContentDragCoordinator.Create(this, SheetScrollView);
    }

    /// <summary>
    /// Gets or sets the callback invoked when the user requests dismissal from the scrim or a pull gesture.
    /// </summary>
    public Func<Task>? DismissRequested { get; set; }

    /// <summary>
    /// Gets or sets the view hosted inside the scrollable sheet body.
    /// </summary>
    public View? SheetContent
    {
        get => (View?)GetValue(SheetContentProperty);
        set => SetValue(SheetContentProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum sheet height as a ratio of the available viewport height.
    /// </summary>
    public double MaximumHeightRatio
    {
        get => (double)GetValue(MaximumHeightRatioProperty);
        set => SetValue(MaximumHeightRatioProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether dragging downward from the top of the sheet content can dismiss the sheet.
    /// </summary>
    public bool IsContentPullToDismissEnabled
    {
        get => (bool)GetValue(IsContentPullToDismissEnabledProperty);
        set => SetValue(IsContentPullToDismissEnabledProperty, value);
    }

    /// <summary>
    /// Gets a value indicating whether the sheet is opening, open, or closing.
    /// </summary>
    public bool IsOpen => _lifecycleState is not SheetLifecycleState.Closed;

    /// <summary>
    /// Shows the sheet and optionally applies a binding context to the hosted content.
    /// </summary>
    /// <param name="bindingContext">The binding context to assign to <see cref="SheetContent" /> while open.</param>
    /// <returns>A task that completes when the opening transition finishes.</returns>
    public async Task ShowAsync(object? bindingContext = null)
    {
        await _transitionLock.WaitAsync();

        bool startedOpening = false;

        try
        {
            if (_lifecycleState is not SheetLifecycleState.Closed)
            {
                return;
            }

            SetLifecycleState(SheetLifecycleState.Opening);
            startedOpening = true;

            if (SheetContent is View sheetContent)
            {
                sheetContent.BindingContext = bindingContext;
            }

            IsVisible = true;
            ConfigureSheetLayout();
            Scrim.CancelAnimations();
            Sheet.CancelAnimations();
            ResetInteractionState();

            Scrim.Opacity = 0;
            Sheet.Opacity = 1;
            Sheet.TranslationY = GetHiddenTranslation();

            await ResetScrollPositionAsync();

            await Task.WhenAll(
                Scrim.FadeToAsync(1, EntranceAnimationDuration, Easing.CubicOut),
                Sheet.TranslateToAsync(0, 0, EntranceAnimationDuration, Easing.CubicOut));

            SetLifecycleState(SheetLifecycleState.Open);
        }
        catch
        {
            if (startedOpening)
            {
                CloseImmediately();
            }

            throw;
        }
        finally
        {
            _transitionLock.Release();
        }
    }

    /// <summary>
    /// Hides the sheet.
    /// </summary>
    /// <returns>A task that completes when the closing transition finishes.</returns>
    public async Task HideAsync()
    {
        await _transitionLock.WaitAsync();

        bool startedClosing = false;

        try
        {
            if (_lifecycleState is SheetLifecycleState.Closed or SheetLifecycleState.Closing)
            {
                return;
            }

            SetLifecycleState(SheetLifecycleState.Closing);
            startedClosing = true;
            Scrim.CancelAnimations();
            Sheet.CancelAnimations();
            ResetInteractionState();
            ConfigureSheetLayout();

            await Task.WhenAll(
                Scrim.FadeToAsync(0, ExitAnimationDuration, Easing.CubicInOut),
                Sheet.TranslateToAsync(0, GetHiddenTranslation(), ExitAnimationDuration, Easing.CubicInOut));
        }
        finally
        {
            if (startedClosing)
            {
                CloseImmediately();
            }

            _transitionLock.Release();
        }
    }

    /// <summary>
    /// Clears any active drag interaction state.
    /// </summary>
    public void ResetInteractionState()
    {
        _activePullPanSource = null;
        _isSheetPulling = false;
        _sheetPullStartTotalY = 0d;
        _contentDragCoordinator?.Reset();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _contentDragCoordinator?.Dispose();
        _transitionLock.Dispose();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        ConfigureSheetLayout();
    }

    private static void OnSheetContentChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not BottomSheetView sheet)
        {
            return;
        }

        if (oldValue is View oldView && ReferenceEquals(sheet.SheetScrollView.Content, oldView))
        {
            sheet.SheetScrollView.Content = null;
        }

        if (newValue is View view)
        {
            view.HorizontalOptions = LayoutOptions.Fill;
            view.VerticalOptions = LayoutOptions.Start;
            sheet.SheetScrollView.Content = view;
            return;
        }

        sheet.SheetScrollView.Content = null;
    }

    private static void OnMaximumHeightRatioChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is BottomSheetView sheet)
        {
            sheet.ConfigureSheetLayout();
        }
    }

    private async Task ResetScrollPositionAsync()
    {
        ResetInteractionState();

        if (SheetScrollView.ScrollX is 0d && SheetScrollView.ScrollY is 0d)
        {
            return;
        }

        await SheetScrollView.ScrollToAsync(0, 0, false);
    }

    private void ConfigureSheetLayout()
    {
        VisualElement? parent = Parent as VisualElement;
        Microsoft.Maui.Devices.DisplayInfo display = Microsoft.Maui.Devices.DeviceDisplay.Current.MainDisplayInfo;
        double width = Width > 0
            ? Width
            : parent?.Width > 0
                ? parent.Width
                : display.Width / display.Density;
        double height = Height > 0
            ? Height
            : parent?.Height > 0
                ? parent.Height
                : display.Height / display.Density;

        if (width > 0)
        {
            Sheet.WidthRequest = width;
            UpdateStableViewportWidth(width);
        }

        if (height > 0 && MaximumHeightRatio > 0)
        {
            UpdateStableViewportHeight(height);
            double maximumSheetHeight = _stableViewportHeight * MaximumHeightRatio;
            Sheet.HeightRequest = maximumSheetHeight;
            Sheet.MaximumHeightRequest = maximumSheetHeight;
            double scrollViewportHeight = GetMaximumScrollViewportHeight(maximumSheetHeight);
            SheetScrollView.HeightRequest = scrollViewportHeight;
            SheetScrollView.MaximumHeightRequest = scrollViewportHeight;
        }
    }

    private double GetMaximumScrollViewportHeight(double maximumSheetHeight)
    {
        double chromeHeight = SheetChrome.HeightRequest > 0
            ? SheetChrome.HeightRequest
            : SheetChrome.Height;
        double verticalPadding = Sheet.Padding.Top + Sheet.Padding.Bottom;
        double viewportHeight = maximumSheetHeight - chromeHeight - verticalPadding;

        return Math.Max(0, viewportHeight);
    }

    private void UpdateStableViewportWidth(double width)
    {
        if (_stableViewportWidth <= 0)
        {
            _stableViewportWidth = width;
            return;
        }

        if (_stableViewportWidth != width)
        {
            _stableViewportWidth = width;
            _stableViewportHeight = 0d;
        }
    }

    private void UpdateStableViewportHeight(double height)
    {
        if (_stableViewportHeight <= 0 || height > _stableViewportHeight)
        {
            _stableViewportHeight = height;
        }
    }

    private async void OnScrimTapped(object? sender, TappedEventArgs e)
    {
        await DismissFromUserAsync();
    }

    private void OnPullPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        OnSheetChromePanUpdated(sender, e);
    }

    private void OnSheetChromePanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                if (_activePullPanSource is not null)
                {
                    return;
                }

                _activePullPanSource = sender;
                HandleSheetPanUpdated(sender, e);
                break;

            case GestureStatus.Running:
                if (!ReferenceEquals(_activePullPanSource, sender))
                {
                    return;
                }

                HandleSheetPanUpdated(sender, e);
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                if (!ReferenceEquals(_activePullPanSource, sender))
                {
                    return;
                }

                HandleSheetPanUpdated(sender, e);
                _activePullPanSource = null;
                break;
        }
    }

    private async void HandleSheetPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (!IsVisible || IsTransitionRunning)
        {
            return;
        }

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _isSheetPulling = false;
                _sheetPullStartTotalY = 0d;
                break;

            case GestureStatus.Running:
                if (!_isSheetPulling)
                {
                    if (e.TotalY <= PullStartThreshold)
                    {
                        return;
                    }

                    _isSheetPulling = true;
                    _sheetPullStartTotalY = e.TotalY;
                    Sheet.CancelAnimations();
                }

                Sheet.TranslationY = Math.Max(0, e.TotalY - _sheetPullStartTotalY);
                break;

            case GestureStatus.Completed:
                if (!_isSheetPulling)
                {
                    return;
                }

                _isSheetPulling = false;
                _sheetPullStartTotalY = 0d;

                if (Sheet.TranslationY >= DismissPullDistance)
                {
                    await DismissFromUserAsync();

                    if (IsOpen)
                    {
                        await RestoreSheetPositionAsync();
                    }

                    break;
                }

                await RestoreSheetPositionAsync();
                break;

            case GestureStatus.Canceled:
                if (!_isSheetPulling)
                {
                    return;
                }

                _isSheetPulling = false;
                _sheetPullStartTotalY = 0d;
                await RestoreSheetPositionAsync();
                break;
        }
    }

    private async Task RestoreSheetPositionAsync()
    {
        if (!IsVisible)
        {
            return;
        }

        Sheet.CancelAnimations();
        await Sheet.TranslateToAsync(0, 0, RestoreAnimationDuration, Easing.CubicOut);
    }

    private async Task DismissFromUserAsync()
    {
        if (DismissRequested is not null)
        {
            await DismissRequested();
            return;
        }

        await HideAsync();
    }

    private bool IsTransitionRunning =>
        _lifecycleState is SheetLifecycleState.Opening or SheetLifecycleState.Closing;

    private void SetLifecycleState(SheetLifecycleState state)
    {
        bool wasOpen = IsOpen;

        _lifecycleState = state;

        if (wasOpen != IsOpen)
        {
            OnPropertyChanged(nameof(IsOpen));
        }
    }

    private void CloseImmediately()
    {
        Scrim.CancelAnimations();
        Sheet.CancelAnimations();
        ResetInteractionState();

        Scrim.Opacity = 0;
        Sheet.TranslationY = GetHiddenTranslation();
        IsVisible = false;

        if (SheetContent is View sheetContent)
        {
            sheetContent.BindingContext = null;
        }

        SetLifecycleState(SheetLifecycleState.Closed);
    }

    private double GetHiddenTranslation()
    {
        double sheetHeight = Sheet.Height > 0 ? Sheet.Height : Sheet.MaximumHeightRequest;
        VisualElement? parent = Parent as VisualElement;
        Microsoft.Maui.Devices.DisplayInfo display = Microsoft.Maui.Devices.DeviceDisplay.Current.MainDisplayInfo;
        double fallbackHeight = Height > 0
            ? Height
            : parent?.Height > 0
                ? parent.Height
                : display.Height / display.Density;

        return sheetHeight > 0 ? sheetHeight : fallbackHeight;
    }

    internal bool CanStartContentPull =>
        IsContentPullToDismissEnabled &&
        IsVisible &&
        !IsTransitionRunning &&
        SheetScrollView.ScrollY <= TopScrollTolerance;

    internal static double ContentPullStartThreshold => ContentPullActivationThreshold;

    internal void StartContentPull(object source)
    {
        if (_activePullPanSource is not null && !ReferenceEquals(_activePullPanSource, source))
        {
            return;
        }

        _activePullPanSource = source;
        HandleSheetPanUpdated(
            source,
            new PanUpdatedEventArgs(GestureStatus.Started, ContentPullGestureId, 0, 0));
    }

    internal void UpdateContentPull(object source, double totalY)
    {
        if (!ReferenceEquals(_activePullPanSource, source))
        {
            return;
        }

        HandleSheetPanUpdated(
            source,
            new PanUpdatedEventArgs(GestureStatus.Running, ContentPullGestureId, 0, totalY));
    }

    internal void CompleteContentPull(object source, GestureStatus status, double totalY)
    {
        if (!ReferenceEquals(_activePullPanSource, source))
        {
            return;
        }

        HandleSheetPanUpdated(
            source,
            new PanUpdatedEventArgs(status, ContentPullGestureId, 0, totalY));

        _activePullPanSource = null;
    }

    private enum SheetLifecycleState
    {
        Closed,
        Opening,
        Open,
        Closing
    }
}
