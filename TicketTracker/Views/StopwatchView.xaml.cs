using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
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
        // Update the timer name without animation
        _previousTimerName = newTimerName;
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