using System.Numerics;
using System.Runtime.CompilerServices;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>
/// Fund 262 (Weisung P12-122, Nico: Tueren glitchen in der Fassade): jede gezeichnete Tuer liegt buendig auf der echten
/// Wandflaeche (<see cref="StadtTueren.NichtBuendig"/>: Wand hoechstens <see cref="StadtTueren.BuendigGrenze"/> vor oder hinter
/// der Tuer, an keiner Stelle fehlend, keine sichtbare Tuer des Gebaeudes darunter), sonst rot. Die eigene Tuer eines
/// Gebaeudes (Tuer-Teil aus dem Editor, eingerastete CC0-Tuer) zaehlt nicht, das Spiel zeichnet dort keine zweite.
/// Tueren, die genau so schon in der Ausgangsstadt stehen (Fassung 0: Modell, Lage zum Haus und Richtung gleich, auch an
/// einem anderen Ort nach „Aussehen tauschen“), bleiben
/// ohne Fund (E1: 4 der 12 gezeichneten Tueren der Fassung 0 sind nach dieser strengen Regel nicht buendig, das Spiel
/// deckt sie seit P8-28 mit einem tiefen Rahmen ab; gemeldet in Meldung P12-82). Nur Werkzeug.
/// </summary>
public static class TuerBuendig
{
    /// <summary>So nah muss eine Tuer an derselben Stelle der Ausgangsstadt liegen, um als unveraendert zu gelten.</summary>
    public const float Gleich = 0.005f;

    /// <summary>Gegenprobe: alle Tueren pruefen, auch die unveraenderten der Ausgangsstadt (nur in Tests, je Ablauf).</summary>
    internal static readonly AsyncLocal<bool> AuchAusgangFuerTests = new();

    /// <summary>Regel aus (nur in Tests anderer Regeln, je Ablauf; die Regel selbst pruefen TuerBuendigTests).</summary>
    internal static readonly AsyncLocal<bool> AusFuerTestsWert = new();

    /// <summary>Schaltet die Regel bis zum Dispose aus (nur in Tests anderer Regeln).</summary>
    internal static IDisposable AusFuerTests()
    {
        AusFuerTestsWert.Value = true;
        return new Zurueck();
    }

    private sealed class Zurueck : IDisposable
    {
        public void Dispose() => AusFuerTestsWert.Value = false;
    }

    private static readonly ConditionalWeakTable<StadtFassung, List<(string Ort, string Modell, Vector2 Lage, Vector2 Aussen)>> Ausgang = new();

    internal static StadtHaus? HausVon(StadtSzene szene, StadtTuer t) => StadtTueren.HausVon(szene, t);

    /// <summary>Aussehen eines Hauses: Namen seiner Stuecke und Groesse (ein Ort traegt als Kennung seinen Namen, nicht das Modell).</summary>
    private static string Modell(StadtSzene szene, StadtHaus h) =>
        string.Join(",", Enumerable.Range(h.StueckeVon, h.StueckeBis - h.StueckeVon).Select(i => szene.Stuecke[i].Name))
        + FormattableString.Invariant($"|{h.Max.X - h.Min.X:0.00}x{h.Max.Z - h.Min.Z:0.00}x{h.Max.Y - h.Min.Y:0.00}");

    private static List<(string, string, Vector2, Vector2)> Tueren(StadtSzene szene) =>
        [.. szene.Tueren.Where(t => !t.Modell).Select(t => (t, h: HausVon(szene, t))).Where(x => x.h is not null)
            .Select(x => (x.t.Ort, Modell(szene, x.h!), x.t.Mitte - new Vector2(x.h!.Min.X, x.h.Min.Z), x.t.Aussen))];

    private static List<(string Ort, string Modell, Vector2 Lage, Vector2 Aussen)> AusgangVon(StadtFassung basis, Spieldaten daten) =>
        Ausgang.GetValue(basis, b =>
        {
            var stadt = b.StadtFuer(b.MitKarte(daten.Inhalt));
            var stand = StadtbauSpiel.Probestand(stadt.Inhalt).Stand;
            return Tueren(StadtSzene.Bauen(stadt, stand, daten.Grafik));
        });

    /// <summary>
    /// Welle S 0.33.0 (Sammelliste 272): Stellen, an denen eine Tuer an diesem Haus genau so steht wie in der Ausgangsstadt
    /// (gezeichnete Mitte relativ zur Ecke des Hauses, Richtung); dort ist sie ohne Fund (Ziel beim Einrasten).
    /// </summary>
    internal static List<(Vector2 Lage, Vector2 Aussen)> AusgangFuer(StadtSzene szene, StadtHaus h, Spieldaten daten)
    {
        if (AuchAusgangFuerTests.Value || AusFuerTestsWert.Value)
        {
            return [];
        }
        var modell = Modell(szene, h);
        return [.. AusgangVon(daten.Fassung, daten).Where(a => a.Modell == modell).Select(a => (a.Lage, a.Aussen))];
    }

    /// <summary>Name eines Orts aus der Karte der Fassung (auch neue Orte), sonst null (Gebaeude ohne Ort).</summary>
    internal static string? OrtName(StadtFassung stand, string id) =>
        (Lernplattform.Core.Spiel.SpielDaten.Get(stand.Karte, "orte") as List<object?> ?? [])
            .Concat(Lernplattform.Core.Spiel.SpielDaten.Get(stand.Stadtdaten, "neue_orte") as List<object?> ?? [])
            .OfType<Lernplattform.Core.Daten.PyObjekt>().FirstOrDefault(o => Py.Gleich(o["id"], id))?["name"] as string;

    /// <summary>Gegenprobe Sammelliste 274 (Rest): Name wie bis S 0.34.0 (Gebaeude ohne Ort mit der Kennung), nur Tests.</summary>
    public static bool NameAltFuerTests { get => NameAlt.Value; set => NameAlt.Value = value; }
    private static readonly AsyncLocal<bool> NameAlt = new();

    /// <summary>
    /// Sammelliste 274 (Rest, Weisung P12-164, Nicos Bild 1 „Die Tür von „kaufhaus““): Name einer Tuer im Fund. Ort der Karte:
    /// sein Name; Gebaeude ohne Ort (Tuer am Zierweg, <paramref name="ort"/> = Modell): Name des Zierwegs, sonst der feste
    /// Name der Kennung (<see cref="ModellNamen.Namen"/>); nie die Kennung, wenn es einen Namen gibt.
    /// </summary>
    internal static string Name(StadtFassung stand, StadtSzene szene, string ort)
    {
        if ((OrtName(stand, ort) ?? szene.Ort(ort)?.Name) is { } n)
        {
            return n;
        }
        if (NameAlt.Value)
        {
            return ort;
        }
        var zier = (Lernplattform.Core.Spiel.SpielDaten.Get(Lernplattform.Core.Spiel.SpielDaten.Get(stand.Stadtdaten, "darstellung") as Lernplattform.Core.Daten.PyObjekt ?? [], "zierwege") as List<object?> ?? [])
            .OfType<Lernplattform.Core.Daten.PyObjekt>().FirstOrDefault(z => Py.Gleich(z["modell"], ort));
        return zier?["name"] is string zn && zn.Length > 0 && zn != ort ? zn
            : ModellNamen.Namen.TryGetValue(ort, out var fest) ? fest
            : ort;
    }

    /// <summary>Rote Funde der Tueren in <paramref name="szene"/> (Szene der Pruefung draussen, Grafik mit eigenen Gebaeuden).</summary>
    public static IEnumerable<StadtbauFund> Pruefen(StadtSzene szene, StadtFassung stand, Spieldaten daten)
    {
        if (AusFuerTestsWert.Value)
        {
            yield break;
        }
        // dasselbe Modell mit der Tuer an derselben Stelle wie in der Ausgangsstadt sieht genau so aus wie dort, auch an
        // einem anderen Ort (Aussehen tauschen) oder in einer anderen Stadt
        var ausgang = !AuchAusgangFuerTests.Value ? AusgangVon(daten.Fassung, daten) : [];
        foreach (var t in szene.Tueren.Where(t => !t.Modell))
        {
            if (HausVon(szene, t) is not { } h)
            {
                continue;
            }
            var lage = t.Mitte - new Vector2(h.Min.X, h.Min.Z);
            var modell = Modell(szene, h);
            if (ausgang.Any(a => a.Modell == modell && Vector2.Distance(a.Lage, lage) <= Gleich && Vector2.Distance(a.Aussen, t.Aussen) <= 0.01f))
            {
                continue;
            }
            var gruende = StadtTueren.NichtBuendig(szene, t);
            if (gruende.Count > 0)
            {
                // Sammelliste 274: der Name aus der Karte (im Probestand der Pruefung heisst ein Ort der Firma wie die Firma)
                var name = Name(stand, szene, t.Ort);
                yield return new StadtbauFund(Texte.TuerNichtBuendig(name, gruende), Texte.TuerNichtBuendigTipp, true, t.Mitte.X, t.Mitte.Y);
            }
        }
    }
}
