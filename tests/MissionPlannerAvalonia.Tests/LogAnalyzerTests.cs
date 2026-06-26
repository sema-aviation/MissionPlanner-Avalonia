using MissionPlannerAvalonia.Services;

namespace MissionPlannerAvalonia.Tests;

public class LogAnalyzerTests {
  [Fact]
  public void Classify_higher_is_worse() {
    Assert.Equal(LogTestStatus.Good, LogAnalyzer.Classify(10, 30, 60, higherWorse: true));
    Assert.Equal(LogTestStatus.Warn, LogAnalyzer.Classify(40, 30, 60, higherWorse: true));
    Assert.Equal(LogTestStatus.Fail, LogAnalyzer.Classify(70, 30, 60, higherWorse: true));
  }

  [Fact]
  public void Classify_lower_is_worse() {
    Assert.Equal(LogTestStatus.Good, LogAnalyzer.Classify(12, 7, 5, higherWorse: false));
    Assert.Equal(LogTestStatus.Warn, LogAnalyzer.Classify(6, 7, 5, higherWorse: false));
    Assert.Equal(LogTestStatus.Fail, LogAnalyzer.Classify(4, 7, 5, higherWorse: false));
  }

  [Fact]
  public void Classify_nan_is_na() {
    Assert.Equal(LogTestStatus.NA, LogAnalyzer.Classify(double.NaN, 30, 60, higherWorse: true));
  }
}
