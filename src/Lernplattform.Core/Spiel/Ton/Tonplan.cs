using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Core.Spiel.Ton;

/// <summary>Boden unter der Figur (Schritte). Schnee nur Vorrat fuer W9 (ohne Ton in W8).</summary>
public enum Untergrund { Stein, Gras, Holz, Teppich, Schnee }

/// <summary>Einzelne Anlaesse fuer einen Ton (Schleifen der Stadt laufen ueber <see cref="Stadtlage"/>).</summary>
public enum Anlass { Schritt, TuerAuf, TuerZu, Kasse, Feierabend, Erfolg, Ticket, Vogelruf }

public readonly record struct Tonanlass(Anlass Art, Untergrund Boden = Untergrund.Stein);

/// <summary>Lautstaerke der Schleifen in der Stadt je Bild (0 = still).</summary>
public sealed record Stadtlage(float Fluss, bool VogelNah, IReadOnlyList<(string Modell, float Laut)> Autos);

/// <summary>
/// Phase 8 W8 (Teilplan P8-15 1.1, Weisungen P8-17, P8-17c, P8-17d): welcher Anlass welchen Ton nimmt,
/// wie laut, und wo. Nur Darstellung: kein Spielereignis, keine Regel, kein Spielwuerfel (Zufall nur
/// fuer die Abwechslung der Toene). Toene in <c>Assets/Toene/toene.json</c> mit dem Feld <c>anlass</c>.
/// </summary>
public static class Tonplan
{
    // Grundlautstaerke je Ton (vor Gruppe und Gesamt), abgestimmt an den Hoerproben
    public const float StadtGrund = 0.55f;
    public const float Fluss = 0.6f;
    public const float Auto = 0.45f;
    public const float Schritt = 0.35f;
    public const float Tuer = 0.6f;
    public const float Kasse = 0.6f;
    public const float Melodie = 0.7f;
    public const float Vogelruf = 0.3f;
    public const float LiedInnen = 0.6f;

    /// <summary>In der Stadt liegt Musik leise unter den Geraeuschen (P8-17b).</summary>
    public const float LiedStadt = 0.35f;

    /// <summary>Haelt ein Auto (vor der Figur, Kreuzung), laeuft der Motor leise weiter (Leerlauf).</summary>
    public const float Leerlauf = 0.3f;

    /// <summary>Ab diesem Abstand sind Autos und Fluss still (dort zeigt W7 die Fernform).</summary>
    public const float Stille = 25f;

    /// <summary>Vogelrufe: Abstand zum naechsten Ruf in Sekunden (zufaellig, P8-17c Punkt 3).</summary>
    public const double RufMin = 8;
    public const double RufMax = 25;

    /// <summary>Seltene, leisere Einzelrufe (Fruehling und Herbst, Weisungen P8-23 B und P8-24).</summary>
    public const double RufSeltenMin = 30;
    public const double RufSeltenMax = 90;
    public const float VogelrufSelten = 0.2f;

    /// <summary>Einzelrufe einer Jahreszeit: Abstand (Sekunden) und Lautstaerke.</summary>
    public sealed record Rufe(double Min, double Max, float Laut);

    /// <summary>
    /// Einzelrufe je Jahreszeit (Nico, Weisung P8-24): Sommer wie bisher (8 bis 25 s), Fruehling und Herbst
    /// selten und leiser (30 bis 90 s), Winter keine.
    /// </summary>
    public static Rufe? RufeFuer(Jahreszeit jahreszeit) => jahreszeit switch
    {
        Jahreszeit.Sommer => new(RufMin, RufMax, Vogelruf),
        Jahreszeit.Winter => null,
        _ => new(RufSeltenMin, RufSeltenMax, VogelrufSelten),
    };

    /// <summary>Vogelrufe nur in einem Park oder so nah an einem Baum.</summary>
    public const float BaumNah = 4f;

    /// <summary>Naehe (Nico 2a): 1 / (1 + Abstand / 6), ab <see cref="Stille"/> still.</summary>
    public static float Naehe(float abstand) => abstand >= Stille ? 0f : 1f / (1f + (Math.Max(0f, abstand) / 6f));

    /// <summary>Boden: Stadt Stein, im Park Gras; Wohnung und Buero Teppich, Geschaefte, Bank, Amt Holz.</summary>
    public static Untergrund Boden(string? ansicht, bool imPark) => ansicht switch
    {
        null => imPark ? Untergrund.Gras : Untergrund.Stein,
        "zuhause" or "buero" => Untergrund.Teppich,
        _ => Untergrund.Holz,
    };

    /// <summary>Schluessel der Toene eines Anlasses (Feld <c>anlass</c> in toene.json).</summary>
    public static string Schluessel(Tonanlass anlass) => anlass.Art switch
    {
        Anlass.Schritt => anlass.Boden switch
        {
            Untergrund.Gras => "schritt_grass",
            Untergrund.Holz => "schritt_wood",
            Untergrund.Teppich => "schritt_carpet",
            // Schnee gibt es erst in W9; bis dahin Stein
            _ => "schritt_concrete",
        },
        Anlass.TuerAuf => "tuer_auf",
        Anlass.TuerZu => "tuer_zu",
        Anlass.Kasse => "kasse",
        Anlass.Feierabend => "melodie_feierabend",
        Anlass.Erfolg => "melodie_erfolg",
        Anlass.Ticket => "melodie_ticket",
        _ => "vogelruf",
    };

    public const string StadtGrundSchluessel = "stadt_grund";
    public const string StadtGrundHerbst = "stadt_grund_herbst";
    public const string StadtGrundWinter = "stadt_grund_winter";

    /// <summary>
    /// Stadt-Grundklang je Jahreszeit (Weisung P8-24): Fruehling und Sommer ruhige Voegel, Herbst leiser Wind
    /// mit wenigen Voegeln, Winter nur gedaempfter Wind.
    /// </summary>
    public static string StadtGrundFuer(Jahreszeit jahreszeit) => jahreszeit switch
    {
        Jahreszeit.Herbst => StadtGrundHerbst,
        Jahreszeit.Winter => StadtGrundWinter,
        _ => StadtGrundSchluessel,
    };
    public const string FlussSchluessel = "fluss";

    public static string AutoSchluessel(string modell) => "auto:" + modell;

    public static Tongruppe Gruppe(Anlass art) => art is Anlass.Feierabend or Anlass.Erfolg or Anlass.Ticket ? Tongruppe.Musik : Tongruppe.Umgebung;

    public static float Laut(Anlass art) => art switch
    {
        Anlass.Schritt => Schritt,
        Anlass.TuerAuf or Anlass.TuerZu => Tuer,
        Anlass.Kasse => Kasse,
        Anlass.Feierabend or Anlass.Erfolg or Anlass.Ticket => Melodie,
        _ => Vogelruf,
    };

    /// <summary>
    /// Anlass aus einem geschriebenen Spielereignis (nach der Aktion, wie das Bild): Kauf im Kaufhaus,
    /// Moebelhaus, Elektromarkt und Kredit = Kasse; Feierabend, Erfolg, richtig geloestes Ticket = Melodie.
    /// </summary>
    public static Anlass? AusEreignis(string art, PyObjekt daten) => art switch
    {
        Ereignisse.MoebelGekauft or Geschaefte.TeilGekauft or Kleidung.Gekauft or Ereignisse.Kredit => Anlass.Kasse,
        Ereignisse.Feierabend => Anlass.Feierabend,
        Ereignisse.Erfolg => Anlass.Erfolg,
        Ereignisse.Erledigt when Py.Wahr(Py.Hat(daten, "richtig") ? daten["richtig"] : null) => Anlass.Ticket,
        _ => null,
    };

    /// <summary>Naechste Variante ohne Wiederholung der letzten (bei mehr als einer).</summary>
    public static int Variante(int anzahl, int letzte, Random zufall)
    {
        if (anzahl <= 1)
        {
            return 0;
        }
        var wahl = zufall.Next(anzahl - 1);
        return wahl >= letzte && letzte >= 0 ? wahl + 1 : wahl;
    }

    public static double NaechsterRuf(Random zufall) => RufMin + (zufall.NextDouble() * (RufMax - RufMin));

    public static double NaechsterRuf(Random zufall, Rufe rufe) => rufe.Min + (zufall.NextDouble() * (rufe.Max - rufe.Min));

    public static bool ImPark(StadtSzene szene, float x, float z) =>
        szene.Parks.Any(p => x >= Math.Min(p.X1, p.X2) && x <= Math.Max(p.X1, p.X2) && z >= Math.Min(p.Z1, p.Z2) && z <= Math.Max(p.Z1, p.Z2));

    public static bool VogelNah(StadtSzene szene, float x, float z) =>
        ImPark(szene, x, z) || szene.Baeume.Any(b => Vector2.Distance(b.Mitte, new Vector2(x, z)) <= BaumNah + b.R);

    /// <summary>Kleinster Abstand von (x, z) zur Mittellinie des Flusses; ohne Fluss unendlich.</summary>
    public static float FlussAbstand(StadtSzene szene, float x, float z)
    {
        var p = new Vector2(x, z);
        var best = float.PositiveInfinity;
        foreach (var linie in szene.Fluesse)
        {
            for (var i = 1; i < linie.Count; i++)
            {
                best = Math.Min(best, Abstand(p, linie[i - 1], linie[i]));
            }
        }
        return best;
    }

    private static float Abstand(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var laenge = ab.LengthSquared();
        var t = laenge <= 1e-9f ? 0f : Math.Clamp(Vector2.Dot(p - a, ab) / laenge, 0f, 1f);
        return Vector2.Distance(p, a + (ab * t));
    }

    /// <summary>
    /// Schleifen der Stadt fuer die Bildmitte (mx, mz): Fluss und jedes Auto nach Naehe (Nico 2a), Autos nur
    /// solange sie fahren (<paramref name="faehrt"/>); ein Auto, das gerade haelt, im Leerlauf.
    /// </summary>
    public static Stadtlage Stadt(StadtSzene szene, float mx, float mz, Verkehr? verkehr, bool faehrt)
    {
        var autos = new List<(string, float)>();
        foreach (var auto in verkehr?.Autos ?? [])
        {
            var (ort, _) = auto.Lage();
            var laut = faehrt ? Naehe(Vector2.Distance(ort, new Vector2(mx, mz))) * (auto.Haelt ? Leerlauf : 1f) : 0f;
            autos.Add((auto.Modell, laut));
        }
        return new Stadtlage(Naehe(FlussAbstand(szene, mx, mz)), VogelNah(szene, mx, mz), autos);
    }
}
