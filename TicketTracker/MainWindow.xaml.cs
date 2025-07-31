using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using TicketTracker.Services;
using TicketTracker.ViewModels;
using TicketTracker.Views;

namespace TicketTracker;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TicketTracker");

    private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "window-settings.json");
    private static readonly string AppSettingsFilePath = Path.Combine(SettingsDirectory, "app-settings.json");
    private readonly EmptyStateView _emptyStateView;
    private readonly SettingsView _settingsView;
    private readonly StopwatchView _stopwatchView;
    private readonly ThemeService _themeService;
    private readonly MainWindowViewModel _viewModel;
    private bool _settingsVisible;
    private bool _sidebarExpanded = true;
    private bool _ticketsSectionVisible = true;

    public MainWindow()
    {
        InitializeComponent();

        // Load window settings
        LoadWindowSettings();

        _themeService = new ThemeService();
        _themeService.ApplyTheme();

        // Listen to theme changes to update navigation button states
        _themeService.ThemeChanged += OnThemeChanged;

        _viewModel = new MainWindowViewModel();

        // Add global key handler for search functionality
        KeyDown += MainWindow_KeyDown;

        // Subscribe to timer selection changes for indicator animation
        _viewModel.TimerSelectionChanged += AnimateIndicatorLine;

        // Subscribe to timer collection changes to update main content
        _viewModel.Timers.CollectionChanged += (s, e) =>
        {
            UpdateMainContent();
            // Hide indicator if no timers exist
            if (_viewModel.Timers.Count == 0) AnimatedIndicator.Visibility = Visibility.Collapsed;
        };

        // Load timer data
        LoadTimerData();

        _stopwatchView = new StopwatchView();
        _stopwatchView.SetViewModel(_viewModel);

        _settingsView = new SettingsView(_themeService, _viewModel);

        _emptyStateView = new EmptyStateView();
        _emptyStateView.CreateTicketRequested += OnCreateTicketRequested;

        // Setup content - initially check if we should show empty state
        UpdateMainContent();
        SettingsContent.Content = _settingsView;

        // Setup sidebar timer list
        SidebarTimerList.ItemsSource = _viewModel.Timers;
        SidebarTimerListCollapsed.ItemsSource = _viewModel.Timers;

        // Event handler for expanded list
        SidebarTimerList.MouseLeftButtonUp += (s, e) =>
        {
            if (e.OriginalSource is FrameworkElement element &&
                element.DataContext is TimerViewModel timer)
            {
                // Switch to stopwatch view if we're in settings
                if (_settingsVisible) ShowStopwatchView();

                _viewModel.SelectTimerCommand.Execute(timer);
            }
        };

        // Event handler for collapsed list
        SidebarTimerListCollapsed.MouseLeftButtonUp += (s, e) =>
        {
            if (e.OriginalSource is FrameworkElement element &&
                element.DataContext is TimerViewModel timer)
            {
                // Switch to stopwatch view if we're in settings
                if (_settingsVisible) ShowStopwatchView();

                _viewModel.SelectTimerCommand.Execute(timer);
            }
        };

        // Setup hover effects for timer list items
        _viewModel.Timers.CollectionChanged += (s, e) => { SetupTimerListHoverEffects(); };

        // Initialize sidebar state - sidebar is always expanded now
        SetupHoverEffects();

        DataContext = _viewModel;

        // Save window settings when window is moved or resized
        LocationChanged += MainWindow_LocationChanged;
        SizeChanged += MainWindow_SizeChanged;
        Closing += MainWindow_Closing;

        // Save timer data periodically
        var saveTimer = new DispatcherTimer();
        saveTimer.Interval = TimeSpan.FromSeconds(5);
        saveTimer.Tick += (s, e) => SaveTimerData();
        saveTimer.Start();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        // Don't handle if focused on input elements
        var focusedElement = Keyboard.FocusedElement;
        if (focusedElement is TextBox || focusedElement is Button)
            return;

        // Handle printable characters (letters, numbers, some symbols)
        if (IsTypableKey(e.Key))
        {
            ShowSearchMode();

            // Convert the key to character and add to search box
            var character = GetCharacterFromKey(e.Key,
                Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift));
            if (!string.IsNullOrEmpty(character) && SearchTextBox != null)
            {
                SearchTextBox.Text = character;
                SearchTextBox.SelectionStart = SearchTextBox.Text.Length; // Move cursor to end
            }

            e.Handled = true;
        }
    }

    private bool IsTypableKey(Key key)
    {
        // Letters
        if (key >= Key.A && key <= Key.Z)
            return true;

        // Numbers
        if (key >= Key.D0 && key <= Key.D9)
            return true;

        // Numpad numbers
        if (key >= Key.NumPad0 && key <= Key.NumPad9)
            return true;

        // Some common symbols and space
        if (key == Key.Space || key == Key.OemMinus || key == Key.OemPlus ||
            key == Key.OemPeriod || key == Key.OemComma)
            return true;

        return false;
    }

    private string GetCharacterFromKey(Key key, bool shift)
    {
        // Letters
        if (key >= Key.A && key <= Key.Z)
        {
            var letter = (char)('A' + (key - Key.A));
            return shift ? letter.ToString() : letter.ToString().ToLower();
        }

        // Numbers (main keyboard)
        if (key >= Key.D0 && key <= Key.D9)
        {
            if (shift)
            {
                var shiftSymbols = new[] { ")", "!", "@", "#", "$", "%", "^", "&", "*", "(" };
                return shiftSymbols[key - Key.D0];
            }

            return ((char)('0' + (key - Key.D0))).ToString();
        }

        // Numpad numbers
        if (key >= Key.NumPad0 && key <= Key.NumPad9)
            return ((char)('0' + (key - Key.NumPad0))).ToString();

        // Special keys
        switch (key)
        {
            case Key.Space: return " ";
            case Key.OemMinus: return shift ? "_" : "-";
            case Key.OemPlus: return shift ? "+" : "=";
            case Key.OemPeriod: return shift ? ">" : ".";
            case Key.OemComma: return shift ? "<" : ",";
            default: return "";
        }
    }


    private void SidebarAddButton_Click(object sender, RoutedEventArgs e)
    {
        ShowInlineTextInput();
    }


    private void ShowInlineTextInput()
    {
        InlineTextInput.Visibility = Visibility.Visible;
        NewTimerNameTextBox.Focus();
    }

    private void HideInlineTextInput()
    {
        InlineTextInput.Visibility = Visibility.Collapsed;
        NewTimerNameTextBox.Text = "";
    }

    private void ConfirmAddButton_Click(object sender, RoutedEventArgs e)
    {
        CreateNewTimer();
    }

    private void CancelAddButton_Click(object sender, RoutedEventArgs e)
    {
        HideInlineTextInput();
    }

    private void NewTimerNameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            CreateNewTimer();
        else if (e.Key == Key.Escape) HideInlineTextInput();
    }


    private void NewTimerNameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        // Use Dispatcher to delay the check, so button clicks can be processed first
        Dispatcher.BeginInvoke(new Action(() =>
        {
            // Check if focus is still within our input area (TextBox or confirm button)
            var focusedElement = Keyboard.FocusedElement as FrameworkElement;
            if (focusedElement == null ||
                (focusedElement != NewTimerNameTextBox &&
                 focusedElement != ConfirmAddButton))
                HideInlineTextInput();
        }), DispatcherPriority.Input);
    }


    private void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        // Check if we're in search mode or normal mode
        if (SearchFieldContainer.Visibility == Visibility.Visible)
        {
            // In search mode - handle based on button state
            var searchText = SearchTextBox?.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(searchText))
            {
                // No text - just hide search mode
                HideSearchMode();
                return;
            }

            // Check if we're in "delete mode" (red X) or "create mode" (green +)
            if (ActionButtonIcon?.Text == "×")
            {
                // Delete/clear mode - clear the search
                SearchTextBox.Text = "";
            }
            else
            {
                // Create mode - create or select the timer
                CreateOrSelectTimer(searchText);
                HideSearchMode();
            }
        }
        else
        {
            // In normal mode - show search
            ShowSearchMode();
        }
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSearchWatermark();
        FilterTimerListAndUpdateCreateButton();
    }

    private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var searchText = SearchTextBox?.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(searchText))
            {
                CreateOrSelectTimer(searchText);
                HideSearchMode();
            }
        }
        else if (e.Key == Key.Escape)
        {
            HideSearchMode();
        }
    }

    private void SearchTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        // Use Dispatcher to delay the check, so button clicks can be processed first
        Dispatcher.BeginInvoke(new Action(() =>
        {
            // Check if focus is still within our search area
            var focusedElement = Keyboard.FocusedElement as FrameworkElement;
            if (focusedElement == null ||
                (focusedElement != SearchTextBox &&
                 focusedElement != ActionButton))
                HideSearchMode();
        }), DispatcherPriority.Input);
    }


    private void ShowSearchMode()
    {
        TicketsHeaderText.Visibility = Visibility.Collapsed;
        SearchFieldContainer.Visibility = Visibility.Visible;
        SearchTextBox?.Focus();

        // Reset button to default + state
        if (ActionButtonIcon != null)
        {
            ActionButtonIcon.Text = "+";
            ActionButtonIcon.FontSize = 20;
            ActionButtonIcon.Foreground = (Brush)FindResource("PrimaryTextBrush");
        }
    }

    private void HideSearchMode()
    {
        SearchFieldContainer.Visibility = Visibility.Collapsed;
        TicketsHeaderText.Visibility = Visibility.Visible;

        if (SearchTextBox != null)
        {
            SearchTextBox.Text = "";
            UpdateSearchWatermark();
        }

        // Reset button to default + state
        if (ActionButtonIcon != null)
        {
            ActionButtonIcon.Text = "+";
            ActionButtonIcon.FontSize = 20;
            ActionButtonIcon.Foreground = (Brush)FindResource("PrimaryTextBrush");
        }

        // Show all timers when hiding search
        foreach (var timer in _viewModel.Timers) SetTimerVisibility(timer, true);
        SetupTimerListHoverEffects();
    }

    private void UpdateSearchWatermark()
    {
        if (SearchWatermark != null && SearchTextBox != null)
            SearchWatermark.Visibility = string.IsNullOrEmpty(SearchTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void FilterTimerListAndUpdateCreateButton()
    {
        var searchText = SearchTextBox?.Text?.Trim().ToLower() ?? "";
        var hasExactMatch = false;

        if (string.IsNullOrEmpty(searchText))
        {
            // Show all timers when search is empty
            foreach (var timer in _viewModel.Timers) SetTimerVisibility(timer, true);

            // Reset button to default + state when empty
            if (ActionButtonIcon != null)
            {
                ActionButtonIcon.Text = "+";
                ActionButtonIcon.FontSize = 20;
                ActionButtonIcon.Foreground = (Brush)FindResource("PrimaryTextBrush");
            }
        }
        else
        {
            // Filter timers based on search text
            foreach (var timer in _viewModel.Timers)
            {
                var isVisible = timer.Name.ToLower().Contains(searchText);
                SetTimerVisibility(timer, isVisible);

                if (timer.Name.ToLower() == searchText) hasExactMatch = true;
            }

            // Update button icon and color based on match
            if (ActionButtonIcon != null)
            {
                if (hasExactMatch)
                {
                    // Show red X when exact match exists
                    ActionButtonIcon.Text = "×";
                    ActionButtonIcon.FontSize = 16;
                    ActionButtonIcon.Foreground = new SolidColorBrush(Color.FromRgb(220, 53, 69)); // Red color
                }
                else
                {
                    // Show + when no exact match
                    ActionButtonIcon.Text = "+";
                    ActionButtonIcon.FontSize = 20;
                    ActionButtonIcon.Foreground = (Brush)FindResource("PrimaryTextBrush");
                }
            }
        }

        // Refresh hover effects after filtering
        SetupTimerListHoverEffects();
    }

    private void SetTimerVisibility(TimerViewModel timer, bool isVisible)
    {
        // Find the container for this timer in both lists and set visibility
        var expandedContainer = SidebarTimerList.ItemContainerGenerator.ContainerFromItem(timer) as FrameworkElement;
        var collapsedContainer =
            SidebarTimerListCollapsed.ItemContainerGenerator.ContainerFromItem(timer) as FrameworkElement;

        if (expandedContainer != null)
            expandedContainer.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

        if (collapsedContainer != null)
            collapsedContainer.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void HamburgerButton_Click(object sender, RoutedEventArgs e)
    {
        AnimateHamburgerButton();
        ToggleSidebar();
    }

    private void AnimateHamburgerButton()
    {
        // Squeeze animation: scale down then back to normal
        var squeezeAnimation = new DoubleAnimation
        {
            From = 1,
            To = 0.8,
            Duration = TimeSpan.FromMilliseconds(100),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        var expandAnimation = new DoubleAnimation
        {
            From = 0.8,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(100),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        // Chain animations
        squeezeAnimation.Completed += (s, e) =>
        {
            HamburgerScale.BeginAnimation(ScaleTransform.ScaleXProperty, expandAnimation);
            HamburgerScale.BeginAnimation(ScaleTransform.ScaleYProperty, expandAnimation);
        };

        // Start squeeze animation
        HamburgerScale.BeginAnimation(ScaleTransform.ScaleXProperty, squeezeAnimation);
        HamburgerScale.BeginAnimation(ScaleTransform.ScaleYProperty, squeezeAnimation);
    }

    private void ToggleSidebar()
    {
        _sidebarExpanded = !_sidebarExpanded;

        if (_sidebarExpanded)
        {
            // Show text elements before animation starts
            TicketsHeaderText.Visibility = Visibility.Visible;
            SettingsButtonText.Visibility = Visibility.Visible;

            // Switch to expanded timer list before animation
            SidebarScrollViewer.Visibility = Visibility.Visible;
            SidebarScrollViewerCollapsed.Visibility = Visibility.Collapsed;

            // Animate expanding sidebar
            AnimateSidebarWidth(60, 250, () =>
            {
                // Refresh hover effects for the visible list
                SetupTimerListHoverEffects();
            });
        }
        else
        {
            // Hide search mode when collapsing sidebar
            HideSearchMode();

            // Animate collapsing sidebar first
            AnimateSidebarWidth(250, 60, () =>
            {
                // Switch to collapsed timer list after animation
                SidebarScrollViewer.Visibility = Visibility.Collapsed;
                SidebarScrollViewerCollapsed.Visibility = Visibility.Visible;

                // Refresh hover effects after animation
                SetupTimerListHoverEffects();
            });
        }
    }

    private void AnimateSidebarWidth(double from, double to, Action? onCompleted = null)
    {
        // Since GridLength doesn't support direct animation, we'll animate the width step by step
        var startTime = DateTime.Now;
        var duration = TimeSpan.FromMilliseconds(80);

        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };

        timer.Tick += (s, e) =>
        {
            var elapsed = DateTime.Now - startTime;
            var progress = Math.Min(elapsed.TotalMilliseconds / duration.TotalMilliseconds, 1.0);

            // Ease out function
            progress = 1 - Math.Pow(1 - progress, 2);

            var currentWidth = from + (to - from) * progress;
            SidebarColumn.Width = new GridLength(currentWidth);

            if (progress >= 1.0)
            {
                timer.Stop();
                onCompleted?.Invoke();
            }
        };

        timer.Start();
    }

    private void AnimateContentToPosition(double targetY, Action? onCompleted = null)
    {
        // Apply initial transform if not already applied
        if (StopwatchContent.RenderTransform == null)
        {
            StopwatchContent.RenderTransform = new TranslateTransform();
        }

        var transform = StopwatchContent.RenderTransform as TranslateTransform;
        if (transform != null)
        {
            var animation = new DoubleAnimation
            {
                To = targetY,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            if (onCompleted != null)
            {
                animation.Completed += (s, e) => onCompleted();
            }

            transform.BeginAnimation(TranslateTransform.YProperty, animation);
        }
        else
        {
            // Fallback: set position immediately
            var translateTransform = new TranslateTransform(0, targetY);
            StopwatchContent.RenderTransform = translateTransform;
            onCompleted?.Invoke();
        }
    }

    private void AnimateFromEmptyStateToStopwatch()
    {
        // Create a smooth transition that simulates scrolling down from the create ticket view
        // to the first ticket in the list
        
        // Start by positioning the empty state view at the current position
        var currentTransform = StopwatchContent.RenderTransform as TranslateTransform;
        var startY = currentTransform?.Y ?? 0;
        
        // Create the animation that simulates scrolling down
        var scrollDownAnimation = new DoubleAnimation
        {
            From = startY,
            To = startY - 150, // Scroll down effect - moving content up to reveal stopwatch below
            Duration = TimeSpan.FromMilliseconds(500),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };

        scrollDownAnimation.Completed += (s, e) =>
        {
            // After the scroll animation, switch to stopwatch content
            StopwatchContent.Content = _stopwatchView;
            
            // Animate the new content sliding up into place
            var slideUpAnimation = new DoubleAnimation
            {
                From = startY - 150,
                To = 0, // Final position
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            
            if (StopwatchContent.RenderTransform is TranslateTransform transform)
            {
                transform.BeginAnimation(TranslateTransform.YProperty, slideUpAnimation);
            }
        };

        // Start the scroll down animation
        if (StopwatchContent.RenderTransform is TranslateTransform initialTransform)
        {
            initialTransform.BeginAnimation(TranslateTransform.YProperty, scrollDownAnimation);
        }
        else
        {
            var newTransform = new TranslateTransform();
            StopwatchContent.RenderTransform = newTransform;
            newTransform.BeginAnimation(TranslateTransform.YProperty, scrollDownAnimation);
        }
    }

    private void AnimateIndicatorLine(TimerViewModel? fromTimer, TimerViewModel? toTimer)
    {
        if (toTimer == null)
        {
            // Hide indicator if no timer selected
            AnimatedIndicator.Visibility = Visibility.Collapsed;
            return;
        }

        var fromIndex = fromTimer != null ? _viewModel.Timers.IndexOf(fromTimer) : -1;
        var toIndex = _viewModel.Timers.IndexOf(toTimer);

        if (toIndex == -1) return;

        var targetY = toIndex * 50 + 1; // 48px height + 2px margin, plus 1px top margin offset

        if (fromIndex == -1 || AnimatedIndicator.Visibility == Visibility.Collapsed)
        {
            // First time or no previous selection - show immediately at target position
            IndicatorTransform.Y = targetY;
            AnimatedIndicator.Visibility = Visibility.Visible;
        }
        else
        {
            // Animate from current position to new position
            var currentY = fromIndex * 50 + 1;
            var animation = new DoubleAnimation
            {
                From = currentY,
                To = targetY,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            AnimatedIndicator.Visibility = Visibility.Visible;
            IndicatorTransform.BeginAnimation(TranslateTransform.YProperty, animation);
        }
    }


    private void CreateNewTimer()
    {
        var timerName = NewTimerNameTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(timerName))
        {
            CreateOrSelectTimer(timerName);
            HideInlineTextInput();
        }
    }

    private void CreateOrSelectTimer(string timerName)
    {
        var wasEmpty = _viewModel.Timers.Count == 0;
        
        // Check if a timer with this name already exists
        var existingTimer = _viewModel.Timers.FirstOrDefault(t =>
            string.Equals(t.Name, timerName, StringComparison.OrdinalIgnoreCase));

        if (existingTimer != null)
        {
            // Timer exists - select it instead of creating a new one
            _viewModel.SelectTimerCommand.Execute(existingTimer);
        }
        else
        {
            // Timer doesn't exist - create a new one
            var newTimer = _viewModel.CreateTimerInline(timerName);
            
            // If this was the first timer created, animate from empty state to stopwatch view
            if (wasEmpty && newTimer != null)
            {
                // Delay the animation slightly to let the UI update
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    AnimateFromEmptyStateToStopwatch();
                }), DispatcherPriority.Loaded);
            }
        }
    }

    private void SettingsNavButton_Click(object sender, RoutedEventArgs e)
    {
        // Only animate if we're not already in settings view
        if (_settingsVisible) return; // Already in settings view - do nothing

        // Show settings view when coming from stopwatch
        ShowSettingsView();
    }

    private void ShowStopwatchView()
    {
        if (_settingsVisible == false)
            // Already in stopwatch view - no animation needed
            return;

        _settingsVisible = false;

        // Animate content transition - fade out settings, then fade in stopwatch
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
        fadeOut.Completed += (s, e) =>
        {
            SettingsOverlay.Visibility = Visibility.Collapsed;
            StopwatchContent.Opacity = 0; // Start with transparent stopwatch
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
            StopwatchContent.BeginAnimation(OpacityProperty, fadeIn);
        };

        SettingsOverlay.BeginAnimation(OpacityProperty, fadeOut);
        SettingsNavButton.Background = Brushes.Transparent;
    }

    private void ShowSettingsView()
    {
        if (_settingsVisible)
            // Already in settings view - no animation needed
            return;

        _settingsVisible = true;
        SettingsOverlay.Visibility = Visibility.Visible;
        SettingsOverlay.Opacity = 0; // Start with transparent overlay

        // Animate content transition - only fade out stopwatch, then fade in settings
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
        fadeOut.Completed += (s, e) =>
        {
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
            SettingsOverlay.BeginAnimation(OpacityProperty, fadeIn);
        };

        StopwatchContent.BeginAnimation(OpacityProperty, fadeOut);
        SettingsNavButton.Background = (Brush)FindResource("NavigationSelectedBrush");
    }

    private void OnThemeChanged(object? sender, ThemeService.Theme theme)
    {
        // Update settings button state when theme changes
        if (_settingsVisible)
            SettingsNavButton.Background = (Brush)FindResource("NavigationSelectedBrush");
        else
            SettingsNavButton.Background = Brushes.Transparent;
    }

    private void LoadWindowSettings()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
                return;

            var json = File.ReadAllText(SettingsFilePath);
            var settings = JsonSerializer.Deserialize<WindowSettings>(json);

            if (settings != null)
            {
                Width = Math.Max(settings.WindowWidth, MinWidth);
                Height = Math.Max(settings.WindowHeight, MinHeight);
                Left = settings.WindowLeft;
                Top = settings.WindowTop;

                if (Enum.TryParse(settings.WindowState, out WindowState ws))
                    WindowState = ws;

                // Ensure window is visible on screen
                EnsureWindowIsOnScreen();
            }
        }
        catch
        {
            // If loading fails, use default values
        }
    }

    private void SaveWindowSettings()
    {
        try
        {
            var settings = new WindowSettings();

            if (WindowState == WindowState.Normal)
            {
                settings.WindowWidth = Width;
                settings.WindowHeight = Height;
                settings.WindowLeft = Left;
                settings.WindowTop = Top;
            }
            else
            {
                // Load existing settings to preserve normal size/position
                if (File.Exists(SettingsFilePath))
                {
                    var existingJson = File.ReadAllText(SettingsFilePath);
                    var existingSettings = JsonSerializer.Deserialize<WindowSettings>(existingJson);
                    if (existingSettings != null)
                    {
                        settings.WindowWidth = existingSettings.WindowWidth;
                        settings.WindowHeight = existingSettings.WindowHeight;
                        settings.WindowLeft = existingSettings.WindowLeft;
                        settings.WindowTop = existingSettings.WindowTop;
                    }
                }
            }

            settings.WindowState = WindowState.ToString();

            // Ensure directory exists
            Directory.CreateDirectory(SettingsDirectory);

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // Ignore errors when saving settings
        }
    }

    private void EnsureWindowIsOnScreen()
    {
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;

        if (Left < 0)
            Left = 0;
        if (Top < 0)
            Top = 0;
        if (Left + Width > screenWidth)
            Left = screenWidth - Width;
        if (Top + Height > screenHeight)
            Top = screenHeight - Height;
    }

    private void MainWindow_LocationChanged(object? sender, EventArgs e)
    {
        SaveWindowSettings();
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        SaveWindowSettings();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        // Stop all running timers before closing
        StopAllTimers();

        SaveWindowSettings();
        SaveTimerData();
    }

    private void StopAllTimers()
    {
        foreach (var timer in _viewModel.Timers)
            if (timer.IsRunning)
                timer.PauseCommand.Execute(null);
    }

    private void LoadTimerData()
    {
        try
        {
            if (!File.Exists(AppSettingsFilePath))
                return;

            var json = File.ReadAllText(AppSettingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);

            if (settings != null && settings.Timers.Count > 0)
            {
                foreach (var timerData in settings.Timers)
                {
                    var timer = _viewModel.CreateTimerInline(timerData.Name);
                    if (timer != null)
                    {
                        timer.SetElapsedTime(TimeSpan.FromTicks(timerData.ElapsedTicks));
                        timer.SetStartTime(timerData.StartTime);
                        timer.SetLastStartTime(timerData.LastStartTime);
                        if (timerData.IsRunning) timer.Start();
                    }
                }

                // Select the previously selected timer
                if (!string.IsNullOrEmpty(settings.SelectedTimerName))
                {
                    var selectedTimer = _viewModel.Timers.FirstOrDefault(t => t.Name == settings.SelectedTimerName);
                    if (selectedTimer != null) _viewModel.SelectTimerCommand.Execute(selectedTimer);
                }
            }
        }
        catch
        {
            // If loading fails, start with empty timer list
        }
    }

    private void SaveTimerData()
    {
        try
        {
            var settings = new AppSettings();

            foreach (var timer in _viewModel.Timers)
                settings.Timers.Add(new TimerData
                {
                    Name = timer.Name,
                    ElapsedTicks = timer.ElapsedTime.Ticks,
                    IsRunning = timer.IsRunning,
                    StartTime = timer.StartTime,
                    LastStartTime = timer.LastStartTime
                });

            settings.SelectedTimerName = _viewModel.SelectedTimer?.Name;

            // Ensure directory exists
            Directory.CreateDirectory(SettingsDirectory);

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(AppSettingsFilePath, json);
        }
        catch
        {
            // Ignore errors when saving settings
        }
    }

    private void SetupHoverEffects()
    {
        // Setup hover effects for buttons that might not work via XAML
        SetupButtonHover(SettingsNavButton);
        SetupButtonHover(HamburgerButton);
        SetupButtonHover(ActionButton);

        // Initial setup for existing timer items
        SetupTimerListHoverEffects();
    }

    private void SetupTimerListHoverEffects()
    {
        // Wait for UI to update, then apply hover effects
        Dispatcher.BeginInvoke(new Action(() =>
        {
            // Setup hover effects for expanded list
            if (SidebarTimerList.Visibility == Visibility.Visible)
                foreach (var item in SidebarTimerList.Items)
                {
                    var container = SidebarTimerList.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                    if (container != null)
                    {
                        var border = FindVisualChild<Border>(container);
                        if (border != null) SetupBorderHover(border);
                    }
                }

            // Setup hover effects for collapsed list
            if (SidebarTimerListCollapsed.Visibility == Visibility.Visible)
                foreach (var item in SidebarTimerListCollapsed.Items)
                {
                    var container =
                        SidebarTimerListCollapsed.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                    if (container != null)
                    {
                        var border = FindVisualChild<Border>(container);
                        if (border != null) SetupBorderHover(border);
                    }
                }
        }), DispatcherPriority.Loaded);
    }

    private void SetupBorderHover(Border border)
    {
        var normalBrush = Brushes.Transparent;
        var hoverBrush = (Brush)FindResource("NavigationHoverBrush");

        border.MouseEnter += (s, e) => { border.Background = hoverBrush; };

        border.MouseLeave += (s, e) => { border.Background = normalBrush; };
    }

    private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T)
                return (T)child;

            var childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null)
                return childOfChild;
        }

        return null;
    }

    private void SetupButtonHover(Button button)
    {
        var normalBrush = Brushes.Transparent;
        var hoverBrush = (Brush)FindResource("NavigationHoverBrush");

        button.MouseEnter += (s, e) => { button.Background = hoverBrush; };

        button.MouseLeave += (s, e) =>
        {
            // Only reset to transparent if not selected
            if (button == SettingsNavButton && _settingsVisible)
                button.Background = (Brush)FindResource("NavigationSelectedBrush");
            else
                button.Background = normalBrush;
        };
    }

    private void UpdateMainContent()
    {
        if (_viewModel.Timers.Count == 0)
        {
            StopwatchContent.Content = _emptyStateView;
            // Initialize empty state at top position without animation
            if (StopwatchContent.RenderTransform == null)
            {
                StopwatchContent.RenderTransform = new TranslateTransform(0, 0);
            }
        }
        else
        {
            StopwatchContent.Content = _stopwatchView;
            // Initialize stopwatch view at normal position without animation
            if (StopwatchContent.RenderTransform == null)
            {
                StopwatchContent.RenderTransform = new TranslateTransform(0, 0);
            }
        }
    }

    private void OnCreateTicketRequested(object? sender, EventArgs e)
    {
        // Show the add ticket functionality
        ShowSearchMode();
        SearchTextBox.Focus();
    }

    private void TicketsHeaderArea_Click(object sender, MouseButtonEventArgs e)
    {
        // Only handle click if we're in settings view
        if (!_settingsVisible) return;

        // Switch back to main view
        ShowStopwatchView();

        // If no timers exist, the empty state will be shown automatically via UpdateMainContent
        // If timers exist, the stopwatch view with current timer will be shown
    }

    private class WindowSettings
    {
        public double WindowWidth { get; set; } = 1000;
        public double WindowHeight { get; set; } = 600;
        public double WindowLeft { get; set; } = 100;
        public double WindowTop { get; set; } = 100;
        public string WindowState { get; set; } = "Normal";
    }

    private class TimerData
    {
        public string Name { get; set; } = "";
        public long ElapsedTicks { get; set; }
        public bool IsRunning { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? LastStartTime { get; set; }
    }

    private class AppSettings
    {
        public List<TimerData> Timers { get; set; } = new();
        public string? SelectedTimerName { get; set; }
    }
}