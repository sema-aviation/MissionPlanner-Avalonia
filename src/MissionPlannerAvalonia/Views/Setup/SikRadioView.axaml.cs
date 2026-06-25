using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MissionPlannerAvalonia.Views.Setup;

public partial class SikRadioView : UserControl {
  public SikRadioView() {
    InitializeComponent();
  }

  private void InitializeComponent() {
    AvaloniaXamlLoader.Load(this);
  }
}
