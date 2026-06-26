using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace MissionPlannerAvalonia.Services;

// Parser for the upstream graphs/*.xml preset files (mavgraphs / mavgraphs2 / mavgraphsMP /
// ekfGraphs / ekf3Graphs). Each <graph> has a name and a whitespace-separated <expression> of
// curves; a "TYPE.FIELD:2" suffix puts that curve on the right (2nd) Y axis. Feeds the LogBrowse
// preset dropdown (CMB_preselect).
public readonly record struct GraphCurve(string Expression, int Axis);

public readonly record struct GraphPreset(string Name, IReadOnlyList<GraphCurve> Curves);

public static class GraphPresets {
  public static List<GraphPreset> Parse(string xml) {
    var doc = XDocument.Parse(xml);
    var list = new List<GraphPreset>();
    foreach (var g in doc.Descendants("graph")) {
      var name = (string?)g.Attribute("name");
      var expr = (string?)g.Element("expression");
      if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(expr)) {
        continue;
      }
      list.Add(new GraphPreset(name!, ParseCurves(expr!)));
    }
    return list;
  }

  public static List<GraphCurve> ParseCurves(string expression) {
    var curves = new List<GraphCurve>();
    foreach (var token in expression.Split((char[]?)null, System.StringSplitOptions.RemoveEmptyEntries)) {
      int axis = 1;
      var ex = token;
      int colon = ex.LastIndexOf(':');
      // Only treat a trailing ":<digit>" as an axis selector (not part of an expression).
      if (colon > 0 && colon == ex.Length - 2 && char.IsDigit(ex[colon + 1])) {
        axis = ex[colon + 1] - '0';
        ex = ex[..colon];
      }
      curves.Add(new GraphCurve(ex, axis));
    }
    return curves;
  }

  // Load every graphs/*.xml under a directory, sorted by name (mirrors mavgraph.readmavgraphsxml).
  public static List<GraphPreset> LoadDirectory(string dir) {
    var all = new List<GraphPreset>();
    if (!Directory.Exists(dir)) {
      return all;
    }
    foreach (var file in Directory.GetFiles(dir, "*.xml").OrderBy(f => f)) {
      try {
        all.AddRange(Parse(File.ReadAllText(file)));
      } catch {
        // skip malformed preset files
      }
    }
    return all.OrderBy(p => p.Name).ToList();
  }
}
