using System.Collections.ObjectModel;
using System.Windows.Threading;
using TicketTracker.Models;

namespace TicketTracker.Services;

public class TimerService
{
    private readonly ObservableCollection<TimerModel> _timers;
    private readonly DispatcherTimer _updateTimer;

    public TimerService()
    {
        _timers = new ObservableCollection<TimerModel>();
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _updateTimer.Tick += OnTimerTick;
        _updateTimer.Start();
    }

    public event EventHandler? TimersUpdated;

    public ObservableCollection<TimerModel> GetTimers()
    {
        return _timers;
    }

    public TimerModel CreateTimer(string name)
    {
        var timer = new TimerModel { Name = name };
        _timers.Add(timer);
        return timer;
    }

    public void StartTimer(TimerModel timer)
    {
        if (timer.IsRunning) return;

        var startTime = DateTime.Now;
        timer.StartTime = startTime;
        timer.LastStartTime = startTime;
        timer.IsRunning = true;
    }

    public void PauseTimer(TimerModel timer)
    {
        if (!timer.IsRunning) return;

        timer.ElapsedTime = timer.GetCurrentElapsedTime();
        timer.StartTime = null;
        // Keep LastStartTime to preserve the information
        timer.IsRunning = false;
    }

    public void ResetTimer(TimerModel timer)
    {
        timer.ElapsedTime = TimeSpan.Zero;
        timer.StartTime = null;
        timer.LastStartTime = null;
        timer.IsRunning = false;
    }

    public void RemoveTimer(TimerModel timer)
    {
        _timers.Remove(timer);
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        TimersUpdated?.Invoke(this, EventArgs.Empty);
    }
}