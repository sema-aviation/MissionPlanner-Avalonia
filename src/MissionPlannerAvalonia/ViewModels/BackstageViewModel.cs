using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MissionPlannerAvalonia.ViewModels;

public partial class BackstagePage : ObservableObject {
  private readonly Func<ViewModelBase> _factory;
  private ViewModelBase? _content;

  public BackstagePage(
      string header,
      Func<ViewModelBase> factory,
      bool advanced = false,
      bool sub = false,
      bool requiresConnection = false,
      Func<bool>? visibleWhen = null
  ) {
    Header = header;
    _factory = factory;
    IsAdvanced = advanced;
    IsSub = sub;
    RequiresConnection = requiresConnection;
    VisibleWhen = visibleWhen;
  }

  public string Header { get; }
  public bool IsAdvanced { get; }
  public bool IsSub { get; }
  public bool RequiresConnection { get; }

  // Optional firmware/vehicle conditioning, mirroring MP's display* flags (e.g. heli-only,
  // plane-only pages). Evaluated alongside connection gating in RefreshVisibility.
  public Func<bool>? VisibleWhen { get; }

  [ObservableProperty]
  private bool _isSelected;

  [ObservableProperty]
  private bool _visible = true;

  public ViewModelBase Content => _content ??= _factory();
}

public partial class BackstageViewModel : ViewModelBase {
  public ObservableCollection<BackstagePage> Pages { get; } = new();

  [ObservableProperty]
  private BackstagePage? _selectedPage;

  [ObservableProperty]
  private ViewModelBase? _currentContent;

  // Settings key under which the last-selected page header is remembered (per backstage).
  private readonly string? _persistKey;

  protected BackstageViewModel(string? persistKey = null) {
    _persistKey = persistKey;
    AppState.ConnectionChanged += OnConnectionChanged;
  }

  private void OnConnectionChanged() {
    if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) {
      RefreshVisibility();
    } else {
      Avalonia.Threading.Dispatcher.UIThread.Post(RefreshVisibility);
    }
  }

  // Hide connection-gated pages until a vehicle is connected, mirroring MP's
  // disconnected nav. Re-select the first visible page if the current one hides.
  protected void RefreshVisibility() {
    bool connected = AppState.IsConnected;
    foreach (var p in Pages) {
      p.Visible = (!p.RequiresConnection || connected) && (p.VisibleWhen?.Invoke() ?? true);
    }
    if (SelectedPage is { Visible: false }) {
      SelectedPage = null;
      SelectFirst();
    }
  }

  partial void OnSelectedPageChanged(BackstagePage? oldValue, BackstagePage? newValue) {
    if (oldValue != null)
      oldValue.IsSelected = false;
    if (newValue != null) {
      newValue.IsSelected = true;
      CurrentContent = newValue.Content;
      if (_persistKey != null && !newValue.IsSub) {
        MissionPlanner.Utilities.Settings.Instance[_persistKey] = newValue.Header;
      }
    }
  }

  [RelayCommand]
  private void Select(BackstagePage? page) {
    if (page != null) {
      SelectedPage = page;
    }
  }

  protected BackstagePage Add(
      string header,
      Func<ViewModelBase> factory,
      bool advanced = false,
      bool sub = false,
      bool requiresConnection = false,
      Func<bool>? visibleWhen = null
  ) {
    var p = new BackstagePage(header, factory, advanced, sub, requiresConnection, visibleWhen);
    Pages.Add(p);
    return p;
  }

  protected void SelectFirst() {
    RefreshVisibility();
    if (SelectedPage != null) {
      return;
    }
    // Restore the last-visited page when remembered and still visible (MP last-page memory).
    if (_persistKey != null
        && MissionPlanner.Utilities.Settings.Instance[_persistKey] is { Length: > 0 } last) {
      foreach (var p in Pages) {
        if (p.Visible && !p.IsSub && p.Header == last) {
          SelectedPage = p;
          return;
        }
      }
    }
    foreach (var p in Pages) {
      if (p.Visible) {
        SelectedPage = p;
        break;
      }
    }
  }
}
