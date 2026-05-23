using System.Windows;
using System.Windows.Controls;

namespace PhotoBIZ.WindowsAgent.ControlCenter;

public partial class MainWindow : Window
{
    private readonly AgentControlCenterViewModel viewModel;
    private bool loaded;

    public MainWindow(AgentControlCenterViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        await viewModel.InitializeAsync();
    }

    private void OnAgentCredentialChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            viewModel.AgentCredential = passwordBox.Password;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AgentControlCenterViewModel.AgentCredential) &&
            string.IsNullOrEmpty(viewModel.AgentCredential) &&
            !string.IsNullOrEmpty(AgentCredentialBox.Password))
        {
            AgentCredentialBox.Password = string.Empty;
        }
    }
}
