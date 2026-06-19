using System.Windows.Controls;
using Compass.App.ViewModels;

namespace Compass.App.Views;

public partial class SettingsView : Page
{
    public SettingsView(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
