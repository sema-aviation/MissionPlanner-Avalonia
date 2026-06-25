using System;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigScriptReplViewModel : ViewModelBase {
  private readonly StringBuilder _buffer = new();

  [ObservableProperty]
  private string _input = "";

  [ObservableProperty]
  private string _output = "";

  [ObservableProperty]
  private bool _autoScroll = true;

  public ConfigScriptReplViewModel() {
    AppendLine("Mission Planner Script REPL (IronPython)");
    AppendLine("Engine status: the embedded Python (IronPython) engine is not available in this build.");
  }

  [RelayCommand]
  private void Run() {
    string code = Input ?? "";
    if (code.Length == 0) {
      return;
    }

    AppendLine(">>> " + code);
    AppendLine("Python scripting is not available in this build (IronPython engine not bundled).");
    Input = "";
  }

  [RelayCommand]
  private void Clear() {
    _buffer.Clear();
    Output = "";
  }

  private void AppendLine(string text) {
    _buffer.Append(text);
    _buffer.Append(Environment.NewLine);
    Output = _buffer.ToString();
  }
}
