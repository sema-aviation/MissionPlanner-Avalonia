using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlannerAvalonia.ViewModels;

public partial class LogBrowseViewModel : ViewModelBase {
  [ObservableProperty]
  private string _info = "Open a .tlog or .bin dataflash log.";

  public void LoadFile(string path) {
    var fi = new FileInfo(path);
    Info = $"{fi.Name}\n{fi.Length / 1024.0 / 1024.0:0.0} MB\n{fi.FullName}";
  }
}
