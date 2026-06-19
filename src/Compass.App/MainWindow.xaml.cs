using System.Windows;
using Compass.App.ViewModels;
using Wpf.Ui.Controls;

namespace Compass.App;

public partial class MainWindow : FluentWindow
{
    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
