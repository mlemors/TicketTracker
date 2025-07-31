using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using TicketTracker.Commands;
using TicketTracker.Services;

namespace TicketTracker.ViewModels;

public class MainWindowViewModel : BaseViewModel
{
    private readonly TimerService _timerService;
    private string _mainTimerDisplay = "00:00:00";
    private string _mainTimerName = "Wählen Sie ein Ticket aus";
    private TimerViewModel? _selectedTimer;

    public MainWindowViewModel()
    {
        _timerService = new TimerService();
        _timerService.TimersUpdated += OnTimersUpdated;

        Timers = new ObservableCollection<TimerViewModel>();

        SelectTimerCommand = new RelayCommand<TimerViewModel>(SelectTimer);
        StartPauseCommand = new RelayCommand(StartPauseCurrentTimer, () => _selectedTimer != null);
        ResetCommand = new RelayCommand(ResetCurrentTimer, () => _selectedTimer != null);
    }

    public ObservableCollection<TimerViewModel> Timers { get; }

    public TimerViewModel? SelectedTimer
    {
        get => _selectedTimer;
        set
        {
            if (SetProperty(ref _selectedTimer, value)) UpdateMainDisplay();
        }
    }

    public string MainTimerDisplay
    {
        get => _mainTimerDisplay;
        private set => SetProperty(ref _mainTimerDisplay, value);
    }

    public string MainTimerName
    {
        get => _mainTimerName;
        private set => SetProperty(ref _mainTimerName, value);
    }

    public string StartPauseButtonText => _selectedTimer?.IsRunning == true ? "Pause" : "Start";

    public ICommand SelectTimerCommand { get; }
    public ICommand StartPauseCommand { get; }
    public ICommand ResetCommand { get; }

    public TimerViewModel? CreateTimerInline(string timerName)
    {
        if (!string.IsNullOrWhiteSpace(timerName))
        {
            var timer = _timerService.CreateTimer(timerName);
            var timerViewModel = new TimerViewModel(timer, _timerService, OnTimerRemoved);
            Timers.Add(timerViewModel);

            if (SelectedTimer == null) SelectTimer(timerViewModel);

            return timerViewModel;
        }

        return null;
    }

    private void SelectTimer(TimerViewModel? timer)
    {
        var previousTimer = _selectedTimer;

        // Clear selection from all timers
        foreach (var t in Timers) t.IsSelected = false;

        // Set selection on the new timer
        if (timer != null) timer.IsSelected = true;

        SelectedTimer = timer;

        // Notify about timer selection change for animation
        TimerSelectionChanged?.Invoke(previousTimer, timer);
    }

    public event Action<TimerViewModel?, TimerViewModel?>? TimerSelectionChanged;

    private void StartPauseCurrentTimer()
    {
        if (_selectedTimer?.IsRunning == true)
            _selectedTimer.PauseCommand.Execute(null);
        else
            _selectedTimer?.StartCommand.Execute(null);
    }

    private void ResetCurrentTimer()
    {
        _selectedTimer?.ResetCommand.Execute(null);
    }

    private void OnTimersUpdated(object? sender, EventArgs e)
    {
        foreach (var timer in Timers) timer.UpdateDisplayTime();

        UpdateMainDisplay();
    }

    private void OnTimerRemoved(TimerViewModel timerViewModel)
    {
        var wasSelected = _selectedTimer == timerViewModel;
        var timerIndex = Timers.IndexOf(timerViewModel);
        var previousSelectedTimer = _selectedTimer;

        Timers.Remove(timerViewModel);

        // If the removed timer was selected, auto-select next available timer
        if (wasSelected && Timers.Count > 0)
        {
            TimerViewModel? newSelectedTimer = null;

            // Try to select the timer that was after the deleted one
            if (timerIndex < Timers.Count)
                newSelectedTimer = Timers[timerIndex];
            // If that was the last timer, select the previous one
            else if (timerIndex > 0)
                newSelectedTimer = Timers[timerIndex - 1];
            // Otherwise select the first available timer
            else
                newSelectedTimer = Timers[0];

            if (newSelectedTimer != null)
                // Use Dispatcher to ensure timer list updates are processed first
                Application.Current.Dispatcher.BeginInvoke(() => { SelectTimer(newSelectedTimer); });
        }
        else if (wasSelected)
        {
            // No timers left, clear selection (will show empty state)
            SelectedTimer = null;
        }
    }

    private void UpdateMainDisplay()
    {
        if (_selectedTimer != null)
        {
            MainTimerDisplay = _selectedTimer.DisplayTime;
            MainTimerName = _selectedTimer.Name;
        }
        else
        {
            MainTimerDisplay = "00:00:00";
            MainTimerName = "Wählen Sie ein Ticket aus";
        }

        OnPropertyChanged(nameof(StartPauseButtonText));
    }
}