using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Rendering;
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
    private GlNetz? _figur;
    private GlNetz? _marke;
    private int _programm;
    private int _ortMvp;
    private int _ortModell;
    private int _ortHell;
    private readonly Stopwatch _uhr = Stopwatch.StartNew();
    private double _letztesBild = -1;
    private readonly Queue<double> _bilder = new();
    private readonly Dictionary<IPointer, Point> _zeiger = [];
    private Point _druckStart;
    private bool _gezogen;
    private double _abstandZwei;
    private double _winkelZwei;
    private double _folgenAb;

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
                _spiel.Geaendert -= AnfordernBild;
                _spiel.SzeneGeaendert -= SzeneNeu;
            }
            _spiel = value;
            _kameras.Clear();
            if (value is not null)
            {
                var b = value.Buero;
                _kameras[ProbeWelt.Stadt] = new Kamera(value.FigurX, value.FigurZ - 1, 17f)
                {
                    Grenzen = (0, 0, value.Szene.Breite, value.Szene.Tiefe),
                };
                _kameras[ProbeWelt.Buero] = new Kamera(b.Breite / 2, b.Tiefe / 2, 22f)
                {
                    Neigung = 62f,
                    Grenzen = (0, 0, b.Breite, b.Tiefe),
                };
                value.Geaendert += AnfordernBild;
                value.SzeneGeaendert += SzeneNeu;
                _szeneNeu = true;
            }
            AnfordernBild();
        }
    }

    private void SzeneNeu()
    {
        _szeneNeu = true;
        AnfordernBild();
    }

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
    public Kamera? AktuelleKamera => _spiel is null ? null : _kameras[_spiel.FlaecheId];

    /// <summary>Messung: laeuft, solange gesetzt, ohne Pause weiter (Kameraschwenk).</summary>
    public bool Dauerlauf { get; set; }

    /// <summary>Start-Test: wird je Bild vor dem Zeichnen gerufen (z. B. Kamera ein Stueck drehen).</summary>
    public Action? VorBild { get; set; }

    public void AnfordernBild() => RequestNextFrameRendering();

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
            _programm = Programm(gl, es, v);
            _ortMvp = gl.GetUniformLocationString(_programm, "uMvp");
            _ortModell = gl.GetUniformLocationString(_programm, "uModell");
            _ortHell = gl.GetUniformLocationString(_programm, "uHell");
            _figur = GlNetz.Laden(gl, ProbeWelt.Figur());
            _marke = GlNetz.Laden(gl, ProbeWelt.Zielmarke());
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
        _szeneNeu = true;
        _programm = 0;
        base.OnOpenGlLost();
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        foreach (var netz in _flaechen.Values.Append(_figur).Append(_marke))
        {
            netz?.Freigeben(gl);
        }
        _flaechen.Clear();
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
            out vec4 fFarbe;
            void main() {
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
        var dt = _letztesBild < 0 ? 0 : Math.Min(jetzt - _letztesBild, 0.1);
        _letztesBild = jetzt;
        if (_spiel is { } spiel && _szeneNeu && GlBereit)
        {
            // Stadt (und Buero) in die Grafikkarte laden: beim Start und wenn sich die sichtbaren Orte aendern
            foreach (var alt in _flaechen.Values)
            {
                alt.Freigeben(gl);
            }
            _flaechen.Clear();
            _flaechen[ProbeWelt.Stadt] = GlNetz.Laden(gl, Netz.Aus(spiel.Szene.Teile));
            _flaechen[ProbeWelt.Buero] = GlNetz.Laden(gl, Netz.Aus(spiel.Buero.Teile));
            _szeneNeu = false;
        }
        var geht = _spiel?.Fortschreiten(dt) ?? false;
        VorBild?.Invoke();
        var kamera = AktuelleKamera;
        if (geht && kamera is not null && jetzt >= _folgenAb)
        {
            // Kamera folgt der Figur weich, solange niemand selbst verschiebt
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
        if (GlBereit && _spiel is not null && kamera is not null && _flaechen.TryGetValue(_spiel.FlaecheId, out var flaeche))
        {
            gl.UseProgram(_programm);
            gl.Uniform1f(_ortHell, IstDunkel() ? 0.72f : 1f);
            var sichtProj = kamera.Sicht * kamera.Projektion(breite, hoehe);
            Zeichnen(gl, flaeche, Matrix4x4.Identity, sichtProj);
            if (_spiel.Ziel is { } ziel && _marke is not null)
            {
                Zeichnen(gl, _marke, Matrix4x4.CreateTranslation((float)ziel.X, 0, (float)ziel.Y), sichtProj);
            }
            if (_figur is not null)
            {
                var modell = Matrix4x4.CreateRotationY(_spiel.FigurRichtung * MathF.PI / 180f)
                    * Matrix4x4.CreateTranslation(_spiel.FigurX, 0, _spiel.FigurZ);
                Zeichnen(gl, _figur, modell, sichtProj);
            }
            if (gl.IsBindVertexArrayAvailable)
            {
                gl.BindVertexArray(0);
            }
        }
        gl.Disable(GL_DEPTH_TEST);
        BilderGesamt++;
        _bilder.Enqueue(jetzt);
        while (_bilder.Count > 0 && _bilder.Peek() < jetzt - 1)
        {
            _bilder.Dequeue();
        }
        BildGezeichnet?.Invoke();
        if (geht || Dauerlauf)
        {
            RequestNextFrameRendering();
        }
    }

    private unsafe void Zeichnen(GlInterface gl, GlNetz netz, Matrix4x4 modell, Matrix4x4 sichtProj)
    {
        var mvp = modell * sichtProj;
        gl.UniformMatrix4fv(_ortMvp, 1, false, &mvp);
        gl.UniformMatrix4fv(_ortModell, 1, false, &modell);
        netz.Binden(gl);
        gl.DrawElements(GL_TRIANGLES, netz.Anzahl, GlUnsignedInt, IntPtr.Zero);
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

    /// <summary>Tippen an (x, y) in Punkten: Stadt -> Haus unter dem Finger, Buero -> zum Bodenpunkt.</summary>
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
            var ecken = netz.Ecken.ToArray();
            fixed (float* zeiger = ecken)
            {
                gl.BufferData(GL_ARRAY_BUFFER, new IntPtr(ecken.Length * sizeof(float)), new IntPtr(zeiger), GL_STATIC_DRAW);
            }
            g._indizes = gl.GenBuffer();
            gl.BindBuffer(GL_ELEMENT_ARRAY_BUFFER, g._indizes);
            var indizes = netz.Indizes.ToArray();
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
