namespace MissionPlannerAvalonia.ViewModels;

public class InfoPageViewModel : ViewModelBase {
  public InfoPageViewModel(
      string title,
      string note = "Not yet ported. Logic available via AppState.comPort."
  ) {
    Title = title;
    Note = note;
  }

  public string Title { get; }
  public string Note { get; }
}
