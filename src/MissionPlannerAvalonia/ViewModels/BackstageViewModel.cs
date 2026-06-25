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
      bool requiresConnection = false
  ) {
    Header = header;
    _factory = factory;
    IsAdvanced = advanced;
    IsSub = sub;
    RequiresConnection = requiresConnection;
  }

  public string Header { get; }
  public bool IsAdvanced { get; }
  public bool IsSub { get; }
  public bool RequiresConnection { get; }

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

  protected BackstageViewModel() {
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
      p.Visible = !p.RequiresConnection || connected;
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
      bool requiresConnection = false
  ) {
    var p = new BackstagePage(header, factory, advanced, sub, requiresConnection);
    Pages.Add(p);
    return p;
  }

  protected void SelectFirst() {
    RefreshVisibility();
    if (SelectedPage == null) {
      foreach (var p in Pages) {
        if (p.Visible) {
          SelectedPage = p;
          break;
        }
      }
    }
  }
}
