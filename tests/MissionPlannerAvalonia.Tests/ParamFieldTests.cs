using MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

namespace MissionPlannerAvalonia.Tests;

public class ParamFieldTests {
  [Fact]
  public void Unknown_param_offline_is_not_present_and_numeric_by_default() {
    var f = new ParamField("ZZ_DOES_NOT_EXIST");
    Assert.False(f.Exists);
    Assert.True(f.IsNumeric);
    Assert.False(f.IsBool);
    Assert.False(f.IsCombo);
  }

  [Fact]
  public void Kind_bool_sets_bool_field() {
    var f = new ParamField("ZZ_BOOL", "bool");
    Assert.True(f.IsBool);
    Assert.False(f.IsNumeric);
  }

  [Fact]
  public void Kind_combo_sets_combo_field() {
    var f = new ParamField("ZZ_COMBO", "combo");
    Assert.True(f.IsCombo);
    Assert.False(f.IsNumeric);
  }
}
