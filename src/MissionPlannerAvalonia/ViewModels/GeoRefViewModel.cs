using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MissionPlanner.Log;
using MissionPlanner.Utilities;
// MetadataExtractor also defines a `Directory` type; pin the unqualified name to System.IO.
using Directory = System.IO.Directory;

namespace MissionPlannerAvalonia.ViewModels;

// Port of MissionPlanner GeoRef/georefimage.cs + ExtLibs/Utilities/GeoRefImageBase.cs.
// Geotags a folder of photos against a dataflash (.bin/.log) flight log by matching each
// photo's EXIF DateTimeOriginal to the vehicle's CAM (camera feedback) or GPS positions.
// Outputs a "geotagged" folder with location.txt (upstream's primary text report) and a KML
// of camera positions. In-place EXIF re-embedding is BLOCKED (see GeoTagAsync notes).
public partial class GeoRefViewModel : ViewModelBase {
  // One matched photo row for the results grid.
  public sealed class GeoTagResult {
    public string Photo { get; init; } = "";
    public double Lat { get; init; }
    public double Lng { get; init; }
    public double Alt { get; init; }
    public string MatchedTime { get; init; } = "";
  }

  // A single vehicle position sample read from the log.
  private sealed class VehicleLoc {
    public DateTime Time;
    public double Lat;
    public double Lon;
    public double AltAMSL;
    public double RelAlt;
    public double GPSAlt;
    public float Roll;
    public float Pitch;
    public float Yaw;
  }

  [ObservableProperty] private string _logPath = "";
  [ObservableProperty] private string _photoDir = "";
  // Seconds offset applied as photoTime - offset before the GPS lookup (TIME_OFFSET mode only).
  [ObservableProperty] private double _timeOffsetSeconds;
  // true => sync each photo to a CAM (camera feedback) message (upstream default); false =>
  // TIME_OFFSET: match photo EXIF time (minus offset) to GPS positions.
  [ObservableProperty] private bool _useCamMessages = true;
  // Use the second GPS unit (GPS2) instead of GPS for the TIME_OFFSET lookup.
  [ObservableProperty] private bool _useGps2;
  [ObservableProperty] private bool _busy;
  [ObservableProperty] private string _status = "Pick a log and a photo folder, then Geo Tag.";
  [ObservableProperty] private string _outputLog = "";

  public ObservableCollection<GeoTagResult> Results { get; } = new();

  private string GpsMsg => UseGps2 ? "GPS2" : "GPS";

  // Append a line to the output log — safe to call from the pipeline's worker thread.
  private void Append(string text) {
    var line = text.EndsWith("\n") ? text : text + "\n";
    if (Dispatcher.UIThread.CheckAccess()) {
      OutputLog += line;
    } else {
      Dispatcher.UIThread.Post(() => OutputLog += line);
    }
  }

  // Run the geotag pipeline on a worker thread; returns when the report files are written.
  public async Task GeoTagAsync() {
    if (!File.Exists(LogPath)) {
      Status = "Log file not found.";
      return;
    }
    if (!Directory.Exists(PhotoDir)) {
      Status = "Photo directory not found.";
      return;
    }

    Busy = true;
    OutputLog = "";
    Status = "Geo tagging…";
    var results = new List<GeoTagResult>();

    try {
      await Task.Run(() => {
        var pics = UseCamMessages ? DoWorkCam() : DoWorkGpsOffset();
        if (pics.Count == 0) {
          Append("No valid matches. Aborting.");
          return;
        }

        // upstream primary outputs (location.txt + KML of camera positions)
        WriteReports(pics);

        foreach (var p in pics) {
          results.Add(new GeoTagResult {
            Photo = Path.GetFileName(p.Path),
            Lat = p.Lat,
            Lng = p.Lon,
            Alt = p.AltAMSL,
            MatchedTime = p.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
          });
        }

        // BLOCKED: re-embedding GPS back into each JPEG's EXIF in place. MetadataExtractor is a
        // READ-ONLY metadata library, so the upstream WriteCoordinatesToImage step (ExifLibrary)
        // is not ported. location.txt + location.kml carry the matched positions instead.
        Append("NOTE: in-place JPEG EXIF GPS write is not available (MetadataExtractor is " +
               "read-only). Wrote location.txt + location.kml instead.");
      });

      Results.Clear();
      foreach (var r in results) {
        Results.Add(r);
      }
      Status = $"Geo tagging done — {results.Count} photos matched. Reports under 'geotagged'.";
    } catch (Exception ex) {
      Append("Error: " + ex);
      Status = "Geo tagging failed: " + ex.Message;
    } finally {
      Busy = false;
    }
  }

  // --- pipeline (mirrors GeoRefImageBase.doworkCAM / doworkGPSOFFSET) ---------------------------

  private sealed class PictureInfo {
    public string Path = "";
    public DateTime Time;
    public double Lat;
    public double Lon;
    public double AltAMSL;
    public double RelAlt;
    public double GPSAlt;
    public float Roll;
    public float Pitch;
    public float Yaw;
  }

  private List<string> ListPhotos() {
    var files = new List<string>();
    foreach (var pat in new[] { "*.jpg", "*.jpeg", "*.tif", "*.tiff" }) {
      files.AddRange(Directory.GetFiles(PhotoDir, pat));
    }
    // de-dup (case-insensitive filesystems can match *.jpg and *.JPG twice)
    return files.Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(f => GetPhotoTime(f)).ThenBy(f => f, StringComparer.Ordinal).ToList();
  }

  private readonly Dictionary<string, DateTime> _photoTimeCache = new();

  // Read a photo's capture time from EXIF DateTimeOriginal (0x9003), falling back to
  // DateTimeDigitized (0x9004), via MetadataExtractor. Returns MinValue if unavailable.
  private DateTime GetPhotoTime(string fn) {
    if (_photoTimeCache.TryGetValue(fn, out var cached)) {
      return cached;
    }
    var dt = DateTime.MinValue;
    try {
      var dirs = ImageMetadataReader.ReadMetadata(fn);
      var exif = dirs.OfType<ExifSubIfdDirectory>().FirstOrDefault();
      if (exif != null) {
        if (exif.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var t) ||
            exif.TryGetDateTime(ExifDirectoryBase.TagDateTimeDigitized, out t)) {
          dt = t;
        }
      }
    } catch {
      // unreadable / non-image — leave as MinValue
    }
    _photoTimeCache[fn] = dt;
    return dt;
  }

  // CAM-message sync: pair the i-th photo (sorted by EXIF time) with the i-th CAM message.
  private List<PictureInfo> DoWorkCam() {
    Append("Reading log for CAM messages…");
    var cam = ReadCamMsgInLog(LogPath);
    Append($"CAM messages found: {cam.Count}");

    var files = ListPhotos();
    Append($"Photos found: {files.Count}");

    if (files.Count != cam.Count) {
      Append($"WARNING: CAM/photo count mismatch — photos: {files.Count} vs CAM: {cam.Count}");
    }

    var camList = cam.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList();
    var outp = new List<PictureInfo>();
    for (int i = 0; i < files.Count && i < camList.Count; i++) {
      var c = camList[i];
      outp.Add(new PictureInfo {
        Path = files[i],
        Time = c.Time,
        Lat = c.Lat,
        Lon = c.Lon,
        AltAMSL = c.AltAMSL,
        RelAlt = c.RelAlt,
        GPSAlt = c.GPSAlt,
        Roll = c.Roll,
        Pitch = c.Pitch,
        Yaw = c.Yaw,
      });
      Append($"Photo {Path.GetFileNameWithoutExtension(files[i])} matched to CAM msg.");
    }
    return outp;
  }

  // TIME_OFFSET sync: for each photo, look up the GPS position nearest (photoTime - offset).
  private List<PictureInfo> DoWorkGpsOffset() {
    Append($"Reading log for {GpsMsg} messages…");
    var gps = ReadGpsMsgInLog(LogPath, GpsMsg);
    Append($"{GpsMsg} positions found: {gps.Count}");

    var files = ListPhotos();
    Append($"Photos found: {files.Count}");

    var outp = new List<PictureInfo>();
    foreach (var file in files) {
      var shot = GetPhotoTime(file);
      if (shot == DateTime.MinValue) {
        Append($"Photo {Path.GetFileNameWithoutExtension(file)} has no EXIF time — skipped.");
        continue;
      }
      var corrected = shot.AddSeconds(-TimeOffsetSeconds).ToUniversalTime();
      var loc = LookForLocation(corrected, gps, 5000);
      if (loc == null) {
        Append($"Photo {Path.GetFileNameWithoutExtension(file)} — no GPS match in log.");
        continue;
      }
      outp.Add(new PictureInfo {
        Path = file,
        Time = loc.Time,
        Lat = loc.Lat,
        Lon = loc.Lon,
        AltAMSL = loc.AltAMSL,
        RelAlt = loc.RelAlt,
        GPSAlt = loc.GPSAlt,
        Roll = loc.Roll,
        Pitch = loc.Pitch,
        Yaw = loc.Yaw,
      });
      Append($"Photo {Path.GetFileNameWithoutExtension(file)} matched {(loc.Time - corrected).TotalMilliseconds:0} ms away.");
    }
    return outp;
  }

  // Nearest position within +/- windowMs of t (mirrors GeoRefImageBase.LookForLocation).
  private static VehicleLoc? LookForLocation(DateTime t, Dictionary<long, VehicleLoc> locs,
      int windowMs) {
    long time = ToMillis(t);
    for (int i = 0; i <= windowMs; i++) {
      if (locs.TryGetValue(time + i, out var a)) {
        return a;
      }
      if (locs.TryGetValue(time - i, out var b)) {
        return b;
      }
    }
    return null;
  }

  // --- log readers (dataflash .bin/.log via DFLogBuffer) ----------------------------------------

  // Read GPS (or GPS2) positions, folding in the last-seen ATT roll/pitch/yaw, keyed by UTC ms.
  private static Dictionary<long, VehicleLoc> ReadGpsMsgInLog(string fn, string gpsToUse) {
    var list = new Dictionary<long, VehicleLoc>();
    float roll = 0, pitch = 0, yaw = 0;

    using var sr = new DFLogBuffer(fn);
    foreach (var item in sr.GetEnumeratorType(new[] { gpsToUse, "ATT" })) {
      if (item.msgtype == "ATT") {
        roll = ParseF(item["Roll"], roll);
        pitch = ParseF(item["Pitch"], pitch);
        yaw = ParseF(item["Yaw"], yaw);
        continue;
      }

      // GPS / GPS2 — only reject on a present-but-no-lock Status (upstream guards statusindex != -1)
      var statusRaw = item["Status"];
      if (statusRaw != null && TryD(statusRaw, out var status) && status < 3) {
        continue;
      }
      if (!TryD(item["Lat"], out var lat) || !TryD(item["Lng"], out var lng)) {
        continue;
      }
      if (lat == 0 && lng == 0) {
        continue;
      }
      var loc = new VehicleLoc {
        Time = item.time.ToUniversalTime(),
        Lat = lat,
        Lon = lng,
        Roll = roll,
        Pitch = pitch,
        Yaw = yaw,
      };
      if (TryD(item["Alt"], out var alt)) {
        loc.AltAMSL = alt;
        loc.GPSAlt = alt;
      }
      if (TryD(item["RAlt"], out var ralt) || TryD(item["RelAlt"], out ralt)) {
        loc.RelAlt = ralt;
      }
      long ms = ToMillis(loc.Time);
      if (loc.Time != DateTime.MinValue && !list.ContainsKey(ms)) {
        list[ms] = loc;
      }
    }
    return list;
  }

  // Read CAM (camera feedback) messages, timestamped from their GPSWeek/GPSTime, keyed by UTC ms.
  private static Dictionary<long, VehicleLoc> ReadCamMsgInLog(string fn) {
    var list = new Dictionary<long, VehicleLoc>();

    using var sr = new DFLogBuffer(fn);
    foreach (var item in sr.GetEnumeratorType(new[] { "CAM" })) {
      var p = new VehicleLoc();

      if (int.TryParse(item["GPSWeek"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var week) &&
          int.TryParse(item["GPSTime"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var gtime)) {
        p.Time = GetTimeFromGps(week, gtime);
      } else {
        p.Time = item.time.ToUniversalTime();
      }

      TryD(item["Lat"], out var lat);
      TryD(item["Lng"], out var lng);
      p.Lat = lat;
      p.Lon = lng;
      if (TryD(item["Alt"], out var alt)) {
        p.AltAMSL = alt;
      }
      if (TryD(item["RelAlt"], out var ralt)) {
        p.RelAlt = ralt;
      }
      if (TryD(item["GPSAlt"], out var galt)) {
        p.GPSAlt = galt;
      }
      p.Roll = ParseF(item["Roll"] ?? item["R"], 0);
      p.Pitch = ParseF(item["Pitch"] ?? item["P"], 0);
      p.Yaw = ParseF(item["Yaw"] ?? item["Y"], 0);

      list[ToMillis(p.Time)] = p;
    }
    return list;
  }

  // --- report writers (location.txt + location.kml under <photoDir>/geotagged) ------------------

  private void WriteReports(List<PictureInfo> pics) {
    string outDir = Path.Combine(PhotoDir, "geotagged");
    Directory.CreateDirectory(outDir);

    // location.txt: "#name latitude/Y longitude/X height/Z yaw pitch roll SAlt" (mirrors upstream)
    string txtPath = Path.Combine(outDir, "location.txt");
    using (var sw = new StreamWriter(txtPath)) {
      sw.WriteLine("#name latitude/Y longitude/X height/Z yaw pitch roll SAlt");
      foreach (var p in pics) {
        sw.WriteLine(string.Join(" ", new[] {
          Path.GetFileName(p.Path),
          Inv(p.Lat), Inv(p.Lon), Inv(p.AltAMSL),
          Inv(p.Yaw), Inv(p.Pitch), Inv(p.Roll), Inv(0.0),
        }));
      }
    }
    Append("Wrote " + txtPath);

    WriteKml(Path.Combine(outDir, "location.kml"), pics);
    Append("Wrote " + Path.Combine(outDir, "location.kml"));
  }

  // Camera positions as Point placemarks plus a connecting path LineString.
  private static void WriteKml(string path, List<PictureInfo> pics) {
    var settings = new XmlWriterSettings { Indent = true };
    using var w = XmlWriter.Create(path, settings);
    w.WriteStartDocument();
    w.WriteStartElement("kml", "http://www.opengis.net/kml/2.2");
    w.WriteStartElement("Document");
    w.WriteElementString("name", "GeoRef");

    var coords = new StringBuilder();
    foreach (var p in pics) {
      w.WriteStartElement("Placemark");
      w.WriteElementString("name", Path.GetFileNameWithoutExtension(p.Path));
      w.WriteStartElement("TimeStamp");
      w.WriteElementString("when", p.Time.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
      w.WriteEndElement();
      w.WriteStartElement("Point");
      w.WriteElementString("altitudeMode", "absolute");
      w.WriteElementString("coordinates",
          $"{Inv(p.Lon)},{Inv(p.Lat)},{Inv(p.AltAMSL)}");
      w.WriteEndElement();
      w.WriteEndElement();

      coords.Append($"{Inv(p.Lon)},{Inv(p.Lat)},{Inv(p.AltAMSL)} ");
    }

    // path connecting the camera positions
    w.WriteStartElement("Placemark");
    w.WriteElementString("name", "path");
    w.WriteStartElement("LineString");
    w.WriteElementString("altitudeMode", "absolute");
    w.WriteElementString("coordinates", coords.ToString().Trim());
    w.WriteEndElement();
    w.WriteEndElement();

    w.WriteEndElement(); // Document
    w.WriteEndElement(); // kml
    w.WriteEndDocument();
  }

  // --- helpers ----------------------------------------------------------------------------------

  private static string Inv(double v) => v.ToString(CultureInfo.InvariantCulture);

  private static bool TryD(string? s, out double v) =>
      double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v);

  private static float ParseF(string? s, float fallback) =>
      float.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;

  private static long ToMillis(DateTime date) {
    var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    return Convert.ToInt64((date.ToUniversalTime() - epoch).TotalMilliseconds);
  }

  // GPS week + ms-of-week → UTC (mirrors GeoRefImageBase.GetTimeFromGps, 17 leap seconds).
  private static DateTime GetTimeFromGps(int week, int milliseconds) {
    const int leapSeconds = 17;
    var datum = new DateTime(1980, 1, 6, 0, 0, 0, DateTimeKind.Utc);
    return datum.AddDays(week * 7).AddMilliseconds(milliseconds).AddSeconds(-leapSeconds);
  }
}
