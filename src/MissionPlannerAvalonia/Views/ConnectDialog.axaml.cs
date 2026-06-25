using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace MissionPlannerAvalonia.Views;

public partial class ConnectDialog : Window {
  public ConnectDialog() {
    AvaloniaXamlLoader.Load(this);
    OkButton.Click += (_, _) => Close(Result());
    CancelButton.Click += (_, _) => Close(null);
  }

  private string?[] Result() => new[] { Value1.Text, Value2.Text };

  // label2 == null hides the second field (single-value prompts like WS url / UDP port).
  public static async Task<string?[]?> Show(
      Window owner, string title, string label1, string value1, string? label2, string value2) {
    var dlg = new ConnectDialog();
    dlg.TitleText.Text = title;
    dlg.Label1.Text = label1;
    dlg.Value1.Text = value1;
    if (label2 == null) {
      dlg.Field2Panel.IsVisible = false;
    } else {
      dlg.Label2.Text = label2;
      dlg.Value2.Text = value2;
    }
    return await dlg.ShowDialog<string?[]?>(owner);
  }
}
