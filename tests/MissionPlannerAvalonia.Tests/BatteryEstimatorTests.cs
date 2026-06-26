using MissionPlannerAvalonia.Services;

namespace MissionPlannerAvalonia.Tests;

public class BatteryEstimatorTests {
  [Fact]
  public void Six_s_at_22_7v_is_not_full() {
    // 22.7V / 6 = 3.78 V/cell — the bug case showed 99%. Curve puts it in the 30-40% band.
    var pct = BatteryEstimator.EstimatePercent(22.7, 99);
    Assert.InRange(pct, 30, 45);
  }

  [Fact]
  public void Full_6s_pack_reads_100() {
    Assert.Equal(100, BatteryEstimator.EstimatePercent(25.2, 0), 0);
  }

  [Fact]
  public void Near_empty_6s_pack_reads_low() {
    // 6S at 3.6 V/cell (21.6V) — near the bottom of the usable range.
    Assert.InRange(BatteryEstimator.EstimatePercent(6 * 3.60, 50), 0, 12);
  }

  [Fact]
  public void Implausible_voltage_falls_back_to_firmware() {
    Assert.Equal(77, BatteryEstimator.EstimatePercent(0, 77), 0);
  }

  [Fact]
  public void Cell_count_inferred_for_4s() {
    // 4S at 15.2V = 3.8 V/cell -> 40%.
    Assert.Equal(40, BatteryEstimator.EstimatePercent(15.2, 0), 0);
  }
}
