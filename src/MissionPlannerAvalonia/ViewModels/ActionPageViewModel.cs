using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;

namespace MissionPlannerAvalonia.ViewModels;

public partial class ActionItem : ObservableObject {
  public ActionItem(string label, ICommand command) {
    Label = label;
    Command = command;
  }

  public string Label { get; }
  public ICommand Command { get; }
}

public partial class ActionPageViewModel : ViewModelBase {
  protected readonly MAVLinkInterface _comPort = AppState.comPort;

  public string Title { get; protected set; } = "";
  public string Instructions { get; protected set; } = "";
  public ObservableCollection<ActionItem> Actions { get; } = new();

  [ObservableProperty]
  private string _log = "";

  public bool IsConnected => _comPort.BaseStream?.IsOpen == true;

  protected ActionItem Action(string label, Action run) => AddAction(label, new RelayCommand(run));

  protected ActionItem Action(string label, ICommand cmd) => AddAction(label, cmd);

  private ActionItem AddAction(string label, ICommand cmd) {
    var a = new ActionItem(label, cmd);
    Actions.Add(a);
    return a;
  }

  protected void AppendLog(string line) {
    void Do() => Log += (Log.Length > 0 ? "\n" : "") + line;
    if (Dispatcher.UIThread.CheckAccess()) {
      Do();
    } else {
      Dispatcher.UIThread.Post(Do);
    }
  }

  protected bool RequireConnection() {
    if (IsConnected) {
      return true;
    }

    AppendLog("Not connected — connect a vehicle first.");
    return false;
  }
}
