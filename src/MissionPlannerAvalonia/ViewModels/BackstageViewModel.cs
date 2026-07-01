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
      Func<bool>? visibleWhen = null,
      bool isHeader = false,
      string? badge = null
  ) {
    Header = header;
    _factory = factory;
    IsAdvanced = advanced;
    IsSub = sub;
    RequiresConnection = requiresConnection;
    VisibleWhen = visibleWhen;
    IsHeader = isHeader;
    Badge = badge;
  }

  public string Header { get; }
  public bool IsAdvanced { get; }
  public bool IsSub { get; }
  public bool RequiresConnection { get; }

  public string? Badge { get; }
  public bool HasBadge => !string.IsNullOrEmpty(Badge);

  public bool IsHeader { get; }
  public BackstagePage? Group { get; set; }

  [ObservableProperty]
  private bool _isExpanded = true;

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

  private readonly string? _persistKey;

  public GCSViews.ConfigurationView.ConfigParamLoadingViewModel ParamLoading { get; } = new();

  [ObservableProperty]
  private bool _showParamLoading;

  private readonly Avalonia.Threading.DispatcherTimer _paramLoadTimer;

  protected BackstageViewModel(string? persistKey = null) {
    _persistKey = persistKey;
    AppState.ConnectionChanged += OnConnectionChanged;
    _paramLoadTimer = new Avalonia.Threading.DispatcherTimer {
      Interval = TimeSpan.FromMilliseconds(300),
    };
    _paramLoadTimer.Tick += (_, _) =>
        ShowParamLoading = AppState.IsConnected && !ParamLoading.GotAllParams;
    _paramLoadTimer.Start();
  }

  private void OnConnectionChanged() {
    if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) {
      RefreshVisibility();
    } else {
      Avalonia.Threading.Dispatcher.UIThread.Post(RefreshVisibility);
    }
  }

  protected void RefreshVisibility() {
    bool connected = AppState.IsConnected;
    foreach (var p in Pages) {
      bool vis = (!p.RequiresConnection || connected) && (p.VisibleWhen?.Invoke() ?? true);

      p.Visible = vis && (p.Group?.IsExpanded ?? true);
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
    if (page == null) {
      return;
    }

    if (page.IsHeader) {
      page.IsExpanded = !page.IsExpanded;
      RefreshVisibility();
      return;
    }
    SelectedPage = page;
  }

  private BackstagePage? _currentGroup;

  protected BackstagePage Add(
      string header,
      Func<ViewModelBase> factory,
      bool advanced = false,
      bool sub = false,
      bool requiresConnection = false,
      Func<bool>? visibleWhen = null,
      string? badge = null
  ) {
    bool isHeader = header.StartsWith(">>", StringComparison.Ordinal);
    var p = new BackstagePage(header, factory, advanced, sub, requiresConnection, visibleWhen, isHeader, badge);
    if (isHeader) {
      _currentGroup = p;
    } else if (sub) {
      p.Group = _currentGroup;
    }
    Pages.Add(p);
    return p;
  }

  protected void SelectFirst() {
    RefreshVisibility();
    if (SelectedPage != null) {
      return;
    }

    if (_persistKey != null
        && MissionPlanner.Utilities.Settings.Instance[_persistKey] is { Length: > 0 } last) {
      foreach (var p in Pages) {
        if (p.Visible && !p.IsSub && !p.IsHeader && p.Header == last) {
          SelectedPage = p;
          return;
        }
      }
    }
    foreach (var p in Pages) {
      if (p.Visible && !p.IsHeader) {
        SelectedPage = p;
        break;
      }
    }
  }
}
