using System.Windows;
using IPCE.Desktop.ViewModels;

namespace IPCE.Desktop;

public partial class MainWindow : Window
{
    private readonly bool _loadStartupDefaults;

    public MainWindow(bool loadStartupDefaults = true)
        : this(new MainViewModel(), loadStartupDefaults)
    {
    }

    public MainWindow(
        MainViewModel viewModel,
        bool loadStartupDefaults = true)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(
            nameof(viewModel));
        _loadStartupDefaults = loadStartupDefaults;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(
        object sender,
        RoutedEventArgs eventArgs)
    {
        Loaded -= OnLoaded;
        if (!_loadStartupDefaults ||
            DataContext is not MainViewModel viewModel)
        {
            return;
        }

        try
        {
            await viewModel.LoadStartupDefaultsAsync();
        }
        catch
        {
            // The ViewModel exposes the user-facing startup error.
        }
    }
}
