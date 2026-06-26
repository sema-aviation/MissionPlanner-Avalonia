using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MissionPlannerAvalonia.Views;

public partial class MAVLinkInspectorView : UserControl {
  public MAVLinkInspectorView() {
    AvaloniaXamlLoader.Load(this);
  }
}
