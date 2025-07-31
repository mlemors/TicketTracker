using System.Windows;
using System.Windows.Controls;

namespace TicketTracker.Views;

public partial class EmptyStateView : UserControl
{
    public EmptyStateView()
    {
        InitializeComponent();
    }

    public event EventHandler? CreateTicketRequested;

    private void CreateTicketButton_Click(object sender, RoutedEventArgs e)
    {
        CreateTicketRequested?.Invoke(this, EventArgs.Empty);
    }
}