using System.Numerics;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Kamera der 3D-Stichprobe: schaut schraeg von oben auf einen Zielpunkt am Boden (Y = 0).
/// Ziehen verschiebt das Ziel am Boden, Zoomen aendert den Abstand. Matrizen mit
/// System.Numerics (Zeilenvektoren); die Speicherfolge passt so ohne Umdrehen zu GLSL
/// (<c>uMvp * vec4(p, 1)</c>).
/// </summary>
public sealed class Kamera
{
    /// <summary>Naechster Abstand: 2 (Nachtrag 1 zu P7-12: die Figur ist jetzt 1,8 m = 0,36 hoch, vorher 5 bei 1,24).</summary>
    public const float AbstandMin = 2f;
    public const float AbstandMax = 40f;

    public Kamera(float zielX, float zielZ, float abstand)
    {
        ZielX = zielX;
        ZielZ = zielZ;
        Abstand = Math.Clamp(abstand, AbstandMin, AbstandMax);
    }

    /// <summary>
    /// Welle A 0.80.0 (Weisung P12-268, Nico Antworten 1 und 3): erlaubter Bereich des Abstands. Ohne Angabe wie bisher
    /// <see cref="AbstandMin"/> bis <see cref="AbstandMax"/> (Werkzeug, Innenraeume); die Spielkamera draussen nimmt
    /// <c>SpielKamera.AbstandMin</c> bis <c>SpielKamera.AbstandMax</c> (Lernplattform.Core). Setzen holt den Abstand in den Bereich.
    /// </summary>
    public (float Min, float Max) Bereich
    {
        get => _bereich;
        set
        {
            var min = Math.Clamp(value.Min, AbstandMin, AbstandMax);
            _bereich = (min, Math.Clamp(value.Max, min, AbstandMax));
            Abstand = Math.Clamp(Abstand, _bereich.Min, _bereich.Max);
        }
    }

    private (float Min, float Max) _bereich = (AbstandMin, AbstandMax);

    /// <summary>
    /// Welle A 0.80.0 (Nico Antwort 2): erlaubter Bereich der Neigung in Grad. Ohne Angabe 0 bis 90 (Werkzeug, Innenraeume:
    /// dort setzt nur der Code die Neigung, keine Grenze greift); die Spielkamera draussen nimmt 35 bis 70.
    /// </summary>
    public (float Min, float Max) NeigungBereich
    {
        get => _neigungBereich;
        set
        {
            _neigungBereich = (value.Min, Math.Max(value.Min, value.Max));
            _neigung = Math.Clamp(_neigung, _neigungBereich.Min, _neigungBereich.Max);
        }
    }

    private (float Min, float Max) _neigungBereich = (0f, 90f);

    /// <summary>
    /// Nur fuer Messungen (Start-Test, Leistungsprobe): Abstand voruebergehend frei von <see cref="AbstandMin"/> bis
    /// <see cref="AbstandMax"/> (Gesamtansicht, feste Messabstaende); beim Ende kommt der vorige Bereich zurueck (der Abstand
    /// wird dabei in ihn geholt). Sonst wuerden Messungen der Gesamtansicht still bei 9 messen.
    /// </summary>
    public IDisposable MessungFrei()
    {
        var vorher = Bereich;
        Bereich = (AbstandMin, AbstandMax);
        _messungen++;
        return new Zurueck(() =>
        {
            _messungen--;
            Bereich = vorher;
        });
    }

    private int _messungen;

    /// <summary>
    /// Welle A 0.97.0 (Weisung P12-312 Punkt 1): eine Messung haelt den Bereich gerade frei (<see cref="MessungFrei"/>); dann
    /// setzt die Spielflaeche den Bereich drinnen nicht neu (die Messung misst ihre festen Abstaende).
    /// </summary>
    public bool MessungLaeuft => _messungen > 0;

    private sealed class Zurueck(Action ende) : IDisposable
    {
        private Action? _ende = ende;

        public void Dispose()
        {
            _ende?.Invoke();
            _ende = null;
        }
    }

    public float ZielX { get; private set; }

    public float ZielZ { get; private set; }

    public float Abstand { get; private set; }

    /// <summary>
    /// Neigung ueber dem Boden in Grad. S 0.7.0 (Weisung P12-45, Plan P12-26b Abschnitt 9): setzbar, damit das
    /// Stadtbau-Werkzeug die Kamera kippen kann; das Spiel setzt sie nur beim Anlegen (unveraendert).
    /// </summary>
    public float Neigung
    {
        get => _neigung;
        set => _neigung = Math.Clamp(value, _neigungBereich.Min, _neigungBereich.Max);
    }

    private float _neigung = 52f;

    /// <summary>Welle A 0.80.0: um <paramref name="grad"/> neigen (positiv = steiler), immer im <see cref="NeigungBereich"/>.</summary>
    public void Neigen(float grad) => Neigung += grad;

    /// <summary>Drehung um die Hochachse in Grad (0 = Blick nach -Z), W4: per zwei Finger oder Tasten Q/E.</summary>
    public float Drehung { get; set; } = 0f;

    public float Sichtfeld { get; init; } = 40f;

    // ---- Welle A 1.1.0 (Sammelliste 409, Weisung P12-328 Punkt 2): Blickfeld der Ich-Sicht ---------------------------------

    /// <summary>Welle A 1.1.0: kleinstes Blickfeld der Ich-Sicht (waagrecht, Grad).</summary>
    public const float IchBlickfeldMin = 60f;

    /// <summary>Welle A 1.1.0: groesstes Blickfeld der Ich-Sicht (waagrecht, Grad).</summary>
    public const float IchBlickfeldMax = 110f;

    /// <summary>
    /// Vorgabe des Blickfelds der Ich-Sicht (waagrecht, Grad). Welle A 1.1.0: 90; Welle A 1.2.0 (Weisung P12-332, Nico 2c): 75,
    /// das Ende eines Flurs rueckt naeher, der Flur wirkt nicht mehr so lang.
    /// </summary>
    public const float IchBlickfeldVorgabe = 75f;

    /// <summary>
    /// Welle A 1.1.0 (Weisung P12-328): Blickfeld der Ich-Sicht waagrecht in Grad (60 bis 110, Vorgabe 90). Die normale Kamera
    /// bleibt bei <see cref="Sichtfeld"/> (40 Grad senkrecht); beim Uebergang gleitet das Blickfeld mit dem Anteil der Ich-Sicht.
    /// </summary>
    public float IchBlickfeld
    {
        get => _ichBlickfeld;
        set => _ichBlickfeld = Math.Clamp(value, IchBlickfeldMin, IchBlickfeldMax);
    }

    private float _ichBlickfeld = IchBlickfeldVorgabe;

    /// <summary>
    /// Welle A 1.1.0: senkrechtes Sichtfeld (Grad) zu einem waagrechten Blickfeld <paramref name="waagrecht"/> bei dem
    /// Seitenverhaeltnis <paramref name="seiten"/> (Breite durch Hoehe). Im Hochformat gilt das Blickfeld fuer die lange
    /// (senkrechte) Seite, sonst wuerde das Bild nach oben und unten uebermaessig weit.
    /// </summary>
    public static float IchSenkrecht(float waagrecht, float seiten) =>
        2f * MathF.Atan(MathF.Tan(waagrecht * MathF.PI / 360f) / Math.Max(seiten, 1f)) * 180f / MathF.PI;

    /// <summary>Welle A 1.1.0: senkrechtes Sichtfeld jetzt (normale Kamera, Ich-Sicht oder der Weg dazwischen).</summary>
    public float SichtfeldJetzt(float seiten) =>
        _ichAnteil <= 0f ? Sichtfeld : Sichtfeld + ((IchSenkrecht(IchBlickfeld, seiten) - Sichtfeld) * _ichAnteil);

    /// <summary>Grenzen fuer das Ziel (Welt), damit man die Stadt nicht verliert.</summary>
    public (float X1, float Z1, float X2, float Z2) Grenzen { get; init; } = (-100, -100, 100, 100);

    /// <summary>
    /// Welle A 0.66.0 (Gelaende): Hoehe des Ziels (die Kamera schaut auf die Figur auf dem Huegel); ohne Gelaende 0, dann
    /// ist alles wie vorher.
    /// </summary>
    public float ZielY { get; set; }

    public Vector3 Ziel => new(ZielX, ZielY, ZielZ);

    /// <summary>Auge der normalen Kamera (schraeg von oben auf das Ziel), ohne Ich-Sicht.</summary>
    private Vector3 AugeNormal
    {
        get
        {
            var n = Neigung * MathF.PI / 180f;
            var d = Drehung * MathF.PI / 180f;
            var waagrecht = Abstand * MathF.Cos(n);
            return Ziel + new Vector3(MathF.Sin(d) * waagrecht, Abstand * MathF.Sin(n), MathF.Cos(d) * waagrecht);
        }
    }

    // ---- Welle A 0.92.0 (Weisung P12-301, Kurzplan P12-181): Ich-Sicht ------------------------------------------------------

    /// <summary>
    /// Welle A 0.92.0: Anteil der Ich-Sicht, 0 = normale Kamera, 1 = Auge im Kopf der Figur (<see cref="IchAuge"/>). Dazwischen
    /// liegt der Weg dorthin: Blickpunkt vom Ziel zum Auge, Neigung zur <see cref="IchNeigung"/>, Weite vom Abstand bis 0
    /// (Schulterblick). Ohne Ich-Sicht (Werkzeug, Messungen) bleibt er 0 und alles ist wie vorher.
    /// </summary>
    public float IchAnteil
    {
        get => _ichAnteil;
        set => _ichAnteil = Math.Clamp(value, 0f, 1f);
    }

    private float _ichAnteil;

    /// <summary>Welle A 0.92.0: wohin der Anteil weich laeuft (<see cref="IchTakt"/>); beim Zoomen setzen ihn die Regeln mit.</summary>
    public float IchZiel
    {
        get => _ichZiel;
        set => _ichZiel = Math.Clamp(value, 0f, 1f);
    }

    private float _ichZiel;

    /// <summary>Welle A 0.92.0: so viel Anteil je Sekunde laeuft der Uebergang (ganz hinein in rund 0,6 s).</summary>
    public const float IchTempo = 1.7f;

    /// <summary>Welle A 0.92.0: Anteil weich zum <see cref="IchZiel"/> bewegen; true = er hat sich bewegt.</summary>
    public bool IchTakt(float dt)
    {
        if (_ichAnteil == _ichZiel)
        {
            return false;
        }
        var schritt = IchTempo * Math.Max(0f, dt);
        _ichAnteil = _ichAnteil < _ichZiel ? Math.Min(_ichZiel, _ichAnteil + schritt) : Math.Max(_ichZiel, _ichAnteil - schritt);
        return true;
    }

    /// <summary>Welle A 0.92.0: ganz in der Ich-Sicht (Anteil 1).</summary>
    public bool Ich => _ichAnteil >= 1f;

    /// <summary>Welle A 0.92.0: diese Kamera darf in die Ich-Sicht (nur die Spielkamera; das Werkzeug nie).</summary>
    public bool IchErlaubt { get; set; }

    /// <summary>Welle A 0.92.0: der Weg in die Ich-Sicht fuehrt ueber den Schulterblick (draussen); sonst nur weich hinein.</summary>
    public bool IchMitSchulter { get; set; }

    /// <summary>Welle A 0.92.0: Stelle des Auges in der Ich-Sicht (Kopf der Figur; setzt die Spielflaeche je Bild).</summary>
    public Vector3 IchAuge { get; set; }

    /// <summary>Welle A 0.92.0: Blick in der Ich-Sicht nach unten in Grad (negativ = nach oben), im <see cref="IchNeigungBereich"/>.</summary>
    public float IchNeigung
    {
        get => _ichNeigung;
        set => _ichNeigung = Math.Clamp(value, _ichNeigungBereich.Min, _ichNeigungBereich.Max);
    }

    private float _ichNeigung = 5f;

    /// <summary>Welle A 0.92.0: erlaubter Blick nach oben (negativ) und unten in der Ich-Sicht.</summary>
    public (float Min, float Max) IchNeigungBereich
    {
        get => _ichNeigungBereich;
        set
        {
            _ichNeigungBereich = (value.Min, Math.Max(value.Min, value.Max));
            _ichNeigung = Math.Clamp(_ichNeigung, _ichNeigungBereich.Min, _ichNeigungBereich.Max);
        }
    }

    private (float Min, float Max) _ichNeigungBereich = (-40f, 75f);

    /// <summary>Welle A 0.92.0: naechste Ebene in der Ich-Sicht (das Auge sitzt im Kopf; die Figur hat 0,3 Einheiten).</summary>
    public const float IchNah = 0.01f;

    /// <summary>Welle A 0.92.0: Blickrichtung (Laenge 1) in der Ich-Sicht aus <see cref="Drehung"/> und <see cref="IchNeigung"/>.</summary>
    public Vector3 IchBlick
    {
        get
        {
            var d = Drehung * MathF.PI / 180f;
            var n = IchNeigung * MathF.PI / 180f;
            return new Vector3(-MathF.Sin(d) * MathF.Cos(n), -MathF.Sin(n), -MathF.Cos(d) * MathF.Cos(n));
        }
    }

    /// <summary>Welle A 0.92.0: weicher Verlauf des Anteils (langsam am Anfang und am Ende).</summary>
    private static float Weich(float a) => a * a * (3f - (2f * a));

    /// <summary>
    /// Welle A 0.92.0: Blickpunkt, Blickrichtung und Weite der Kamera. Ohne Ich-Sicht genau wie bisher (Ziel, Richtung vom Auge
    /// zum Ziel, Abstand); mit Anteil a laeuft der Blickpunkt zum Auge der Figur, die Neigung zur Neigung der Ich-Sicht und die
    /// Weite von Abstand bis 0.
    /// </summary>
    private (Vector3 Punkt, Vector3 Richtung, float Weite) Blick()
    {
        var normalAuge = AugeNormal;
        if (_ichAnteil <= 0f)
        {
            return (Ziel, Vector3.Normalize(Ziel - normalAuge), Abstand);
        }
        var s = Weich(_ichAnteil);
        var d = Drehung * MathF.PI / 180f;
        var n = (Neigung + ((IchNeigung - Neigung) * s)) * MathF.PI / 180f;
        var richtung = new Vector3(-MathF.Sin(d) * MathF.Cos(n), -MathF.Sin(n), -MathF.Cos(d) * MathF.Cos(n));
        return (Vector3.Lerp(Ziel, IchAuge, s), richtung, Abstand * (1f - _ichAnteil));
    }

    public Vector3 Auge
    {
        get
        {
            if (_ichAnteil <= 0f)
            {
                return AugeNormal;
            }
            var (punkt, richtung, weite) = Blick();
            return punkt - (richtung * weite);
        }
    }

    public Matrix4x4 Sicht
    {
        get
        {
            if (_ichAnteil <= 0f)
            {
                return Matrix4x4.CreateLookAt(AugeNormal, Ziel, Vector3.UnitY);
            }
            var (punkt, richtung, weite) = Blick();
            var auge = punkt - (richtung * weite);
            // ganz senkrecht waere die Hochachse ungeeignet; die Ich-Neigung bleibt unter 90 Grad
            return Matrix4x4.CreateLookAt(auge, auge + richtung, Vector3.UnitY);
        }
    }

    /// <summary>
    /// Naechste Ebene des Sichtraums: je weiter vorn, desto genauer der Tiefenpuffer (am Handy 16 Bit;
    /// mit 0,5 flimmerten Fussweg/Strasse und Wasser/Bruecke, Wunsch Nico 25.10.2026). Sie waechst mit
    /// dem Abstand (bei 5 wie bisher 1,5), damit die Kamera nah an die kleinere Figur kann, ohne vorn etwas
    /// abzuschneiden, und weit weg der Tiefenpuffer genau bleibt.
    /// </summary>
    public float Nah => _ichAnteil <= 0f ? Math.Max(0.3f, Abstand * 0.3f)
        : Math.Clamp(Abstand * (1f - _ichAnteil) * 0.3f, IchNah, Math.Max(0.3f, Abstand * 0.3f));

    /// <summary>
    /// Nachtrag 1 zu P7-13 (Wunsch 2): das Bild um diesen Anteil der halben Bildhoehe nach oben schieben
    /// (Bildraum, 0 = Ziel in der Mitte). So steht das Ziel (die Figur) in der Mitte des freien Bereichs
    /// zwischen Kopfzeile und Textkarte statt hinter der Karte; Tippen rechnet mit demselben Bild.
    /// </summary>
    public float Versatz { get; set; }

    public Matrix4x4 Projektion(float breite, float hoehe) =>
        Matrix4x4.CreatePerspectiveFieldOfView(SichtfeldJetzt(Math.Max(breite, 1) / Math.Max(hoehe, 1)) * MathF.PI / 180f, Math.Max(breite, 1) / Math.Max(hoehe, 1), Nah, 200f)
        * Matrix4x4.CreateTranslation(0, Versatz * (1f - _ichAnteil), 0);

    public void Setzen(float zielX, float zielZ)
    {
        ZielX = Math.Clamp(zielX, Grenzen.X1, Grenzen.X2);
        ZielZ = Math.Clamp(zielZ, Grenzen.Z1, Grenzen.Z2);
    }

    /// <summary>
    /// Ziehen um <paramref name="dx"/>/<paramref name="dy"/> Bildpunkte bei Bildhoehe <paramref name="hoehe"/>:
    /// der Boden folgt dem Finger ungefaehr (Massstab aus Abstand und Sichtfeld).
    /// </summary>
    public void Verschieben(float dx, float dy, float hoehe)
    {
        var proPunkt = 2f * Abstand * MathF.Tan(Sichtfeld * MathF.PI / 360f) / Math.Max(hoehe, 1);
        var d = Drehung * MathF.PI / 180f;
        var rechts = new Vector2(MathF.Cos(d), -MathF.Sin(d));
        var vor = new Vector2(-MathF.Sin(d), -MathF.Cos(d));
        var n = Neigung * MathF.PI / 180f;
        var schritt = (-dx * proPunkt * rechts) + (dy * proPunkt / MathF.Max(MathF.Sin(n), 0.3f) * vor);
        Setzen(ZielX + schritt.X, ZielZ + schritt.Y);
    }

    /// <summary>Faktor &gt; 1 = naeher heran.</summary>
    public void Zoomen(float faktor)
    {
        if (faktor > 0)
        {
            Abstand = Math.Clamp(Abstand / faktor, _bereich.Min, _bereich.Max);
        }
    }

    /// <summary>Um die Hochachse drehen (Grad), Ergebnis immer in [0, 360).</summary>
    public void Drehen(float grad)
    {
        Drehung = ((Drehung + grad) % 360f + 360f) % 360f;
    }

    /// <summary>Strahl vom Auge durch den Bildpunkt (x, y): Start und Richtung (Laenge 1) oder null.</summary>
    public (Vector3 Start, Vector3 Richtung)? Strahl(float x, float y, float breite, float hoehe)
    {
        if (!Matrix4x4.Invert(Sicht * Projektion(breite, hoehe), out var umgekehrt))
        {
            return null;
        }
        var nx = (2f * x / Math.Max(breite, 1)) - 1f;
        var ny = 1f - (2f * y / Math.Max(hoehe, 1));
        var nah = Vector4.Transform(new Vector4(nx, ny, 0f, 1f), umgekehrt);
        var fern = Vector4.Transform(new Vector4(nx, ny, 1f, 1f), umgekehrt);
        var a = new Vector3(nah.X, nah.Y, nah.Z) / nah.W;
        var b = new Vector3(fern.X, fern.Y, fern.Z) / fern.W;
        var r = b - a;
        return r.LengthSquared() < 1e-12f ? null : (a, Vector3.Normalize(r));
    }

    /// <summary>Bildpunkt eines Weltpunkts (x, y in Punkten) oder null, wenn er hinter der Kamera liegt.</summary>
    public (float X, float Y)? Bildpunkt(Vector3 p, float breite, float hoehe)
    {
        var v = Vector4.Transform(new Vector4(p, 1f), Sicht * Projektion(breite, hoehe));
        if (v.W <= 1e-6f)
        {
            return null;
        }
        return ((v.X / v.W + 1f) / 2f * breite, (1f - (v.Y / v.W)) / 2f * hoehe);
    }

    /// <summary>Punkt am Boden (Y = 0) unter dem Bildpunkt (x, y) oder null (Blick in den Himmel).</summary>
    public Vector2? Boden(float x, float y, float breite, float hoehe)
    {
        if (!Matrix4x4.Invert(Sicht * Projektion(breite, hoehe), out var umgekehrt))
        {
            return null;
        }
        var nx = (2f * x / Math.Max(breite, 1)) - 1f;
        var ny = 1f - (2f * y / Math.Max(hoehe, 1));
        var nah = Vector4.Transform(new Vector4(nx, ny, 0f, 1f), umgekehrt);
        var fern = Vector4.Transform(new Vector4(nx, ny, 1f, 1f), umgekehrt);
        var a = new Vector3(nah.X, nah.Y, nah.Z) / nah.W;
        var b = new Vector3(fern.X, fern.Y, fern.Z) / fern.W;
        if (MathF.Abs(b.Y - a.Y) < 1e-6f)
        {
            return null;
        }
        var t = -a.Y / (b.Y - a.Y);
        if (t < 0)
        {
            return null;
        }
        var p = a + ((b - a) * t);
        return new Vector2(p.X, p.Z);
    }

    /// <summary>
    /// Welle A 0.66.0: Punkt auf dem Gelaende unter dem Bildpunkt; <paramref name="hoeheAn"/> = Hoehe des Bodens an (x, z).
    /// Der Strahl wird mit der Ebene in der Hoehe des letzten Treffers geschnitten, bis sich der Punkt kaum noch bewegt
    /// (hoechstens 12 Mal). Ohne Gelaende (null) wie <see cref="Boden(float, float, float, float)"/>.
    /// </summary>
    public Vector2? Boden(float x, float y, float breite, float hoehe, Func<float, float, float>? hoeheAn)
    {
        if (Boden(x, y, breite, hoehe) is not { } p0 || hoeheAn is null || Strahl(x, y, breite, hoehe) is not { } s || MathF.Abs(s.Richtung.Y) < 1e-4f)
        {
            return Boden(x, y, breite, hoehe);
        }
        var p = p0;
        for (var i = 0; i < 12; i++)
        {
            var h = hoeheAn(p.X, p.Y);
            var t = (h - s.Start.Y) / s.Richtung.Y;
            var q = s.Start + (s.Richtung * t);
            var neu = new Vector2(q.X, q.Z);
            // halber Schritt gegen Pendeln an steilen Haengen
            neu = i < 2 ? neu : Vector2.Lerp(p, neu, 0.5f);
            if (Vector2.Distance(neu, p) < 0.002f)
            {
                return neu;
            }
            p = neu;
        }
        return p;
    }
}
