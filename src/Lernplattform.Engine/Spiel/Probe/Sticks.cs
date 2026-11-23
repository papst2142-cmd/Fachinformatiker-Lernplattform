namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle A 0.96.0 (Sammelliste 385 Welle 2, Weisung P12-309, Nico Antworten 2a und 4): die zwei Sticks am Handy und Tablet,
/// ohne Spielwissen. Ein Daumen, der im unteren Teil des freien Bereichs (zwischen Kopfzeile und Leiste, <see cref="ZoneOben"/>)
/// aufsetzt, ist zuerst nur ein Kandidat: bewegt er sich weniger als <see cref="AnfangAb"/>, bleibt er ein Tipp (Antippen wie heute). Erst ab
/// diesem kleinen Weg wird er zum Stick, links zum Stick fuer das Gehen, rechts zum Stick fuer das Drehen. Der Stick erscheint,
/// wo der Daumen aufsetzte, und verschwindet beim Loslassen. Er liegt immer ganz im freien Bereich (nie ueber Kopfzeile oder
/// Leiste; ist der Bereich zu klein, wird er kleiner). Beide Sticks gehen gleichzeitig (zwei Daumen). Die Eingabe rechnet
/// <see cref="Steuerung.AusStick"/> (Totzone, volles Tempo ab drei Vierteln). Die Flaeche fuehrt die Finger und entscheidet,
/// wann zwei Finger wie heute zoomen und drehen (dann vergisst sie die Kandidaten).
/// </summary>
public sealed class Sticks
{
    /// <summary>Radius eines Sticks in Punkten (Durchmesser etwa 120 dp, Weisung P12-309).</summary>
    public const double Radius = 60;

    /// <summary>Weg des Daumens in Punkten, ab dem aus einem Tipp ein Stick wird (gleich dem Ziehen der Flaeche).</summary>
    public const double AnfangAb = 8;

    /// <summary>
    /// Rechter Stick: so viele Punkte je Sekunde zieht er bei vollem Ausschlag, als zoege man mit der rechten Maustaste
    /// (0,4 Grad je Punkt, also 120 Grad je Sekunde Drehung).
    /// </summary>
    public const double PunkteJeSekunde = 300;

    /// <summary>Welcher Stick: links = gehen, rechts = drehen (draussen die Kamera, in der Ich-Sicht den Blick).</summary>
    public enum Seite
    {
        Links,
        Rechts,
    }

    /// <summary>
    /// Freier Bereich der Flaeche: Breite und Hoehe in Punkten, oben die Kopfzeile, unten die Leiste (verdeckt). Welle A 0.99.0
    /// (Sammelliste 399): die Leiste steht mittig und reicht waagerecht nur von <paramref name="LeisteVon"/> bis
    /// <paramref name="LeisteBis"/>; links und rechts daneben setzt ein Daumen auch ganz unten einen Stick (dort liegen die
    /// Daumen). Ohne Angabe (0, 0) verdeckt die Leiste wie bis A 0.98.0 die ganze Breite.
    /// </summary>
    public readonly record struct Bereich(double Breite, double Hoehe, double Oben, double Unten, double LeisteVon = 0, double LeisteBis = 0)
    {
        /// <summary>Liegt (x, y) unten neben der Leiste (frei fuer einen Daumen)?</summary>
        public bool NebenLeiste(double x, double y) =>
            LeisteBis > LeisteVon && y > Hoehe - Unten && y <= Hoehe && (x < LeisteVon || x > LeisteBis);

        public double FreiHoehe => Math.Max(0, Hoehe - Oben - Unten);

        /// <summary>
        /// Welle A 1.0.0 (Sammelliste 405): Flaeche (x1, y1, x2, y2), die frei bleibt (die runden Knoepfe am rechten Daumen mit Rand):
        /// dort setzt kein Daumen einen Stick, und ein Stick ragt nie hinein. default = keine.
        /// </summary>
        public (double X1, double Y1, double X2, double Y2) Sperre { get; init; }

        /// <summary>
        /// Welle A 1.6.0 (Sammelliste 444): Flaeche, die der linke Stick frei haelt (Knopf der Handlung und Symbol der Ich-Sicht
        /// ueber dem linken Stick, mit Rand); default = keine.
        /// </summary>
        public (double X1, double Y1, double X2, double Y2) SperreLinks { get; init; }

        /// <summary>Liegt (x, y) in der <see cref="Sperre"/> oder der <see cref="SperreLinks"/>?</summary>
        /// <summary>
        /// Welle A 1.6.0 (Weisung P12-361): Flaeche des Handy-Symbols neben einem Stick (x1, y1, x2, y2); dort setzt kein Stick auf,
        /// und ein Stick daneben rueckt nach aussen (in die Ecke); default = keines.
        /// </summary>
        public (double X1, double Y1, double X2, double Y2) SperreTelefon { get; init; }

        public bool Gesperrt(double x, double y) => In(Sperre, x, y) || In(SperreLinks, x, y) || In(SperreTelefon, x, y);

        private static bool In((double X1, double Y1, double X2, double Y2) q, double x, double y) =>
            q.X2 > q.X1 && x >= q.X1 && x <= q.X2 && y >= q.Y1 && y <= q.Y2;
    }

    /// <summary>
    /// Welle A 1.0.0 (Sammelliste 405, Nico: „Sticks weiter nach unten in die Ecken“): Abstand eines Sticks zum unteren Rand, wenn er
    /// neben der Leiste liegt (dort ist bis zum Rand Platz), und zur Leiste. Bis A 0.99.0 endete jeder Stick ueber der Hoehe der Leiste
    /// (<see cref="AltFuerTests"/>).
    /// </summary>
    public const double EckRand = 8;

    private static readonly AsyncLocal<bool> Alt = new();

    /// <summary>Gegenprobe (nur Tests, AsyncLocal): Sticks enden ueber der Hoehe der Leiste und kennen keine Sperre (Stand A 0.99.0).</summary>
    public static bool AltFuerTests { get => Alt.Value; set => Alt.Value = value; }

    /// <summary>
    /// Ein sichtbarer Stick: Mitte (wo der Daumen aufsetzte, nach <see cref="Lage"/> in den freien Bereich gerueckt), Stelle des
    /// Aufsetzens, Daumen jetzt, Radius (hoechstens <see cref="Radius"/>). Der Ausschlag zaehlt immer ab der Stelle des
    /// Aufsetzens: ist die Mitte gerueckt (Daumen nah an der Leiste), geht die Figur trotzdem erst, wenn der Daumen zieht.
    /// </summary>
    public sealed class Stick
    {
        internal Stick(long finger, Seite seite, double mitteX, double mitteY, double radius, double startX, double startY)
        {
            Finger = finger;
            Art = seite;
            MitteX = mitteX;
            MitteY = mitteY;
            Groesse = radius;
            StartX = startX;
            StartY = startY;
            DaumenX = startX;
            DaumenY = startY;
        }

        public double StartX { get; }

        public double StartY { get; }

        public long Finger { get; }

        public Seite Art { get; }

        public double MitteX { get; }

        public double MitteY { get; }

        /// <summary>Radius dieses Sticks in Punkten.</summary>
        public double Groesse { get; }

        public double DaumenX { get; internal set; }

        public double DaumenY { get; internal set; }

        /// <summary>Knopf: ab der Mitte um den Ausschlag des Daumens versetzt, hoechstens bis zum Rand.</summary>
        public (double X, double Y) Knopf
        {
            get
            {
                var (dx, dy) = (DaumenX - StartX, DaumenY - StartY);
                var laenge = Math.Sqrt((dx * dx) + (dy * dy));
                return laenge <= Groesse || laenge < 1e-9 ? (MitteX + dx, MitteY + dy) : (MitteX + (dx / laenge * Groesse), MitteY + (dy / laenge * Groesse));
            }
        }

        /// <summary>Eingabe (vor, seitwaerts) wie <see cref="Steuerung.AusStick"/>, Ausschlag ab der Stelle des Aufsetzens.</summary>
        public (double Vor, double Seit) Eingabe => Steuerung.AusStick(DaumenX - StartX, DaumenY - StartY, Groesse);
    }

    private readonly Dictionary<long, (Seite Seite, double X, double Y, Bereich Bereich)> _kandidaten = [];

    public Stick? Links { get; private set; }

    public Stick? Rechts { get; private set; }

    /// <summary>Ist gerade ein Stick zu sehen?</summary>
    public bool Aktiv => Links is not null || Rechts is not null;

    /// <summary>Kandidaten (Daumen unten, noch nicht gezogen).</summary>
    public int Kandidaten => _kandidaten.Count;

    /// <summary>
    /// Oberer Rand des Bereichs, in dem ein Daumen zum Stick werden kann: das untere Drittel des freien Bereichs, mindestens so
    /// hoch wie ein Stick (2 Radien), hoechstens die untere Haelfte. Die Mitte des Bilds bleibt so fuer die Gesten von heute
    /// (zwei Finger zoomen und drehen, ein Finger sieht sich in der Ich-Sicht um).
    /// </summary>
    public static double ZoneOben(Bereich b) =>
        b.Hoehe - b.Unten - Math.Max(b.FreiHoehe / 3, Math.Min(b.FreiHoehe / 2, 2 * Radius));

    /// <summary>
    /// Seite fuer einen Daumen bei (<paramref name="x"/>, <paramref name="y"/>): links oder rechts im unteren Teil des freien
    /// Bereichs (ab <see cref="ZoneOben"/>), sonst null (oben, in der Kopfzeile oder in der Leiste, oder der Bereich ist so klein, dass der Stick
    /// kleiner als sein halber Radius wuerde).
    /// </summary>
    public static Seite? SeiteFuer(double x, double y, Bereich b)
    {
        if (b.Breite < 2 * Radius || b.FreiHoehe < Radius || x < 0 || x > b.Breite || (y > b.Hoehe - b.Unten && !b.NebenLeiste(x, y)) || y < ZoneOben(b)
            || (!Alt.Value && b.Gesperrt(x, y)))
        {
            return null;
        }
        return x < b.Breite / 2 ? Seite.Links : Seite.Rechts;
    }

    /// <summary>
    /// Mitte und Radius eines Sticks, der bei (<paramref name="x"/>, <paramref name="y"/>) aufsetzt: so nah wie moeglich an der
    /// Stelle, ganz im freien Bereich und in seiner Haelfte (links oder rechts); der Radius wird kleiner, wenn der Bereich nicht
    /// reicht (bei einem Bereich nach <see cref="SeiteFuer"/> nie kleiner als die Haelfte).
    /// </summary>
    public static (double X, double Y, double Radius) Lage(double x, double y, Seite seite, Bereich b)
    {
        var halbBreite = b.Breite / 2;
        var r = Math.Min(Radius, Math.Min(b.FreiHoehe / 2, halbBreite / 2));
        var (links, rechts) = seite == Seite.Links ? (r, halbBreite - r) : (halbBreite + r, b.Breite - r);
        var mx = rechts < links ? (links + rechts) / 2 : Math.Clamp(x, links, rechts);
        var (oben, unten) = (b.Oben + r, b.Hoehe - b.Unten - r);
        // A 1.0.0: neben der Leiste bis an den unteren Rand (die Ecke), solange der Kreis die Leiste waagerecht nicht beruehrt
        if (!Alt.Value && b.LeisteBis > b.LeisteVon && (seite == Seite.Links ? mx + r <= b.LeisteVon - EckRand : mx - r >= b.LeisteBis + EckRand))
        {
            unten = Math.Max(unten, b.Hoehe - EckRand - r);
        }
        var my = unten < oben ? (oben + unten) / 2 : Math.Clamp(y, oben, unten);
        // A 1.0.0: nie in die Sperre (runde Knoepfe): zuerst nach links (in der eigenen Haelfte), sonst nach oben ruecken.
        // A 1.2.0 (Sammelliste 412): stehen die Knoepfe ueber dem Stick, rueckt er zuerst nach unten (in die Ecke)
        // Welle A 1.6.0 (Sammelliste 444): der linke Stick haelt den Knopf der Handlung frei (gespiegelt: zuerst nach unten in die
        // Ecke, sonst nach rechts, sonst nach links (sehr flaches Bild: die Knoepfe stehen rechts neben der Ecke), sonst nach oben)
        if (!Alt.Value && seite == Seite.Links && b.SperreLinks.X2 > b.SperreLinks.X1 && KreisInRechteck(mx, my, r, b.SperreLinks))
        {
            var q = b.SperreLinks;
            if (q.Y2 + r <= unten + 1e-9)
            {
                my = Math.Max(my, q.Y2 + r);
            }
            else if (q.X2 + r <= rechts)
            {
                mx = Math.Max(mx, q.X2 + r);
            }
            else if (q.X1 - r >= links)
            {
                mx = Math.Min(mx, q.X1 - r);
            }
            else
            {
                my = Math.Max(oben, Math.Min(my, q.Y1 - r));
            }
        }
        if (!Alt.Value && b.Sperre.X2 > b.Sperre.X1 && KreisInRechteck(mx, my, r, b.Sperre))
        {
            var linksDavor = b.Sperre.X1 - r;
            if (b.Sperre.Y2 + r <= unten + 1e-9)
            {
                my = Math.Max(my, b.Sperre.Y2 + r);
            }
            else if (linksDavor >= links)
            {
                mx = Math.Min(mx, linksDavor);
            }
            else
            {
                my = Math.Max(oben, Math.Min(my, b.Sperre.Y1 - r));
            }
        }
        // Welle A 1.6.0 (Weisung P12-361): neben dem Handy-Symbol rueckt der Stick nach aussen (in die Ecke), sonst nach oben
        if (!Alt.Value && b.SperreTelefon.X2 > b.SperreTelefon.X1 && KreisInRechteck(mx, my, r, b.SperreTelefon))
        {
            var q = b.SperreTelefon;
            if (seite == Seite.Links && q.X1 - r >= links)
            {
                mx = Math.Min(mx, q.X1 - r);
            }
            else if (seite == Seite.Rechts && q.X2 + r <= rechts)
            {
                mx = Math.Max(mx, q.X2 + r);
            }
            else
            {
                my = Math.Max(oben, Math.Min(my, q.Y1 - r));
            }
        }
        return (mx, my, r);
    }

    /// <summary>Beruehrt der Kreis (Mitte, Radius) das Rechteck?</summary>
    public static bool KreisInRechteck(double x, double y, double r, (double X1, double Y1, double X2, double Y2) q)
    {
        var dx = x - Math.Clamp(x, q.X1, q.X2);
        var dy = y - Math.Clamp(y, q.Y1, q.Y2);
        return (dx * dx) + (dy * dy) < r * r - 1e-6;
    }

    /// <summary>Daumen setzt auf: true = Kandidat fuer den Stick seiner Seite (die Seite hat noch keinen Stick).</summary>
    public bool Druecken(long finger, double x, double y, Bereich b)
    {
        if (SeiteFuer(x, y, b) is not { } seite || (seite == Seite.Links ? Links : Rechts) is not null
            || _kandidaten.Values.Any(k => k.Seite == seite))
        {
            return false;
        }
        _kandidaten[finger] = (seite, x, y, b);
        return true;
    }

    /// <summary>
    /// Daumen bewegt sich: ein Kandidat wird ab <see cref="AnfangAb"/> zum Stick, wenn <paramref name="allein"/> (kein zweiter
    /// Finger zoomt gerade); ein Stick folgt dem Daumen. Rueckgabe: der Stick dieses Fingers oder null.
    /// </summary>
    public Stick? Bewegen(long finger, double x, double y, bool allein = true)
    {
        if (Links?.Finger == finger || Rechts?.Finger == finger)
        {
            var stick = Links?.Finger == finger ? Links! : Rechts!;
            stick.DaumenX = x;
            stick.DaumenY = y;
            return stick;
        }
        if (!_kandidaten.TryGetValue(finger, out var k) || Math.Abs(x - k.X) + Math.Abs(y - k.Y) <= AnfangAb || !allein)
        {
            return null;
        }
        _kandidaten.Remove(finger);
        var (mx, my, r) = Lage(k.X, k.Y, k.Seite, k.Bereich);
        var neu = new Stick(finger, k.Seite, mx, my, r, k.X, k.Y) { DaumenX = x, DaumenY = y };
        if (k.Seite == Seite.Links)
        {
            Links = neu;
        }
        else
        {
            Rechts = neu;
        }
        return neu;
    }

    /// <summary>Ist dieser Finger ein Stick?</summary>
    public bool IstStick(long finger) => Links?.Finger == finger || Rechts?.Finger == finger;

    /// <summary>Daumen los: true = es war ein Stick (er verschwindet; nie ein Tipp).</summary>
    public bool Loslassen(long finger)
    {
        _kandidaten.Remove(finger);
        if (Links?.Finger == finger)
        {
            Links = null;
            return true;
        }
        if (Rechts?.Finger == finger)
        {
            Rechts = null;
            return true;
        }
        return false;
    }

    /// <summary>Kandidaten vergessen (zwei Finger zoomen und drehen wie heute).</summary>
    public void KandidatenVergessen() => _kandidaten.Clear();

    /// <summary>Alles los (Fokus verloren, Spiel-Handy offen, Flaeche weg).</summary>
    public void AlleLos()
    {
        _kandidaten.Clear();
        Links = null;
        Rechts = null;
    }

    /// <summary>
    /// Rechter Stick in einem Bild von <paramref name="sekunden"/>: Weg wie mit der rechten Maustaste gezogen (dx nach rechts,
    /// dy nach unten, in Punkten); (0, 0) ohne Stick oder in der Totzone. Waagrecht wie ein Stick am Spielgeraet: Stick nach
    /// rechts = der Blick dreht nach rechts (Welle A 0.97.0, Sammelliste 393, Weisung P12-313; bis A 0.96.0 war das Vorzeichen
    /// der Maus uebernommen: die Maus zieht die Welt, nach rechts gezogen dreht der Blick nach links). Senkrecht: nach oben =
    /// Blick hoch (in der Ich-Sicht) bzw. flacher (draussen, wie Taste F), also wie die Maus nach unten gezogen (unveraendert).
    /// </summary>
    public (double Dx, double Dy) Drehen(double sekunden)
    {
        if (Rechts is not { } r)
        {
            return (0, 0);
        }
        var (vor, seit) = r.Eingabe;
        var weg = PunkteJeSekunde * Math.Max(0, sekunden);
        return (-seit * weg, vor * weg);
    }
}
