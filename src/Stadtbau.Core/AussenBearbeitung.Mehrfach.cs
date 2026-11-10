using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle W-B S 0.30.0 (Weisung P12-136, Kurzplan P12-79 Wunsch 1, Nico 1a): mehrere Stuecke draussen gemeinsam schieben,
/// drehen, kopieren und loeschen. Jede Aktion ist EIN Schritt im Verlauf (die Oberflaeche ruft genau eine dieser Methoden in
/// einem <c>Projekt.AussenAendern</c>). Regeln (Plan P12-79, E1):
/// <list type="bullet">
/// <item>Eine Gruppe rastet nicht ein (Strassenenden, Haeuser an den Gehweg): sie bleibt, wie sie zueinander steht. Geschoben
/// wird in ganzen Rasterschritten (0,5, mit „Einrasten“ aus 0,1).</item>
/// <item>Gedreht wird die ganze Gruppe in Vierteln um ihre Mitte (auf dem Raster 0,5): Haeuser drehen im Spiel nur in Vierteln.
/// Strassen, Kurven, Fluesse, Bahnen, Teiche und Fusswege drehen ihre Punkte genau um diese Mitte (eine Kurve rastet dabei
/// nicht neu ein); Haeuser, Parks, Katalog-Stuecke und Stege drehen sich wie mit dem Knopf und wandern dann an ihre Stelle;
/// Laternen, Baeume, Beete, Notizen und der Paketplatz wandern nur.</item>
/// <item>Was ein Stueck ohnehin mitnimmt, wird nicht doppelt bewegt: Zebrastreifen auf einer gewaehlten geraden Strasse,
/// Zufahrten und Laternen eines gewaehlten Kreisverkehrs (<see cref="OhneMitgenommene"/>).</item>
/// <item>Haeuser mit Ort und der Paketplatz lassen sich nicht kopieren und nicht loeschen (wie einzeln); sie werden
/// uebersprungen und gezaehlt.</item>
/// </list>
/// </summary>
public static partial class AussenBearbeitung
{
    /// <summary>Ergebnis einer Aktion mit mehreren Stuecken: neue Auswahl, ausgefuehrt, uebersprungen.</summary>
    public sealed record MehrfachErgebnis(List<AussenStelle> Auswahl, int Erledigt, int Uebersprungen);

    /// <summary>Mitte eines Stuecks (wie in <see cref="Alle"/>), null = gibt es nicht.</summary>
    public static Vector2? MitteVon(PyObjekt aussen, AussenStelle s)
    {
        s = Ganz(s);
        foreach (var (stelle, x, y) in Alle(aussen))
        {
            if (stelle == s)
            {
                return new Vector2((float)x, (float)y);
            }
        }
        return null;
    }

    /// <summary>Stuecke im Rechteck (Mitte darin); ausgeblendete Ebenen und automatische Laternen nicht (sie folgen den Strassen).</summary>
    public static List<AussenStelle> ImRahmen(PyObjekt aussen, double x1, double y1, double x2, double y2, IReadOnlySet<AussenArt>? aus = null)
    {
        var (ax, bx) = (Math.Min(x1, x2), Math.Max(x1, x2));
        var (ay, by) = (Math.Min(y1, y2), Math.Max(y1, y2));
        return [.. Alle(aussen).Where(a => a.X >= ax && a.X <= bx && a.Y >= ay && a.Y <= by)
            .Select(a => a.Stelle)
            .Where(s => aus is null || !aus.Contains(s.Art))
            .Where(s => s.Art != AussenArt.Laterne || !LaternenAutomatik.IstAutomatisch(aussen, s.Nummer))];
    }

    /// <summary>Stuecke im Bild-Rechteck (3D): Mitte nach <paramref name="bildpunkt"/> im Rechteck.</summary>
    public static List<AussenStelle> ImBildRahmen(PyObjekt aussen, Func<double, double, (double X, double Y)?> bildpunkt, double x1, double y1, double x2, double y2,
        IReadOnlySet<AussenArt>? aus = null)
    {
        var (ax, bx) = (Math.Min(x1, x2), Math.Max(x1, x2));
        var (ay, by) = (Math.Min(y1, y2), Math.Max(y1, y2));
        return [.. Alle(aussen)
            .Where(a => a.Stelle.Art != AussenArt.Notiz && (aus is null || !aus.Contains(a.Stelle.Art)))
            .Where(a => a.Stelle.Art != AussenArt.Laterne || !LaternenAutomatik.IstAutomatisch(aussen, a.Stelle.Nummer))
            .Where(a => bildpunkt(a.X, a.Y) is { } b && b.X >= ax && b.X <= bx && b.Y >= ay && b.Y <= by)
            .Select(a => a.Stelle)];
    }

    /// <summary>Stueck in die Gruppe nehmen oder (wenn schon darin) herausnehmen; Eckpunkte zaehlen als ganzes Stueck.</summary>
    public static List<AussenStelle> Umschalten(IReadOnlyList<AussenStelle> gruppe, AussenStelle s)
    {
        s = Ganz(s);
        return gruppe.Contains(s) ? [.. gruppe.Where(g => g != s)] : [.. gruppe, s];
    }

    /// <summary>
    /// Die Gruppe ohne die Stuecke, die ein anderes gewaehltes Stueck ohnehin mitnimmt (Zebrastreifen einer gewaehlten
    /// geraden Strasse, Zufahrten und Laternen eines gewaehlten Kreisverkehrs), ohne doppelte und ohne ungueltige.
    /// </summary>
    public static List<AussenStelle> OhneMitgenommene(PyObjekt aussen, IEnumerable<AussenStelle> gruppe)
    {
        var liste = gruppe.Select(Ganz).Distinct().Where(s => Gueltig(aussen, s)).ToList();
        var mit = new HashSet<AussenStelle>();
        foreach (var s in liste.Where(s => s.Art == AussenArt.Strasse))
        {
            foreach (var z in ZebrasAuf(aussen, s))
            {
                mit.Add(new AussenStelle(AussenArt.Zebrastreifen, z));
            }
            if (IstKreisverkehr(aussen, s))
            {
                var (zufahrten, laternen) = KreisverkehrTeile(aussen, s);
                foreach (var z in zufahrten)
                {
                    mit.Add(new AussenStelle(AussenArt.Strasse, z));
                }
                foreach (var l in laternen)
                {
                    mit.Add(new AussenStelle(AussenArt.Laterne, l));
                }
            }
        }
        // ein Ring kann eine Zufahrt eines anderen Rings sein? nein: Zufahrten sind gerade Strassen; der Ring selbst bleibt immer
        return [.. liste.Where(s => !mit.Contains(s) || IstKreisverkehr(aussen, s))];
    }

    /// <summary>Linien (Strassen, Wasser, Bahn, Fusswege) zuerst: Haeuser haengen ihren Weg danach an die schon bewegten Strassen.</summary>
    private static List<AussenStelle> Reihenfolge(IEnumerable<AussenStelle> gruppe) =>
        [.. gruppe.OrderBy(s => s.Art is AussenArt.Strasse or AussenArt.Fluss or AussenArt.Bahn or AussenArt.Teich or AussenArt.Fussweg ? 0 : 1)];

    // ---- Schieben ---------------------------------------------------------------------------------

    /// <summary>
    /// Alle Stuecke um (dx, dy) in ganzen Rasterschritten verschieben (ein Schritt), ohne Einrasten. Gibt die Gruppe zurueck
    /// (gleiche Stellen; nichts verschoben = <see cref="MehrfachErgebnis.Erledigt"/> 0).
    /// </summary>
    public static MehrfachErgebnis Verschieben(PyObjekt aussen, IReadOnlyList<AussenStelle> gruppe, double dx, double dy, bool raster = true)
    {
        var alle = gruppe.Select(Ganz).Distinct().ToList();
        (dx, dy) = (Schritt(dx, raster), Schritt(dy, raster));
        if (dx == 0 && dy == 0)
        {
            return new MehrfachErgebnis(alle, 0, 0);
        }
        var bewegen = Reihenfolge(OhneMitgenommene(aussen, alle));
        foreach (var s in bewegen)
        {
            VerschiebenGenau(aussen, s, dx, dy);
        }
        return new MehrfachErgebnis([.. alle.Where(s => Gueltig(aussen, s))], bewegen.Count, 0);
    }

    // ---- Drehen -----------------------------------------------------------------------------------

    /// <summary>Mitte der Gruppe auf dem Raster 0,5 (Mitte aller Umrisse).</summary>
    public static Vector2 GruppenMitte(PyObjekt aussen, IEnumerable<AussenStelle> gruppe)
    {
        var punkte = gruppe.SelectMany(s => Bauen3D.Umriss(aussen, s)).SelectMany(f => f).ToList();
        if (punkte.Count == 0)
        {
            punkte = [.. gruppe.Select(s => MitteVon(aussen, s)).OfType<Vector2>()];
        }
        if (punkte.Count == 0)
        {
            return Vector2.Zero;
        }
        return new Vector2((float)Bearbeitung.AufRaster((punkte.Min(p => p.X) + punkte.Max(p => p.X)) / 2, Bearbeitung.Raster),
            (float)Bearbeitung.AufRaster((punkte.Min(p => p.Y) + punkte.Max(p => p.Y)) / 2, Bearbeitung.Raster));
    }

    /// <summary>Vierteldrehung der ganzen Gruppe um ihre Mitte (im Uhrzeigersinn, <paramref name="rueckwaerts"/> dagegen), ein Schritt.</summary>
    public static MehrfachErgebnis Drehen(PyObjekt aussen, IReadOnlyList<AussenStelle> gruppe, bool rueckwaerts = false)
    {
        var alle = gruppe.Select(Ganz).Distinct().ToList();
        var drehen = OhneMitgenommene(aussen, alle);
        if (drehen.Count == 0)
        {
            return new MehrfachErgebnis(alle, 0, 0);
        }
        // Zebrastreifen gewaehlter Strassen drehen als Punkte mit (eine gedrehte Strasse nimmt sie nicht mit)
        var zebras = drehen.Where(s => s.Art == AussenArt.Strasse).SelectMany(s => ZebrasAuf(aussen, s)).Select(z => new AussenStelle(AussenArt.Zebrastreifen, z))
            .Concat(drehen.Where(s => s.Art == AussenArt.Zebrastreifen)).Distinct().ToList();
        var mitte = GruppenMitte(aussen, drehen);
        Vector2 Dreh(Vector2 p) => rueckwaerts
            ? new Vector2(mitte.X + (p - mitte).Y, mitte.Y - (p - mitte).X)
            : new Vector2(mitte.X - (p - mitte).Y, mitte.Y + (p - mitte).X);
        var zebraZiele = zebras.Select(z => (z, Ziel: MitteVon(aussen, z) is { } m ? Dreh(m) : (Vector2?)null)).ToList();
        var anzahl = 0;
        foreach (var s in Reihenfolge(drehen.Where(s => s.Art != AussenArt.Zebrastreifen)))
        {
            anzahl++;
            switch (s.Art)
            {
                case AussenArt.Strasse:
                {
                    var l = Objekt(Aussen.Landschaft(aussen)[s.Nummer]);
                    var kreis = Kreisverkehr.IstRing(l) ? KreisverkehrTeile(aussen, s) : default;
                    PunkteAbbilden(Liste(l["punkte"]), Dreh, Strassenkurve.IstKurve(l));
                    if (kreis.Zufahrten is not null)
                    {
                        KreisverkehrMitnehmen(aussen, kreis, Dreh);
                    }
                    break;
                }
                case AussenArt.Fluss or AussenArt.Bahn or AussenArt.Teich:
                    PunkteAbbilden(Eckpunkte(aussen, s), Dreh, false);
                    break;
                case AussenArt.Fussweg:
                    PunkteAbbilden(Liste(Objekt(Aussen.Fusswege(aussen)[s.Nummer])["punkte"]), Dreh, true);
                    break;
                default:
                {
                    if (MitteVon(aussen, s) is not { } vorher)
                    {
                        break;
                    }
                    var ziel = Dreh(vorher);
                    var mal = rueckwaerts ? 3 : 1;
                    if (s.Art is AussenArt.Haus or AussenArt.Gebaeude)
                    {
                        HausDrehen(aussen, s, mal);
                    }
                    else if (s.Art is AussenArt.Park or AussenArt.Katalog or AussenArt.Steg)
                    {
                        for (var i = 0; i < mal; i++)
                        {
                            Drehen(aussen, s);
                        }
                    }
                    if (MitteVon(aussen, s) is { } nachher)
                    {
                        VerschiebenGenau(aussen, s, R(ziel.X - nachher.X), R(ziel.Y - nachher.Y));
                    }
                    break;
                }
            }
        }
        foreach (var (z, ziel) in zebraZiele)
        {
            if (ziel is { } p && Zebrastreifen.Legen(0, p, ZebraStrassen(aussen)) is { } neu && z.Nummer < ZebraListe(aussen).Count)
            {
                ZebraListe(aussen)[z.Nummer] = ZebraObjekt(neu.Mitte);
            }
            anzahl += drehen.Contains(z) ? 1 : 0;
        }
        return new MehrfachErgebnis([.. alle.Where(s => Gueltig(aussen, s))], anzahl, 0);
    }

    /// <summary>Punkte einer Linie abbilden (Kurven auf 0,0001 wie gesetzt, sonst 0,01).</summary>
    private static void PunkteAbbilden(List<object?> punkte, Func<Vector2, Vector2> abbilden, bool fein)
    {
        var linie = Linie(punkte);
        for (var i = 0; i < punkte.Count && i < linie.Count; i++)
        {
            var p = abbilden(linie[i]);
            punkte[i] = fein ? KurvenPunkt(p) : PunktObjekt(p);
        }
    }

    // ---- Kopieren ---------------------------------------------------------------------------------

    /// <summary>
    /// Kopie der ganzen Gruppe rechts daneben (alle Kopien um denselben Versatz: Breite der Gruppe plus ein Rasterschritt, auf
    /// das Raster 0,5), ein Schritt. Neue Auswahl = die Kopien. Zebrastreifen einer mitkopierten Strasse kommen mit auf die
    /// Kopie; ein Zebrastreifen ohne seine Strasse wird wie einzeln laengs daneben kopiert.
    /// </summary>
    public static MehrfachErgebnis Kopieren(PyObjekt aussen, IReadOnlyList<AussenStelle> gruppe)
    {
        var kopieren = OhneMitgenommene(aussen, gruppe);
        var punkte = kopieren.SelectMany(s => Bauen3D.Umriss(aussen, s)).SelectMany(f => f).ToList();
        var breite = punkte.Count == 0 ? 0 : punkte.Max(p => p.X) - punkte.Min(p => p.X);
        var versatz = Math.Ceiling((breite + Bearbeitung.Raster) / Bearbeitung.Raster) * Bearbeitung.Raster;
        var strassenMitZebra = kopieren.Where(s => s.Art == AussenArt.Strasse).SelectMany(s => ZebrasAuf(aussen, s)).Distinct()
            .Select(z => MitteVon(aussen, new AussenStelle(AussenArt.Zebrastreifen, z))).OfType<Vector2>().ToList();
        var neu = new List<AussenStelle>();
        var uebersprungen = 0;
        foreach (var s in Reihenfolge(kopieren))
        {
            if (s.Art == AussenArt.Zebrastreifen)
            {
                continue;
            }
            var vorher = MitteVon(aussen, s);
            if (Kopieren(aussen, s) is not { } k || vorher is null)
            {
                uebersprungen++;
                continue;
            }
            if (MitteVon(aussen, k) is { } nachher)
            {
                VerschiebenGenau(aussen, k, R(vorher.Value.X + versatz - nachher.X), R(vorher.Value.Y - nachher.Y));
            }
            neu.Add(k);
        }
        foreach (var p in strassenMitZebra)
        {
            if (ZebraSetzen(aussen, p.X + versatz, p.Y) is { } z)
            {
                neu.Add(z);
            }
        }
        foreach (var z in kopieren.Where(s => s.Art == AussenArt.Zebrastreifen))
        {
            if (Kopieren(aussen, z) is { } k)
            {
                neu.Add(k);
            }
            else
            {
                uebersprungen++;
            }
        }
        return new MehrfachErgebnis([.. neu.Where(s => Gueltig(aussen, s))], neu.Count, uebersprungen);
    }

    // ---- Loeschen ---------------------------------------------------------------------------------

    /// <summary>Das Objekt der Daten, das ein Stueck ausmacht (zum Wiederfinden, nachdem andere geloescht sind).</summary>
    private static object? Bezug(PyObjekt aussen, AussenStelle s)
    {
        var land = Aussen.Landschaft(aussen);
        return s.Art switch
        {
            AussenArt.Haus => s.Ort,
            AussenArt.Paketplatz => "paketplatz",
            AussenArt.Baum => Liste(Objekt(land[s.Nummer])["punkte"])[s.Punkt],
            AussenArt.Beet => Aussen.Beete(aussen)[s.Nummer],
            AussenArt.Laterne => Aussen.Laternen(aussen)[s.Nummer],
            AussenArt.Katalog => Aussen.KatalogStuecke(aussen)[s.Nummer],
            AussenArt.Teich => Aussen.Teiche(aussen)[s.Nummer],
            AussenArt.Steg => Aussen.Stege(aussen)[s.Nummer],
            AussenArt.Fussweg => Aussen.Fusswege(aussen)[s.Nummer],
            AussenArt.Zebrastreifen => ZebraListe(aussen)[s.Nummer],
            AussenArt.Notiz => NotizListe(aussen)[s.Nummer],
            _ => land[s.Nummer],
        };
    }

    /// <summary>Stelle zu einem Bezug im jetzigen Stand (null = schon weg, z. B. Zebrastreifen mit seiner Strasse).</summary>
    private static AussenStelle? Wiederfinden(PyObjekt aussen, AussenArt art, object? bezug)
    {
        if (bezug is string ort)
        {
            return art == AussenArt.Paketplatz ? new AussenStelle(AussenArt.Paketplatz, 0) : new AussenStelle(AussenArt.Haus, -1, 0, ort);
        }
        foreach (var (s, _, _) in Alle(aussen))
        {
            if (s.Art == art && ReferenceEquals(Bezug(aussen, s), bezug))
            {
                return s;
            }
        }
        return null;
    }

    /// <summary>Alle Stuecke loeschen (ein Schritt); Haeuser mit Ort und Paketplatz bleiben und zaehlen als uebersprungen.</summary>
    public static MehrfachErgebnis Loeschen(PyObjekt aussen, IReadOnlyList<AussenStelle> gruppe)
    {
        var liste = gruppe.Select(Ganz).Distinct().Where(s => Gueltig(aussen, s)).ToList();
        var bezuege = liste.Select(s => (s.Art, Bezug: Bezug(aussen, s))).ToList();
        var (weg, uebersprungen) = (0, 0);
        foreach (var (art, bezug) in bezuege)
        {
            if (Wiederfinden(aussen, art, bezug) is not { } s)
            {
                continue;   // schon mit einem anderen Stueck gegangen
            }
            if (LoeschenNicht(s) is not null)
            {
                uebersprungen++;
                continue;
            }
            Loeschen(aussen, s);
            weg++;
        }
        return new MehrfachErgebnis([], weg, uebersprungen);
    }
}
