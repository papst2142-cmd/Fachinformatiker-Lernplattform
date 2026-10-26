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
    private readonly Dictionary<InnenWand, GlNetz> _innenWaende = [];
    private readonly HashSet<InnenWand> _durchsichtig = [];
    private readonly HashSet<StadtHaus> _verdeckt = [];
    private readonly Dictionary<string, (PyObjekt? Aussehen, GlNetz Netz)> _figurZuletzt = [];
    private PyObjekt? _ringAussehen;
    private Innenraum? _innenGeladen;
    private bool _innenNeu;
    private GlNetz? _markierung;
    private string _markierungSchluessel = "";
    private GlNetz? _ausruf;
    private GlNetz? _marke;
    private GlNetz? _ring;
    private string _ringSchluessel = "";
    private int _programm;
    private int _ortMvp;
    private int _ortModell;
    private int _ortHell;
    private int _ortDurch;
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
                _innenNeu = true;
                InnenNeu();
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
        _figurZuletzt.Clear();
        _ringAussehen = null;
        AnfordernBild();
    }

    private void SzeneNeu()
    {
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
            BildGezeichnet -= Einmal;
            fertig.TrySetResult();
        }
        BildGezeichnet += Einmal;
        RequestNextFrameRendering();
        return fertig.Task;
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
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
            _marke = GlNetz.Laden(gl, ProbeWelt.Zielmarke());
            _ausruf = GlNetz.Laden(gl, Figur.Ausrufezeichen(0.7f));
            _szeneNeu = true;
            GlBereit = true;
        }
        catch (Exception e)
        {
            GlFehler = e.Message;
        }
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
        _innenWaende.Clear();
        _markierung = null;
        _markierungSchluessel = "";
        _ring = null;
        _ringAussehen = null;
        _ringSchluessel = "";
        _szeneNeu = true;
        _innenNeu = true;
        _programm = 0;
        base.OnOpenGlLost();
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        foreach (var netz in _flaechen.Values.Concat(_haeuser.Values).Concat(_figuren.Values).Concat(_innenWaende.Values).Append(_innen).Append(_markierung).Append(_marke).Append(_ausruf).Append(_ring))
        {
            netz?.Freigeben(gl);
        }
        _flaechen.Clear();
        _haeuser.Clear();
        _figuren.Clear();
        _figurZuletzt.Clear();
        _innen = null;
        _innenWaende.Clear();
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

    private static int Programm(GlInterface gl, bool es, GlVersion v)
    {
        var kopf = Kopf(es, v);
        const string ecke = """
            in vec3 aOrt;
            in vec3 aNormale;
            in vec3 aFarbe;
            uniform mat4 uMvp;
            uniform mat4 uModell;
            out vec3 vFarbe;
            out vec3 vNormale;
            void main() {
                gl_Position = uMvp * vec4(aOrt, 1.0);
                vNormale = (uModell * vec4(aNormale, 0.0)).xyz;
                vFarbe = aFarbe;
            }
            """;
        const string pixel = """
            in vec3 vFarbe;
            in vec3 vNormale;
            uniform float uHell;
            uniform float uDurch;
            out vec4 fFarbe;
            void main() {
                // halb durchsichtig ohne Mischen: jedes zweite Pixel faellt weg (Schachbrett)
                if (uDurch > 0.5 && mod(floor(gl_FragCoord.x) + floor(gl_FragCoord.y), 2.0) < 0.5) {
                    discard;
                }
                vec3 licht = normalize(vec3(0.35, 0.85, 0.45));
                float d = abs(dot(normalize(vNormale), licht));
                fFarbe = vec4(vFarbe * (0.48 + 0.52 * d) * uHell, 1.0);
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
        var bytesBeginn = GC.GetAllocatedBytesForCurrentThread();
        var abstandMs = _letztesBild < 0 ? 0 : (jetzt - _letztesBild) * 1000;
        var dt = _letztesBild < 0 ? 0 : Math.Min(jetzt - _letztesBild, 0.1);
        _aufrufe = 0;
        _dreiecke = 0;
        _letztesBild = jetzt;
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
            _flaechen[ProbeWelt.Stadt] = GlNetz.Laden(gl, spiel.Szene.NetzBauen());
            foreach (var haus in spiel.Szene.Haeuser)
            {
                _haeuser[haus] = GlNetz.Laden(gl, spiel.Szene.HausNetz(haus));
            }
            _szeneNeu = false;
        }
        if (_spiel is { } s2 && _innenNeu && GlBereit)
        {
            _innen?.Freigeben(gl);
            foreach (var alt in _innenWaende.Values)
            {
                alt.Freigeben(gl);
            }
            _innenWaende.Clear();
            // Nachtrag 1 zu P7-13 (Wunsch 3): Waende einzeln, jede kann halb durchsichtig werden
            _innen = s2.Innen is { } innen ? GlNetz.Laden(gl, innen.NetzOhneWaende) : null;
            foreach (var wand in s2.Innen?.Waende ?? [])
            {
                _innenWaende[wand] = GlNetz.Laden(gl, s2.Innen!.WandNetzFertig(wand));
            }
            _innenGeladen = s2.Innen;
            _innenNeu = false;
        }
        var nachLaden = _uhr.Elapsed.TotalSeconds;
        var geht = _spiel?.Fortschreiten(dt) ?? false;
        VorBild?.Invoke();
        var nachLogik = _uhr.Elapsed.TotalSeconds;
        var verdecktSek = 0.0;
        var kamera = AktuelleKamera;
        if (geht)
        {
            _nachlaufBis = jetzt + 1.5;
        }
        if ((geht || jetzt < _nachlaufBis) && kamera is not null && jetzt >= _folgenAb)
        {
            // Kamera folgt der Figur weich (nach der Ankunft noch kurz, bis sie mittig steht), solange
            // niemand selbst verschiebt
            var k = (float)Math.Min(1, dt * 2.5);
            kamera.Setzen(kamera.ZielX + ((_spiel!.FigurX - kamera.ZielX) * k), kamera.ZielZ + ((_spiel.FigurZ - kamera.ZielZ) * k));
        }
        var skalierung = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var breite = (int)Math.Max(1, Bounds.Width * skalierung);
        var hoehe = (int)Math.Max(1, Bounds.Height * skalierung);
        gl.Viewport(0, 0, breite, hoehe);
        var himmel = Himmel();
        gl.ClearColor(himmel.R / 255f, himmel.G / 255f, himmel.B / 255f, 1f);
        gl.ClearDepth(1f);
        gl.Enable(GL_DEPTH_TEST);
        gl.DepthFunc(GlLequal);
        gl.DepthMask(1);
        gl.Clear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
        var flaeche = _spiel is null ? null : _spiel.ImInnenraum ? _innen : _flaechen.GetValueOrDefault(ProbeWelt.Stadt);
        if (GlBereit && _spiel is not null && kamera is not null && flaeche is not null)
        {
            gl.UseProgram(_programm);
            gl.Uniform1f(_ortHell, IstDunkel() ? 0.72f : 1f);
            gl.Uniform1f(_ortDurch, 0f);
            var sichtProj = kamera.Sicht * kamera.Projektion(breite, hoehe);
            Zeichnen(gl, flaeche, Matrix4x4.Identity, sichtProj);
            if (_spiel.Ziel is { } ziel && _marke is not null)
            {
                Zeichnen(gl, _marke, Matrix4x4.CreateTranslation((float)ziel.X, _spiel.ImInnenraum ? 0.03f : StadtSzene.Hoehe.Uebergang + 0.012f, (float)ziel.Y), sichtProj);
            }
            var innen = _spiel.Innen;
            var groesse = innen is null ? Figur.StadtHoehe : Innenraum.FigurHoehe;
            var aussehen = _spiel.Ablauf.Stand.Profil is { } profil ? SpielDaten.Get(profil, "aussehen") as PyObjekt : null;
            var spieler = FigurNetz(gl, "#spieler", aussehen);
            var figurLage = Matrix4x4.CreateScale(groesse) * Matrix4x4.CreateRotationY(_spiel.FigurRichtung * MathF.PI / 180f)
                * Matrix4x4.CreateTranslation(_spiel.FigurX, innen is null ? StadtSzene.Hoehe.Figur : 0f, _spiel.FigurZ);
            Zeichnen(gl, spieler, figurLage, sichtProj);
            if (innen is not null)
            {
                foreach (var person in innen.Personen)
                {
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
        Messen(jetzt, abstandMs, nachLaden, nachLogik, verdecktSek, nachZeichnen, bytesBeginn);
        if (geht || Dauerlauf || jetzt < _nachlaufBis || jetzt < _weiterBis)
        {
            RequestNextFrameRendering();
        }
    }

    /// <summary>Teilzeiten des Bildes in <see cref="Zeiten"/> eintragen (Speicherbereinigung und Speicher seit dem letzten Bild).</summary>
    private void Messen(double start, double abstandMs, double nachLaden, double nachLogik, double verdecktSek, double nachZeichnen, long bytesBeginn)
    {
        var ende = _uhr.Elapsed.TotalSeconds;
        Span<int> gc = [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)];
        var bytes = GC.GetAllocatedBytesForCurrentThread();
        var teile = _spiel?.TeileMessen() ?? default;
        var bild = new BildZeiten.Bild(start, abstandMs, (nachLogik - nachLaden) * 1000, ((nachLaden - start) + verdecktSek) * 1000,
            (nachZeichnen - nachLogik - verdecktSek) * 1000, (ende - nachZeichnen) * 1000,
            gc[0] - _gcVorher[0], gc[1] - _gcVorher[1], gc[2] - _gcVorher[2], bytes - _bytesVorher, _spiel?.ImInnenraum ?? false,
            _endeVorher < 0 ? 0 : (start - _endeVorher) * 1000, _layouts, _layoutMs, _auftraegeMs + (_spiel?.AktionenMessen() ?? 0), bytes - bytesBeginn,
            teile.WegMs, teile.EreignisMs);
        if (Systemwerte.Seit is { } seit && BildZeiten.Dauer(bild) >= BildZeiten.LangsamAbMs)
        {
            // Weisung P7-16: Werte des Systems (Android) seit dem Ende des letzten Bildes, nur fuer langsame Bilder
            bild = bild with { System = seit(_endeVorherStempel) };
        }
        var aufnehmen = _bytesVorher >= 0;
        gc.CopyTo(_gcVorher);
        _bytesVorher = bytes;
        _layouts = 0;
        _layoutMs = 0;
        _auftraegeMs = 0;
        _endeVorher = ende;
        _endeVorherStempel = Stopwatch.GetTimestamp();
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

    /// <summary>Figur (Hoehe 1) mit Aussehen, je Person einmal geladen.</summary>
    private GlNetz FigurNetz(GlInterface gl, string id, PyObjekt? aussehen)
    {
        // Wunsch 4: dasselbe Aussehen wie im letzten Bild ohne neuen Schluessel (der kostete je Bild und
        // Figur ein neues Woerterbuch und Zeichenketten, also Arbeit fuer die Speicherbereinigung)
        if (_figurZuletzt.TryGetValue(id, out var zuletzt) && ReferenceEquals(zuletzt.Aussehen, aussehen))
        {
            return zuletzt.Netz;
        }
        var schluessel = id + "|" + string.Join(",", Aussehen.Normalisieren(aussehen).Select(p => p.Value));
        if (!_figuren.TryGetValue(schluessel, out var netz))
        {
            netz = GlNetz.Laden(gl, _spiel?.Innen?.PersonNetz(id, aussehen) ?? Figur.Netz(_spiel?.Grafik, aussehen, 1f));
            _figuren[schluessel] = netz;
        }
        _figurZuletzt[id] = (aussehen, netz);
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
        RequestNextFrameRendering();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var p = e.GetPosition(this);
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
        var (dx, dy) = taste switch
        {
            Key.Left => (TastenSchritt, 0f),
            Key.Right => (-TastenSchritt, 0f),
            Key.Up => (0f, TastenSchritt),
            Key.Down => (0f, -TastenSchritt),
            _ => (0f, 0f),
        };
        if (dx != 0 || dy != 0)
        {
            kamera.Verschieben(dx, dy, (float)Math.Max(1, Bounds.Height));
        }
        else if (taste == Key.Q)
        {
            kamera.Drehen(-15f);
        }
        else if (taste == Key.E)
        {
            kamera.Drehen(15f);
        }
        else if (taste is Key.OemPlus or Key.Add)
        {
            kamera.Zoomen(1.15f);
        }
        else if (taste is Key.OemMinus or Key.Subtract)
        {
            kamera.Zoomen(1 / 1.15f);
        }
        else if (taste == Key.Enter)
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

        public int Anzahl { get; private set; }

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
            gl.VertexAttribPointer(0, 3, GL_FLOAT, 0, Lagen, IntPtr.Zero);
            gl.VertexAttribPointer(1, 3, GL_FLOAT, 0, Lagen, new IntPtr(3 * sizeof(float)));
            gl.VertexAttribPointer(2, 3, GL_FLOAT, 0, Lagen, new IntPtr(6 * sizeof(float)));
            gl.EnableVertexAttribArray(0);
            gl.EnableVertexAttribArray(1);
            gl.EnableVertexAttribArray(2);
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
            gl.DeleteBuffer(_ecken);
            gl.DeleteBuffer(_indizes);
        }
    }
}
