namespace TicketTracker.Models;

public class TimerModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; init; } = string.Empty;
    public TimeSpan ElapsedTime { get; set; } = TimeSpan.Zero;
    public DateTime? StartTime { get; set; }
    public DateTime? LastStartTime { get; set; }
    public bool IsRunning { get; set; }

    public TimeSpan GetCurrentElapsedTime()
    {
        if (!IsRunning || StartTime == null)
            return ElapsedTime;

        return ElapsedTime + (DateTime.Now - StartTime.Value);
    }
}