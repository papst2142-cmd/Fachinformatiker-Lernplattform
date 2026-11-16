using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Welle Nachladen A 0.82.0 / S 0.54.0 (Weisung P12-280, Nico 1c): jede Karte ist in feste Kacheln von
/// <see cref="Groesse"/> × <see cref="Groesse"/> geteilt (Stadtteile). Name aus der Lage: Buchstabe der Spalte (A = links)
/// und Nummer der Reihe (1 = oben), wie A1, B3. Nico muss nichts tun; jede Stadt, auch Stadt 1, hat sofort Kacheln.
/// Stadtteil-Namen als gezogene Bereiche (nur Anzeige) kommen spaeter (Welle S 0.55.0); das Format nimmt sie schon auf
/// (Abschnitt <see cref="AbschnittNamen"/> in den Stadtdaten, Formatnummer 3).
/// </summary>
public static class Stadtteile
{
    /// <summary>Kantenlaenge einer Kachel (Felder); 2 × 2 Zellen des Paket-Netzes.</summary>
    public const float Groesse = 16f;

    /// <summary>Groesste Karte je Seite (Nico 2b).</summary>
    public const double GroessteSeite = 256;

    /// <summary>Bis zu dieser Seitenlaenge bleibt die Stadt-Datei im alten Format (sonst Formatnummer 3).</summary>
    public const double AltesFormatBis = 64;

    /// <summary>Abschnitt der Stadtdaten fuer gezogene Stadtteil-Namen (spaetere Welle; heute nur gelesen und geprueft).</summary>
    public const string AbschnittNamen = "stadtteile";

    /// <summary>Eine Kachel (Spalte von links, Reihe von oben, je ab 0).</summary>
    public readonly record struct Kachel(int Spalte, int Reihe)
    {
        /// <summary>Linke obere Ecke (x, z).</summary>
        public Vector2 Min => new(Spalte * Groesse, Reihe * Groesse);

        /// <summary>Rechte untere Ecke (x, z).</summary>
        public Vector2 Max => new((Spalte + 1) * Groesse, (Reihe + 1) * Groesse);

        /// <summary>Mitte der Kachel.</summary>
        public Vector2 Mitte => (Min + Max) / 2;

        /// <summary>Name wie A1, B3 (ab Spalte 26 zwei Buchstaben: AA, AB …).</summary>
        public string Name => Buchstaben(Spalte) + (Reihe + 1).ToString(CultureInfo.InvariantCulture);

        /// <summary>Kuerzester Abstand eines Punkts zur Kachel (0 = darin).</summary>
        public float Abstand(Vector2 p)
        {
            var dx = MathF.Max(MathF.Max(Min.X - p.X, 0f), p.X - Max.X);
            var dz = MathF.Max(MathF.Max(Min.Y - p.Y, 0f), p.Y - Max.Y);
            return MathF.Sqrt((dx * dx) + (dz * dz));
        }

        /// <summary>Liegt der Punkt darin (links und oben eingeschlossen, rechts und unten nicht).</summary>
        public bool Drin(float x, float z) => x >= Min.X && x < Max.X && z >= Min.Y && z < Max.Y;
    }

    private static string Buchstaben(int spalte) =>
        spalte < 26 ? ((char)('A' + spalte)).ToString() : Buchstaben((spalte / 26) - 1) + (char)('A' + (spalte % 26));

    /// <summary>Spalten und Reihen einer Karte von <paramref name="breite"/> × <paramref name="tiefe"/>.</summary>
    public static (int Spalten, int Reihen) Raster(float breite, float tiefe) =>
        (Math.Max(1, (int)MathF.Ceiling(breite / Groesse)), Math.Max(1, (int)MathF.Ceiling(tiefe / Groesse)));

    /// <summary>Alle Kacheln einer Karte, Reihe fuer Reihe.</summary>
    public static IEnumerable<Kachel> Alle(float breite, float tiefe)
    {
        var (s, r) = Raster(breite, tiefe);
        for (var j = 0; j < r; j++)
        {
            for (var i = 0; i < s; i++)
            {
                yield return new Kachel(i, j);
            }
        }
    }

    /// <summary>Die Kachel, in der der Punkt liegt (am Rand und ausserhalb die naechste der Karte).</summary>
    public static Kachel Von(float x, float z, float breite, float tiefe)
    {
        var (s, r) = Raster(breite, tiefe);
        return new Kachel(Math.Clamp((int)MathF.Floor(x / Groesse), 0, s - 1), Math.Clamp((int)MathF.Floor(z / Groesse), 0, r - 1));
    }

    /// <summary>Ist die Karte groesser als <see cref="AltesFormatBis"/> (Formatnummer 3)?</summary>
    public static bool GrosseKarte(PyObjekt? karte) =>
        karte is not null && Py.Hat(karte, "welt") && karte["welt"] is PyObjekt welt
        && ((Py.Hat(welt, "breite") && welt["breite"] is long or double && Py.F(welt["breite"]) > AltesFormatBis)
            || (Py.Hat(welt, "hoehe") && welt["hoehe"] is long or double && Py.F(welt["hoehe"]) > AltesFormatBis));

    /// <summary>Haben die Stadtdaten den Abschnitt der Stadtteil-Namen (Formatnummer 3)?</summary>
    public static bool HatNamen(PyObjekt? stadtdaten) => stadtdaten is not null && Py.Hat(stadtdaten, AbschnittNamen);
}

/// <summary>
/// Welle Nachladen (Weisung P12-280, Plan P12-165 Abschnitt 3): welche Kacheln das Spiel mit fester Kamera geladen haben muss.
/// Nah = jede Kachel, die den Kreis <see cref="Nah"/> um die Figur beruehrt (die feste Kamera sieht den Boden bis rund 19,
/// Abstand 9, Neigung 35, Sichtfeld 40, Breitbild; plus Zugabe). Vorladen = bis <see cref="Vor"/>. Entladen erst ueber
/// <see cref="Weg"/> (kein Hin und Her an der Grenze). Reine Rechnung ohne Grafikkarte.
/// </summary>
public static class Ladeplan
{
    /// <summary>Kreis der Kacheln, die gezeichnet werden muessen.</summary>
    public const float Nah = 24f;

    /// <summary>Kreis der Kacheln, die im Hintergrund vorbereitet werden.</summary>
    public const float Vor = Nah + Stadtteile.Groesse;

    /// <summary>Ab diesem Abstand wird eine Kachel freigegeben.</summary>
    public const float Weg = Nah + (2 * Stadtteile.Groesse);

    /// <summary>Kacheln im Kreis <paramref name="radius"/> um (x, z), naechste zuerst.</summary>
    public static List<Stadtteile.Kachel> ImKreis(float x, float z, float radius, float breite, float tiefe)
    {
        var p = new Vector2(x, z);
        return [.. Stadtteile.Alle(breite, tiefe).Where(k => k.Abstand(p) <= radius).OrderBy(k => k.Abstand(p)).ThenBy(k => k.Reihe).ThenBy(k => k.Spalte)];
    }

    /// <summary>Kacheln, die nah gebraucht werden.</summary>
    public static List<Stadtteile.Kachel> Noetig(float x, float z, float breite, float tiefe) => ImKreis(x, z, Nah, breite, tiefe);

    /// <summary>Kacheln, die vorbereitet werden sollen (nah zuerst, dann der Ring).</summary>
    public static List<Stadtteile.Kachel> Vorbereiten(float x, float z, float breite, float tiefe) => ImKreis(x, z, Vor, breite, tiefe);

    /// <summary>Darf eine geladene Kachel freigegeben werden?</summary>
    public static bool Freigeben(Stadtteile.Kachel k, float x, float z) => k.Abstand(new Vector2(x, z)) > Weg;
}
