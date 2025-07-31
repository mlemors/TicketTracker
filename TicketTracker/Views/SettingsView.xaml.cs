using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TicketTracker.Services;
using TicketTracker.ViewModels;

namespace TicketTracker.Views;

public partial class SettingsView : UserControl
{
    private readonly MainWindowViewModel? _mainViewModel;
    private readonly ThemeService _themeService;
    private bool _isDropdownOpen;

    public SettingsView(ThemeService themeService, MainWindowViewModel? mainViewModel = null)
    {
        _themeService = themeService;
        _mainViewModel = mainViewModel;
        InitializeComponent();

        // Set initial state based on current theme
        UpdateThemeDisplay();

        // Listen to theme changes
        _themeService.ThemeChanged += OnThemeChanged;

        // Setup hover effects
        SetupHoverEffects();
    }

    private void ThemeDropdownButton_Click(object sender, RoutedEventArgs e)
    {
        _isDropdownOpen = !_isDropdownOpen;
        UpdateDropdownState();
    }

    private void SystemThemeOption_Click(object sender, RoutedEventArgs e)
    {
        _themeService.CurrentTheme = ThemeService.Theme.System;
        CloseDropdown();
    }

    private void LightThemeOption_Click(object sender, RoutedEventArgs e)
    {
        _themeService.CurrentTheme = ThemeService.Theme.Light;
        CloseDropdown();
    }

    private void DarkThemeOption_Click(object sender, RoutedEventArgs e)
    {
        _themeService.CurrentTheme = ThemeService.Theme.Dark;
        CloseDropdown();
    }

    private void OnThemeChanged(object? sender, ThemeService.Theme theme)
    {
        UpdateThemeDisplay();
    }

    private void UpdateThemeDisplay()
    {
        var themeText = _themeService.CurrentTheme switch
        {
            ThemeService.Theme.System => "System",
            ThemeService.Theme.Light => "Hell",
            ThemeService.Theme.Dark => "Dunkel",
            _ => "System"
        };

        SelectedThemeText.Text = themeText;

        // Update radio buttons
        SystemThemeRadio.IsChecked = _themeService.CurrentTheme == ThemeService.Theme.System;
        LightThemeRadio.IsChecked = _themeService.CurrentTheme == ThemeService.Theme.Light;
        DarkThemeRadio.IsChecked = _themeService.CurrentTheme == ThemeService.Theme.Dark;
    }

    private void UpdateDropdownState()
    {
        if (_isDropdownOpen)
        {
            // Show dropdown with animation
            ThemeDropdownContent.Visibility = Visibility.Visible;
            DropdownArrow.Text = "⌃"; // Up arrow

            // Animate dropdown opening
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
            ThemeDropdownContent.BeginAnimation(OpacityProperty, fadeIn);
        }
        else
        {
            // Hide dropdown with animation
            DropdownArrow.Text = "⌄"; // Down arrow

            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            fadeOut.Completed += (s, e) => ThemeDropdownContent.Visibility = Visibility.Collapsed;
            ThemeDropdownContent.BeginAnimation(OpacityProperty, fadeOut);
        }
    }

    private void CloseDropdown()
    {
        _isDropdownOpen = false;
        UpdateDropdownState();
    }

    private void ResetDataButton_Click(object sender, RoutedEventArgs e)
    {
        // Clear all timers without confirmation dialog
        ClearAllTimers();
    }

    private void ClearAllTimers()
    {
        if (_mainViewModel == null) return;

        try
        {
            // Create a copy of the timer list to avoid modification during iteration
            var timersToRemove = _mainViewModel.Timers.ToList();

            // Remove all timers
            foreach (var timer in timersToRemove) timer.RemoveCommand.Execute(null);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Fehler beim Löschen der Tickets:\n{ex.Message}",
                "Fehler",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void SetupHoverEffects()
    {
        // Setup hover effects for all interactive buttons
        SetupButtonHover(ThemeDropdownButton);
        SetupButtonHover(SystemThemeOption);
        SetupButtonHover(LightThemeOption);
        SetupButtonHover(DarkThemeOption);
        SetupButtonHover(ResetDataButton);
    }

    private void SetupButtonHover(Button button)
    {
        var normalBrush = Brushes.Transparent;
        var hoverBrush = (Brush)Application.Current.FindResource("NavigationHoverBrush");

        button.MouseEnter += (s, e) => { button.Background = hoverBrush; };

        button.MouseLeave += (s, e) => { button.Background = normalBrush; };
    }
}