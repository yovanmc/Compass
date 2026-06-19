using System.Windows.Controls;
using Compass.App.ViewModels;

namespace Compass.App.Views;

public partial class RecommendView : Page
{
    public RecommendView(RecommendViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    // Bindings inside a MouseBinding can't resolve a VM command (InputBindings are
    // outside the visual tree and don't inherit DataContext), so route card clicks here.
    private void Card_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement fe
            && fe.DataContext is RecommendationRow row
            && DataContext is RecommendViewModel vm)
        {
            vm.OpenDetailCommand.Execute(row);
        }
    }
}
