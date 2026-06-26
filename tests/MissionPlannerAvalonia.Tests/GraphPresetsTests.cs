using System.Linq;
using MissionPlannerAvalonia.Services;

namespace MissionPlannerAvalonia.Tests;

public class GraphPresetsTests {
  [Fact]
  public void Parses_name_and_curves_with_axis_suffix() {
    const string xml = """
      <graphs>
        <graph name="Test/Velocity"><expression>NKF1.VE NKF3.IVE:2</expression></graph>
      </graphs>
      """;
    var presets = GraphPresets.Parse(xml);
    Assert.Single(presets);
    Assert.Equal("Test/Velocity", presets[0].Name);
    Assert.Equal(2, presets[0].Curves.Count);
    Assert.Equal("NKF1.VE", presets[0].Curves[0].Expression);
    Assert.Equal(1, presets[0].Curves[0].Axis);
    Assert.Equal("NKF3.IVE", presets[0].Curves[1].Expression);
    Assert.Equal(2, presets[0].Curves[1].Axis);
  }

  [Fact]
  public void Keeps_derived_math_expressions_intact() {
    var curves = GraphPresets.ParseCurves("sqrt(XKF1.PE**2+XKF1.PN**2) ATT.DesRoll-ATT.Roll:2");
    Assert.Equal("sqrt(XKF1.PE**2+XKF1.PN**2)", curves[0].Expression);
    Assert.Equal(1, curves[0].Axis);
    Assert.Equal("ATT.DesRoll-ATT.Roll", curves[1].Expression);
    Assert.Equal(2, curves[1].Axis);
  }
}
