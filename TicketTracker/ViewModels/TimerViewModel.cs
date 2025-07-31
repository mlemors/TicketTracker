using System.Windows.Input;
using TicketTracker.Commands;
using TicketTracker.Models;
using TicketTracker.Services;

namespace TicketTracker.ViewModels;

public class TimerViewModel : BaseViewModel
{
    private readonly Action<TimerViewModel>? _onRemove;
    private readonly TimerModel _timer;
    private readonly TimerService _timerService;
    private string _displayTime = "00:00:00.0";
    private bool _isSelected;

    public TimerViewModel(TimerModel timer, TimerService timerService, Action<TimerViewModel>? onRemove = null)
    {
        _timer = timer;
        _timerService = timerService;
        _onRemove = onRemove;

        StartCommand = new RelayCommand(StartCommand_Execute, () => !_timer.IsRunning);
        PauseCommand = new RelayCommand(Pause, () => _timer.IsRunning);
        ResetCommand = new RelayCommand(Reset);
        RemoveCommand = new RelayCommand(Remove);
        OpenTicketCommand = new RelayCommand(OpenTicketLink, () => !string.IsNullOrWhiteSpace(Name));

        UpdateDisplayTime();
    }

    public string Id => _timer.Id;
    public string Name => _timer.Name;
    public bool IsRunning => _timer.IsRunning;
    public TimeSpan ElapsedTime => _timer.GetCurrentElapsedTime();
    public DateTime? StartTime => _timer.StartTime;
    public DateTime? LastStartTime => _timer.LastStartTime;

    public string DisplayTime
    {
        get => _displayTime;
        private set => SetProperty(ref _displayTime, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public ICommand StartCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand OpenTicketCommand { get; }

    public void UpdateDisplayTime()
    {
        var elapsed = _timer.GetCurrentElapsedTime();
        DisplayTime = FormatTime(elapsed);
        OnPropertyChanged(nameof(IsRunning));
    }

    public void Start()
    {
        _timerService.StartTimer(_timer);
        OnPropertyChanged(nameof(IsRunning));
    }

    public void SetElapsedTime(TimeSpan elapsedTime)
    {
        _timer.ElapsedTime = elapsedTime;
        UpdateDisplayTime();
    }

    public void SetStartTime(DateTime? startTime)
    {
        _timer.StartTime = startTime;
        OnPropertyChanged(nameof(StartTime));
    }

    public void SetLastStartTime(DateTime? lastStartTime)
    {
        _timer.LastStartTime = lastStartTime;
        OnPropertyChanged(nameof(LastStartTime));
    }

    private void StartCommand_Execute()
    {
        Start();
    }

    private void Pause()
    {
        _timerService.PauseTimer(_timer);
        OnPropertyChanged(nameof(IsRunning));
    }

    private void Reset()
    {
        _timerService.ResetTimer(_timer);
        OnPropertyChanged(nameof(IsRunning));
        UpdateDisplayTime();
    }

    private void Remove()
    {
        _timerService.RemoveTimer(_timer);
        _onRemove?.Invoke(this);
    }

    private void OpenTicketLink()
    {
        if (!string.IsNullOrWhiteSpace(Name))
        {
            var url = $"https://jtl-software.atlassian.net/browse/{Name}";
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { /* Fehlerbehandlung falls gewünscht */ }
        }
    }

    private static string FormatTime(TimeSpan timeSpan)
    {
        return $"{(int)timeSpan.TotalHours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
    }
}