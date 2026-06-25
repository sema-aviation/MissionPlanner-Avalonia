using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia;

[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator"
)]
public class ViewLocator : IDataTemplate {
  public Control? Build(object? param) {
    if (param is null) {
      return null;
    }

    var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
    var type = Type.GetType(name);

    if (type != null) {
      return (Control)Activator.CreateInstance(type)!;
    }

    if (param is ViewModels.GCSViews.ConfigurationView.ParamPageBase) {
      return new Controls.ParamFieldsView();
    }

    if (param is ViewModels.ActionPageViewModel) {
      return new Views.ActionPageView();
    }

    return new TextBlock { Text = "Not Found: " + name };
  }

  public bool Match(object? data) {
    return data is ViewModelBase;
  }
}
