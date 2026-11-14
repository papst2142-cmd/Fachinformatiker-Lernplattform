using System.Numerics;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>
/// Weisung P12-254 (Nico Frage 2 = a, „nur auf der Wiese malen“): der Gelaende-Pinsel aendert nur Rasterpunkte, die nach
/// der Regel der Pruefung frei sind (<see cref="StadtbauPruefung.GelaendeMalbar"/>: Rand 1,5 zu Haus, Strasse, Weg, Gleis,
/// Wasser, Platz, Haltestelle). Die Regel wird je Stand der Stadt ohne das Gelaende einmal gebaut und je Rasterpunkt gemerkt
/// (Malen aendert die Hindernisse nicht, darum bleibt sie fuer viele Zuege gleich).
/// </summary>
public sealed partial class Projekt
{
    private (string Schluessel, Func<Vector2, bool>? Regel, Dictionary<(int I, int J), bool> Gemerkt)? _malbar;

    /// <summary>Darf der Pinsel den Rasterpunkt (i, j) aendern? null = keine Sperre (Gegenprobe oder Stadt laesst sich nicht bauen).</summary>
    public Func<int, int, bool>? PinselMalbar()
    {
        if (AussenBearbeitung.PinselOhneSperreFuerTests)
        {
            return null;
        }
        var a = AussenStand;
        var teil = "stadtdaten/" + Gelaende.Abschnitt;
        var schluessel = string.Join("\n", Aussen.TeileText(a).Where(e => e.Key != teil).OrderBy(e => e.Key, StringComparer.Ordinal)
            .Select(e => e.Key + "=" + e.Value));
        if (_malbar is not { } m || m.Schluessel != schluessel)
        {
            Func<Vector2, bool>? regel = null;
            if (SzeneOhneLaternen(a, true) is { } szene)
            {
                using var nachsicht = Nachsicht.Werkzeugansicht();
                var fassung = Aussen.Fassung(a, Basis, Innenraeume(), 1, null, Werkzeug.Kennzeile, "", "");
                regel = StadtbauPruefung.GelaendeMalbar(szene, fassung);
            }
            m = (schluessel, regel, []);
            _malbar = m;
        }
        if (m.Regel is not { } r)
        {
            return null;
        }
        var gemerkt = m.Gemerkt;
        return (i, j) =>
        {
            if (!gemerkt.TryGetValue((i, j), out var frei))
            {
                frei = r(new Vector2(i * Gelaende.Raster, j * Gelaende.Raster));
                gemerkt[(i, j)] = frei;
            }
            return frei;
        };
    }
}
