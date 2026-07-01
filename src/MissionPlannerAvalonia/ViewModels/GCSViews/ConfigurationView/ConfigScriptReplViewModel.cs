using System;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlannerAvalonia.Services;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigScriptReplViewModel : ViewModelBase {
  private readonly StringBuilder _buffer = new();
  private readonly LuaScriptHost _host = new();

  [ObservableProperty]
  private string _input = "";

  [ObservableProperty]
  private string _output = "";

  [ObservableProperty]
  private bool _autoScroll = true;

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(RunCommand))]
  [NotifyCanExecuteChangedFor(nameof(AbortCommand))]
  private bool _running;

  public ConfigScriptReplViewModel() {
    _host.Output += OnHostOutput;
    AppendLine("Mission Planner Script REPL (Lua / MoonSharp)");
    AppendLine("Bound globals: comPort (MAVLinkInterface), cs (current state).");
    AppendLine("Use mp_should_abort() / mp_check_abort() inside loops to honour Abort.");
    AppendLine("");
  }

  private bool NotRunning => !Running;

  [RelayCommand(CanExecute = nameof(NotRunning))]
  private async Task Run() {
    string code = Input ?? "";
    if (code.Length == 0) {
      return;
    }

    AppendLine("> " + code);
    Input = "";
    Running = true;
    try {
      await _host.RunAsync(code);
    } catch (Exception ex) {
      AppendLine("Error: " + ex.Message);
    } finally {
      Running = false;
    }
  }

  [RelayCommand(CanExecute = nameof(Running))]
  private void Abort() {
    _host.Abort();
    AppendLine("(abort requested)");
  }

  [RelayCommand]
  private void Clear() {
    _buffer.Clear();
    Output = "";
  }

  private void OnHostOutput(string text) {
    if (Dispatcher.UIThread.CheckAccess()) {
      AppendLine(text);
    } else {
      Dispatcher.UIThread.Post(() => AppendLine(text));
    }
  }

  private void AppendLine(string text) {
    _buffer.Append(text);
    _buffer.Append(Environment.NewLine);
    Output = _buffer.ToString();
  }
}
