using System.Windows.Controls;
using Compass.App.ViewModels;

namespace Compass.App.Views;

public partial class InsightsView : Page
{
    public InsightsView(InsightsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
