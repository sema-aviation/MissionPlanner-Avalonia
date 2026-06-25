using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MissionPlannerAvalonia.ViewModels;

public partial class BackstagePage : ObservableObject {
  private readonly Func<ViewModelBase> _factory;
  private ViewModelBase? _content;

  public BackstagePage(string header, Func<ViewModelBase> factory, bool advanced = false, bool sub = false) {
    Header = header;
    _factory = factory;
    IsAdvanced = advanced;
    IsSub = sub;
  }

  public string Header { get; }
  public bool IsAdvanced { get; }
  public bool IsSub { get; }

  [ObservableProperty]
  private bool _isSelected;

  public ViewModelBase Content => _content ??= _factory();
}

public partial class BackstageViewModel : ViewModelBase {
  public ObservableCollection<BackstagePage> Pages { get; } = new();

  [ObservableProperty]
  private BackstagePage? _selectedPage;

  [ObservableProperty]
  private ViewModelBase? _currentContent;

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
      bool sub = false
  ) {
    var p = new BackstagePage(header, factory, advanced, sub);
    Pages.Add(p);
    return p;
  }

  protected void SelectFirst() {
    if (SelectedPage == null && Pages.Count > 0) {
      SelectedPage = Pages[0];
    }
  }
}
