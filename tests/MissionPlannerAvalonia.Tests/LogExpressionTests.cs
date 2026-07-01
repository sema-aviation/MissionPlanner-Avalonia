using System.Collections.Generic;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Tests;

public class LogExpressionTests {
  [Fact]
  public void IsExpression_detects_operators() {
    Assert.True(LogBrowseViewModel.IsExpression("BAT.Volt*BAT.Curr"));
    Assert.True(LogBrowseViewModel.IsExpression("ATT.Roll-ATT.Pitch"));
    Assert.False(LogBrowseViewModel.IsExpression("ATT.Roll"));
  }

  [Fact]
  public void EvalExpression_computes_product() {
    var refs = new[] { "BAT.Volt", "BAT.Curr" };
    var values = new Dictionary<string, double> { ["BAT.Volt"] = 22.2, ["BAT.Curr"] = 10.0 };
    Assert.Equal(222.0, LogBrowseViewModel.EvalExpression("BAT.Volt*BAT.Curr", refs, values)!.Value, 6);
  }

  [Fact]
  public void EvalExpression_handles_negative_values_and_precedence() {
    var refs = new[] { "ATT.Roll", "ATT.Pitch" };
    var values = new Dictionary<string, double> { ["ATT.Roll"] = 2.0, ["ATT.Pitch"] = -3.5 };

    Assert.Equal(5.5, LogBrowseViewModel.EvalExpression("ATT.Roll-ATT.Pitch", refs, values)!.Value, 6);
  }

  [Fact]
  public void EvalExpression_returns_null_on_divide_by_zero() {
    var refs = new[] { "X.a", "X.b" };
    var values = new Dictionary<string, double> { ["X.a"] = 1.0, ["X.b"] = 0.0 };
    Assert.Null(LogBrowseViewModel.EvalExpression("X.a/X.b", refs, values));
  }
}
