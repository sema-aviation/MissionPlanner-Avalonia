using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;

namespace MissionPlannerAvalonia.ViewModels;

// Live MAVLink message inspector — 1:1 port of upstream Controls/MAVLinkInspector.cs.
// Groups the live packet stream as sysid -> compid -> message-type -> fields, each
// message-type header showing its Hz rate / msgid / Bps, refreshed on a UI-thread timer.
// Packets are accumulated off-thread via PacketInspector (exactly like upstream) and the
// tree is rebuilt at ~3 Hz to throttle redraw. Pause/resume + a message-type filter +
// optional GCS (sent) traffic mirror the upstream toolbar.
public partial class MAVLinkInspectorViewModel : ViewModelBase, IDisposable {
  private readonly MAVLinkInterface _mav = AppState.comPort;
  private readonly PacketInspector<MAVLink.MAVLinkMessage> _mavi = new();
  private readonly DispatcherTimer _timer;

  // sysid nodes; each carries a child map for O(1) lookup (mirrors TreeView.Nodes.Find).
  public ObservableCollection<InspectorNode> Tree { get; } = new();
  private readonly Dictionary<string, InspectorNode> _rootMap = new();

  [ObservableProperty]
  private bool _isPaused;

  [ObservableProperty]
  private bool _showGcsTraffic;

  [ObservableProperty]
  private string _filter = "";

  [ObservableProperty]
  private string _status = "Listening on the active MAVLink link…";

  public MAVLinkInspectorViewModel() {
    _mav.OnPacketReceived += MavOnPacketReceived;

    // Upstream uses a 333 ms WinForms Timer; match it (≈3 Hz redraw).
    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(333) };
    _timer.Tick += (_, _) => Rebuild();
    _timer.Start();
  }

  partial void OnShowGcsTrafficChanged(bool value) {
    // Mirror chk_gcstraffic: feed sent packets into the same inspector.
    _mav.OnPacketSent -= MavOnPacketReceived;
    if (value) {
      _mav.OnPacketSent += MavOnPacketReceived;
    }
  }

  partial void OnFilterChanged(string value) => Rebuild();

  private void MavOnPacketReceived(object? sender, MAVLink.MAVLinkMessage msg) {
    if (IsPaused) {
      return;
    }

    // Same accumulation call as upstream MavOnOnPacketReceived (off the UI thread).
    _mavi.Add(msg.sysid, msg.compid, msg.msgid, msg, msg.Length);
  }

  [RelayCommand]
  private void TogglePause() => IsPaused = !IsPaused;

  [RelayCommand]
  private void Clear() {
    _mavi.Clear();
    Tree.Clear();
    _rootMap.Clear();
  }

  public string PauseLabel => IsPaused ? "Resume" : "Pause";

  partial void OnIsPausedChanged(bool value) => OnPropertyChanged(nameof(PauseLabel));

  // Rebuild/refresh the tree from the accumulated packets (UI thread, throttled by the timer).
  private void Rebuild() {
    var filter = Filter?.Trim() ?? "";

    foreach (var m in _mavi.GetPacketMessages()) {
      var sysNode = GetOrAdd(_rootMap, Tree, m.sysid.ToString(),
          () => new InspectorNode { Key = m.sysid.ToString(), Header = "Vehicle " + m.sysid });

      var compKey = m.compid.ToString();
      var compNode = GetOrAdd(sysNode.Map, sysNode.Children, compKey,
          () => new InspectorNode {
            Key = compKey,
            Header = "Comp " + m.compid + " " + (MAVLink.MAV_COMPONENT)m.compid,
          });

      // Message-type filter: skip types whose name doesn't match.
      if (filter.Length > 0 &&
          m.msgtypename.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) {
        continue;
      }

      var msgKey = m.msgid.ToString();
      var msgNode = GetOrAdd(compNode.Map, compNode.Children, msgKey,
          () => new InspectorNode { Key = msgKey, Header = m.msgtypename });

      var header = m.msgtypename + " (" +
                   _mavi.SeenRate(m.sysid, m.compid, m.msgid).ToString("0.0 Hz") +
                   ", #" + m.msgid + ") " +
                   _mavi.SeenBps(m.sysid, m.compid, m.msgid).ToString("0Bps");
      if (msgNode.Header != header) {
        msgNode.Header = header;
      }

      var minfo = MAVLink.MAVLINK_MESSAGE_INFOS.GetMessageInfo(m.msgid);
      if (minfo.type == null) {
        continue;
      }

      foreach (var field in minfo.type.GetFields()) {
        var fieldNode = GetOrAdd(msgNode.Map, msgNode.Children, field.Name,
            () => new InspectorNode { Key = field.Name });

        object? value = field.GetValue(m.data);

        if (field.Name == "time_unix_usec") {
          var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
          try {
            value = epoch.AddMilliseconds((ulong)value! / 1000);
          } catch {
          }
        }

        if (field.FieldType.IsArray && value is Array arr) {
          if (field.Name is "param_id" or "text" or "model_name" or "vendor_name" or "uri" or
              "cam_definition_uri") {
            value = Encoding.ASCII.GetString((byte[])arr);
          } else {
            value = arr.Cast<object>().Aggregate("", (a, b) => a.Length == 0 ? b.ToString() : a + "," + b);
          }
        }

        var text = string.Format("{0,-32} {1,20} {2,-20}", field.Name, value, field.FieldType);
        if (fieldNode.Header != text) {
          fieldNode.Header = text;
        }
      }
    }
  }

  private static InspectorNode GetOrAdd(Dictionary<string, InspectorNode> map,
      ObservableCollection<InspectorNode> children, string key, Func<InspectorNode> factory) {
    if (map.TryGetValue(key, out var existing)) {
      return existing;
    }

    var node = factory();
    map[key] = node;
    InsertSorted(children, node);
    return node;
  }

  // Keep children ordered (mirrors upstream treeView1.Sort()).
  private static void InsertSorted(ObservableCollection<InspectorNode> children, InspectorNode node) {
    int i = 0;
    while (i < children.Count &&
           string.CompareOrdinal(children[i].Header, node.Header) < 0) {
      i++;
    }

    children.Insert(i, node);
  }

  public void Dispose() {
    _timer.Stop();
    _mav.OnPacketReceived -= MavOnPacketReceived;
    _mav.OnPacketSent -= MavOnPacketReceived;
  }
}

// One row in the inspector tree (sysid / compid / message-type / field). Header is the
// monospaced display text; Map gives fast child lookup by key.
public partial class InspectorNode : ObservableObject {
  public string Key { get; set; } = "";

  [ObservableProperty]
  private string _header = "";

  public ObservableCollection<InspectorNode> Children { get; } = new();
  public Dictionary<string, InspectorNode> Map { get; } = new();
}
