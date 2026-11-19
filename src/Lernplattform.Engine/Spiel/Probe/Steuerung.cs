namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle A 0.95.0 (Sammelliste 385 „frei gehen“, Weisung P12-306): Baustein der Engine fuer das freie Gehen mit Tasten (PC) und
/// spaeter mit dem Stick (Handy). Er kennt kein Spielwissen: aus der Eingabe und der Drehung der Kamera rechnet er eine
/// Gehrichtung (Diagonale nie schneller), und ein Schritt geht in kleinen Teilschritten gegen eine beliebige Sperre
/// (<c>frei(x, z)</c>), gleitet an ihr entlang und rastet hoechstens <see cref="Zustand.EinrastenRest"/> seitlich ein, wo der
/// Aufrufer es erlaubt. Das Spiel gibt die Sperre (draussen Laufraster und feine Sperre, drinnen das Raster der Wege).
/// </summary>
public static class Steuerung
{
    /// <summary>Laenge eines Teilschritts: nie weiter, damit keine Sperre (Raster 0,05 bzw. 0,25) uebersprungen wird.</summary>
    public const double Teilschritt = 0.02;

    /// <summary>Stick: Ausschlag (Anteil am Radius) unter dem nichts geschieht.</summary>
    public const double Totzone = 0.15;

    /// <summary>Stick: ab diesem Ausschlag volles Tempo (Kurzplan: „ab etwa drei Viertel“).</summary>
    public const double VollAb = 0.75;

    /// <summary>Einrasten: Stufen seitlich (je Seite), in denen ein gesperrter Teilschritt neben der Gehrichtung versucht wird.</summary>
    public const double EinrastenStufe = 0.02;

    /// <summary>
    /// Zustand ueber mehrere Schritte: wie viel seitliches Einrasten fuer die jetzige Gehrichtung noch erlaubt ist. Eine neue
    /// Richtung setzt ihn mit <see cref="Neu"/> zurueck.
    /// </summary>
    public sealed class Zustand
    {
        public double EinrastenRest { get; set; }

        public (double X, double Z) Richtung { get; private set; }

        /// <summary>Neue Gehrichtung: das Einrasten beginnt wieder bei <paramref name="einrasten"/>.</summary>
        public void Neu((double X, double Z) richtung, double einrasten)
        {
            if (Math.Abs(richtung.X - Richtung.X) > 1e-6 || Math.Abs(richtung.Z - Richtung.Z) > 1e-6)
            {
                Richtung = richtung;
                EinrastenRest = einrasten;
            }
        }
    }

    /// <summary>Eingabe aus vier gehaltenen Tasten: (vor, seitwaerts) je -1, 0 oder 1 (gegenueberliegende heben sich auf).</summary>
    public static (double Vor, double Seit) AusTasten(bool vor, bool zurueck, bool links, bool rechts) =>
        ((vor ? 1 : 0) - (zurueck ? 1 : 0), (rechts ? 1 : 0) - (links ? 1 : 0));

    /// <summary>
    /// Eingabe aus einem Stick: Versatz des Daumens (<paramref name="dx"/> nach rechts, <paramref name="dy"/> nach unten, in
    /// Bildpunkten) bei Radius <paramref name="radius"/>. Unter <see cref="Totzone"/> nichts, ab <see cref="VollAb"/> volles
    /// Tempo, dazwischen gleichmaessig; die Richtung bleibt die des Daumens.
    /// </summary>
    public static (double Vor, double Seit) AusStick(double dx, double dy, double radius)
    {
        if (radius <= 0)
        {
            return (0, 0);
        }
        var anteil = Math.Sqrt((dx * dx) + (dy * dy)) / radius;
        if (anteil < Totzone)
        {
            return (0, 0);
        }
        var staerke = Math.Min(1, (anteil - Totzone) / (VollAb - Totzone));
        var laenge = Math.Sqrt((dx * dx) + (dy * dy));
        return (-dy / laenge * staerke, dx / laenge * staerke);
    }

    /// <summary>
    /// Gehrichtung am Boden (x, z) aus der Eingabe und der Drehung der Kamera in Grad (0 = Blick nach -Z, wie
    /// <see cref="Kamera.Drehung"/>): „vor“ ist vom Bildschirm weg (in Blickrichtung), „seit“ nach rechts. Die Laenge ist
    /// hoechstens 1 (schraeg nie schneller), 0 bei keiner Eingabe.
    /// </summary>
    public static (double X, double Z) Richtung(double vor, double seit, double drehung)
    {
        var d = drehung * Math.PI / 180;
        var (vorX, vorZ) = (-Math.Sin(d), -Math.Cos(d));
        var (rechtsX, rechtsZ) = (Math.Cos(d), -Math.Sin(d));
        var x = (vorX * vor) + (rechtsX * seit);
        var z = (vorZ * vor) + (rechtsZ * seit);
        var laenge = Math.Sqrt((x * x) + (z * z));
        if (laenge < 1e-9)
        {
            return (0, 0);
        }
        return laenge > 1 ? (x / laenge, z / laenge) : (x, z);
    }

    /// <summary>Ergebnis eines Schritts: neue Stelle, wirklich gegangener Weg und ob eine Sperre den Schritt aufhielt.</summary>
    public readonly record struct Ergebnis(double X, double Z, double Weg, bool Angestossen);

    /// <summary>
    /// Ein Schritt von (<paramref name="x"/>, <paramref name="z"/>) in Richtung (<paramref name="rx"/>, <paramref name="rz"/>)
    /// (Laenge hoechstens 1, sie ist der Anteil am Tempo) ueber die Weite <paramref name="weite"/>, in Teilschritten von hoechstens
    /// <see cref="Teilschritt"/>. Ist ein Teilschritt gesperrt, geht er nur laengs der freien Achse weiter (Gleiten an der Wand),
    /// sonst, wo <paramref name="einrastenErlaubt"/> die neue Stelle annimmt, mit einem kleinen seitlichen Versatz quer zur
    /// Gehrichtung (nur vorwaerts, zusammen hoechstens <see cref="Zustand.EinrastenRest"/>), sonst bleibt die Figur stehen.
    /// <paramref name="frei"/> muss fuer jede Stelle des Ergebnisses gelten (die Startstelle wird nie verlassen, wenn kein
    /// Teilschritt frei ist).
    /// </summary>
    public static Ergebnis Schritt(double x, double z, double rx, double rz, double weite, Func<double, double, bool> frei,
        Zustand? zustand = null, Func<double, double, bool>? einrastenErlaubt = null)
    {
        var laenge = Math.Sqrt((rx * rx) + (rz * rz));
        if (laenge < 1e-9 || weite <= 0)
        {
            return new(x, z, 0, false);
        }
        var gesamt = weite * Math.Min(1, laenge);
        var (ex, ez) = (rx / laenge, rz / laenge);
        var teile = Math.Max(1, (int)Math.Ceiling(gesamt / Teilschritt));
        var stueck = gesamt / teile;
        var weg = 0.0;
        var angestossen = false;
        for (var i = 0; i < teile; i++)
        {
            var (sx, sz) = (ex * stueck, ez * stueck);
            if (frei(x + sx, z + sz))
            {
                (x, z) = (x + sx, z + sz);
                weg += stueck;
                continue;
            }
            angestossen = true;
            // Gleiten: nur der Anteil laengs einer Achse, die frei ist (der groessere zuerst)
            var achsen = Math.Abs(sx) >= Math.Abs(sz) ? new[] { (sx, 0.0), (0.0, sz) } : new[] { (0.0, sz), (sx, 0.0) };
            var geglitten = false;
            foreach (var (ax, az) in achsen)
            {
                if (Math.Abs(ax) + Math.Abs(az) > 1e-9 && frei(x + ax, z + az))
                {
                    (x, z) = (x + ax, z + az);
                    weg += Math.Abs(ax) + Math.Abs(az);
                    geglitten = true;
                    break;
                }
            }
            if (geglitten)
            {
                continue;
            }
            if (einrastenErlaubt is not null && zustand is { EinrastenRest: > 1e-9 } z0 && Einrasten(x, z, ex, ez, stueck, frei, einrastenErlaubt, z0) is { } neu)
            {
                weg += Math.Sqrt(((neu.X - x) * (neu.X - x)) + ((neu.Z - z) * (neu.Z - z)));
                (x, z) = neu;
                continue;
            }
            break;
        }
        return new(x, z, weg, angestossen);
    }

    /// <summary>Kleiner seitlicher Versatz mit einem Teilschritt nach vorn (naechste Stufe zuerst, beide Seiten), sonst null.</summary>
    private static (double X, double Z)? Einrasten(double x, double z, double ex, double ez, double stueck, Func<double, double, bool> frei,
        Func<double, double, bool> erlaubt, Zustand zustand)
    {
        var (qx, qz) = (-ez, ex);
        for (var versatz = EinrastenStufe; versatz <= zustand.EinrastenRest + 1e-9; versatz += EinrastenStufe)
        {
            foreach (var seite in new[] { 1.0, -1.0 })
            {
                var (nx, nz) = (x + (ex * stueck) + (qx * versatz * seite), z + (ez * stueck) + (qz * versatz * seite));
                if (frei(nx, nz) && erlaubt(nx, nz))
                {
                    zustand.EinrastenRest -= versatz;
                    return (nx, nz);
                }
            }
        }
        return null;
    }
}
