using Avalonia.OpenGL;
using Avalonia.Threading;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Spiel;

/// <summary>
/// Welle Grafik G1 (Weisung P12-182): die Grafikstufe beim Zeichnen. Werte der Stufe (<see cref="GrafikWerte"/>; ohne
/// Angabe Mittel = Stand vor G1, auch im Werkzeug), Bildaufloesung unter 100 % (die Stadt wird in einen eigenen
/// Bildspeicher gezeichnet und dann weich auf das ganze Bild gestreckt), Bildbremse (naechstes Bild erst nach
/// 1 / Bilder-hoechstens Sekunden) und die Messung der Belastung (<see cref="Belastung"/>, GPU-Zeit ueber die Zeitabfrage
/// der Grafik, ein bis zwei Bilder spaeter gelesen, wartet nie). Bei Mittel laeuft genau der Weg vor G1 (kein eigener
/// Bildspeicher, keine Bremse); die Zeitabfrage aendert kein Bild.
/// </summary>
public sealed partial class StadtFlaeche
{
    private const int GlFramebuffer = 0x8D40;
    private const int GlReadFramebuffer = 0x8CA8;
    private const int GlDrawFramebuffer = 0x8CA9;
    private const int GlRenderbuffer = 0x8D41;
    private const int GlRgba8 = 0x8058;
    private const int GlDepth24 = 0x81A6;
    private const int GlColorAttachment0 = 0x8CE0;
    private const int GlDepthAttachment = 0x8D00;
    private const int GlFramebufferComplete = 0x8CD5;
    private const int GlLinear = 0x2601;
    private const int GlTimeElapsed = 0x88BF;
    private const int GlQueryResult = 0x8866;
    private const int GlQueryResultAvailable = 0x8867;
    private const int GlGpuDisjoint = 0x8FBB;
    private const int GlExtensions = 0x1F03;

    private volatile Grafikwerte _grafikWunsch = Grafikstufen.Mittel;

    /// <summary>Werte des laufenden Bilds (zu Beginn jedes Bilds aus <see cref="GrafikWerte"/> uebernommen).</summary>
    private Grafikwerte _gw = Grafikstufen.Mittel;

    /// <summary>Werte der Grafikstufe; ohne Angabe Mittel (Werkzeug, Tests). Wirkt ab dem naechsten Bild.</summary>
    public Grafikwerte GrafikWerte
    {
        get => _grafikWunsch;
        set
        {
            if (_grafikWunsch == value)
            {
                return;
            }
            _grafikWunsch = value;
            Anfordern();
        }
    }

    /// <summary>Messung der Belastung (Mittel der letzten Sekunden; <see cref="Belastung.Letzte"/> fuer die Optionen-Karte).</summary>
    public Belastung Belastung { get; } = new();

    /// <summary>Name der Grafik laut Treiber (fuer die automatische Wahl der Stufe), leer vor dem ersten Start.</summary>
    public string GlRenderer { get; private set; } = "";

    /// <summary>Der Treiber kann einen eigenen Bildspeicher und das Strecken (Aufloesung unter 100 %).</summary>
    public bool AufloesungDa { get; private set; }

    /// <summary>Der Treiber kann die Zeitabfrage der GPU (sonst GPU-Zeit „nicht verfuegbar“).</summary>
    public bool ZeitabfrageDa { get; private set; }

    /// <summary>Breite und Hoehe des letzten gezeichneten 3D-Bilds in Bildpunkten (Start-Test: Aufloesung).</summary>
    public (int Breite, int Hoehe) LetzteBildgroesse { get; private set; }

    private bool _autosHalbGeladen;

    // eigener Bildspeicher
    private IntPtr _glGenFb, _glBindFb, _glGenRb, _glBindRb, _glRbStorage, _glFbRb, _glFbStatus, _glBlit, _glDelFb, _glDelRb;
    private int _bildFb, _bildFarbe, _bildTiefe, _bildB, _bildH;

    // Zeitabfrage
    private IntPtr _glGenQ, _glBeginQ, _glEndQ, _glQObjiv, _glQObjui64v, _glGetIntegerv;
    private bool _abfrageEs;
    private readonly int[] _abfragen = new int[3];
    private readonly bool[] _abfrageLaeuft = new bool[3];
    private int _abfrageNaechste;
    private int _abfrageAktiv = -1;

    private int _bremseWartet;

    /// <summary>Beginn des letzten gezeichneten Bilds (Sekunden auf <c>_uhr</c>) fuer die Bildbremse.</summary>
    private double _bremseBildBeginn = double.NegativeInfinity;

    private unsafe void GrafikEinrichten(GlInterface gl, bool es, GlVersion v)
    {
        GlRenderer = gl.Renderer ?? "";
        var ab3 = v.Major >= 3;
        _glGenFb = GlFunktion(gl, "glGenFramebuffers");
        _glBindFb = GlFunktion(gl, "glBindFramebuffer");
        _glGenRb = GlFunktion(gl, "glGenRenderbuffers");
        _glBindRb = GlFunktion(gl, "glBindRenderbuffer");
        _glRbStorage = GlFunktion(gl, "glRenderbufferStorage");
        _glFbRb = GlFunktion(gl, "glFramebufferRenderbuffer");
        _glFbStatus = GlFunktion(gl, "glCheckFramebufferStatus");
        _glBlit = GlFunktion(gl, "glBlitFramebuffer");
        _glDelFb = GlFunktion(gl, "glDeleteFramebuffers");
        _glDelRb = GlFunktion(gl, "glDeleteRenderbuffers");
        AufloesungDa = ab3 && _glGenFb != 0 && _glBindFb != 0 && _glGenRb != 0 && _glBindRb != 0 && _glRbStorage != 0
            && _glFbRb != 0 && _glFbStatus != 0 && _glBlit != 0 && _glDelFb != 0 && _glDelRb != 0;
        _bildFb = _bildFarbe = _bildTiefe = _bildB = _bildH = 0;
        // Zeitabfrage: OpenGL ab 3.3 im Kern, OpenGL ES nur mit GL_EXT_disjoint_timer_query
        _abfrageEs = es;
        _glGetIntegerv = GlFunktion(gl, "glGetIntegerv");
        if (es)
        {
            var erweiterungen = "";
            var getString = GlFunktion(gl, "glGetString");
            if (getString != 0)
            {
                var p = ((delegate* unmanaged[Stdcall]<int, IntPtr>)getString)(GlExtensions);
                erweiterungen = p == 0 ? "" : System.Runtime.InteropServices.Marshal.PtrToStringAnsi(p) ?? "";
            }
            var da = erweiterungen.Contains("GL_EXT_disjoint_timer_query", StringComparison.Ordinal);
            _glGenQ = da ? GlFunktion(gl, "glGenQueriesEXT", "glGenQueries") : 0;
            _glBeginQ = da ? GlFunktion(gl, "glBeginQueryEXT", "glBeginQuery") : 0;
            _glEndQ = da ? GlFunktion(gl, "glEndQueryEXT", "glEndQuery") : 0;
            _glQObjiv = da ? GlFunktion(gl, "glGetQueryObjectuivEXT", "glGetQueryObjectuiv") : 0;
            _glQObjui64v = da ? GlFunktion(gl, "glGetQueryObjectui64vEXT") : 0;
        }
        else
        {
            var da = v.Major > 3 || (v.Major == 3 && v.Minor >= 3);
            _glGenQ = da ? GlFunktion(gl, "glGenQueries") : 0;
            _glBeginQ = da ? GlFunktion(gl, "glBeginQuery") : 0;
            _glEndQ = da ? GlFunktion(gl, "glEndQuery") : 0;
            _glQObjiv = da ? GlFunktion(gl, "glGetQueryObjectiv") : 0;
            _glQObjui64v = da ? GlFunktion(gl, "glGetQueryObjectui64v") : 0;
        }
        ZeitabfrageDa = _glGenQ != 0 && _glBeginQ != 0 && _glEndQ != 0 && _glQObjiv != 0 && _glQObjui64v != 0;
        if (ZeitabfrageDa)
        {
            fixed (int* ids = _abfragen)
            {
                ((delegate* unmanaged[Stdcall]<int, int*, void>)_glGenQ)(_abfragen.Length, ids);
            }
            Array.Clear(_abfrageLaeuft);
            _abfrageAktiv = -1;
            _abfrageNaechste = 0;
        }
    }

    /// <summary>Eigenen Bildspeicher und Abfragen freigeben (Zusammenhang endet oder geht verloren).</summary>
    private unsafe void GrafikFrei(GlInterface? gl)
    {
        if (gl is not null && _bildFb != 0)
        {
            BildspeicherFrei();
        }
        _bildFb = _bildFarbe = _bildTiefe = _bildB = _bildH = 0;
        ZeitabfrageDa = false;
        AufloesungDa = false;
        _abfrageAktiv = -1;
        Array.Clear(_abfrageLaeuft);
    }

    private unsafe void BildspeicherFrei()
    {
        var fb = _bildFb;
        var rb = stackalloc int[2] { _bildFarbe, _bildTiefe };
        ((delegate* unmanaged[Stdcall]<int, int*, void>)_glDelFb)(1, &fb);
        ((delegate* unmanaged[Stdcall]<int, int*, void>)_glDelRb)(2, rb);
        _bildFb = _bildFarbe = _bildTiefe = 0;
    }

    /// <summary>
    /// Zu Beginn eines Bilds: Werte der Stufe uebernehmen; bei Aufloesung unter 100 % den eigenen Bildspeicher binden.
    /// Rueckgabe: Groesse, in die gezeichnet wird (bei 100 % genau die des Bilds).
    /// </summary>
    private unsafe (int Breite, int Hoehe) GrafikBildBeginnen(GlInterface gl, int breite, int hoehe)
    {
        var prozent = _gw.Aufloesung;
        if (prozent >= 100 || !AufloesungDa || _spiel is null)
        {
            LetzteBildgroesse = (breite, hoehe);
            return (breite, hoehe);
        }
        var b = Math.Max(1, (breite * prozent) / 100);
        var h = Math.Max(1, (hoehe * prozent) / 100);
        if (_bildFb == 0 || _bildB != b || _bildH != h)
        {
            if (_bildFb != 0)
            {
                BildspeicherFrei();
            }
            int fb, farbe, tiefe;
            ((delegate* unmanaged[Stdcall]<int, int*, void>)_glGenFb)(1, &fb);
            ((delegate* unmanaged[Stdcall]<int, int*, void>)_glGenRb)(1, &farbe);
            ((delegate* unmanaged[Stdcall]<int, int*, void>)_glGenRb)(1, &tiefe);
            var bindRb = (delegate* unmanaged[Stdcall]<int, int, void>)_glBindRb;
            var speicher = (delegate* unmanaged[Stdcall]<int, int, int, int, void>)_glRbStorage;
            bindRb(GlRenderbuffer, farbe);
            speicher(GlRenderbuffer, GlRgba8, b, h);
            bindRb(GlRenderbuffer, tiefe);
            speicher(GlRenderbuffer, GlDepth24, b, h);
            bindRb(GlRenderbuffer, 0);
            ((delegate* unmanaged[Stdcall]<int, int, void>)_glBindFb)(GlFramebuffer, fb);
            var anhaengen = (delegate* unmanaged[Stdcall]<int, int, int, int, void>)_glFbRb;
            anhaengen(GlFramebuffer, GlColorAttachment0, GlRenderbuffer, farbe);
            anhaengen(GlFramebuffer, GlDepthAttachment, GlRenderbuffer, tiefe);
            var status = ((delegate* unmanaged[Stdcall]<int, int>)_glFbStatus)(GlFramebuffer);
            (_bildFb, _bildFarbe, _bildTiefe, _bildB, _bildH) = (fb, farbe, tiefe, b, h);
            if (status != GlFramebufferComplete)
            {
                // der Treiber kann diesen Bildspeicher nicht: ab jetzt volle Aufloesung
                ((delegate* unmanaged[Stdcall]<int, int, void>)_glBindFb)(GlFramebuffer, _zielFb);
                BildspeicherFrei();
                AufloesungDa = false;
                LetzteBildgroesse = (breite, hoehe);
                return (breite, hoehe);
            }
        }
        ((delegate* unmanaged[Stdcall]<int, int, void>)_glBindFb)(GlFramebuffer, _bildFb);
        LetzteBildgroesse = (b, h);
        return (b, h);
    }

    /// <summary>Ziel-Bildspeicher von Avalonia fuer dieses Bild.</summary>
    private int _zielFb;

    /// <summary>Am Ende eines Bilds: den eigenen Bildspeicher weich auf das ganze Bild strecken.</summary>
    private unsafe void GrafikBildEnde(int breite, int hoehe)
    {
        if (LetzteBildgroesse == (breite, hoehe) || _bildFb == 0)
        {
            return;
        }
        var binden = (delegate* unmanaged[Stdcall]<int, int, void>)_glBindFb;
        binden(GlReadFramebuffer, _bildFb);
        binden(GlDrawFramebuffer, _zielFb);
        ((delegate* unmanaged[Stdcall]<int, int, int, int, int, int, int, int, int, int, void>)_glBlit)(
            0, 0, _bildB, _bildH, 0, 0, breite, hoehe, 0x4000, GlLinear);
        binden(GlFramebuffer, _zielFb);
    }

    /// <summary>Zeitabfrage: fertige Ergebnisse lesen (wartet nie), dann eine neue beginnen, wenn eine frei ist.</summary>
    private unsafe void ZeitabfrageBeginnen()
    {
        if (!ZeitabfrageDa)
        {
            return;
        }
        var lesenIv = (delegate* unmanaged[Stdcall]<int, int, int*, void>)_glQObjiv;
        var lesen64 = (delegate* unmanaged[Stdcall]<int, int, ulong*, void>)_glQObjui64v;
        var gestoert = false;
        if (_abfrageEs && _glGetIntegerv != 0)
        {
            int d;
            ((delegate* unmanaged[Stdcall]<int, int*, void>)_glGetIntegerv)(GlGpuDisjoint, &d);
            gestoert = d != 0;
        }
        for (var i = 0; i < _abfragen.Length; i++)
        {
            if (!_abfrageLaeuft[i] || i == _abfrageAktiv)
            {
                continue;
            }
            int fertig;
            lesenIv(_abfragen[i], GlQueryResultAvailable, &fertig);
            if (fertig == 0)
            {
                continue;
            }
            ulong ns;
            lesen64(_abfragen[i], GlQueryResult, &ns);
            _abfrageLaeuft[i] = false;
            if (!gestoert)
            {
                Belastung.GpuZeit(ns / 1_000_000.0);
            }
        }
        if (!_abfrageLaeuft[_abfrageNaechste])
        {
            ((delegate* unmanaged[Stdcall]<int, int, void>)_glBeginQ)(GlTimeElapsed, _abfragen[_abfrageNaechste]);
            _abfrageAktiv = _abfrageNaechste;
            _abfrageLaeuft[_abfrageNaechste] = true;
            _abfrageNaechste = (_abfrageNaechste + 1) % _abfragen.Length;
        }
    }

    private unsafe void ZeitabfrageEnde()
    {
        if (_abfrageAktiv < 0 || !ZeitabfrageDa)
        {
            return;
        }
        ((delegate* unmanaged[Stdcall]<int, void>)_glEndQ)(GlTimeElapsed);
        _abfrageAktiv = -1;
    }

    /// <summary>
    /// Naechstes Bild anfordern; mit Bildbremse (Bilder je Sekunde hoechstens) erst, wenn seit dem Beginn dieses Bilds
    /// volle 1 / Bilder Sekunden vergangen sind (nie frueher, auch nicht um wenige Millisekunden: sonst kamen bei 30
    /// rund 32 bis 33 Bilder in einer Sekunde, PR-Lauf 829). Ohne Bremse wie vor G1 sofort.
    /// </summary>
    private void NaechstesBildAnfordern(double bildBeginn)
    {
        _bremseBildBeginn = bildBeginn;
        GebremstAnfordern();
    }

    /// <summary>
    /// Jede Anforderung eines Bilds bei Bildbremse (auch aus <see cref="Anfordern"/>): vor Ablauf von 1 / Bilder Sekunden
    /// seit dem Beginn des letzten Bilds wird gewartet, eine wartende Anforderung genuegt. Ohne Bremse sofort.
    /// </summary>
    private void GebremstAnfordern()
    {
        var bilder = _gw.BilderHoechstens;
        if (bilder <= 0)
        {
            RequestNextFrameRendering();
            return;
        }
        var warten = _bremseBildBeginn + (1.0 / bilder) - _uhr.Elapsed.TotalSeconds;
        if (warten <= 0)
        {
            RequestNextFrameRendering();
            return;
        }
        if (Interlocked.Exchange(ref _bremseWartet, 1) == 1)
        {
            return;
        }
        _ = Task.Delay(TimeSpan.FromSeconds(warten)).ContinueWith(_ => Dispatcher.UIThread.Post(() =>
        {
            Interlocked.Exchange(ref _bremseWartet, 0);
            GebremstAnfordern();
        }), TaskScheduler.Default);
    }

    /// <summary>
    /// Zu Beginn eines Bilds: Werte der Stufe uebernehmen; aendert sich „Autos halb“, laufender Uebergang sofort fertig und
    /// die Netze der Autos aus dem laufenden Verkehr neu (die Autos fahren weiter, nur die Zahl der gezeichneten aendert sich).
    /// </summary>
    private void GrafikWerteUebernehmen(GlInterface gl)
    {
        _gw = _grafikWunsch;
        if (_autosHalbGeladen == _gw.AutosHalb)
        {
            return;
        }
        _autosHalbGeladen = _gw.AutosHalb;
        if (_uebergang is { } u)
        {
            u.Abschliessen();
            UebergangAbschliessen(gl);
        }
        AutoNetzeNeu(gl);
    }

    /// <summary>Netze der Autos bzw. Gestalten aus dem laufenden Verkehr mit „Autos halb“ der Stufe neu hochladen.</summary>
    private void AutoNetzeNeu(GlInterface gl)
    {
        foreach (var alt in _autoNetze.Values.Concat(_neuNetze.Values))
        {
            alt.Freigeben(gl);
        }
        _autoNetze.Clear();
        _neuNetze.Clear();
        if (_verkehr is { } verkehr && _spiel?.Grafik is { } grafik)
        {
            _autoNetze[false] = GlNetz.LadenMitNummern(gl, AutoNetze(verkehr, _gestalten, grafik, false, _gw.AutosHalb));
            _autoNetze[true] = GlNetz.LadenMitNummern(gl, AutoNetze(verkehr, _gestalten, grafik, true, _gw.AutosHalb));
        }
    }
}
