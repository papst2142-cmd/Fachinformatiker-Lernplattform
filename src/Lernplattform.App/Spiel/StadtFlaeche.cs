using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Lernplattform.Core.Spiel.Probe;
using static Avalonia.OpenGL.GlConsts;

namespace Lernplattform.Spiel;

/// <summary>
/// Phase 7, W1 (Stichprobe, Plan Abschnitt 3 Technik A): 3D-Flaeche mit Avalonias eingebautem
/// <see cref="OpenGlControlBase"/> und eigener kleiner Darstellung (OpenGL ES 3.0 bzw. OpenGL 3.x,
/// Matrizen aus System.Numerics, keine Fremdbibliothek). Zeichnet die aktuelle Flaeche aus
/// <see cref="ProbeSpiel"/>, die Figur und die Zielmarke. Tippen/Klick geht hin, Ziehen
/// verschiebt, zwei Finger oder Mausrad zoomen, Pfeiltasten gehen, Eingabe betritt.
/// </summary>
public sealed class StadtFlaeche : OpenGlControlBase
{
    private const int Lagen = Netz.Zahlen * sizeof(float);
    // in GlConsts nicht enthalten (Werte aus der OpenGL-Spezifikation)
    private const int GlLequal = 0x0203;
    private const int GlUnsignedInt = 0x1405;
    private const double ZiehenAb = 8;

    private readonly ProbeSpiel _spiel;
    private readonly Dictionary<string, Kamera> _kameras = [];
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
    private double _folgenAb;

    public StadtFlaeche(ProbeSpiel spiel)
    {
        _spiel = spiel;
        Focusable = true;
        ClipToBounds = true;
        _kameras[ProbeWelt.Stadt] = new Kamera(spiel.FigurX + 5, spiel.FigurZ - 1, 17f)
        {
            Grenzen = (0, 0, ProbeWelt.StadtBreite, ProbeWelt.StadtTiefe),
        };
        _kameras[ProbeWelt.Buero] = new Kamera(spiel.Welt.BueroFlaeche.Breite / 2, spiel.Welt.BueroFlaeche.Tiefe / 2, 22f)
        {
            Neigung = 62f,
            Grenzen = (0, 0, spiel.Welt.BueroFlaeche.Breite, spiel.Welt.BueroFlaeche.Tiefe),
        };
        _spiel.Geaendert += AnfordernBild;
    }

    /// <summary>OpenGL steht (Pflicht-Messpunkt "GL-Zusammenhang erzeugt").</summary>
    public bool GlBereit { get; private set; }

    /// <summary>Fehler beim Einrichten (Shader), sonst null.</summary>
    public string? GlFehler { get; private set; }

    /// <summary>"OpenGL ES 3.0 · Renderer · Version" fuer Probe-Zeile und Messdatei.</summary>
    public string GlText { get; private set; } = "";

    public long BilderGesamt { get; private set; }

    /// <summary>Bilder in der letzten Sekunde.</summary>
    public int BilderJeSekunde => _bilder.Count;

    /// <summary>Wird nach jedem gezeichneten Bild gesetzt (Messung).</summary>
    public event Action? BildGezeichnet;

    public event Action? GlGestartet;

    public Kamera AktuelleKamera => _kameras[_spiel.Flaeche.Id];

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
        try
        {
            var v = GlVersion;
            var es = v.Type == GlProfileType.OpenGLES;
            GlText = (es ? "OpenGL ES " : "OpenGL ") + v.Major + "." + v.Minor + " · " + gl.Renderer + " · " + gl.Version;
            _programm = Programm(gl, es, v);
            _ortMvp = gl.GetUniformLocationString(_programm, "uMvp");
            _ortModell = gl.GetUniformLocationString(_programm, "uModell");
            _ortHell = gl.GetUniformLocationString(_programm, "uHell");
            _flaechen[ProbeWelt.Stadt] = GlNetz.Laden(gl, Netz.Aus(_spiel.Welt.StadtFlaeche.Teile));
            _flaechen[ProbeWelt.Buero] = GlNetz.Laden(gl, Netz.Aus(_spiel.Welt.BueroFlaeche.Teile));
            _figur = GlNetz.Laden(gl, ProbeWelt.Figur());
            _marke = GlNetz.Laden(gl, ProbeWelt.Zielmarke());
            GlBereit = true;
        }
        catch (Exception e)
        {
            GlFehler = e.Message;
        }
        GlGestartet?.Invoke();
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
        var geht = _spiel.Fortschreiten(dt);
        VorBild?.Invoke();
        var kamera = AktuelleKamera;
        if (geht && jetzt >= _folgenAb)
        {
            // Kamera folgt der Figur weich, solange niemand selbst verschiebt
            var k = (float)Math.Min(1, dt * 2.5);
            kamera.Setzen(kamera.ZielX + ((_spiel.FigurX - kamera.ZielX) * k), kamera.ZielZ + ((_spiel.FigurZ - kamera.ZielZ) * k));
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
        if (GlBereit && _flaechen.TryGetValue(_spiel.Flaeche.Id, out var flaeche))
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
        if (_zeiger.Count >= 2)
        {
            var neu = Abstand();
            if (_abstandZwei > 1 && neu > 1)
            {
                AktuelleKamera.Zoomen((float)(neu / _abstandZwei));
            }
            _abstandZwei = neu;
            AnfordernBild();
            return;
        }
        if (!_gezogen && Math.Abs(p.X - _druckStart.X) + Math.Abs(p.Y - _druckStart.Y) > ZiehenAb)
        {
            _gezogen = true;
        }
        if (_gezogen)
        {
            AktuelleKamera.Verschieben((float)(p.X - vorher.X), (float)(p.Y - vorher.Y), (float)Bounds.Height);
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
        AktuelleKamera.Zoomen(MathF.Pow(1.12f, (float)e.Delta.Y));
        AnfordernBild();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var (dx, dz) = e.Key switch
        {
            Key.Left => (-1, 0),
            Key.Right => (1, 0),
            Key.Up => (0, -1),
            Key.Down => (0, 1),
            _ => (0, 0),
        };
        if (dx != 0 || dz != 0)
        {
            _spiel.Schritt(dx, dz);
            _folgenAb = 0;
            AnfordernBild();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && _spiel.Betreten())
        {
            e.Handled = true;
        }
    }

    private double Abstand()
    {
        var p = _zeiger.Values.Take(2).ToArray();
        return p.Length < 2 ? 0 : Math.Sqrt(Math.Pow(p[0].X - p[1].X, 2) + Math.Pow(p[0].Y - p[1].Y, 2));
    }

    /// <summary>Tippen an (x, y) in Punkten: Haus in der Naehe -> zur Tuer, sonst zum Bodenpunkt.</summary>
    public void Tippen(Point p)
    {
        var boden = AktuelleKamera.Boden((float)p.X, (float)p.Y, (float)Bounds.Width, (float)Bounds.Height);
        if (boden is not { } b)
        {
            return;
        }
        _folgenAb = 0;
        if (_spiel.HausBei(b.X, b.Y) is { } haus)
        {
            _spiel.GeheZu(haus);
        }
        else
        {
            _spiel.GeheZu(b.X, b.Y);
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
