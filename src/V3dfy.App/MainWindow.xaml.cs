using System.Windows;
using V3dfy.App.ViewModels;

namespace V3dfy.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            e.Data.GetData(DataFormats.FileDrop) is string[] { Length: 1 } files)
        {
            viewModel.SelectDroppedVideo(files[0]);
        }
    }
}
