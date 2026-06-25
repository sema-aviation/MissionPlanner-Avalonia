using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigExtendedTuningViewModel : TuningPageBase {
  [ObservableProperty]
  private bool _lockRollPitch = true;

  private bool _mirroring;

  public ConfigExtendedTuningViewModel() {
    Title = "Extended Tuning";
    Intro = "Copter / QuadPlane PID gains and tuning. "
            + "Fields show n/a if not present on this firmware.";
    Rebuild();
  }

  protected override void Build() {
    Groups.Add(new TuningGroup("Transmitter Tuning")
        .Combo("Tune", "TUNE")
        .Num("Tune Min", "TUNE_LOW", "TUNE_MIN")
        .Num("Tune Max", "TUNE_HIGH", "TUNE_MAX")
        .Combo("CH6 Opt", "CH6_OPT", "CH6_OPTION", "RC6_OPTION")
        .Combo("CH7 Opt", "CH7_OPT", "CH7_OPTION", "RC7_OPTION")
        .Combo("CH8 Opt", "CH8_OPT", "CH8_OPTION", "RC8_OPTION")
        .Combo("CH9 Opt", "CH9_OPT", "CH9_OPTION", "RC9_OPTION")
        .Combo("CH10 Opt", "CH10_OPT", "CH10_OPTION", "RC10_OPTION"));

    Groups.Add(new TuningGroup("Rate Roll")
        .Num("P", "RATE_RLL_P", "ATC_RAT_RLL_P", "Q_A_RAT_RLL_P")
        .Num("I", "RATE_RLL_I", "ATC_RAT_RLL_I", "Q_A_RAT_RLL_I")
        .Num("IMAX", "ATC_RAT_RLL_IMAX", "Q_A_RAT_RLL_IMAX", "RATE_RLL_IMAX")
        .Num("D", "RATE_RLL_D", "ATC_RAT_RLL_D", "Q_A_RAT_RLL_D")
        .Num("FLTE", "RATE_RLL_FILT", "ATC_RAT_RLL_FILT", "ATC_RAT_RLL_FLTE", "Q_A_RAT_RLL_FLTE")
        .Num("FLTD", "ATC_RAT_RLL_FLTD", "Q_A_RAT_RLL_FLTD")
        .Num("FLTT", "ATC_RAT_RLL_FLTT", "Q_A_RAT_RLL_FLTT"));

    Groups.Add(new TuningGroup("Rate Pitch")
        .Num("P", "RATE_PIT_P", "ATC_RAT_PIT_P", "Q_A_RAT_PIT_P")
        .Num("I", "RATE_PIT_I", "ATC_RAT_PIT_I", "Q_A_RAT_PIT_I")
        .Num("IMAX", "ATC_RAT_PIT_IMAX", "Q_A_RAT_PIT_IMAX", "RATE_PIT_IMAX")
        .Num("D", "RATE_PIT_D", "ATC_RAT_PIT_D", "Q_A_RAT_PIT_D")
        .Num("FLTE", "RATE_PIT_FILT", "ATC_RAT_PIT_FILT", "ATC_RAT_PIT_FLTE", "Q_A_RAT_PIT_FLTE")
        .Num("FLTD", "ATC_RAT_PIT_FLTD", "Q_A_RAT_PIT_FLTD")
        .Num("FLTT", "ATC_RAT_PIT_FLTT", "Q_A_RAT_PIT_FLTT"));

    Groups.Add(new TuningGroup("Rate Yaw")
        .Num("P", "RATE_YAW_P", "ATC_RAT_YAW_P", "Q_A_RAT_YAW_P")
        .Num("I", "RATE_YAW_I", "ATC_RAT_YAW_I", "Q_A_RAT_YAW_I")
        .Num("IMAX", "ATC_RAT_YAW_IMAX", "Q_A_RAT_YAW_IMAX", "RATE_YAW_IMAX")
        .Num("D", "RATE_YAW_D", "ATC_RAT_YAW_D", "Q_A_RAT_YAW_D")
        .Num("FLTE", "RATE_YAW_FILT", "ATC_RAT_YAW_FILT", "ATC_RAT_YAW_FLTE", "Q_A_RAT_YAW_FLTE")
        .Num("FLTD", "ATC_RAT_YAW_FLTD", "Q_A_RAT_YAW_FLTD")
        .Num("FLTT", "ATC_RAT_YAW_FLTT", "Q_A_RAT_YAW_FLTT"));

    Groups.Add(new TuningGroup("Stabilize Roll (Error to Rate)")
        .Num("P", "STB_RLL_P", "ATC_ANG_RLL_P", "Q_A_ANG_RLL_P")
        .Num("ACCEL MAX", "ATC_ACCEL_R_MAX", "Q_A_ACCEL_R_MAX", "ATC_ACC_R_MAX", "Q_A_ACC_R_MAX"));

    Groups.Add(new TuningGroup("Stabilize Pitch (Error to Rate)")
        .Num("P", "STB_PIT_P", "ATC_ANG_PIT_P", "Q_A_ANG_PIT_P")
        .Num("ACCEL MAX", "ATC_ACCEL_P_MAX", "Q_A_ACCEL_P_MAX", "ATC_ACC_P_MAX", "Q_A_ACC_P_MAX"));

    Groups.Add(new TuningGroup("Stabilize Yaw (Error to Rate)")
        .Num("P", "STB_YAW_P", "ATC_ANG_YAW_P", "Q_A_ANG_YAW_P")
        .Num("ACCEL MAX", "ATC_ACCEL_Y_MAX", "Q_A_ACCEL_Y_MAX", "ATC_ACC_Y_MAX", "Q_A_ACC_Y_MAX"));

    Groups.Add(new TuningGroup("Throttle Accel (Accel to motor)")
        .Num("P", "THR_ACCEL_P", "ACCEL_Z_P", "PSC_ACCZ_P", "Q_P_ACCZ_P", "PSC_D_ACC_P", "Q_P_D_ACC_P")
        .Num("I", "THR_ACCEL_I", "ACCEL_Z_I", "PSC_ACCZ_I", "Q_P_ACCZ_I", "PSC_D_ACC_I", "Q_P_D_ACC_I")
        .Num("IMAX", "THR_ACCEL_IMAX", "ACCEL_Z_IMAX", "PSC_ACCZ_IMAX", "Q_P_ACCZ_IMAX", "PSC_D_ACC_IMAX", "Q_P_D_ACC_IMAX")
        .Num("D", "THR_ACCEL_D", "ACCEL_Z_D", "PSC_ACCZ_D", "Q_P_ACCZ_D", "PSC_D_ACC_D", "Q_P_D_ACC_D"));

    Groups.Add(new TuningGroup("Throttle Rate (VSpd to accel)")
        .Num("P", "THR_RATE_P", "VEL_Z_P", "PSC_VELZ_P", "Q_P_VELZ_P", "PSC_D_VEL_P", "Q_P_D_VEL_P"));

    Groups.Add(new TuningGroup("Altitude Hold (Alt to climbrate)")
        .Num("P", "THR_ALT_P", "POS_Z_P", "PSC_POSZ_P", "Q_P_POSZ_P", "PSC_D_POS_P", "Q_P_D_POS_P"));

    Groups.Add(new TuningGroup("Velocity XY (Vel to Accel)")
        .Num("P", "LOITER_LAT_P", "VEL_XY_P", "PSC_VELXY_P", "Q_P_VELXY_P", "PSC_NE_VEL_P", "Q_P_NE_VEL_P")
        .Num("I", "LOITER_LAT_I", "VEL_XY_I", "PSC_VELXY_I", "Q_P_VELXY_I", "PSC_NE_VEL_I", "Q_P_NE_VEL_I")
        .Num("IMAX", "LOITER_LAT_IMAX", "VEL_XY_IMAX", "PSC_VELXY_IMAX", "Q_P_VELXY_IMAX", "PSC_NE_VEL_IMAX", "Q_P_NE_VEL_IMAX")
        .Num("D", "LOITER_LAT_D", "PSC_VELXY_D", "Q_P_VELXY_D", "PSC_NE_VEL_D", "Q_P_NE_VEL_D"));

    Groups.Add(new TuningGroup("Position XY (Dist to Speed)")
        .Num("P", "HLD_LAT_P", "POS_XY_P", "PSC_POSXY_P", "Q_P_POSXY_P")
        .Num("INPUT TC", "ATC_INPUT_TC", "Q_A_INPUT_TC"));

    Groups.Add(new TuningGroup("WPNav (cm's)")
        .Num("Speed", "WPNAV_SPEED", "Q_WP_SPEED", "WP_SPD", "Q_WP_SPD")
        .Num("Radius", "WPNAV_RADIUS", "Q_WP_RADIUS", "WP_RADIUS_M", "Q_WP_RADIUS_M")
        .Num("Speed Dn", "WPNAV_SPEED_DN", "Q_WP_SPEED_DN", "WP_SPD_DN", "Q_WP_SPD_DN")
        .Num("Speed Up", "WPNAV_SPEED_UP", "Q_WP_SPEED_UP", "WP_SPD_UP", "Q_WP_SPD_UP")
        .Num("Loiter Speed", "WPNAV_LOIT_SPEED", "LOIT_SPEED", "Q_LOIT_SPEED", "LOIT_SPEED_MS", "Q_LOIT_SPEED_MS"));

    Groups.Add(new TuningGroup("Basic Filters")
        .Num("Gyro", "INS_GYRO_FILTER")
        .Num("Accel", "INS_ACCEL_FILTER"));

    Groups.Add(new TuningGroup("Static Notch Filter")
        .Combo("Enabled", "INS_NOTCH_ENABLE")
        .Num("Frequency", "INS_NOTCH_FREQ")
        .Num("BandWidth", "INS_NOTCH_BW")
        .Num("Attenuation", "INS_NOTCH_ATT"));

    Groups.Add(new TuningGroup("Harmonic Notch Filter")
        .Combo("Enabled", "INS_HNTCH_ENABLE")
        .Combo("Mode", "INS_HNTCH_MODE")
        .Num("Reference", "INS_HNTCH_REF")
        .Num("Frequency", "INS_HNTCH_FREQ")
        .Num("Attenuation", "INS_HNTCH_ATT")
        .Num("Bandwidth", "INS_HNTCH_BW")
        .Combo("Options", "INS_HNTCH_OPTS")
        .Num("Harmonics", "INS_HNTCH_HMNCS"));

    Groups.Add(new TuningGroup("Filter Logs")
        .Combo("Mask", "INS_LOG_BAT_MASK")
        .Num("Options", "INS_LOG_BAT_OPT"));

    Wire();
  }

  private void Wire() {
    foreach (var row in Groups.SelectMany(g => g.Rows)) {
      row.PropertyChanged -= OnRowChanged;
      row.PropertyChanged += OnRowChanged;
    }

    var rllP = Find("RATE_RLL_P", "ATC_RAT_RLL_P", "Q_A_RAT_RLL_P");
    var pitP = Find("RATE_PIT_P", "ATC_RAT_PIT_P", "Q_A_RAT_PIT_P");
    var rllI = Find("RATE_RLL_I", "ATC_RAT_RLL_I", "Q_A_RAT_RLL_I");
    var pitI = Find("RATE_PIT_I", "ATC_RAT_PIT_I", "Q_A_RAT_PIT_I");
    var rllD = Find("RATE_RLL_D", "ATC_RAT_RLL_D", "Q_A_RAT_RLL_D");
    var pitD = Find("RATE_PIT_D", "ATC_RAT_PIT_D", "Q_A_RAT_PIT_D");

    bool differs = (rllP?.Value != pitP?.Value) || (rllI?.Value != pitI?.Value)
                   || (rllD?.Value != pitD?.Value)
                   || comPort.MAV.param.ContainsKey("H_SWASH_TYPE");
    LockRollPitch = !differs;
  }

  private TuningRow? Find(params string[] names) {
    var name = Tuning.Resolve(names);
    return Groups.SelectMany(g => g.Rows).FirstOrDefault(r => r.Name == name);
  }

  private void OnRowChanged(object? sender, PropertyChangedEventArgs e) {
    if (_mirroring || !LockRollPitch) {
      return;
    }

    if (e.PropertyName != nameof(TuningRow.Value) || sender is not TuningRow row) {
      return;
    }

    if (!row.Name.Contains("_RLL_")) {
      return;
    }

    var target = Groups.SelectMany(g => g.Rows)
        .FirstOrDefault(r => r.Name == row.Name.Replace("_RLL_", "_PIT_"));
    if (target == null || !target.Exists) {
      return;
    }

    _mirroring = true;
    target.Value = row.Value;
    _mirroring = false;
  }
}
