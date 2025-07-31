using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TicketTracker.ViewModels;

namespace TicketTracker.Views;

public partial class StopwatchView : UserControl
{
    private string? _previousTimerName;
    private MainWindowViewModel? _viewModel;

    public StopwatchView()
    {
        InitializeComponent();
    }

    public void SetViewModel(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;

        // Initialize _previousTimerName with the empty state
        _previousTimerName = "Wählen Sie einen Timer aus";

        // Timer Display binden
        TimerDisplay.SetBinding(TextBlock.TextProperty,
            new Binding("MainTimerDisplay"));

        // Update start time display when timer changes
        UpdateStartTimeDisplay();

        // Button Commands binden
        StartPauseButton.SetBinding(Button.CommandProperty,
            new Binding("StartPauseCommand"));
        ResetButton.SetBinding(Button.CommandProperty,
            new Binding("ResetCommand"));

        // Setup Delete button visibility
        UpdateDeleteButtonVisibility();

        // Button text updates and timer selection animation
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(viewModel.StartPauseButtonText))
            {
                UpdateStartPauseButton(viewModel.StartPauseButtonText);
                UpdateStartTimeDisplay();
            }
            else if (e.PropertyName == nameof(viewModel.MainTimerName))
            {
                AnimateTimerChange(viewModel.MainTimerName);
                UpdateDeleteButtonVisibility();
                UpdateStartTimeDisplay();
            }
        };

        UpdateStartPauseButton(viewModel.StartPauseButtonText);
        _previousTimerName = viewModel.MainTimerName;
    }


    private void UpdateStartPauseButton(string text)
    {
        if (text == "Start")
        {
            StartPauseIcon.Text = "▶️";
            StartPauseText.Text = "Start";
        }
        else
        {
            StartPauseIcon.Text = "⏸️";
            StartPauseText.Text = "Pause";
        }
    }

    private void AnimateTimerChange(string newTimerName)
    {
        // Skip animation for initial load or when going to empty state
        if (_previousTimerName == null || _previousTimerName == newTimerName ||
            newTimerName == "Wählen Sie einen Timer aus")
        {
            _previousTimerName = newTimerName;
            return;
        }

        // When switching FROM empty state to first timer, use smooth fade-in like settings page
        if (_previousTimerName == "Wählen Sie einen Timer aus")
        {
            _previousTimerName = newTimerName;

            // Smooth fade-in animation like settings page
            var fadeInAnimation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            BeginAnimation(OpacityProperty, fadeInAnimation);
            return;
        }

        // Determine animation direction based on timer position in list
        var slideDown = ShouldSlideDown(_previousTimerName, newTimerName);

        // Create slide out animation with opacity to hide content changes
        var slideOutAnimation = new DoubleAnimation
        {
            From = 0,
            To = slideDown ? -ActualHeight : ActualHeight,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };

        // Also fade out during slide to hide any content flicker
        var fadeOutAnimation = new DoubleAnimation
        {
            From = 1.0,
            To = 0.3,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };

        slideOutAnimation.Completed += (s, e) =>
        {
            // Reset position to opposite side
            ContentTransform.Y = slideDown ? ActualHeight : -ActualHeight;

            // Update the timer name so data binding reflects new content
            _previousTimerName = newTimerName;

            // Slide in from opposite side
            var slideInAnimation = new DoubleAnimation
            {
                From = slideDown ? ActualHeight : -ActualHeight,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            // Fade back in during slide
            var fadeInAnimation = new DoubleAnimation
            {
                From = 0.3,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            ContentTransform.BeginAnimation(TranslateTransform.YProperty, slideInAnimation);
            BeginAnimation(OpacityProperty, fadeInAnimation);
        };

        // Start slide out animation with fade
        ContentTransform.BeginAnimation(TranslateTransform.YProperty, slideOutAnimation);
        BeginAnimation(OpacityProperty, fadeOutAnimation);
    }

    private bool ShouldSlideDown(string fromTimerName, string toTimerName)
    {
        if (_viewModel == null) return true;

        // Find positions of both timers in the list
        var fromIndex = -1;
        var toIndex = -1;

        for (var i = 0; i < _viewModel.Timers.Count; i++)
        {
            if (_viewModel.Timers[i].Name == fromTimerName)
                fromIndex = i;
            if (_viewModel.Timers[i].Name == toTimerName)
                toIndex = i;
        }

        // If we can't find both timers, default to slide down
        if (fromIndex == -1 || toIndex == -1)
            return true;

        // If moving to a timer higher in the list (lower index), slide up
        // If moving to a timer lower in the list (higher index), slide down
        return toIndex > fromIndex;
    }

    private void UpdateDeleteButtonVisibility()
    {
        // Show delete button only when a timer is selected
        if (_viewModel?.SelectedTimer != null && _viewModel.MainTimerName != "Wählen Sie einen Timer aus")
            DeleteButton.Visibility = Visibility.Visible;
        else
            DeleteButton.Visibility = Visibility.Collapsed;
    }

    private void UpdateStartTimeDisplay()
    {
        if (_viewModel?.SelectedTimer?.LastStartTime != null)
        {
            var lastStartTime = _viewModel.SelectedTimer.LastStartTime.Value;
            StartDateTime.Text = lastStartTime.ToString("dd.MM.yyyy HH:mm:ss");
        }
        else if (_viewModel?.SelectedTimer != null)
        {
            StartDateTime.Text = "Noch nicht gestartet";
        }
        else
        {
            // No timer selected at all
            StartDateTime.Text = "";
        }

        StartDateTime.Visibility = Visibility.Visible;
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.SelectedTimer != null) ShowDeleteHoverMenu(DeleteButton, _viewModel.SelectedTimer);
    }

    private void ShowDeleteHoverMenu(Button deleteButton, TimerViewModel timer)
    {
        // Create popup for hover menu
        var popup = new Popup
        {
            PlacementTarget = deleteButton,
            Placement = PlacementMode.Top,
            AllowsTransparency = true,
            StaysOpen = false
        };

        // Create menu content
        var menuBorder = new Border
        {
            Background = (Brush)Application.Current.FindResource("SurfaceBrush"),
            BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 0, 0, 2)
        };

        var menuStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(8)
        };

        // Confirmation text
        var confirmText = new TextBlock
        {
            Text = $"Timer \"{timer.Name}\" löschen?",
            FontSize = 12,
            Foreground = (Brush)Application.Current.FindResource("PrimaryTextBrush"),
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 200
        };

        // Button container
        var buttonStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        // Delete button
        var deleteBtn = new Button
        {
            Content = "Löschen",
            Width = 60,
            Height = 24,
            FontSize = 11,
            Background = new SolidColorBrush(Color.FromRgb(220, 53, 69)), // Red
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(0, 0, 4, 0)
        };

        // Cancel button
        var cancelBtn = new Button
        {
            Content = "Abbrechen",
            Width = 60,
            Height = 24,
            FontSize = 11,
            Background = (Brush)Application.Current.FindResource("SurfaceBrush"),
            Foreground = (Brush)Application.Current.FindResource("PrimaryTextBrush"),
            BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
            BorderThickness = new Thickness(1)
        };

        // Event handlers
        deleteBtn.Click += (s, e) =>
        {
            timer.RemoveCommand.Execute(null);
            popup.IsOpen = false;
        };

        cancelBtn.Click += (s, e) => { popup.IsOpen = false; };

        // Assemble menu
        buttonStack.Children.Add(deleteBtn);
        buttonStack.Children.Add(cancelBtn);
        menuStack.Children.Add(confirmText);
        menuStack.Children.Add(buttonStack);
        menuBorder.Child = menuStack;
        popup.Child = menuBorder;

        // Show popup
        popup.IsOpen = true;
    }
}