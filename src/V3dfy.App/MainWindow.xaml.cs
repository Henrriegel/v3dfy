using System.Collections.Specialized;
using System.Windows;
using V3dfy.App.ViewModels;

namespace V3dfy.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ConversionLogs.CollectionChanged += OnConversionLogsChanged;
        }
    }

    private void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            !viewModel.IsConversionRunning &&
            e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] { Length: 1 } files)
        {
            viewModel.SelectDroppedVideo(files[0]);
        }
    }

    private void OnConversionLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel { IsConversionRunning: true } viewModel ||
            viewModel.ConversionLogs.Count == 0)
        {
            return;
        }

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (viewModel.ConversionLogs.Count > 0)
            {
                ConversionLiveLogList.ScrollIntoView(viewModel.ConversionLogs[^1]);
            }
        }));
    }
}
