using System.Globalization;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Messung;

/// <summary>
/// Welle A 0.83.0 (Weisung P12-283 Fund 1): die Nummernmarken der Karte im Spiel-Handy liegen ganz innerhalb der Karte und
/// ueberlappen sich nicht. Geprueft ueber dieselbe Rechnung wie das Zeichnen (<see cref="StadtKarte.Marken"/>) in den
/// Groessen eines Handys und Tablets, je Schriftstufe, mit Marke „!“ an jedem Ort (schlimmster Fall). Gegenprobe: mit den
/// Marken wie bis A 0.82.0 (<see cref="StadtKarte.MarkenAltFuerTests"/>) meldet dieselbe Pruefung Funde.
/// </summary>
public static partial class AutoMessung
{
    private static readonly (float B, float H, float Schrift)[] MarkenLagen =
        [(320, 320, 1f), (360, 360, 1.3f), (400, 400, 1f), (320, 320, 1.6f), (600, 420, 2f)];

    /// <summary>Funde der Marken (leer = gut) und ob die Gegenprobe rot war.</summary>
    internal static (List<string> Funde, bool GegenprobeRot, string Wert) MarkenPruefen(StadtSzene szene)
    {
        var inv = CultureInfo.InvariantCulture;
        var karte = new StadtKarte(szene, szene.Orte.Select(o => o.Id).ToHashSet(StringComparer.Ordinal));
        var funde = new List<string>();
        var alt = 0;
        foreach (var (b, h, f) in MarkenLagen)
        {
            var s = karte.Massstab(b, h);
            funde.AddRange(karte.MarkenFund(karte.Marken(s, f), s, f).Select(x => string.Create(inv, $"{b:F0} x {h:F0}, Schrift {f}: {x}")));
            StadtKarte.MarkenAltFuerTests = true;
            try
            {
                alt += karte.MarkenFund(karte.Marken(s, f), s, f).Count;
            }
            finally
            {
                StadtKarte.MarkenAltFuerTests = false;
            }
        }
        return (funde, alt > 0, string.Create(inv, $"{karte.Orte.Count} Marken in {MarkenLagen.Length} Lagen: {funde.Count} Funde; Gegenprobe Marken wie bis A 0.82.0: {alt} Funde"));
    }
}
