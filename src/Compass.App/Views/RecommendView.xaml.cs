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
}
