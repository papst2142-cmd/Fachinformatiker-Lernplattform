using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Rendering;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Spiel.Ton;
using static Avalonia.OpenGL.GlConsts;

namespace Lernplattform.Spiel;

/// <summary>
/// Phase 7, W1 (Stichprobe, Plan Abschnitt 3 Technik A), ab W4 die ganze Stadt: 3D-Flaeche mit
/// Avalonias eingebautem <see cref="OpenGlControlBase"/> und eigener kleiner Darstellung (OpenGL ES 3.0
/// bzw. OpenGL 3.x, Matrizen aus System.Numerics, keine Fremdbibliothek). Zeichnet die Stadt oder das
/// Buero aus <see cref="StadtSpiel"/>, die Figur und die Zielmarke.
/// Bedienung (Weisung P7-9 Punkt 3a): Tippen/Klick auf ein Haus geht hin (im Buero: zum Bodenpunkt),
/// Ziehen verschiebt die Ansicht, zwei Finger zoomen und drehen, Mausrad zoomt, Pfeiltasten
/// verschieben, Q/E drehen, +/- zoomen, Eingabe geht hinein. Die Flaeche faengt jeden Zeiger selbst
/// (Capture) und liegt nie in einer ScrollView, sonst nimmt die Seite die Geste (Fehler S24, W1).
/// Treffer zaehlt die ganze Flaeche (<see cref="ICustomHitTest"/>): das GL-Bild selbst ist fuer
/// Avalonia kein gezeichneter Inhalt, sonst ginge ein Finger an der Flaeche vorbei an den Rahmen.
/// W5: Innenraeume (<see cref="Innenraum"/>), Figuren aus dem CC0-Modell mit Aussehen, Personen mit
/// "!" bei einem Auftrag, Vorschau beim Einrichten; steht die Figur hinter einem Haus, wird es halb
/// durchsichtig gezeichnet (Rasterung im Pixel-Shader, ohne Mischen: Sammelliste 102).
/// </summary>
public sealed class StadtFlaeche : OpenGlControlBase, ICustomHitTest
{
    private const int Lagen = Netz.Zahlen * sizeof(float);
    // in GlConsts nicht enthalten (Werte aus der OpenGL-Spezifikation)
    private const int GlLequal = 0x0203;
    private const int GlUnsignedInt = 0x1405;
    private const double ZiehenAb = 8;

    private StadtSpiel? _spiel;
    private readonly Dictionary<string, Kamera> _kameras = [];
    private bool _szeneNeu;
    private readonly Dictionary<string, GlNetz> _flaechen = [];
    private readonly Dictionary<StadtHaus, GlNetz> _haeuser = [];
    private readonly Dictionary<string, GlNetz> _figuren = [];
    private GlNetz? _innen;
    private Dictionary<InnenWand, GlNetz> _innenWaende = [];
    // Weisung P8-55 (W12): jeder hochgeladene Innenraum bleibt in der Grafikkarte, solange er im Vorrat des Spiels ist
    private readonly Dictionary<Innenraum, (GlNetz Netz, Dictionary<InnenWand, GlNetz> Waende)> _innenGl = [];
    // Weisung P8-55 (W12): gebaute Netze der Figuren (Prozessor), bleiben ueber den Verlust des GL-Zusammenhangs
    private readonly ConcurrentDictionary<string, Netz> _figurenCpu = new(StringComparer.Ordinal);
    private readonly HashSet<InnenWand> _durchsichtig = [];
    private readonly HashSet<StadtHaus> _verdeckt = [];
    private readonly Dictionary<string, (PyObjekt? Aussehen, IReadOnlyList<Figur.Stueck>? Kleidung, GlNetz Netz)> _figurZuletzt = [];
    private PyObjekt? _ringAussehen;
    private Innenraum? _innenGeladen;
    private bool _innenNeu;
    private GlNetz? _markierung;
    private string _markierungSchluessel = "";
    private GlNetz? _ausruf;
    private GlNetz? _marke;
    private GlNetz? _ring;

    // Phase 8 W7: Kleingruen (nur bis GruenWeite gezeichnet) und Autos
    private GlNetz? _gruen;
    private Verkehr? _verkehr;
    private readonly Dictionary<bool, GlNetz> _autoNetze = [];   // alle Autos in einem Netz (Weisung P8-14b), nah und fern
    private readonly Matrix4x4[] _autoLagen = new Matrix4x4[AutosHoechstens];
    // Phase 8 W11: Festzeiten-Verkehr (Gestalten statt Autos) und das eigene Netz der Laternen, Tannen und Kuerbisse
    private FestVerkehr.Satz _satz = FestVerkehr.Satz.Autos;
    private List<FestVerkehr.Gestalt>? _gestalten;
    private bool _satzInnen = true;
    private long? _satzUhr;
    private readonly Dictionary<bool, GlNetz> _paket = [];
    // Patch A 0.29.1 (Weisung P10-3): Uebergang zwischen Autos und Festgestalten mit der Uhr, ohne Hausbesuch
    private FestUebergang? _uebergang;
    private List<FestVerkehr.Gestalt>? _neuGestalten;
    private readonly Dictionary<bool, GlNetz> _neuNetze = [];
    private SatzVorrat? _uebergangBau;
    private bool _satzPruefen = true;
    private bool _spielNeu = true;
    private string? _szeneFest;

    /// <summary>Phase 8 W11: wer gerade auf den Strassen faehrt bzw. laeuft (Start-Test); im Uebergang der alte Satz.</summary>
    public FestVerkehr.Satz Satz => _satz;

    /// <summary>Patch A 0.29.1: der laufende Uebergang zum neuen Satz (null = keiner).</summary>
    public FestUebergang? Uebergang => _uebergang;

    /// <summary>
    /// Patch A 0.29.1, nur Start-Test: eine geaenderte feste Uhr (<see cref="Tageslicht.FesteUhr"/>) wechselt den Satz
    /// nach und nach wie die Uhr des Spiels (sonst sofort, wie bisher).
    /// </summary>
    public static bool UhrSanft { get; set; }

    /// <summary>Phase 8 W11: die Festgestalten (null = Autos).</summary>
    public IReadOnlyList<FestVerkehr.Gestalt>? Gestalten => _gestalten;
    private int _autoProgramm;
    private int _autoOrtSichtProj;
    private int _autoOrtLagen;
    private int _autoOrtHell;
    private int _autoOrtDurch;

    /// <summary>So viele Autos fasst ein Netz (Groesse des Felds uAutos im Shader).</summary>
    public const int AutosHoechstens = 16;

    /// <summary>Kleingruen nur bis zu diesem Kameraabstand (in der Gesamtansicht waere es nur wenige Pixel gross).</summary>
    public const float GruenWeite = 18f;

    /// <summary>Ab diesem Kameraabstand zeigen Autos eine einfache Fernform (zwei Kaesten in der Lackfarbe).</summary>
    public const float AutoFern = 25f;

    /// <summary>Start-Test: feste Fahrzeit je Bild fuer die Autos (gleiche Bilder), 0 = echte Bildzeit.</summary>
    public static double AutoSchritt { get; set; }

    /// <summary>Start-Test: festes Bild der Schrittbewegung (-1 = nach dem Weg der Figur).</summary>
    public static int ProbeBild { get; set; } = -1;

    /// <summary>Die Autos der Stadt (null ohne Grafik oder vor dem ersten Bild).</summary>
    public Verkehr? Verkehr => _verkehr;

    /// <summary>Haeufigste kraeftige Farbe des Modells (Lack), sonst Grau.</summary>
    private static Vector3 Hauptfarbe(GrafikModell m)
    {
        var zaehl = new Dictionary<int, int>();
        for (var d = 0; d < m.Dreiecke; d++)
        {
            int r = m.Farben[d * 3], g = m.Farben[(d * 3) + 1], b = m.Farben[(d * 3) + 2];
            if (Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)) < 40)
            {
                continue;
            }
            var k = (r << 16) | (g << 8) | b;
            zaehl[k] = zaehl.GetValueOrDefault(k) + 1;
        }
        if (zaehl.Count == 0)
        {
            return new Vector3(0.6f);
        }
        var best = zaehl.MaxBy(p => p.Value).Key;
        return new Vector3(((best >> 16) & 255) / 255f, ((best >> 8) & 255) / 255f, (best & 255) / 255f);
    }
    private string _ringSchluessel = "";
    private int _programm;
    private int _ortMvp;
    private int _ortModell;
    private int _ortHell;
    private int _ortDurch;
    private int[] _lichtOrte = [];
    private int[] _autoLichtOrte = [];
    private Tageslicht.Licht _lichtVon = Tageslicht.Tag;
    private Tageslicht.Licht _lichtZiel = Tageslicht.Tag;
    private double _lichtSeit = double.MinValue;
    private bool _lichtNeu = true;
    private bool _lichtInnen;
    private long? _lichtFest;
    private readonly Stopwatch _uhr = Stopwatch.StartNew();
    private double _letztesBild = -1;
    private readonly Queue<double> _bilder = new();
    private readonly Dictionary<IPointer, Point> _zeiger = [];
    private Point _druckStart;
    private bool _gezogen;
    private double _abstandZwei;
    private double _winkelZwei;
    private double _folgenAb;
    private double _weiterBis;   // Nachtrag 1 zu P7-12: nach einer Bedienung im Takt des Bildschirms weiterzeichnen
    private int _aufrufe;
    private long _dreiecke;
    private double _nachlaufBis;
    private readonly int[] _gcVorher = new int[3];
    private long _bytesVorher = -1;
    private double _endeVorher = -1;
    private long _endeVorherStempel = Stopwatch.GetTimestamp();
    private int _layouts;
    private double _layoutMs;
    private double _auftraegeMs;
    // Weisung P7-17: steht die Bildschleife (kein naechstes Bild angefordert), ist die Zeit bis zum ersten
    // Anlass (Zeiger oder Anforderung) Ruhe, kein Warten; dazu die Zeitstempel fuer langsame Bilder
    private bool _ruht = true;
    private long _anforderungStempel;
    private long _zeigerStempel;

    /// <summary>Nachtrag 1 zu P7-13 (Wunsch 4): Zeitmessung je Bild (Datei spiel_bilder.log, Problembericht).</summary>
    public BildZeiten Zeiten { get; } = new();

    /// <summary>Weisung P7-15: ein Layout-Durchlauf des Fensters seit dem letzten Bild (zaehlt fuer das naechste Bild).</summary>
    public void LayoutGezaehlt() => _layouts++;

    /// <summary>Weisung P7-15: Zeit fuer das Layout der Spielseite seit dem letzten Bild.</summary>
    public void LayoutGemessen(double ms) => _layoutMs += ms;

    /// <summary>Weisung P7-15: Zeit eines eigenen Auftrags auf dem Oberflaechen-Thread ausserhalb des Bildes (Textzeile, Eingabe).</summary>
    public void AuftragGemessen(double ms) => _auftraegeMs += ms;

    private static double MsSeit(long beginn) => Stopwatch.GetElapsedTime(beginn).TotalMilliseconds;

    public StadtFlaeche()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    /// <summary>Jeder Punkt innerhalb der Flaeche trifft sie (Gesten, Tippen, Mausrad); Avalonia gibt
    /// den Punkt im Raum des Fensters.</summary>
    public bool HitTest(Point point) =>
        TopLevel.GetTopLevel(this) is Visual wurzel && wurzel.TranslatePoint(point, this) is { } lokal
        && new Rect(Bounds.Size).Contains(lokal);

    /// <summary>Das Spiel (kommt nach dem Laden der Inhalte); bis dahin zeichnet die Flaeche nur den Himmel.</summary>
    public StadtSpiel? Spiel
    {
        get => _spiel;
        set
        {
            if (_spiel is not null)
            {
                _spiel.Geaendert -= SpielGeaendert;
                _spiel.SzeneGeaendert -= SzeneNeu;
                _spiel.InnenGeaendert -= InnenNeu;
            }
            _spiel = value;
            _kameras.Clear();
            if (value is not null)
            {
                // Nachtrag 1 zu P7-13 (Wunsch 2): naeher an der Figur (vorher Abstand 10, die Figur war am S24
                // rund 45 Bildpunkte hoch); die Figur steht ueber den Versatz in der Mitte des freien Bereichs
                _kameras[ProbeWelt.Stadt] = new Kamera(value.FigurX, value.FigurZ, StartAbstand)
                {
                    Grenzen = (0, 0, value.Szene.Breite, value.Szene.Tiefe),
                };
                value.Geaendert += SpielGeaendert;
                value.SzeneGeaendert += SzeneNeu;
                value.InnenGeaendert += InnenNeu;
                _szeneNeu = true;
                _spielNeu = true;
                _innenNeu = true;
                InnenNeu();
                if (_glFrisch)
                {
                    // Weisung P8-55 (W12): das erste Spiel nach dem Einrichten von OpenGL wird vorgeladen
                    VorladenStarten();
                }
            }
            AnfordernBild();
        }
    }

    /// <summary>Neuer Innenraum (oder neue Einrichtung): Netz neu laden; bei einem anderen Ort neue Kamera auf der Figur.</summary>
    private void InnenNeu()
    {
        _innenNeu = true;
        if (_spiel?.Innen is { } innen && (!_kameras.ContainsKey(innen.Ansicht) || _innenGeladen?.Ansicht != innen.Ansicht))
        {
            // schraeg von vorn (Sueden) mit Blick in die Raeume, auf die Mitte des ganzen Innenraums (Nachtrag 2
            // zu P7-12: vorher auf die Figur an der Tuer, dann lag der Laden halb ausserhalb des Bildes)
            _kameras[innen.Ansicht] = new Kamera(innen.Breite / 2, innen.Tiefe / 2, Math.Clamp(Math.Max(innen.Breite, innen.Tiefe) * 0.85f, 12f, 28f))
            {
                Neigung = 58f,
                Grenzen = (0, 0, innen.Breite, innen.Tiefe),
            };
        }
        AnfordernBild();
    }

    /// <summary>Spielstand geaendert (z. B. neues Aussehen): Figuren beim naechsten Bild neu nachschlagen.</summary>
    private void SpielGeaendert()
    {
        _lichtNeu = true;
        _satzPruefen = true;
        _figurZuletzt.Clear();
        _ringAussehen = null;
        AnfordernBild();
    }

    private void SzeneNeu()
    {
        _lichtNeu = true;
        _satzPruefen = true;
        _szeneNeu = true;
        AnfordernBild();
    }

    /// <summary>Abstand der Stadt-Kamera beim Start (Nachtrag 1 zu P7-13, Wunsch 2).</summary>
    public const float StartAbstand = 6f;

    /// <summary>
    /// Nachtrag 1 zu P7-13 (Wunsch 2): von Kopfzeile (oben) und Textkarte (unten) verdeckte Hoehe in Punkten;
    /// die Kamera stellt ihr Ziel (die Figur) in die Mitte des freien Bereichs dazwischen.
    /// </summary>
    public (double Oben, double Unten) Frei
    {
        get => _frei;
        set
        {
            if (_frei != value)
            {
                _frei = value;
                AnfordernBild();
            }
        }
    }

    private (double Oben, double Unten) _frei;

    /// <summary>Versatz der Kamera fuer den freien Bereich (Bildraum, hoechstens 0,6).</summary>
    private float FreiVersatz() =>
        Bounds.Height > 1 ? (float)Math.Clamp((_frei.Unten - _frei.Oben) / Bounds.Height, -0.6, 0.6) : 0f;

    /// <summary>OpenGL steht (Pflicht-Messpunkt "GL-Zusammenhang erzeugt").</summary>
    public bool GlBereit { get; private set; }

    /// <summary>Fehler beim Einrichten (Shader), sonst null.</summary>
    public string? GlFehler { get; private set; }

    /// <summary>"OpenGL ES 3.0 · Renderer · Version" fuer Probe-Zeile und Messdatei.</summary>
    public string GlText { get; private set; } = "";

    /// <summary>Kurzform fuer die Zeile ueber der Stadt: Version und Grafikchip ohne Treiberangaben.</summary>
    public string GlKurz { get; private set; } = "";

    public long BilderGesamt { get; private set; }

    /// <summary>Bilder in der letzten Sekunde.</summary>
    public int BilderJeSekunde => _bilder.Count;

    /// <summary>Wird nach jedem gezeichneten Bild gesetzt (Messung).</summary>
    public event Action? BildGezeichnet;

    public event Action? GlGestartet;

    /// <summary>Kamera der gezeigten Flaeche (Stadt oder Buero); null, solange das Spiel noch laedt.</summary>
    public Kamera? AktuelleKamera
    {
        get
        {
            if (_spiel is null)
            {
                return null;
            }
            var kamera = _kameras[_spiel.FlaecheId];
            kamera.Versatz = FreiVersatz();
            return kamera;
        }
    }

    /// <summary>Messung: laeuft, solange gesetzt, ohne Pause weiter (Kameraschwenk).</summary>
    public bool Dauerlauf { get; set; }

    /// <summary>Start-Test: wird je Bild vor dem Zeichnen gerufen (z. B. Kamera ein Stueck drehen).</summary>
    public Action? VorBild { get; set; }

    /// <summary>
    /// Neues Bild anfordern. Nachtrag 1 zu P7-12 (Wunsch 4): danach noch 0,75 s im Takt des Bildschirms
    /// weiterzeichnen; vorher kam beim Ziehen und Zoomen nur je Fingerbewegung ein Bild (am S24 rund 55/s,
    /// obwohl die Grafik 120/s schafft).
    /// </summary>
    public void AnfordernBild()
    {
        _weiterBis = _uhr.Elapsed.TotalSeconds + 0.75;
        // Nico 3a: bedient = die Autos fahren noch Verkehr.Nachlauf Sekunden
        _verkehr?.Bedient(_uhr.Elapsed.TotalSeconds);
        Anfordern();
    }

    /// <summary>Weisung P7-17: naechstes Bild anfordern; steht die Bildschleife, den ersten Anlass merken.</summary>
    private void Anfordern()
    {
        if (_ruht && _anforderungStempel == 0)
        {
            _anforderungStempel = Stopwatch.GetTimestamp();
        }
        RequestNextFrameRendering();
    }

    /// <summary>Zeichenaufrufe im letzten Bild (Messung, Wunsch 4).</summary>
    public int LetzteZeichenaufrufe { get; private set; }

    /// <summary>Dreiecke im letzten Bild (Messung, Wunsch 4).</summary>
    public long LetzteDreiecke { get; private set; }

    /// <summary>Rechenzeit des Programms fuer das letzte Bild in ms (ohne Warten auf die Grafikkarte).</summary>
    public double LetzteZeichenzeitMs { get; private set; }

    /// <summary>Wartet auf das naechste gezeichnete 3D-Bild (Messung); fordert es an.</summary>
    public Task NaechstesBildAsync()
    {
        var fertig = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Einmal()
        {
            // Weisung P8-55 (W12): Bilder waehrend des Vorladens zeigen nur den Himmel, gewartet wird auf ein echtes
            if (_laedtVor)
            {
                return;
            }
            BildGezeichnet -= Einmal;
            fertig.TrySetResult();
        }
        BildGezeichnet += Einmal;
        Anfordern();
        return fertig.Task;
    }

    /// <summary>Weisung P8-52 (nur Leistungsprobe): Stadt so oft zeichnen (1 = normal).</summary>
    public static int ProbeMehrfach { get; set; } = 1;

    /// <summary>Weisung P8-52 (nur Leistungsprobe): so viele Zeichenaufrufe je Bild mindestens (0 = normal).</summary>
    public static int ProbeAufrufe { get; set; }

    /// <summary>Weisung P8-52 (nur Leistungsprobe): Stadt beim naechsten Bild neu in die Grafikkarte laden.</summary>
    public void ProbeNeuLaden() => SzeneNeu();

    /// <summary>Weisung P8-51 (nur Messung W12): jedes Bild zusaetzlich hier, solange gesetzt.</summary>
    public static List<BildZeiten.Bild>? Mitschnitt { get; set; }

    /// <summary>Weisung P8-51 (nur Messung W12): Dauer des letzten Einrichtens (Shader, Grundnetze) in ms.</summary>
    public double GlInitMs { get; private set; } = -1;

    protected override void OnOpenGlInit(GlInterface gl)
    {
        var initBeginn = Stopwatch.GetTimestamp();
        GlFehler = null;
        try
        {
            var v = GlVersion;
            var es = v.Type == GlProfileType.OpenGLES;
            GlText = (es ? "OpenGL ES " : "OpenGL ") + v.Major + "." + v.Minor + " · " + gl.Renderer + " · " + gl.Version;
            var grafik = gl.Renderer ?? "";
            var klammer = grafik.IndexOf(" (", StringComparison.Ordinal);
            GlKurz = (es ? "OpenGL ES " : "OpenGL ") + v.Major + "." + v.Minor + " · " + (klammer > 0 ? grafik[..klammer] : grafik);
            // Weisung P7-15: laeuft das Zeichnen im Oberflaechen-Thread, wartet jedes Bild auf Layout und Auftraege
            Zeiten.Kopf = "Grafik: " + GlText + " · Bild im Oberflächen-Thread: " + (Avalonia.Threading.Dispatcher.UIThread.CheckAccess() ? "ja" : "nein")
                + (Systemwerte.Kopf?.Invoke() is { Length: > 0 } system ? "\n" + system : "");
            _programm = Programm(gl, es, v);
            _ortMvp = gl.GetUniformLocationString(_programm, "uMvp");
            _ortModell = gl.GetUniformLocationString(_programm, "uModell");
            _ortHell = gl.GetUniformLocationString(_programm, "uHell");
            _ortDurch = gl.GetUniformLocationString(_programm, "uDurch");
            _lichtOrte = [.. new[] { "uTag", "uTonR", "uTonG", "uTonB", "uNacht", "uAugeX", "uAugeY", "uAugeZ" }.Select(n => gl.GetUniformLocationString(_programm, n))];
            _autoProgramm = Programm(gl, es, v, autos: true);
            _autoOrtSichtProj = gl.GetUniformLocationString(_autoProgramm, "uSichtProj");
            _autoOrtLagen = gl.GetUniformLocationString(_autoProgramm, "uAutos");
            _autoOrtHell = gl.GetUniformLocationString(_autoProgramm, "uHell");
            _autoOrtDurch = gl.GetUniformLocationString(_autoProgramm, "uDurch");
            _autoLichtOrte = [.. new[] { "uTag", "uTonR", "uTonG", "uTonB", "uNacht", "uAugeX", "uAugeY", "uAugeZ" }.Select(n => gl.GetUniformLocationString(_autoProgramm, n))];
            _marke = GlNetz.Laden(gl, ProbeWelt.Zielmarke());
            _ausruf = GlNetz.Laden(gl, Figur.Ausrufezeichen(0.7f));
            _szeneNeu = true;
            GlBereit = true;
            // Weisung P8-55 (W12): nach jedem Einrichten (Seite geoeffnet, Android nach dem Hintergrund) erst alles
            // vorladen; ist das Spiel schon da, warten die Bilder bis dahin (kein Bauen im Bild)
            _glFrisch = true;
            _hochladen = null;
            if (_spiel is not null)
            {
                _vorladeAufgabe = null;
                _laedtVor = true;
                Avalonia.Threading.Dispatcher.UIThread.Post(VorladenStarten);
            }
        }
        catch (Exception e)
        {
            GlFehler = e.Message;
        }
        GlInitMs = MsSeit(initBeginn);
        GlGestartet?.Invoke();
    }

    /// <summary>Zusammenhang verloren (z. B. Treiber neu gestartet): bis zum neuen Start gilt "nicht bereit".</summary>
    protected override void OnOpenGlLost()
    {
        GlBereit = false;
        _flaechen.Clear();
        _haeuser.Clear();
        _figuren.Clear();
        _figurZuletzt.Clear();
        _innen = null;
        _innenWaende = [];
        _innenGl.Clear();
        _hochladen = null;
        _vorladeAufgabe = null;
        _glFrisch = false;
        _laedtVor = false;
        _markierung = null;
        _markierungSchluessel = "";
        _ring = null;
        _ringAussehen = null;
        _ringSchluessel = "";
        _szeneNeu = true;
        _innenNeu = true;
        _programm = 0;
        _autoProgramm = 0;
        _gruen = null;
        _autoNetze.Clear();
        _neuNetze.Clear();
        _paket.Clear();
        base.OnOpenGlLost();
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        foreach (var netz in _flaechen.Values.Concat(_haeuser.Values).Concat(_figuren.Values).Concat(_innenGl.Values.SelectMany(e => e.Waende.Values.Append(e.Netz)))
            .Append(_markierung).Append(_marke).Append(_ausruf).Append(_ring).Append(_gruen).Concat(_autoNetze.Values).Concat(_neuNetze.Values).Concat(_paket.Values))
        {
            netz?.Freigeben(gl);
        }
        _innenGl.Clear();
        _hochladen = null;
        _vorladeAufgabe = null;
        _glFrisch = false;
        _laedtVor = false;
        _gruen = null;
        _autoNetze.Clear();
        _neuNetze.Clear();
        _paket.Clear();
        if (_autoProgramm != 0)
        {
            gl.DeleteProgram(_autoProgramm);
            _autoProgramm = 0;
        }
        _flaechen.Clear();
        _haeuser.Clear();
        _figuren.Clear();
        _figurZuletzt.Clear();
        _innen = null;
        _innenWaende = [];
        _markierung = null;
        _ring = null;
        _ringAussehen = null;
        _ringSchluessel = "";
        if (_programm != 0)
        {
            gl.DeleteProgram(_programm);
            _programm = 0;
        }
        GlBereit = false;
    }

    private static string Kopf(bool es, GlVersion v) =>
        es ? "#version 300 es\nprecision mediump float;\n"
        : v.Major > 3 || (v.Major == 3 && v.Minor >= 3) ? "#version 330 core\n" : "#version 150\n";

    /// <summary>
    /// Weisung P8-14b: alle Autos in einem Netz und einem Zeichenaufruf. Jede Ecke traegt die Nummer ihres Autos
    /// (aNummer), die Lage jedes Autos kommt je Bild aus dem Feld uAutos.
    /// </summary>
    private const string AutoEcke = """
        in vec3 aOrt;
        in vec3 aNormale;
        in vec3 aFarbe;
        in float aNummer;
        uniform mat4 uSichtProj;
        uniform mat4 uAutos[16];
        out vec3 vFarbe;
        out vec3 vNormale;
        out vec3 vWelt;
        void main() {
            mat4 lage = uAutos[int(aNummer + 0.5)];
            vWelt = (lage * vec4(aOrt, 1.0)).xyz;
            gl_Position = uSichtProj * vec4(vWelt, 1.0);
            vNormale = (lage * vec4(aNormale, 0.0)).xyz;
            vFarbe = aFarbe;
        }
        """;

    private static int Programm(GlInterface gl, bool es, GlVersion v, bool autos = false)
    {
        var kopf = Kopf(es, v);
        var ecke = autos ? AutoEcke : """
            in vec3 aOrt;
            in vec3 aNormale;
            in vec3 aFarbe;
            uniform mat4 uMvp;
            uniform mat4 uModell;
            out vec3 vFarbe;
            out vec3 vNormale;
            out vec3 vWelt;
            void main() {
                gl_Position = uMvp * vec4(aOrt, 1.0);
                vWelt = (uModell * vec4(aOrt, 1.0)).xyz;
                vNormale = (uModell * vec4(aNormale, 0.0)).xyz;
                vFarbe = aFarbe;
            }
            """;
        const string pixel = """
            in vec3 vFarbe;
            in vec3 vNormale;
            in vec3 vWelt;
            uniform float uHell;
            uniform float uDurch;
            uniform float uTag;
            uniform float uTonR;
            uniform float uTonG;
            uniform float uTonB;
            uniform float uNacht;
            uniform float uAugeX;
            uniform float uAugeY;
            uniform float uAugeZ;
            out vec4 fFarbe;
            void main() {
                // halb durchsichtig ohne Mischen: jedes zweite Pixel faellt weg (Schachbrett)
                if (uDurch > 0.5 && mod(floor(gl_FragCoord.x) + floor(gl_FragCoord.y), 2.0) < 0.5) {
                    discard;
                }
                vec3 licht = normalize(vec3(0.35, 0.85, 0.45));
                float d = abs(dot(normalize(vNormale), licht));
                // Phase 8 W9 (Nico 5b): Schnee traegt die Marke +2 (Jahreszeitfarben.SchneeMarke) und wird im dunklen
                // Design nicht abgedunkelt
                vec3 farbe = vFarbe;
                float hell = uHell;
                // Phase 8 W10 (Nico 3b): Marke +4 = leuchtet nachts (Fenster, Laternen; beim Netzbau gesetzt,
                // Nachtlicht.Marke); am Tag sieht die Flaeche aus wie vorher
                float glut = 0.0;
                if (farbe.r > 3.5) {
                    farbe -= vec3(4.0);
                    // fast von der Seite gesehen (Fenster an einer Seitenwand) kein heller Strich: das Licht nimmt
                    // mit dem flachen Blick ab (Weisung P8-34, Seitenkante der Bank)
                    vec3 blick = normalize(vec3(uAugeX, uAugeY, uAugeZ) - vWelt);
                    glut = smoothstep(0.15, 0.45, abs(dot(normalize(vNormale), blick)));
                }
                if (farbe.r > 1.5) {
                    farbe -= vec3(2.0);
                    hell = uTag;
                }
                vec3 ton = vec3(uTonR, uTonG, uTonB);
                vec3 normal = farbe * (0.48 + 0.52 * d) * hell * ton;
                // P8-49: die Lichter am Bahnuebergang (Bahnuebergang.Licht, dunkelrot) leuchten nachts rot, alles andere im Lampenlicht
                vec3 leuchten = distance(farbe, vec3(0.55, 0.07, 0.06)) < 0.02 ? vec3(1.0, 0.12, 0.08) : vec3(0.99, 0.86, 0.55);
                fFarbe = vec4(mix(normal, leuchten, glut * uNacht), 1.0);
            }
            """;
        var vs = gl.CreateShader(GL_VERTEX_SHADER);
        var fehler = gl.CompileShaderAndGetError(vs, kopf + ecke);
        if (fehler is not null)
        {
            throw new InvalidOperationException("Ecken-Shader: " + fehler);
        }
        var fs = gl.CreateShader(GL_FRAGMENT_SHADER);
        fehler = gl.CompileShaderAndGetError(fs, kopf + pixel);
        if (fehler is not null)
        {
            throw new InvalidOperationException("Pixel-Shader: " + fehler);
        }
        var p = gl.CreateProgram();
        gl.AttachShader(p, vs);
        gl.AttachShader(p, fs);
        gl.BindAttribLocationString(p, 0, "aOrt");
        gl.BindAttribLocationString(p, 1, "aNormale");
        gl.BindAttribLocationString(p, 2, "aFarbe");
        if (autos)
        {
            gl.BindAttribLocationString(p, 3, "aNummer");
        }
        fehler = gl.LinkProgramAndGetError(p);
        gl.DeleteShader(vs);
        gl.DeleteShader(fs);
        if (fehler is not null)
        {
            throw new InvalidOperationException("Programm: " + fehler);
        }
        return p;
    }

    protected override unsafe void OnOpenGlRender(GlInterface gl, int fb)
    {
        var jetzt = _uhr.Elapsed.TotalSeconds;
        var startStempel = Stopwatch.GetTimestamp();
        var ruhte = _ruht;
        _ruht = false;
        var bytesBeginn = GC.GetAllocatedBytesForCurrentThread();
        var abstandMs = _letztesBild < 0 ? 0 : (jetzt - _letztesBild) * 1000;
        var dt = _letztesBild < 0 ? 0 : Math.Min(jetzt - _letztesBild, 0.1);
        _aufrufe = 0;
        _dreiecke = 0;
        _letztesBild = jetzt;
        if (_laedtVor && GlBereit)
        {
            VorladenBild(gl, jetzt, abstandMs, bytesBeginn, ruhte, startStempel);
            return;
        }
        if (_spiel is { } spiel && _szeneNeu && GlBereit)
        {
            // Stadt in die Grafikkarte laden: beim Start und wenn sich die sichtbaren Orte aendern;
            // jedes Haus fuer sich (es kann durchsichtig werden)
            foreach (var alt in _flaechen.Values.Concat(_haeuser.Values))
            {
                alt.Freigeben(gl);
            }
            _flaechen.Clear();
            _haeuser.Clear();
            // Weisung P8-55 (W12): die Netze baut die Szene einmal (bei einem Fest- oder Jahreszeitwechsel schon im
            // Haus im Hintergrund), hier wird nur noch hochgeladen
            var fertig = spiel.Szene.Vorbereiten();
            _flaechen[ProbeWelt.Stadt] = GlNetz.Laden(gl, fertig.Stadt);
            foreach (var haus in spiel.Szene.Haeuser)
            {
                _haeuser[haus] = GlNetz.Laden(gl, fertig.Haeuser[haus]);
            }
            _gruen?.Freigeben(gl);
            _gruen = fertig.Gruen is { } gn ? GlNetz.Laden(gl, gn) : null;
            foreach (var alt in _paket.Values)
            {
                alt.Freigeben(gl);
            }
            _paket.Clear();
            if (fertig.PaketNah is { } nah && fertig.PaketFern is { } fernNetz)
            {
                // W11 (Nico 5a): Laternen, Tannen und Kuerbisse, nah die Paket-Modelle, fern einfache Kaesten
                _paket[false] = GlNetz.Laden(gl, nah);
                _paket[true] = GlNetz.Laden(gl, fernNetz);
            }
            // die Autos behalten ihre Lage, wenn die Szene neu geladen wird (neue Orte sichtbar); eine Szene mit
            // anderer Festzeit (neuer Tag) oder ein neues Spiel bekommt sofort seinen eigenen Satz; aendert sich draussen
            // in derselben Stadt nur die Uhr, wechselt der Satz nach und nach (Patch A 0.29.1)
            if (spiel.Grafik is not null
                && FestVerkehr.Fuer(spiel.Szene.Fest, Tageslicht.FesteUhr ?? spiel.Jetzt.Uhr, spiel.Szene.Jahreszeit) is var satzNeu
                && (_verkehr is null || (satzNeu != (_uebergang?.Neu ?? _satz) && (spiel.ImInnenraum || _spielNeu || _szeneFest != spiel.Szene.Fest))))
            {
                SatzSetzen(gl, spiel, satzNeu, jetzt);
            }
            _spielNeu = false;
            _szeneFest = spiel.Szene.Fest;
            _szeneNeu = false;
        }
        if (_spiel is { } s2 && _innenNeu && GlBereit)
        {
            // Weisung P8-55 (W12): schon hochgeladene Innenraeume (Vorrat) werden genommen, nicht mehr gebrauchte freigegeben
            if (s2.Innen is { } innen)
            {
                var geladen = InnenHochladen(gl, innen);
                _innen = geladen.Netz;
                _innenWaende = geladen.Waende;
            }
            else
            {
                _innen = null;
                _innenWaende = [];
            }
            InnenAufraeumen(gl, s2);
            _innenGeladen = s2.Innen;
            _innenNeu = false;
        }
        if (UhrSanft && Tageslicht.FesteUhr != _satzUhr)
        {
            _satzPruefen = true;
        }
        if (_spiel is { } s3 && GlBereit && _verkehr is not null && s3.Grafik is not null
            && ((_satzInnen && !s3.ImInnenraum) || (Tageslicht.FesteUhr != _satzUhr && !UhrSanft)))
        {
            // W11 (Teilplan 1): beim Betreten der Stadt (oder fester Uhr im Start-Test) sofort der richtige Satz
            var neu = FestVerkehr.Fuer(s3.Szene.Fest, Tageslicht.FesteUhr ?? s3.Jetzt.Uhr, s3.Szene.Jahreszeit);
            if (neu != _satz || _uebergang is not null)
            {
                SatzSetzen(gl, s3, neu, jetzt);
            }
            _satzPruefen = false;
        }
        else if (_spiel is { } s4 && GlBereit && _verkehr is not null && s4.Grafik is { } grafikU && !_laedtVor)
        {
            UebergangTakt(gl, s4, grafikU, jetzt);
        }
        _satzInnen = _spiel?.ImInnenraum ?? true;
        _satzUhr = Tageslicht.FesteUhr;
        var nachLaden = _uhr.Elapsed.TotalSeconds;
        var geht = _spiel?.Fortschreiten(dt) ?? false;
        VorBild?.Invoke();
        var nachLogik = _uhr.Elapsed.TotalSeconds;
        var verdecktSek = 0.0;
        var kamera = AktuelleKamera;
        if (geht)
        {
            _nachlaufBis = jetzt + 1.5;
            _verkehr?.Bedient(jetzt);
        }
        if ((geht || jetzt < _nachlaufBis) && kamera is not null && jetzt >= _folgenAb)
        {
            // Kamera folgt der Figur weich (nach der Ankunft noch kurz, bis sie mittig steht), solange
            // niemand selbst verschiebt
            var k = (float)Math.Min(1, dt * 2.5);
            kamera.Setzen(kamera.ZielX + ((_spiel!.FigurX - kamera.ZielX) * k), kamera.ZielZ + ((_spiel.FigurZ - kamera.ZielZ) * k));
        }
        if (_spiel is { } tonSpiel && kamera is not null && TonDienst.Aktiv)
        {
            // Phase 8 W8: Schleifen der Stadt nach der Bildmitte (Fluss, Autos nach Naehe, Voegel)
            TonDienst.Takt(tonSpiel.ImInnenraum, tonSpiel.ImInnenraum ? null
                : Tonplan.Stadt(tonSpiel.Szene, kamera.ZielX, kamera.ZielZ, TonVerkehr(), _verkehr?.Faehrt(jetzt) == true && (_gestalten is null || _uebergang is not null)));
        }
        var skalierung = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var breite = (int)Math.Max(1, Bounds.Width * skalierung);
        var hoehe = (int)Math.Max(1, Bounds.Height * skalierung);
        gl.Viewport(0, 0, breite, hoehe);
        var himmel = Himmel();
        var licht = AktuellesLicht();
        var tagAnteil = (licht.Hell - 0.42f) / 0.58f;
        var hv = System.Numerics.Vector3.Lerp(licht.Himmel, new System.Numerics.Vector3(himmel.R / 255f, himmel.G / 255f, himmel.B / 255f), Math.Clamp(tagAnteil, 0f, 1f));
        gl.ClearColor(hv.X, hv.Y, hv.Z, 1f);
        gl.ClearDepth(1f);
        gl.Enable(GL_DEPTH_TEST);
        gl.DepthFunc(GlLequal);
        gl.DepthMask(1);
        gl.Clear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
        var flaeche = _spiel is null ? null : _spiel.ImInnenraum ? _innen : _flaechen.GetValueOrDefault(ProbeWelt.Stadt);
        if (GlBereit && _spiel is not null && kamera is not null && flaeche is not null)
        {
            gl.UseProgram(_programm);
            gl.Uniform1f(_ortHell, (IstDunkel() ? 0.72f : 1f) * licht.Hell);
            LichtSetzen(gl, _lichtOrte, licht, kamera.Auge);
            gl.Uniform1f(_ortDurch, 0f);
            var sichtProj = kamera.Sicht * kamera.Projektion(breite, hoehe);
            // Weisung P8-52 (nur Leistungsprobe der Probe-APK): die Stadt mehrfach an derselben Stelle (sonst einmal)
            for (var mal = 0; mal < (_spiel.ImInnenraum ? 1 : ProbeMehrfach); mal++)
            {
            Zeichnen(gl, flaeche, Matrix4x4.Identity, sichtProj);
            if (!_spiel.ImInnenraum && _gruen is not null && kamera.Abstand <= GruenWeite)
            {
                Zeichnen(gl, _gruen, Matrix4x4.Identity, sichtProj);
            }
            if (!_spiel.ImInnenraum && _paket.TryGetValue(kamera.Abstand > AutoFern, out var paket))
            {
                Zeichnen(gl, paket, Matrix4x4.Identity, sichtProj);
            }
            if (mal > 0)
            {
                foreach (var netz in _haeuser.Values)
                {
                    Zeichnen(gl, netz, Matrix4x4.Identity, sichtProj);
                }
            }
            }
            if (!_spiel.ImInnenraum && _verkehr is not null && _spiel.Grafik is { } grafikA)
            {
                var schritt = AutoSchritt > 0 ? AutoSchritt : _verkehr.Faehrt(jetzt) ? dt : 0;
                if (_uebergang is { } u)
                {
                    // Patch A 0.29.1: tauschen nur ausserhalb des Sichtkegels, am Rundlauf oder nach der Hoechstzeit
                    u.Schritt(schritt, _spiel.FigurX, _spiel.FigurZ, (p, halb) => FestUebergang.ImSichtkegel(sichtProj, p, halb));
                    if (u.Fertig)
                    {
                        UebergangAbschliessen(gl);
                    }
                }
                else if (schritt > 0)
                {
                    _verkehr.Weiter(schritt, _spiel.FigurX, _spiel.FigurZ);
                }
                // ab AutoFern Abstand eine einfache Fernform (zwei Kaesten in der Hauptfarbe des Autos)
                var fern = kamera.Abstand > AutoFern;
                Func<Verkehr.Auto, bool>? zeigen = _uebergang is { } u2 ? u2.Aktiv : null;
                SatzZeichnen(gl, _autoNetze, _gestalten, _uebergang?.AltVerkehr ?? _verkehr, zeigen, fern, grafikA, sichtProj);
                if (_uebergang is { } u3)
                {
                    SatzZeichnen(gl, _neuNetze, _neuGestalten, u3.NeuVerkehr, zeigen, fern, grafikA, sichtProj);
                }
            }
            if (_spiel.Ziel is { } ziel && _marke is not null)
            {
                Zeichnen(gl, _marke, Matrix4x4.CreateTranslation((float)ziel.X, _spiel.ImInnenraum ? 0.03f : StadtSzene.Hoehe.Uebergang + 0.012f, (float)ziel.Y), sichtProj);
            }
            var innen = _spiel.Innen;
            var groesse = innen is null ? Figur.StadtHoehe : Innenraum.FigurHoehe;
            var aussehen = _spiel.Ablauf.Stand.Profil is { } profil ? SpielDaten.Get(profil, "aussehen") as PyObjekt : null;
            // Sammelliste 125: Bild der Schrittbewegung nach dem zurueckgelegten Weg (nie nach der Uhr)
            var bild = ProbeBild >= 0 ? ProbeBild : _spiel.SchrittBild;
            var spieler = FigurNetz(gl, "#spieler", aussehen, _spiel.Kleidung, _spiel.Bewegung, bild);
            var figurLage = Matrix4x4.CreateScale(groesse) * Matrix4x4.CreateRotationY(_spiel.FigurRichtung * MathF.PI / 180f)
                * Matrix4x4.CreateTranslation(_spiel.FigurX, innen is null ? StadtSzene.Hoehe.Figur : 0f, _spiel.FigurZ);
            Zeichnen(gl, spieler, figurLage, sichtProj);
            if (innen is not null)
            {
                foreach (var person in innen.Personen)
                {
                    if (innen.Sitz(person) is { } sitz)
                    {
                        // Phase 9 W1 (Weisung P9-3): auf dem Schreibtischstuhl sitzend, mit dem Gesicht zum Tisch
                        Zeichnen(gl, FigurNetz(gl, person.Id, person.Aussehen, null, Figur.Sitzen, 0), Matrix4x4.CreateScale(groesse)
                            * Matrix4x4.CreateRotationY(sitz.Blick) * Matrix4x4.CreateTranslation(sitz.X, 0, sitz.Z), sichtProj);
                        if (person.Auftrag && _ausruf is not null)
                        {
                            Zeichnen(gl, _ausruf, Matrix4x4.CreateTranslation(sitz.X, groesse + 0.25f, sitz.Z), sichtProj);
                        }
                        continue;
                    }
                    // Personen schauen zur Spielfigur
                    var blick = MathF.Atan2(_spiel.FigurX - person.X, _spiel.FigurZ - person.Z);
                    Zeichnen(gl, FigurNetz(gl, person.Id, person.Aussehen), Matrix4x4.CreateScale(groesse) * Matrix4x4.CreateRotationY(blick)
                        * Matrix4x4.CreateTranslation(person.X, 0, person.Z), sichtProj);
                    if (person.Auftrag && _ausruf is not null)
                    {
                        Zeichnen(gl, _ausruf, Matrix4x4.CreateTranslation(person.X, groesse + 0.25f, person.Z), sichtProj);
                    }
                }
                // Waende vor der Kamera und zwischen Auge und Figur halb durchsichtig (Sammelliste 102)
                var d0 = _uhr.Elapsed.TotalSeconds;
                innen.Durchsichtig(kamera.Auge, _spiel.FigurX, _spiel.FigurZ, groesse, _durchsichtig);
                verdecktSek += _uhr.Elapsed.TotalSeconds - d0;
                foreach (var (wand, netz) in _innenWaende)
                {
                    gl.Uniform1f(_ortDurch, _durchsichtig.Contains(wand) ? 1f : 0f);
                    Zeichnen(gl, netz, Matrix4x4.Identity, sichtProj);
                }
                gl.Uniform1f(_ortDurch, 0f);
                VerdecktZahl = _durchsichtig.Count;
                if (Markierung(gl) is { } markierung)
                {
                    gl.Uniform1f(_ortDurch, 1f);
                    Zeichnen(gl, markierung, Matrix4x4.Identity, sichtProj);
                    gl.Uniform1f(_ortDurch, 0f);
                }
            }
            else
            {
                // Haeuser; wer zwischen Auge und Figur steht, halb durchsichtig (Sammelliste 102)
                var v0 = _uhr.Elapsed.TotalSeconds;
                _spiel.Szene.Verdeckt(kamera.Auge, _spiel.FigurX, _spiel.FigurZ, Figur.StadtHoehe, _verdeckt);
                verdecktSek += _uhr.Elapsed.TotalSeconds - v0;
                foreach (var (haus, netz) in _haeuser)
                {
                    var durch = _verdeckt.Contains(haus);
                    gl.Uniform1f(_ortDurch, durch ? 1f : 0f);
                    Zeichnen(gl, netz, Matrix4x4.Identity, sichtProj);
                }
                gl.Uniform1f(_ortDurch, 0f);
                VerdecktZahl = _verdeckt.Count;
                // Nachtrag 1 zu P7-13 (Wunsch 2): Kreis um die Figur in ihrer Kreisfarbe, ohne Tiefenpruefung
                // (kein Haus, Baum oder Schild verdeckt ihn), danach die Figur noch einmal ganz vorn
                gl.Disable(GL_DEPTH_TEST);
                Zeichnen(gl, Ring(gl, aussehen), Matrix4x4.CreateTranslation(_spiel.FigurX, StadtSzene.Hoehe.Figur + 0.01f, _spiel.FigurZ), sichtProj);
                gl.Enable(GL_DEPTH_TEST);
                gl.Clear(GL_DEPTH_BUFFER_BIT);
                Zeichnen(gl, spieler, figurLage, sichtProj);
            }
            // Weisung P8-52 (nur Leistungsprobe): weitere Zeichenaufrufe bis zur Zahl der Stufe, ein kleines Netz unter dem Boden
            while (_aufrufe < ProbeAufrufe && _marke is not null)
            {
                Zeichnen(gl, _marke, Matrix4x4.CreateTranslation(0f, -50f, 0f), sichtProj);
            }
            if (gl.IsBindVertexArrayAvailable)
            {
                gl.BindVertexArray(0);
            }
        }
        gl.Disable(GL_DEPTH_TEST);
        LetzteZeichenaufrufe = _aufrufe;
        LetzteDreiecke = _dreiecke;
        LetzteZeichenzeitMs = (_uhr.Elapsed.TotalSeconds - jetzt) * 1000;
        BilderGesamt++;
        _bilder.Enqueue(jetzt);
        while (_bilder.Count > 0 && _bilder.Peek() < jetzt - 1)
        {
            _bilder.Dequeue();
        }
        var nachZeichnen = _uhr.Elapsed.TotalSeconds;
        BildGezeichnet?.Invoke();
        Messen(jetzt, abstandMs, nachLaden, nachLogik, verdecktSek, nachZeichnen, bytesBeginn, ruhte, startStempel);
        if (geht || Dauerlauf || jetzt < _nachlaufBis || jetzt < _weiterBis || (_verkehr is not null && _spiel is { ImInnenraum: false } && _verkehr.Faehrt(jetzt)))
        {
            RequestNextFrameRendering();
        }
        else
        {
            _ruht = true;
        }
    }

    // ---- Weisung P8-55 (W12, Teilplan Abschnitt 3): Vorladen beim Oeffnen der Spielseite ----------------

    /// <summary>So lange darf das Hochladen je Bild dauern (die Ladeanzeige laeuft weiter); mindestens ein Schritt je Bild.</summary>
    public const double HochladenJeBildMs = 10;

    private volatile bool _glFrisch;
    private volatile bool _laedtVor;
    private volatile Task<Vorladepaket>? _vorladeAufgabe;
    private Queue<Action<GlInterface>>? _hochladen;
    private int _hochladenGesamt;
    private int _hochladenErledigt;
    private long _vorladenBeginn;

    /// <summary>Alles, was das Vorladen auf dem Prozessor baut (im Hintergrund).</summary>
    private sealed record Vorladepaket(StadtSzene Szene, StadtSzene.Fertig Netze, SatzPaket? Satz, bool VerkehrBleibt,
        IReadOnlyList<string> Figuren, IReadOnlyList<Innenraum> Innen, PyObjekt? Aussehen);

    /// <summary>
    /// Weisung P8-55 (W12): wird gerade vorgeladen (Ladeanzeige)? Waehrenddessen zeichnet die Flaeche nur den Himmel
    /// und laedt in kleinen Stuecken je Bild hoch; <see cref="NaechstesBildAsync"/> wartet bis danach.
    /// </summary>
    public bool LaedtVor => _laedtVor;

    /// <summary>Anteil des Hochladens fuer den Fortschrittsring (0 bis 1).</summary>
    public double VorladenAnteil => Ladeanzeige.Anteil(_hochladenErledigt, _hochladenGesamt);

    /// <summary>Sekunden seit Beginn des Vorladens (Ladeanzeige erst ab 0,3 s).</summary>
    public double VorladenSekunden => _laedtVor && _vorladenBeginn != 0 ? Stopwatch.GetElapsedTime(_vorladenBeginn).TotalSeconds : 0;

    /// <summary>Dauer des letzten Vorladens bis fertig in ms (Start-Test, nur Info), null = noch keins.</summary>
    public double? VorladenMs { get; private set; }

    /// <summary>Wie oft vorgeladen wurde (Start-Test: Android nach dem Hintergrund, Seite neu geoeffnet).</summary>
    public int VorladenZahl { get; private set; }

    /// <summary>Beginn und Ende des Vorladens (aus dem Faden der Grafik; die Seite reicht es an die Oberflaeche weiter).</summary>
    public event Action? VorladenGeaendert;

    /// <summary>
    /// Vorladen beginnen (Oberflaechen-Thread): auf dem Prozessor im Hintergrund die Netze der Szene, die Autos bzw.
    /// Festgestalten nah und fern, alle Schrittbilder der Spielfigur, den Kreis und alle Innenraeume mit ihren Personen;
    /// danach laedt <see cref="VorladenBild"/> alles hoch und zeichnet jedes Netz einmal unsichtbar (die Shader werden im
    /// Treiber uebersetzt). Schon gebaute Netze (nach dem Hintergrund unter Android) werden nur neu hochgeladen.
    /// </summary>
    private void VorladenStarten()
    {
        if (!_glFrisch || _spiel is not { } spiel || !GlBereit)
        {
            if (_vorladeAufgabe is null)
            {
                _laedtVor = false;
            }
            return;
        }
        _glFrisch = false;
        var szene = spiel.Szene;
        var grafik = spiel.Grafik;
        var satz = grafik is null ? FestVerkehr.Satz.Autos : FestVerkehr.Fuer(szene.Fest, Tageslicht.FesteUhr ?? spiel.Jetzt.Uhr, szene.Jahreszeit);
        // gleicher Satz: die Autos behalten ihre Lage, nur die Netze werden neu hochgeladen
        var verkehrAlt = _verkehr is not null && satz == _satz && _uebergang is null ? _verkehr : null;
        var gestaltenAlt = _gestalten;
        var aussehen = spiel.Ablauf.Stand.Profil is { } profil ? SpielDaten.Get(profil, "aussehen") as PyObjekt : null;
        var kleidung = spiel.Kleidung;
        var innenFertig = spiel.AlleVorbereiten();
        var figurenCpu = _figurenCpu;
        _vorladenBeginn = Stopwatch.GetTimestamp();
        _hochladenErledigt = 0;
        _hochladenGesamt = 0;
        _vorladeAufgabe = Task.Run(async () =>
        {
            var netze = szene.Vorbereiten();
            SatzPaket? satzPaket = null;
            if (grafik is not null)
            {
                satzPaket = verkehrAlt is not null
                    ? new SatzPaket(szene, satz, gestaltenAlt, verkehrAlt, AutoNetze(verkehrAlt, gestaltenAlt, grafik, false), AutoNetze(verkehrAlt, gestaltenAlt, grafik, true))
                    : SatzPaketBauen(szene, grafik, satz);
            }
            var figuren = new List<string>();
            void Figur1(string schluessel, Func<Netz> bauen)
            {
                figurenCpu.GetOrAdd(schluessel, _ => bauen());
                figuren.Add(schluessel);
            }
            // die Spielfigur: stehend und alle Schrittbilder (Rennen in der Stadt, Gehen in den Innenraeumen)
            Figur1(FigurSchluessel("#spieler", aussehen, kleidung), () => kleidung is { Count: > 0 } ? Figur.Netz(grafik, aussehen, 1f, kleidung) : Figur.Netz(grafik, aussehen, 1f));
            foreach (var bewegung in new[] { Figur.Rennen, Figur.Gehen })
            {
                for (var bild = 0; bild < Figur.Bilder; bild++)
                {
                    var b = bild;
                    Figur1(FigurSchluessel("#spieler", aussehen, kleidung, bewegung, b), () => Figur.Netz(grafik, aussehen, 1f, kleidung, bewegung, b));
                }
            }
            await innenFertig.ConfigureAwait(false);
            var innen = spiel.Vorrat;
            foreach (var raum in innen)
            {
                foreach (var person in raum.Personen)
                {
                    Figur1(FigurSchluessel(person.Id, person.Aussehen, null), () => raum.PersonNetz(person.Id, person.Aussehen) ?? Figur.Netz(grafik, person.Aussehen, 1f));
                    if (raum.Sitz(person) is not null)
                    {
                        // Phase 9 W1: sitzende Personen ebenso vorab (Sitzbild)
                        Figur1(FigurSchluessel(person.Id, person.Aussehen, null, Figur.Sitzen, 0), () => Figur.Netz(grafik, person.Aussehen, 1f, null, Figur.Sitzen, 0));
                    }
                }
            }
            return new Vorladepaket(szene, netze, satzPaket, verkehrAlt is not null, figuren, innen, aussehen);
        });
        _laedtVor = true;
        VorladenGeaendert?.Invoke();
        Anfordern();
    }

    /// <summary>Start-Test: das jetzige Spiel noch einmal vorladen wie beim Oeffnen der Seite (nur mit OpenGL).</summary>
    public void Vorladen()
    {
        _glFrisch = GlBereit;
        VorladenStarten();
    }

    /// <summary>
    /// Ein Bild waehrend des Vorladens: nur der Himmel; ist der Prozessor fertig, Hochladen in Stuecken
    /// (hoechstens <see cref="HochladenJeBildMs"/> je Bild), zuletzt unsichtbar zeichnen. Danach zeichnet die Flaeche normal.
    /// </summary>
    private void VorladenBild(GlInterface gl, double jetzt, double abstandMs, long bytesBeginn, bool ruhte, long startStempel)
    {
        var skalierung = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        gl.Viewport(0, 0, (int)Math.Max(1, Bounds.Width * skalierung), (int)Math.Max(1, Bounds.Height * skalierung));
        var himmel = Himmel();
        gl.ClearColor(himmel.R / 255f, himmel.G / 255f, himmel.B / 255f, 1f);
        gl.Clear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
        var fertig = false;
        if (_vorladeAufgabe is { IsCompleted: true } aufgabe)
        {
            if (!aufgabe.IsCompletedSuccessfully)
            {
                // Fehler beim Bauen: wie vor W12 weiter (die Flaeche baut beim Zeichnen)
                fertig = true;
            }
            else
            {
                _hochladen ??= HochladenPlanen(aufgabe.Result, jetzt);
                var beginn = Stopwatch.GetTimestamp();
                do
                {
                    _hochladen.Dequeue()(gl);
                    _hochladenErledigt++;
                }
                while (_hochladen.Count > 0 && MsSeit(beginn) < HochladenJeBildMs);
                fertig = _hochladen.Count == 0;
            }
        }
        gl.Disable(GL_DEPTH_TEST);
        BilderGesamt++;
        _bilder.Enqueue(jetzt);
        while (_bilder.Count > 0 && _bilder.Peek() < jetzt - 1)
        {
            _bilder.Dequeue();
        }
        var nachLaden = _uhr.Elapsed.TotalSeconds;
        BildGezeichnet?.Invoke();
        Messen(jetzt, abstandMs, nachLaden, nachLaden, 0, nachLaden, bytesBeginn, ruhte, startStempel);
        if (fertig)
        {
            _hochladen = null;
            _vorladeAufgabe = null;
            VorladenMs = Stopwatch.GetElapsedTime(_vorladenBeginn).TotalMilliseconds;
            VorladenZahl++;
            _laedtVor = false;
            VorladenGeaendert?.Invoke();
        }
        RequestNextFrameRendering();
    }

    /// <summary>Die Schritte des Hochladens: Stadt, Haeuser, Gruen, Paket, Autos/Gestalten, Figuren, Kreis, Innenraeume, Anwaermen.</summary>
    private Queue<Action<GlInterface>> HochladenPlanen(Vorladepaket paket, double jetzt)
    {
        var schritte = new Queue<Action<GlInterface>>();
        schritte.Enqueue(gl =>
        {
            foreach (var alt in _flaechen.Values.Concat(_haeuser.Values).Concat(_paket.Values).Append(_gruen))
            {
                alt?.Freigeben(gl);
            }
            _flaechen.Clear();
            _haeuser.Clear();
            _paket.Clear();
            _gruen = null;
            _flaechen[ProbeWelt.Stadt] = GlNetz.Laden(gl, paket.Netze.Stadt);
        });
        foreach (var (haus, netz) in paket.Netze.Haeuser)
        {
            schritte.Enqueue(gl => _haeuser[haus] = GlNetz.Laden(gl, netz));
        }
        if (paket.Netze.Gruen is { } gruen)
        {
            schritte.Enqueue(gl => _gruen = GlNetz.Laden(gl, gruen));
        }
        if (paket.Netze.PaketNah is { } nah && paket.Netze.PaketFern is { } fern)
        {
            schritte.Enqueue(gl => _paket[false] = GlNetz.Laden(gl, nah));
            schritte.Enqueue(gl => _paket[true] = GlNetz.Laden(gl, fern));
        }
        schritte.Enqueue(gl =>
        {
            // eine Szene, die sich waehrend des Vorladens geaendert hat, laedt das naechste Bild wie bisher
            _szeneNeu = !ReferenceEquals(_spiel?.Szene, paket.Szene);
            if (paket.Satz is { } satz)
            {
                SatzEinsetzen(gl, satz, jetzt, paket.VerkehrBleibt);
            }
        });
        foreach (var schluessel in paket.Figuren)
        {
            schritte.Enqueue(gl =>
            {
                if (!_figuren.ContainsKey(schluessel) && _figurenCpu.TryGetValue(schluessel, out var netz))
                {
                    _figuren[schluessel] = GlNetz.Laden(gl, netz);
                }
            });
        }
        schritte.Enqueue(gl => Ring(gl, paket.Aussehen));
        foreach (var raum in paket.Innen)
        {
            schritte.Enqueue(gl => InnenHochladen(gl, raum));
        }
        schritte.Enqueue(gl =>
        {
            _innenNeu = true;
            Anwaermen(gl);
        });
        _hochladenGesamt = schritte.Count;
        return schritte;
    }

    /// <summary>
    /// Jedes hochgeladene Netz einmal zeichnen, in einem Bildpunkt und danach geloescht (unsichtbar): der Treiber
    /// uebersetzt die Shader und legt die Puffer jetzt an, nicht beim ersten echten Bild (Handy-Treiber).
    /// </summary>
    private void Anwaermen(GlInterface gl)
    {
        gl.Viewport(0, 0, 1, 1);
        gl.Enable(GL_DEPTH_TEST);
        gl.DepthFunc(GlLequal);
        gl.UseProgram(_programm);
        gl.Uniform1f(_ortHell, 1f);
        LichtSetzen(gl, _lichtOrte, Tageslicht.Tag, default);
        var netze = _flaechen.Values.Concat(_haeuser.Values).Concat(_paket.Values).Concat(_figuren.Values)
            .Concat(_innenGl.Values.SelectMany(e => e.Waende.Values.Prepend(e.Netz))).Append(_gruen).Append(_marke).Append(_ausruf).Append(_ring);
        var durch = 0f;
        foreach (var netz in netze)
        {
            if (netz is not null)
            {
                // halb durchsichtig (Schachbrett) und normal, beide Wege des Pixel-Shaders
                gl.Uniform1f(_ortDurch, durch);
                durch = 1f - durch;
                Zeichnen(gl, netz, Matrix4x4.Identity, Matrix4x4.Identity);
            }
        }
        gl.Uniform1f(_ortDurch, 0f);
        foreach (var netz in _autoNetze.Values)
        {
            ZeichnenAutos(gl, netz, AutosHoechstens, Matrix4x4.Identity);
        }
        if (gl.IsBindVertexArrayAvailable)
        {
            gl.BindVertexArray(0);
        }
        gl.Clear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
    }

    /// <summary>Innenraum in die Grafikkarte (oder schon dort): Netz ohne Waende und jede Wand fuer sich (halb durchsichtig).</summary>
    private (GlNetz Netz, Dictionary<InnenWand, GlNetz> Waende) InnenHochladen(GlInterface gl, Innenraum innen)
    {
        if (!_innenGl.TryGetValue(innen, out var geladen))
        {
            // Nachtrag 1 zu P7-13 (Wunsch 3): Waende einzeln, jede kann halb durchsichtig werden
            geladen = (GlNetz.Laden(gl, innen.NetzOhneWaende), innen.Waende.ToDictionary(w => w, w => GlNetz.Laden(gl, innen.WandNetzFertig(w))));
            _innenGl[innen] = geladen;
        }
        return geladen;
    }

    /// <summary>Innenraeume freigeben, die weder gezeigt werden noch im Vorrat des Spiels sind.</summary>
    private void InnenAufraeumen(GlInterface gl, StadtSpiel spiel)
    {
        var behalten = new HashSet<Innenraum>(spiel.Vorrat, ReferenceEqualityComparer.Instance);
        if (spiel.Innen is { } jetzt)
        {
            behalten.Add(jetzt);
        }
        foreach (var alt in _innenGl.Keys.Where(k => !behalten.Contains(k)).ToList())
        {
            var (netz, waende) = _innenGl[alt];
            netz.Freigeben(gl);
            foreach (var wand in waende.Values)
            {
                wand.Freigeben(gl);
            }
            _innenGl.Remove(alt);
        }
    }

    /// <summary>
    /// Weisung P7-17: Ruhe vor diesem Bild in ms. Stand die Bildschleife, zaehlt die Zeit vom Ende des letzten
    /// Bildes bis zum ersten Anlass (Zeiger oder Anforderung) als Ruhe; ohne eigenen Anlass (Avalonia zeichnet
    /// von sich aus neu) die ganze Luecke.
    /// </summary>
    private double Ruhe(bool ruhte, long startStempel)
    {
        if (!ruhte)
        {
            return 0;
        }
        var anlass = Erster(_anforderungStempel, _zeigerStempel > _endeVorherStempel ? _zeigerStempel : 0);
        var bis = anlass > 0 ? Math.Min(anlass, startStempel) : startStempel;
        return Math.Max(0, Stopwatch.GetElapsedTime(_endeVorherStempel, bis).TotalMilliseconds);
    }

    private static long Erster(long a, long b) => a == 0 ? b : b == 0 ? a : Math.Min(a, b);

    /// <summary>
    /// Weisung P7-17: fuer ein langsames Bild die Zeitpunkte der Eingabe vor Bildbeginn (Finger auf/ab von
    /// Android, Zeiger-Ereignis von Avalonia, erste Anforderung), nur die seit dem Ende des letzten Bildes.
    /// </summary>
    private string? Eingabe(long startStempel)
    {
        var teile = new List<string>(3);
        void Dazu(string name, long stempel)
        {
            if (stempel > _endeVorherStempel && stempel <= startStempel)
            {
                teile.Add(name + " " + BildZeiten.Ms(Stopwatch.GetElapsedTime(stempel, startStempel).TotalMilliseconds));
            }
        }
        Dazu("Finger", Systemwerte.Finger);
        Dazu("Zeiger", _zeigerStempel);
        Dazu("Anforderung", _anforderungStempel);
        return teile.Count == 0 ? null : "vor Bildbeginn: " + string.Join(", ", teile) + " ms";
    }

    /// <summary>Teilzeiten des Bildes in <see cref="Zeiten"/> eintragen (Speicherbereinigung und Speicher seit dem letzten Bild).</summary>
    private void Messen(double start, double abstandMs, double nachLaden, double nachLogik, double verdecktSek, double nachZeichnen, long bytesBeginn,
        bool ruhte, long startStempel)
    {
        var ende = _uhr.Elapsed.TotalSeconds;
        Span<int> gc = [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)];
        var bytes = GC.GetAllocatedBytesForCurrentThread();
        var teile = _spiel?.TeileMessen() ?? default;
        var bild = new BildZeiten.Bild(start, abstandMs, (nachLogik - nachLaden) * 1000, ((nachLaden - start) + verdecktSek) * 1000,
            (nachZeichnen - nachLogik - verdecktSek) * 1000, (ende - nachZeichnen) * 1000,
            gc[0] - _gcVorher[0], gc[1] - _gcVorher[1], gc[2] - _gcVorher[2], bytes - _bytesVorher, _spiel?.ImInnenraum ?? false,
            _endeVorher < 0 ? 0 : (start - _endeVorher) * 1000, _layouts, _layoutMs, _auftraegeMs + (_spiel?.AktionenMessen() ?? 0), bytes - bytesBeginn,
            teile.WegMs, teile.EreignisMs, RuheMs: _endeVorher < 0 ? 0 : Ruhe(ruhte, startStempel));
        if (BildZeiten.Dauer(bild) >= BildZeiten.LangsamAbMs)
        {
            // Weisung P7-16: Werte des Systems (Android) seit dem Ende des letzten Bildes, nur fuer langsame Bilder;
            // Weisung P7-17: dazu die Zeitpunkte der Eingabe
            bild = bild with { System = Systemwerte.Seit?.Invoke(_endeVorherStempel), Eingabe = Eingabe(startStempel) };
        }
        _anforderungStempel = 0;
        var aufnehmen = _bytesVorher >= 0;
        gc.CopyTo(_gcVorher);
        _bytesVorher = bytes;
        _layouts = 0;
        _layoutMs = 0;
        _auftraegeMs = 0;
        _endeVorher = ende;
        _endeVorherStempel = Stopwatch.GetTimestamp();
        Mitschnitt?.Add(bild);
        if (aufnehmen)
        {
            // die Messung selbst (alle 10 s eine Zeile) zaehlt als Auftrag vor dem naechsten Bild
            var beginn = Stopwatch.GetTimestamp();
            Zeiten.Aufnehmen(bild);
            AuftragGemessen(MsSeit(beginn));
        }
    }

    /// <summary>Wie viele Haeuser im letzten Bild halb durchsichtig waren (Start-Test, Fotos).</summary>
    public int VerdecktZahl { get; private set; }

    /// <summary>Kreis um die Spielfigur (Radius 0,22 = 1,1 m), neu geladen, wenn sich die Kreisfarbe aendert.</summary>
    private GlNetz Ring(GlInterface gl, PyObjekt? aussehen)
    {
        if (_ring is not null && ReferenceEquals(aussehen, _ringAussehen))
        {
            return _ring;
        }
        _ringAussehen = aussehen;
        var schluessel = Aussehen.Normalisieren(aussehen)["kreis"] as string ?? "";
        if (_ring is null || schluessel != _ringSchluessel)
        {
            _ring?.Freigeben(gl);
            _ring = GlNetz.Laden(gl, Figur.Ring(aussehen, RingRadius, 0.06f));
            _ringSchluessel = schluessel;
        }
        return _ring;
    }

    /// <summary>Radius des Kreises um die Figur in der Stadt.</summary>
    public const float RingRadius = 0.22f;

    /// <summary>Schluessel einer Figur: Person, Aussehen, Kleidung und (beim Gehen) Bild der Schrittbewegung.</summary>
    private static string FigurSchluessel(string id, PyObjekt? aussehen, IReadOnlyList<Figur.Stueck>? kleidung, string? bewegung = null, int bild = -1) =>
        id + "|" + string.Join(",", Aussehen.Normalisieren(aussehen).Select(p => p.Value))
        + (kleidung is { Count: > 0 } ? "|" + string.Join(",", kleidung.Select(k => k.Slot + ":" + k.Form + ":" + k.Farbe)) : "")
        + (bild >= 0 && bewegung is not null ? "|" + Figur.BildName(bewegung, bild) : "");

    /// <summary>Figur (Hoehe 1) mit Aussehen (die Spielfigur mit Kleidung aus dem Kaufhaus), je Person einmal geladen.</summary>
    private GlNetz FigurNetz(GlInterface gl, string id, PyObjekt? aussehen, IReadOnlyList<Figur.Stueck>? kleidung = null, string? bewegung = null, int bild = -1)
    {
        if (bild >= 0 && bewegung is not null)
        {
            // Bilder der Schrittbewegung: je Aussehen, Kleidung und Bild einmal gebaut (Sammelliste 125)
            var sb = FigurSchluessel(id, aussehen, kleidung, bewegung, bild);
            if (!_figuren.TryGetValue(sb, out var nb))
            {
                var grafik = _spiel?.Grafik;
                nb = GlNetz.Laden(gl, _figurenCpu.GetOrAdd(sb, _ => Figur.Netz(grafik, aussehen, 1f, kleidung, bewegung, bild)));
                _figuren[sb] = nb;
            }
            return nb;
        }
        // Wunsch 4: dasselbe Aussehen wie im letzten Bild ohne neuen Schluessel (der kostete je Bild und
        // Figur ein neues Woerterbuch und Zeichenketten, also Arbeit fuer die Speicherbereinigung);
        // StadtSpiel.Kleidung gibt dieselbe Liste zurueck, solange sich nichts aendert
        if (_figurZuletzt.TryGetValue(id, out var zuletzt) && ReferenceEquals(zuletzt.Aussehen, aussehen) && ReferenceEquals(zuletzt.Kleidung, kleidung))
        {
            return zuletzt.Netz;
        }
        var schluessel = FigurSchluessel(id, aussehen, kleidung);
        if (!_figuren.TryGetValue(schluessel, out var netz))
        {
            var (grafik, innen) = (_spiel?.Grafik, _spiel?.Innen);
            netz = GlNetz.Laden(gl, _figurenCpu.GetOrAdd(schluessel, _ => kleidung is { Count: > 0 } ? Figur.Netz(grafik, aussehen, 1f, kleidung)
                : innen?.PersonNetz(id, aussehen) ?? Figur.Netz(grafik, aussehen, 1f)));
            _figuren[schluessel] = netz;
        }
        _figurZuletzt[id] = (aussehen, kleidung, netz);
        return netz;
    }

    /// <summary>Vorschau beim Einrichten (gruen/gelb/rot) und Rahmen um das gewaehlte Moebel; null = nichts.</summary>
    private GlNetz? Markierung(GlInterface gl)
    {
        if (_spiel is not { Einrichten: true } spiel)
        {
            return null;
        }
        var teile = new List<Teil>();
        if (spiel.Vorschau is { } v)
        {
            var farbe = Netz.Farbe(spiel.VorschauFarbe switch { "gruen" => "#34D399", "gelb" => "#FBBF24", _ => "#F87171" });
            float x = (float)Py.F(v["x"]), z = (float)Py.F(v["y"]), w = (float)Py.F(v["w"]), t = (float)Py.F(v["h"]);
            teile.Add(new Teil(TeilArt.Quader, x, 0.04f, z, x + w, 0.9f, z + t, farbe));
        }
        else if (spiel.Ausgewaehlt is { } stueck && Wohnung.AufgestellteMoebel(spiel.Ablauf.Stand).FirstOrDefault(m => m.Stueck == stueck) is { } m)
        {
            float x = (float)m.X, z = (float)m.Y, w = (float)Py.F(m.W), t = (float)Py.F(m.H);
            var farbe = Netz.Farbe("#22D3EE");
            teile.Add(new Teil(TeilArt.Quader, x - 0.08f, 0.04f, z - 0.08f, x + w + 0.08f, 0.07f, z + t + 0.08f, farbe));
        }
        if (teile.Count == 0)
        {
            return null;
        }
        var schluessel = string.Join(";", teile);
        if (schluessel != _markierungSchluessel || _markierung is null)
        {
            _markierung?.Freigeben(gl);
            _markierung = GlNetz.Laden(gl, Netz.Aus(teile));
            _markierungSchluessel = schluessel;
        }
        return _markierung;
    }

    /// <summary>Netz eines Autos im eigenen Raum: das Modell oder (fern) zwei Kaesten in seiner Hauptfarbe; leer ohne Modell.</summary>
    private static Netz AutoNetz(GrafikBibliothek grafik, string modell, bool fern)
    {
        if (grafik.Modell(modell) is not { } am)
        {
            return new Netz();
        }
        if (!fern)
        {
            var nz = new Netz();
            nz.Hinzu(am, Matrix4x4.Identity);
            return nz;
        }
        var farbe = Hauptfarbe(am);
        float h = am.Max.Y - am.Min.Y;
        return Netz.Aus(
        [
            new Teil(TeilArt.Quader, am.Min.X, am.Min.Y + (h * 0.12f), am.Min.Z, am.Max.X, am.Min.Y + (h * 0.55f), am.Max.Z, farbe),
            new Teil(TeilArt.Quader, am.Min.X * 0.85f, am.Min.Y + (h * 0.55f), am.Min.Z * 0.5f, am.Max.X * 0.85f, am.Max.Y, am.Max.Z * 0.45f, farbe * 0.8f),
        ]);
    }

    /// <summary>Alle Autos in einem Zeichenaufruf (eigenes Programm), danach wieder das Programm der Szene.</summary>
    /// <summary>
    /// Phase 8 W11: Autos oder Festgestalten setzen (neuer Verkehr nach <see cref="FestVerkehr.VerkehrFuer"/>, die
    /// Netze der Autos bzw. Gestalten werden neu geladen).
    /// </summary>
    private void SatzSetzen(GlInterface gl, StadtSpiel spiel, FestVerkehr.Satz satz, double jetzt) =>
        SatzEinsetzen(gl, SatzVorbereitetNehmen(spiel.Szene, satz) ?? SatzPaketBauen(spiel.Szene, spiel.Grafik!, satz), jetzt);

    /// <summary>Weisung P8-55 (W12): Gestalten, Verkehr und beide Netze (nah, fern) eines Satzes, auf dem Prozessor gebaut.</summary>
    private sealed record SatzPaket(StadtSzene Szene, FestVerkehr.Satz Satz, List<FestVerkehr.Gestalt>? Gestalten, Verkehr Verkehr, List<Netz> Nah, List<Netz> Fern);

    private static SatzPaket SatzPaketBauen(StadtSzene szene, GrafikBibliothek grafik, FestVerkehr.Satz satz)
    {
        var gestalten = satz == FestVerkehr.Satz.Autos ? null : FestVerkehr.Gestalten(grafik, satz);
        var verkehr = FestVerkehr.VerkehrFuer(szene, satz);
        return new SatzPaket(szene, satz, gestalten, verkehr, AutoNetze(verkehr, gestalten, grafik, false), AutoNetze(verkehr, gestalten, grafik, true));
    }

    /// <summary>Netze fuer den einen Zeichenaufruf der Autos bzw. Festgestalten (nah oder fern).</summary>
    private static List<Netz> AutoNetze(Verkehr verkehr, List<FestVerkehr.Gestalt>? gestalten, GrafikBibliothek grafik, bool fern) =>
        gestalten is not null ? FestVerkehr.Netze(gestalten, fern) : [.. verkehr.Autos.Take(AutosHoechstens).Select(a => AutoNetz(grafik, a.Modell, fern))];

    /// <summary>Satz einsetzen: alte Netze frei, neuer Verkehr (oder der alte bei <paramref name="verkehrBleibt"/>), beide Netze hochladen.</summary>
    private void SatzEinsetzen(GlInterface gl, SatzPaket paket, double jetzt, bool verkehrBleibt = false)
    {
        foreach (var alt in _autoNetze.Values.Concat(_neuNetze.Values))
        {
            alt.Freigeben(gl);
        }
        _autoNetze.Clear();
        // Patch A 0.29.1: ein laufender Uebergang endet (der Satz ist sofort ganz da)
        _neuNetze.Clear();
        _uebergang = null;
        _neuGestalten = null;
        _uebergangBau = null;
        if (!verkehrBleibt)
        {
            _satz = paket.Satz;
            _gestalten = paket.Gestalten;
            _verkehr = paket.Verkehr;
            _verkehr.Bedient(jetzt);
        }
        _autoNetze[false] = GlNetz.LadenMitNummern(gl, paket.Nah);
        _autoNetze[true] = GlNetz.LadenMitNummern(gl, paket.Fern);
    }

    /// <summary>
    /// Patch A 0.29.1 (Weisung P10-3, Nico "Nach und nach, unsichtbar"): je Bild pruefen, ob sich draussen der Satz mit
    /// der Uhr aendert (nur nach einer Aenderung des Spiels oder der Szene); dann den neuen Satz im Hintergrund bauen
    /// und, sobald er fertig ist, den Uebergang beginnen (<see cref="FestUebergang"/>). Im Haus endet ein Uebergang
    /// sofort (dort sieht niemand die Strassen).
    /// </summary>
    private void UebergangTakt(GlInterface gl, StadtSpiel spiel, GrafikBibliothek grafik, double jetzt)
    {
        if (spiel.ImInnenraum)
        {
            if (_uebergang is { } innenU)
            {
                innenU.Abschliessen();
                UebergangAbschliessen(gl);
            }
            return;
        }
        if (_uebergangBau is { } bau && bau.Aufgabe.IsCompleted)
        {
            _uebergangBau = null;
            if (bau.Aufgabe.IsCompletedSuccessfully && ReferenceEquals(bau.Szene, spiel.Szene) && _verkehr is { } alt)
            {
                var paket = bau.Aufgabe.Result;
                _neuNetze[false] = GlNetz.LadenMitNummern(gl, paket.Nah);
                _neuNetze[true] = GlNetz.LadenMitNummern(gl, paket.Fern);
                _neuGestalten = paket.Gestalten;
                _uebergang = new FestUebergang(_satz, alt, paket.Satz, paket.Verkehr);
                _verkehr = _uebergang.Verkehr;
            }
            _satzPruefen = true;
        }
        if (!_satzPruefen)
        {
            return;
        }
        _satzPruefen = false;
        var ziel = FestVerkehr.Fuer(spiel.Szene.Fest, Tageslicht.FesteUhr ?? spiel.Jetzt.Uhr, spiel.Szene.Jahreszeit);
        if (ziel == (_uebergangBau?.Satz ?? _uebergang?.Neu ?? _satz))
        {
            return;
        }
        if (_uebergang is { } laufend)
        {
            // die Uhr hat sich waehrend eines Uebergangs noch einmal geaendert: den laufenden sofort fertig machen
            laufend.Abschliessen();
            UebergangAbschliessen(gl);
            if (ziel == _satz)
            {
                return;
            }
        }
        if (_satz != FestVerkehr.Satz.Autos && ziel != FestVerkehr.Satz.Autos)
        {
            // von einem Fest zu einem anderen gibt es in einer Stadt nicht (eine Szene hat ein Fest); sicherheitshalber sofort
            SatzSetzen(gl, spiel, ziel, jetzt);
            return;
        }
        var szene = spiel.Szene;
        _uebergangBau = new SatzVorrat(szene, ziel, Task.Run(() => SatzPaketBauen(szene, grafik, ziel)));
    }

    /// <summary>Patch A 0.29.1: Uebergang fertig, der neue Satz ist ganz da (Netze, Gestalten, Verkehr).</summary>
    private void UebergangAbschliessen(GlInterface gl)
    {
        if (_uebergang is not { } u)
        {
            return;
        }
        foreach (var alt in _autoNetze.Values)
        {
            alt.Freigeben(gl);
        }
        _autoNetze.Clear();
        foreach (var (fern, netz) in _neuNetze)
        {
            _autoNetze[fern] = netz;
        }
        _neuNetze.Clear();
        _satz = u.Neu;
        _gestalten = _neuGestalten;
        _verkehr = u.NeuVerkehr;
        _neuGestalten = null;
        _uebergang = null;
    }

    /// <summary>
    /// Autos oder Festgestalten eines Satzes in einem Zeichenaufruf; <paramref name="vorlage"/> = Verkehr des Satzes (gleiche
    /// Reihenfolge wie die Netze), <paramref name="zeigen"/> = nur diese Fahrzeuge zeigen (Uebergang, sonst alle).
    /// </summary>
    private void SatzZeichnen(GlInterface gl, Dictionary<bool, GlNetz> netze, List<FestVerkehr.Gestalt>? gestalten, Verkehr vorlage,
        Func<Verkehr.Auto, bool>? zeigen, bool fern, GrafikBibliothek grafik, Matrix4x4 sichtProj)
    {
        if (!netze.TryGetValue(fern, out var netz))
        {
            netz = GlNetz.LadenMitNummern(gl, AutoNetze(vorlage, gestalten, grafik, fern));
            netze[fern] = netz;
        }
        if (gestalten is not null)
        {
            // W11: Festgestalten im selben Zeichenaufruf wie die Autos (nah alle Schrittbilder, das gezeigte je Gestalt)
            ZeichnenAutos(gl, netz, FestVerkehr.Lagen(gestalten, vorlage, fern, _autoLagen, zeigen), sichtProj);
            return;
        }
        var anzahl = Math.Min(vorlage.Autos.Count, AutosHoechstens);
        for (var i = 0; i < anzahl; i++)
        {
            var auto = vorlage.Autos[i];
            if (zeigen is not null && !zeigen(auto))
            {
                _autoLagen[i] = Matrix4x4.CreateScale(0) * Matrix4x4.CreateTranslation(0, -1000, 0);
            }
            else if (grafik.Modell(auto.Modell) is { } am)
            {
                var (ort, richtung) = auto.Lage(auto.Weg);
                var anker = new Vector3((am.Min.X + am.Max.X) / 2, am.Min.Y, (am.Min.Z + am.Max.Z) / 2);
                _autoLagen[i] = StadtSzene.Lage(am, anker, new Vector3(StadtSzene.Meter * 1.1f), new Vector3(ort.X, StadtSzene.Hoehe.Strasse, ort.Y), richtung);
            }
        }
        ZeichnenAutos(gl, netz, anzahl, sichtProj);
    }

    /// <summary>Patch A 0.29.1: fuer die Toene nur die Autos, die gerade fahren (im Uebergang ohne die Gestalten).</summary>
    private Verkehr? TonVerkehr()
    {
        if (_uebergang is not { } u)
        {
            return _verkehr;
        }
        var autos = u.Alt == FestVerkehr.Satz.Autos ? u.AltVerkehr : u.Neu == FestVerkehr.Satz.Autos ? u.NeuVerkehr : null;
        var v = new Verkehr();
        if (autos is not null)
        {
            v.Autos.AddRange(u.Verkehr.Autos.Where(autos.Autos.Contains));
        }
        return v;
    }

    private sealed record SatzVorrat(StadtSzene Szene, FestVerkehr.Satz Satz, Task<SatzPaket> Aufgabe);

    private volatile SatzVorrat? _satzVorrat;

    /// <summary>Den im Haus vorbereiteten Satz nehmen, wenn er zu Szene und Satz passt (sonst null).</summary>
    private SatzPaket? SatzVorbereitetNehmen(StadtSzene szene, FestVerkehr.Satz satz)
    {
        if (_satzVorrat is not { } v || !ReferenceEquals(v.Szene, szene) || v.Satz != satz)
        {
            return null;
        }
        _satzVorrat = null;
        try
        {
            return v.Aufgabe.GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Weisung P8-55 (W12, Teilplan 3.5): im Haus den Satz fuer das Hinausgehen im Hintergrund vorbereiten (Festgestalten
    /// oder Autos mit ihren Netzen), wenn er sich gegenueber dem jetzigen aendert (Daemmerung, neues Fest). Fuer die Szene,
    /// die das Spiel beim Hinausgehen zeigen wird (<see cref="StadtSpiel.SzeneDraussen"/>). Die Seite ruft das im Takt.
    /// </summary>
    public void Vorausbauen()
    {
        if (_spiel is not { ImInnenraum: true } spiel || spiel.Grafik is not { } grafik || _laedtVor || _verkehr is null)
        {
            return;
        }
        var szene = spiel.SzeneDraussen;
        var satz = FestVerkehr.Fuer(szene.Fest, Tageslicht.FesteUhr ?? spiel.Jetzt.Uhr, szene.Jahreszeit);
        if (satz == _satz || (_satzVorrat is { } v && ReferenceEquals(v.Szene, szene) && v.Satz == satz))
        {
            return;
        }
        _satzVorrat = new SatzVorrat(szene, satz, Task.Run(() => SatzPaketBauen(szene, grafik, satz)));
    }

    private unsafe void ZeichnenAutos(GlInterface gl, GlNetz netz, int anzahl, Matrix4x4 sichtProj)
    {
        gl.UseProgram(_autoProgramm);
        gl.UniformMatrix4fv(_autoOrtSichtProj, 1, false, &sichtProj);
        fixed (Matrix4x4* lagen = _autoLagen)
        {
            gl.UniformMatrix4fv(_autoOrtLagen, anzahl, false, lagen);
        }
        var licht = AktuellesLicht();
        gl.Uniform1f(_autoOrtHell, (IstDunkel() ? 0.72f : 1f) * licht.Hell);
        LichtSetzen(gl, _autoLichtOrte, licht, AktuelleKamera?.Auge ?? default);
        gl.Uniform1f(_autoOrtDurch, 0f);
        netz.Binden(gl);
        gl.DrawElements(GL_TRIANGLES, netz.Anzahl, GlUnsignedInt, IntPtr.Zero);
        _aufrufe++;
        _dreiecke += netz.Anzahl / 3;
        gl.UseProgram(_programm);
    }

    private unsafe void Zeichnen(GlInterface gl, GlNetz netz, Matrix4x4 modell, Matrix4x4 sichtProj)
    {
        var mvp = modell * sichtProj;
        gl.UniformMatrix4fv(_ortMvp, 1, false, &mvp);
        gl.UniformMatrix4fv(_ortModell, 1, false, &modell);
        netz.Binden(gl);
        gl.DrawElements(GL_TRIANGLES, netz.Anzahl, GlUnsignedInt, IntPtr.Zero);
        _aufrufe++;
        _dreiecke += netz.Anzahl / 3;
    }

    /// <summary>
    /// Phase 8 W10 (Nico 3b): Licht nach der Tagesuhr der Stadt und der Jahreszeit, innen immer Tag. Die Uhr wird nur
    /// nach einer Aenderung des Spiels neu gelesen (nicht je Bild); springt sie, blendet das Licht in
    /// <see cref="Tageslicht.Ueberblenden"/> ueber (Wechsel zwischen innen und aussen und feste Uhr sofort).
    /// </summary>
    private Tageslicht.Licht AktuellesLicht()
    {
        var jetzt = _uhr.Elapsed.TotalSeconds;
        var innen = _spiel is null || _spiel.ImInnenraum;
        var fest = Tageslicht.FesteUhr;
        if (_lichtNeu || innen != _lichtInnen || fest != _lichtFest)
        {
            var ziel = innen ? Tageslicht.Tag : Tageslicht.Fuer(fest ?? _spiel!.Jetzt.Uhr, Jahreszeiten.Heute());
            var sofort = innen != _lichtInnen || fest != _lichtFest || _lichtSeit == double.MinValue;
            if (ziel != _lichtZiel || sofort)
            {
                _lichtVon = sofort ? ziel : LichtBei(jetzt);
                _lichtZiel = ziel;
                _lichtSeit = jetzt;
            }
            _lichtNeu = false;
            _lichtInnen = innen;
            _lichtFest = fest;
        }
        var licht = LichtBei(jetzt);
        if (licht != _lichtZiel)
        {
            AnfordernBild();
        }
        return licht;
    }

    private Tageslicht.Licht LichtBei(double jetzt) =>
        Tageslicht.Mischen(_lichtVon, _lichtZiel, (float)((jetzt - _lichtSeit) / Tageslicht.Ueberblenden.TotalSeconds));

    /// <summary>Licht im Bild (Start-Test: Tag, Daemmerung, Nacht).</summary>
    public Tageslicht.Licht LichtJetzt => _lichtZiel;

    private static void LichtSetzen(GlInterface gl, int[] orte, Tageslicht.Licht l, System.Numerics.Vector3 auge)
    {
        gl.Uniform1f(orte[0], l.Hell);
        gl.Uniform1f(orte[1], l.Ton.X);
        gl.Uniform1f(orte[2], l.Ton.Y);
        gl.Uniform1f(orte[3], l.Ton.Z);
        gl.Uniform1f(orte[4], l.Nacht);
        gl.Uniform1f(orte[5], auge.X);
        gl.Uniform1f(orte[6], auge.Y);
        gl.Uniform1f(orte[7], auge.Z);
    }

    private Color Himmel() =>
        this.TryFindResource("Pinsel.bg", ActualThemeVariant, out var r) && r is ISolidColorBrush b ? b.Color : Color.FromRgb(220, 235, 250);

    private bool IstDunkel()
    {
        var c = Himmel();
        return (0.299 * c.R) + (0.587 * c.G) + (0.114 * c.B) < 110;
    }

    // ---- Bedienung ----

    /// <summary>Neue Groesse (Fenster, Drehen, Ortsliste): sofort neu zeichnen, sonst bleibt das alte Bild stehen.</summary>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Anfordern();
    }

    /// <summary>Phase 8 W8 (Sammelliste 132/135): Maus ueber den Kameragriff (rechte Taste dreht, nie Gehen).</summary>
    private readonly Kameragriff _griff = new(ZiehenAb);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _zeigerStempel = Stopwatch.GetTimestamp();
        Focus();
        var p = e.GetPosition(this);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            var art = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
            if (art is PointerUpdateKind.LeftButtonPressed or PointerUpdateKind.RightButtonPressed)
            {
                _griff.Druecken(art == PointerUpdateKind.RightButtonPressed, p.X, p.Y);
                e.Pointer.Capture(this);
            }
            e.Handled = true;
            return;
        }
        _zeiger[e.Pointer] = p;
        if (_zeiger.Count == 1)
        {
            _druckStart = p;
            _gezogen = false;
        }
        else if (_zeiger.Count == 2)
        {
            _abstandZwei = Abstand();
            _winkelZwei = Winkel();
            _gezogen = true;
        }
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            if (!_griff.Gedrueckt || AktuelleKamera is not { } maus)
            {
                return;
            }
            var mp = e.GetPosition(this);
            MausSchritt(maus, _griff.Bewegen(mp.X, mp.Y));
            return;
        }
        if (!_zeiger.TryGetValue(e.Pointer, out var vorher))
        {
            return;
        }
        var p = e.GetPosition(this);
        _zeiger[e.Pointer] = p;
        if (AktuelleKamera is not { } kamera)
        {
            return;
        }
        if (_zeiger.Count >= 2)
        {
            // zwei Finger: Abstand = Zoom, Winkel = Drehen um die Hochachse
            var neu = Abstand();
            if (_abstandZwei > 1 && neu > 1)
            {
                kamera.Zoomen((float)(neu / _abstandZwei));
            }
            _abstandZwei = neu;
            var winkel = Winkel();
            var dreh = winkel - _winkelZwei;
            dreh = dreh > 180 ? dreh - 360 : dreh < -180 ? dreh + 360 : dreh;
            kamera.Drehen((float)dreh);
            _winkelZwei = winkel;
            _folgenAb = _uhr.Elapsed.TotalSeconds + 3;
            AnfordernBild();
            return;
        }
        if (!_gezogen && Math.Abs(p.X - _druckStart.X) + Math.Abs(p.Y - _druckStart.Y) > ZiehenAb)
        {
            _gezogen = true;
        }
        if (_gezogen)
        {
            kamera.Verschieben((float)(p.X - vorher.X), (float)(p.Y - vorher.Y), (float)Bounds.Height);
            _folgenAb = _uhr.Elapsed.TotalSeconds + 3;
            AnfordernBild();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _zeigerStempel = Stopwatch.GetTimestamp();
        if (e.Pointer.Type == PointerType.Mouse)
        {
            var art = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
            if (art is PointerUpdateKind.LeftButtonReleased or PointerUpdateKind.RightButtonReleased)
            {
                var tippen = _griff.Loslassen(art == PointerUpdateKind.RightButtonReleased);
                if (!_griff.Gedrueckt)
                {
                    e.Pointer.Capture(null);
                }
                if (tippen)
                {
                    Tippen(e.GetPosition(this));
                }
            }
            e.Handled = true;
            return;
        }
        var war = _zeiger.Remove(e.Pointer);
        e.Pointer.Capture(null);
        if (war && _zeiger.Count == 0 && !_gezogen)
        {
            Tippen(e.GetPosition(this));
        }
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            _griff.Abbrechen();
        }
        _zeiger.Remove(e.Pointer);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        AktuelleKamera?.Zoomen(MathF.Pow(1.12f, (float)e.Delta.Y));
        AnfordernBild();
        e.Handled = true;
    }

    /// <summary>Weite eines Pfeiltasten-Schritts in Bildpunkten (wie Ziehen).</summary>
    public const float TastenSchritt = 48f;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Taste(e.Key))
        {
            e.Handled = true;
        }
    }

    /// <summary>Taste anwenden (auch fuer den Start-Test); true, wenn die Flaeche sie genommen hat.</summary>
    public bool Taste(Key taste)
    {
        if (AktuelleKamera is not { } kamera || _spiel is not { } spiel)
        {
            return false;
        }
        // Phase 8 W8 (Sammelliste 133): Tasten nach der Tastenbelegung (Standard wie bisher)
        var aufgabe = Tastenbelegung.Aktuell.AufgabeFuer(taste.ToString());
        var (dx, dy) = aufgabe switch
        {
            Tastenbelegung.Links => (TastenSchritt, 0f),
            Tastenbelegung.Rechts => (-TastenSchritt, 0f),
            Tastenbelegung.Hoch => (0f, TastenSchritt),
            Tastenbelegung.Runter => (0f, -TastenSchritt),
            _ => (0f, 0f),
        };
        if (dx != 0 || dy != 0)
        {
            kamera.Verschieben(dx, dy, (float)Math.Max(1, Bounds.Height));
        }
        else if (aufgabe == Tastenbelegung.DrehLinks)
        {
            kamera.Drehen(-15f);
        }
        else if (aufgabe == Tastenbelegung.DrehRechts)
        {
            kamera.Drehen(15f);
        }
        else if (aufgabe == Tastenbelegung.Naeher)
        {
            kamera.Zoomen(1.15f);
        }
        else if (aufgabe == Tastenbelegung.Weiter)
        {
            kamera.Zoomen(1 / 1.15f);
        }
        else if (aufgabe == Tastenbelegung.Hinein)
        {
            return spiel.Betreten();
        }
        else
        {
            return false;
        }
        _folgenAb = _uhr.Elapsed.TotalSeconds + 3;
        AnfordernBild();
        return true;
    }

    /// <summary>Ein Schritt der Maus anwenden (auch fuer den Start-Test): Ziehen verschiebt, rechte Taste dreht.</summary>
    public void MausSchritt(Kamera kamera, Kameragriff.Schritt schritt)
    {
        switch (schritt.Art)
        {
            case Kameragriff.Art.Ziehen:
                kamera.Verschieben((float)schritt.Dx, (float)schritt.Dy, (float)Bounds.Height);
                break;
            case Kameragriff.Art.Drehen:
                kamera.Drehen(schritt.Grad);
                break;
            default:
                return;
        }
        _folgenAb = _uhr.Elapsed.TotalSeconds + 3;
        AnfordernBild();
    }

    /// <summary>Start-Test: Maus mit rechter Taste von x1 nach x2 ziehen (wie echte Ereignisse ueber den Griff).</summary>
    public bool ProbeRechtsZiehen(double x1, double x2, double y)
    {
        if (AktuelleKamera is not { } kamera)
        {
            return false;
        }
        _griff.Druecken(true, x1, y);
        MausSchritt(kamera, _griff.Bewegen(x2, y));
        var tippen = _griff.Loslassen(true);
        if (tippen)
        {
            Tippen(new Point(x2, y));
        }
        return !tippen;
    }

    private double Abstand()
    {
        var p = _zeiger.Values.Take(2).ToArray();
        return p.Length < 2 ? 0 : Math.Sqrt(Math.Pow(p[0].X - p[1].X, 2) + Math.Pow(p[0].Y - p[1].Y, 2));
    }

    /// <summary>Winkel der Linie zwischen den zwei Fingern in Grad.</summary>
    private double Winkel()
    {
        var p = _zeiger.Values.Take(2).ToArray();
        return p.Length < 2 ? 0 : Math.Atan2(p[1].Y - p[0].Y, p[1].X - p[0].X) * 180 / Math.PI;
    }

    /// <summary>Kamera sofort auf die Figur stellen (nach einem Sprung ohne Gehen, z. B. im Start-Test).</summary>
    public void KameraAufFigur()
    {
        if (AktuelleKamera is { } kamera && _spiel is { } spiel)
        {
            kamera.Setzen(spiel.FigurX, spiel.FigurZ);
            AnfordernBild();
        }
    }

    /// <summary>Tippen an (x, y) in Punkten: Stadt -> Haus unter dem Finger, sonst zum Bodenpunkt; Buero -> zum Bodenpunkt.</summary>
    public void Tippen(Point p)
    {
        if (AktuelleKamera is not { } kamera || _spiel is not { } spiel)
        {
            return;
        }
        _folgenAb = 0;
        if (spiel.ImInnenraum)
        {
            if (kamera.Boden((float)p.X, (float)p.Y, (float)Bounds.Width, (float)Bounds.Height) is { } b)
            {
                spiel.GeheZu(b.X, b.Y);
            }
        }
        else if (kamera.Strahl((float)p.X, (float)p.Y, (float)Bounds.Width, (float)Bounds.Height) is { } strahl
            && spiel.Szene.OrtBei(strahl.Start, strahl.Richtung) is { } ort)
        {
            spiel.GeheZu(ort.Id);
        }
        else if (kamera.Boden((float)p.X, (float)p.Y, (float)Bounds.Width, (float)Bounds.Height) is { } boden)
        {
            // kein Haus getroffen: frei dorthin gehen (P7-10 Nachtrag 1, Wunsch 3)
            spiel.GeheZu(boden.X, boden.Y);
        }
        AnfordernBild();
    }

    /// <summary>Ein Dreiecksnetz in Puffern der Grafikkarte (mit Vertex-Array, wo vorhanden).</summary>
    private sealed class GlNetz
    {
        private int _ecken;
        private int _indizes;
        private int _vao;
        private bool _nummern;

        public int Anzahl { get; private set; }

        /// <summary>
        /// Weisung P8-14b: mehrere Netze in einem, jede Ecke mit der Nummer ihres Netzes als zehnte Zahl
        /// (Attribut 3, aNummer), fuer einen Zeichenaufruf mit je einer Lage aus einem Feld.
        /// </summary>
        public static unsafe GlNetz LadenMitNummern(GlInterface gl, IReadOnlyList<Netz> netze)
        {
            var ecken = new List<float>();
            var indizes = new List<uint>();
            for (var n = 0; n < netze.Count; n++)
            {
                var basis = (uint)(ecken.Count / (Netz.Zahlen + 1));
                var quelle = netze[n].EckenFeld;
                for (var e = 0; e < quelle.Length; e += Netz.Zahlen)
                {
                    for (var k = 0; k < Netz.Zahlen; k++)
                    {
                        ecken.Add(quelle[e + k]);
                    }
                    ecken.Add(n);
                }
                foreach (var i in netze[n].IndizesFeld)
                {
                    indizes.Add(basis + i);
                }
            }
            var g = new GlNetz { Anzahl = indizes.Count, _nummern = true };
            if (gl.IsGenVertexArraysAvailable)
            {
                g._vao = gl.GenVertexArray();
                gl.BindVertexArray(g._vao);
            }
            g._ecken = gl.GenBuffer();
            gl.BindBuffer(GL_ARRAY_BUFFER, g._ecken);
            fixed (float* zeiger = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(ecken))
            {
                gl.BufferData(GL_ARRAY_BUFFER, new IntPtr(ecken.Count * sizeof(float)), new IntPtr(zeiger), GL_STATIC_DRAW);
            }
            g._indizes = gl.GenBuffer();
            gl.BindBuffer(GL_ELEMENT_ARRAY_BUFFER, g._indizes);
            fixed (uint* zeiger = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(indizes))
            {
                gl.BufferData(GL_ELEMENT_ARRAY_BUFFER, new IntPtr(indizes.Count * sizeof(uint)), new IntPtr(zeiger), GL_STATIC_DRAW);
            }
            g.Attribute(gl);
            if (gl.IsGenVertexArraysAvailable)
            {
                gl.BindVertexArray(0);
            }
            return g;
        }

        public static unsafe GlNetz Laden(GlInterface gl, Netz netz)
        {
            var g = new GlNetz { Anzahl = netz.Indizes.Count };
            if (gl.IsGenVertexArraysAvailable)
            {
                g._vao = gl.GenVertexArray();
                gl.BindVertexArray(g._vao);
            }
            g._ecken = gl.GenBuffer();
            gl.BindBuffer(GL_ARRAY_BUFFER, g._ecken);
            // Weisung P7-16: direkt aus dem Netz hochladen (keine Kopie als neues Feld)
            var ecken = netz.EckenFeld;
            fixed (float* zeiger = ecken)
            {
                gl.BufferData(GL_ARRAY_BUFFER, new IntPtr(ecken.Length * sizeof(float)), new IntPtr(zeiger), GL_STATIC_DRAW);
            }
            g._indizes = gl.GenBuffer();
            gl.BindBuffer(GL_ELEMENT_ARRAY_BUFFER, g._indizes);
            var indizes = netz.IndizesFeld;
            fixed (uint* zeiger = indizes)
            {
                gl.BufferData(GL_ELEMENT_ARRAY_BUFFER, new IntPtr(indizes.Length * sizeof(uint)), new IntPtr(zeiger), GL_STATIC_DRAW);
            }
            g.Attribute(gl);
            if (gl.IsGenVertexArraysAvailable)
            {
                gl.BindVertexArray(0);
            }
            return g;
        }

        private void Attribute(GlInterface gl)
        {
            var schritt = _nummern ? Lagen + sizeof(float) : Lagen;
            gl.VertexAttribPointer(0, 3, GL_FLOAT, 0, schritt, IntPtr.Zero);
            gl.VertexAttribPointer(1, 3, GL_FLOAT, 0, schritt, new IntPtr(3 * sizeof(float)));
            gl.VertexAttribPointer(2, 3, GL_FLOAT, 0, schritt, new IntPtr(6 * sizeof(float)));
            gl.EnableVertexAttribArray(0);
            gl.EnableVertexAttribArray(1);
            gl.EnableVertexAttribArray(2);
            if (_nummern)
            {
                gl.VertexAttribPointer(3, 1, GL_FLOAT, 0, schritt, new IntPtr(Netz.Zahlen * sizeof(float)));
                gl.EnableVertexAttribArray(3);
            }
            else if (_vao == 0)
            {
                // ohne Vertex-Array bliebe Attribut 3 vom Netz der Autos eingeschaltet (GlInterface kennt kein Abschalten)
                AttributAus(gl, 3);
            }
        }

        private static unsafe void AttributAus(GlInterface gl, uint stelle)
        {
            var f = gl.GetProcAddress("glDisableVertexAttribArray");
            if (f != IntPtr.Zero)
            {
                ((delegate* unmanaged[Stdcall]<uint, void>)f)(stelle);
            }
        }

        public void Binden(GlInterface gl)
        {
            if (_vao != 0)
            {
                gl.BindVertexArray(_vao);
                return;
            }
            gl.BindBuffer(GL_ARRAY_BUFFER, _ecken);
            gl.BindBuffer(GL_ELEMENT_ARRAY_BUFFER, _indizes);
            Attribute(gl);
        }

        public void Freigeben(GlInterface gl)
        {
            if (_vao != 0)
            {
                gl.DeleteVertexArray(_vao);
            }
            if (_ecken != 0)
            {
                gl.DeleteBuffer(_ecken);
            }
            if (_indizes != 0)
            {
                gl.DeleteBuffer(_indizes);
            }
            // Weisung P8-55 (W12): ein zweites Freigeben (Vorrat und Anzeige teilen Netze) tut nichts
            (_vao, _ecken, _indizes) = (0, 0, 0);
        }
    }
}
