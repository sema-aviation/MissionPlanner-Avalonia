using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using Core.Geometry;
using KMLib;
using KMLib.Feature;
using MissionPlanner.Log;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.Services;

public class DataFlashLog {
  public static IReadOnlyList<(double lat, double lng, double alt, DateTime time)> ReadTrack(string binPath) {
    var track = new List<(double lat, double lng, double alt, DateTime time)>();

    using var log = new DFLogBuffer(binPath);

    foreach (var item in log.GetEnumeratorType(new[] { "GPS" })) {
      if (!int.TryParse(item["Status"], out var status) || status < 3) {
        continue;
      }

      if (!double.TryParse(item["Lat"], NumberStyles.Any, CultureInfo.InvariantCulture, out var lat) ||
          !double.TryParse(item["Lng"], NumberStyles.Any, CultureInfo.InvariantCulture, out var lng) ||
          !double.TryParse(item["Alt"], NumberStyles.Any, CultureInfo.InvariantCulture, out var alt)) {
        continue;
      }

      if (lat is < -90 or > 90 || lng is < -180 or > 180) {
        continue;
      }

      if (lat == 0 && lng == 0) {
        continue;
      }

      track.Add((lat, lng, alt, item.time));
    }

    return track;
  }

  public static IReadOnlyList<(double time, double value)> ReadField(string binPath, string msgType, string field) {
    var data = new List<(double time, double value)>();

    using var log = new DFLogBuffer(binPath);

    foreach (var item in log.GetEnumeratorType(new[] { msgType })) {
      var raw = item[field];
      if (raw == null) {
        continue;
      }

      if (!double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)) {
        continue;
      }

      data.Add((item.timems / 1000.0, value));
    }

    return data;
  }

  public static void ExportKml(string binPath, string outKmlPath) {
    WriteKmlTrack(ReadTrack(binPath), outKmlPath);
  }

  public static void ExportGpx(string binPath, string outGpxPath) {
    WriteGpxTrack(ReadTrack(binPath), outGpxPath);
  }

  public static void ExportMatlab(string binPath, Action<string>? progress = null) {
    MatLab.ProcessLog(binPath, progress);
  }

  public static void ConvertBinToLog(string binPath, string outTextLogPath) {
    BinaryLog.ConvertBin(binPath, outTextLogPath);
  }

  internal static void WriteKmlTrack(IReadOnlyList<(double lat, double lng, double alt, DateTime time)> track,
    string outKmlPath) {
    var kml = new KMLRoot();
    var folder = new Folder("Track");

    var line = new LineString { AltitudeMode = AltitudeMode.absolute, Extrude = true };
    var coords = new Coordinates();
    foreach (var (lat, lng, alt, _) in track) {
      coords.Add(new Point3D(lng, lat, alt));
    }

    line.coordinates = coords;

    var placemark = new Placemark { name = "Flight Path", LineString = line };
    folder.Add(placemark);

    kml.Document.Add(folder);
    kml.Save(outKmlPath);
  }

  internal static void WriteGpxTrack(IReadOnlyList<(double lat, double lng, double alt, DateTime time)> track,
    string outGpxPath) {
    var settings = new XmlWriterSettings { Indent = true };
    using var writer = XmlWriter.Create(outGpxPath, settings);

    writer.WriteStartDocument();
    writer.WriteStartElement("gpx");
    writer.WriteAttributeString("version", "1.1");
    writer.WriteAttributeString("creator", "Mission Planner");
    writer.WriteAttributeString("xmlns", "http://www.topografix.com/GPX/1/1");

    writer.WriteStartElement("trk");
    writer.WriteStartElement("trkseg");

    foreach (var (lat, lng, alt, time) in track) {
      writer.WriteStartElement("trkpt");
      writer.WriteAttributeString("lat", lat.ToString(CultureInfo.InvariantCulture));
      writer.WriteAttributeString("lon", lng.ToString(CultureInfo.InvariantCulture));
      writer.WriteElementString("ele", alt.ToString(CultureInfo.InvariantCulture));
      writer.WriteElementString("time",
        time.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
      writer.WriteEndElement();
    }

    writer.WriteEndElement();
    writer.WriteEndElement();
    writer.WriteEndElement();
    writer.WriteEndDocument();
  }
}
