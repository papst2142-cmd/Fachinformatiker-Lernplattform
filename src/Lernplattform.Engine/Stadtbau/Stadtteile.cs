using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Welle Nachladen A 0.82.0 / S 0.54.0 (Weisung P12-280, Nico 1c): jede Karte ist in feste Kacheln von
/// <see cref="Groesse"/> × <see cref="Groesse"/> geteilt (Stadtteile). Name aus der Lage: Buchstabe der Spalte (A = links)
/// und Nummer der Reihe (1 = oben), wie A1, B3. Nico muss nichts tun; jede Stadt, auch Stadt 1, hat sofort Kacheln.
/// Stadtteil-Namen (seit A 0.83.0 / S 0.55.0, Weisung P12-282): gezogene Bereiche nur zur Anzeige, Abschnitt
/// <see cref="AbschnittNamen"/> der Stadtdaten (Formatnummer 3, <see cref="Bereich"/>, Regeln <see cref="Fehler"/>).
/// </summary>
public static class Stadtteile
{
    /// <summary>Kantenlaenge einer Kachel (Felder); 2 × 2 Zellen des Paket-Netzes.</summary>
    public const float Groesse = 16f;

    /// <summary>Groesste Karte je Seite (Nico 2b).</summary>
    public const double GroessteSeite = 256;

    /// <summary>Bis zu dieser Seitenlaenge bleibt die Stadt-Datei im alten Format (sonst Formatnummer 3).</summary>
    public const double AltesFormatBis = 64;

    /// <summary>Abschnitt der Stadtdaten fuer gezogene Stadtteil-Namen (Liste von <see cref="Bereich"/>, seit S 0.55.0).</summary>
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

    // ---- Stadtteil-Namen (Welle A 0.83.0 / S 0.55.0, Weisung P12-282 Punkt 2 und 3) ------------------------------------

    /// <summary>Ein Name ist hoechstens so lang (Zeichen); das Schild im Spiel und die Karte zeigen ihn ganz.</summary>
    public const int NameHoechstens = 24;

    /// <summary>Ein Bereich ist mindestens so breit und so tief (Felder).</summary>
    public const double BereichMindestens = 4;

    /// <summary>Raster der Ecken eines Bereichs (wie alles andere im Werkzeug).</summary>
    public const double BereichRaster = 0.5;

    /// <summary>
    /// Ein gezogener Stadtteil mit Namen: Rechteck von (<see cref="X1"/>, <see cref="Z1"/>) bis (<see cref="X2"/>, <see cref="Z2"/>),
    /// Norden oben. NUR Anzeige (Karte im Spiel-Handy, Schild beim Betreten); keine Spielregel, kein Ereignis, kein
    /// Fingerabdruck. Unabhaengig vom Raster der Kacheln (<see cref="Kachel"/>), das nur das Laden ordnet.
    /// </summary>
    public sealed record Bereich(string Name, double X1, double Z1, double X2, double Z2)
    {
        /// <summary>Liegt der Punkt darin (links und oben eingeschlossen, rechts und unten nicht; wie eine Kachel).</summary>
        public bool Drin(double x, double z) => x >= X1 && x < X2 && z >= Z1 && z < Z2;

        public Vector2 Mitte => new((float)((X1 + X2) / 2), (float)((Z1 + Z2) / 2));

        /// <summary>Als Eintrag der Stadtdaten (Schluessel sortiert wie jede Stadt-Datei).</summary>
        public PyObjekt AlsDaten() => new() { { "name", Name }, { "x1", X1 }, { "x2", X2 }, { "z1", Z1 }, { "z2", Z2 } };
    }

    /// <summary>
    /// Alle gueltigen Bereiche der Stadtdaten in ihrer Reihenfolge (ohne Abschnitt: leer). Ein ungueltiger Eintrag fehlt hier
    /// (das Spiel zeigt ihn nicht), die Pruefung (<see cref="Fehler"/>) meldet ihn rot und sperrt damit den Export; nie still.
    /// </summary>
    public static IReadOnlyList<Bereich> Bereiche(PyObjekt? stadtdaten)
    {
        if (stadtdaten is null || !Py.Hat(stadtdaten, AbschnittNamen) || stadtdaten[AbschnittNamen] is not List<object?> liste)
        {
            return [];
        }
        var ergebnis = new List<Bereich>();
        foreach (var e in liste)
        {
            if (Lesen(e) is { } b && b.Name.Trim().Length > 0 && b.X2 > b.X1 && b.Z2 > b.Z1)
            {
                ergebnis.Add(b);
            }
        }
        return ergebnis;
    }

    /// <summary>Der Bereich am Punkt (der erste in der Reihenfolge der Daten), sonst null.</summary>
    public static Bereich? An(IReadOnlyList<Bereich> bereiche, double x, double z) => bereiche.FirstOrDefault(b => b.Drin(x, z));

    private static Bereich? Lesen(object? e)
    {
        if (e is not PyObjekt o || !Py.Hat(o, "name") || o["name"] is not string name)
        {
            return null;
        }
        double? Zahl(string k) => Py.Hat(o, k) && o[k] is long or double ? Py.F(o[k]) : null;
        return Zahl("x1") is { } x1 && Zahl("z1") is { } z1 && Zahl("x2") is { } x2 && Zahl("z2") is { } z2 ? new Bereich(name, x1, z1, x2, z2) : null;
    }

    /// <summary>
    /// Regeln der Stadtteil-Namen (rot): Abschnitt ist eine Liste; jeder Eintrag hat genau name, x1, z1, x2, z2; Name nicht
    /// leer, ohne Leerzeichen am Rand, hoechstens <see cref="NameHoechstens"/> Zeichen, jeder Name nur einmal (gross/klein
    /// gleich); Ecken auf dem Raster 0,5, ganz auf der Karte, mindestens 4 × 4; zwei Bereiche ueberdecken sich nicht.
    /// Rueckgabe: Text und Stelle je Fund.
    /// </summary>
    public static List<(string Text, double X, double Z)> Fehler(PyObjekt? stadtdaten, double breite, double tiefe)
    {
        var fehler = new List<(string, double, double)>();
        if (stadtdaten is null || !Py.Hat(stadtdaten, AbschnittNamen))
        {
            return fehler;
        }
        if (stadtdaten[AbschnittNamen] is not List<object?> liste)
        {
            fehler.Add(("Der Abschnitt „stadtteile“ ist keine Liste.", breite / 2, tiefe / 2));
            return fehler;
        }
        var gut = new List<(int Nr, Bereich B)>();
        var namen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < liste.Count; i++)
        {
            var nr = i + 1;
            var o = liste[i] as PyObjekt;
            var b = Lesen(liste[i]);
            if (o is null || b is null || o.Count != 5)
            {
                fehler.Add(($"Stadtteil Nr. {nr} ist falsch gespeichert (nötig: name, x1, z1, x2, z2).", breite / 2, tiefe / 2));
                continue;
            }
            var (mx, mz) = ((b.X1 + b.X2) / 2, (b.Z1 + b.Z2) / 2);
            var name = b.Name;
            if (name.Trim().Length == 0)
            {
                fehler.Add(($"Stadtteil Nr. {nr} hat keinen Namen.", mx, mz));
            }
            else if (name.Trim() != name)
            {
                fehler.Add(($"Der Name von Stadtteil Nr. {nr} („{name}“) beginnt oder endet mit einem Leerzeichen.", mx, mz));
            }
            else if (name.Length > NameHoechstens)
            {
                fehler.Add(($"Der Name „{name}“ ist länger als {NameHoechstens} Zeichen.", mx, mz));
            }
            else if (namen.TryGetValue(name, out var frueher))
            {
                fehler.Add(($"Den Namen „{name}“ tragen Stadtteil Nr. {frueher} und Nr. {nr}.", mx, mz));
            }
            else
            {
                namen[name] = nr;
            }
            static bool AufRaster(double v) => Math.Abs((v / BereichRaster) - Math.Round(v / BereichRaster)) < 1e-9;
            if (!AufRaster(b.X1) || !AufRaster(b.Z1) || !AufRaster(b.X2) || !AufRaster(b.Z2))
            {
                fehler.Add(($"Die Ecken von Stadtteil „{name}“ liegen nicht auf dem Raster 0,5.", mx, mz));
            }
            if (b.X1 < 0 || b.Z1 < 0 || b.X2 > breite || b.Z2 > tiefe)
            {
                fehler.Add(($"Stadtteil „{name}“ reicht über den Rand der Karte.", Math.Clamp(mx, 0, breite), Math.Clamp(mz, 0, tiefe)));
            }
            if (b.X2 - b.X1 < BereichMindestens - 1e-9 || b.Z2 - b.Z1 < BereichMindestens - 1e-9)
            {
                fehler.Add(($"Stadtteil „{name}“ ist kleiner als 4 × 4.", mx, mz));
                continue;
            }
            foreach (var (andereNr, a) in gut)
            {
                if (Math.Min(a.X2, b.X2) - Math.Max(a.X1, b.X1) > 1e-9 && Math.Min(a.Z2, b.Z2) - Math.Max(a.Z1, b.Z1) > 1e-9)
                {
                    fehler.Add(($"Stadtteil „{a.Name}“ (Nr. {andereNr}) und „{name}“ (Nr. {nr}) überdecken sich.",
                        (Math.Max(a.X1, b.X1) + Math.Min(a.X2, b.X2)) / 2, (Math.Max(a.Z1, b.Z1) + Math.Min(a.Z2, b.Z2)) / 2));
                }
            }
            gut.Add((nr, b));
        }
        return fehler;
    }
}

/// <summary>
/// Welle Nachladen (Weisung P12-280, Plan P12-165 Abschnitt 3): welche Kacheln das Spiel mit fester Kamera geladen haben muss.
/// Nah = jede Kachel, die den Kreis <see cref="Nah"/> um die Figur beruehrt (die feste Kamera sieht den Boden hoechstens rund 17
/// von der Figur, Abstand 7 bis 9, Neigung 35 bis 70, Sichtfeld 40, Breitbild; plus Zugabe): sie liegt auf der Grafikkarte.
/// Vorbereitet (im Hintergrund gebaut, noch nicht hochgeladen) wird bis <see cref="Vor"/>. Freigegeben wird erst ueber
/// <see cref="NahWeg"/> bzw. <see cref="VorWeg"/> (kein Hin und Her an der Grenze). Die Kreise sind so klein gehalten, dass
/// das gleichzeitig Geladene unter 48 MB Netzdaten bleibt (Nico 4a). Reine Rechnung ohne Grafikkarte.
/// </summary>
public static class Ladeplan
{
    /// <summary>Kreis der Kacheln, die gezeichnet werden muessen (auf der Grafikkarte).</summary>
    public const float Nah = 24f;

    /// <summary>Ab diesem Abstand wird eine Kachel auf der Grafikkarte freigegeben.</summary>
    public const float NahWeg = Nah + 4f;

    /// <summary>Kreis der Kacheln, die im Hintergrund vorbereitet werden (der naechste Ring nach dem nahen Kreis).</summary>
    public const float Vor = Nah + 8f;

    /// <summary>Ab diesem Abstand wird eine vorbereitete (noch nicht hochgeladene) Kachel verworfen.</summary>
    public const float VorWeg = Vor + 4f;

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

    /// <summary>Darf eine Kachel auf der Grafikkarte freigegeben werden?</summary>
    public static bool FreigebenNah(Stadtteile.Kachel k, float x, float z) => k.Abstand(new Vector2(x, z)) > NahWeg;

    /// <summary>Darf eine vorbereitete Kachel verworfen werden?</summary>
    public static bool FreigebenVor(Stadtteile.Kachel k, float x, float z) => k.Abstand(new Vector2(x, z)) > VorWeg;
}
