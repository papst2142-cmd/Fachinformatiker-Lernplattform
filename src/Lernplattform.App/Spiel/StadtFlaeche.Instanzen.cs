using System.Numerics;
using Avalonia.OpenGL;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Avalonia.OpenGL.GlConsts;

namespace Lernplattform.Spiel;

/// <summary>
/// Welle D2 (Weisung P12-157, Nico 1a): Instancing. Jeder <see cref="InstanzSatz"/> ist ein Netz im Raum des Modells plus
/// eine Liste von Lagen; je Bild kommen nur die Lagen der Stuecke im Sichtkegel in den Puffer, gezeichnet wird mit EINEM
/// Aufruf (glDrawElementsInstanced, Lage als Attribut 4-7 je Stueck). Fehlt dem Treiber das (OpenGL unter 3.3 ohne
/// Erweiterung), zeichnet die Flaeche jedes Stueck einzeln mit dem Programm der Szene (gleiches Bild, mehr Aufrufe).
/// </summary>
public sealed partial class StadtFlaeche
{
    private const int GlDynamicDraw = 0x88E8;

    /// <summary>Ein hochgeladener Satz: Netz nah, fern (oder null), Puffer fuer die Lagen.</summary>
    private sealed record GlSatz(InstanzSatz Satz, GlNetz Nah, GlNetz? Fern, int LagenNah, int LagenFern);

    private readonly List<GlSatz> _instanzen = [];
    private Matrix4x4[] _instanzLagen = [];
    private int _instanzProgramm;
    private int _instanzOrtSichtProj;
    private int _instanzOrtHell;
    private int _instanzOrtDurch;
    private int[] _instanzLichtOrte = [];
    private int[] _instanzDunstOrte = [];
    private IntPtr _glTeiler;
    private IntPtr _glInstanziert;

    /// <summary>Gegenprobe (Start-Test): Instancing der Grafikkarte nicht nutzen, jedes Stueck einzeln zeichnen.</summary>
    public static bool InstanzenEinzelnFuerTests { get; set; }

    /// <summary>Hat der Treiber Instancing (glVertexAttribDivisor und glDrawElementsInstanced, Vertex-Arrays)?</summary>
    public bool InstancingDa { get; private set; }

    private const string InstanzEcke = """
        in vec3 aOrt;
        in vec3 aNormale;
        in vec3 aFarbe;
        in vec4 aLage0;
        in vec4 aLage1;
        in vec4 aLage2;
        in vec4 aLage3;
        uniform mat4 uSichtProj;
        out vec3 vFarbe;
        out vec3 vNormale;
        out vec3 vWelt;
        void main() {
            mat4 lage = mat4(aLage0, aLage1, aLage2, aLage3);
            vWelt = (lage * vec4(aOrt, 1.0)).xyz;
            gl_Position = uSichtProj * vec4(vWelt, 1.0);
            vNormale = (lage * vec4(aNormale, 0.0)).xyz;
            vFarbe = aFarbe;
        }
        """;

    private static IntPtr GlFunktion(GlInterface gl, params string[] namen)
    {
        foreach (var n in namen)
        {
            var f = gl.GetProcAddress(n);
            if (f != IntPtr.Zero)
            {
                return f;
            }
        }
        return IntPtr.Zero;
    }

    /// <summary>Beim Einrichten: Programm fuer Saetze und die beiden Funktionen des Treibers.</summary>
    private void InstanzenEinrichten(GlInterface gl, bool es, GlVersion v)
    {
        _glTeiler = GlFunktion(gl, "glVertexAttribDivisor", "glVertexAttribDivisorARB", "glVertexAttribDivisorEXT");
        _glInstanziert = GlFunktion(gl, "glDrawElementsInstanced", "glDrawElementsInstancedARB", "glDrawElementsInstancedEXT");
        InstancingDa = _glTeiler != IntPtr.Zero && _glInstanziert != IntPtr.Zero && gl.IsGenVertexArraysAvailable;
        if (!InstancingDa)
        {
            return;
        }
        _instanzProgramm = Programm(gl, es, v, instanzen: true);
        _instanzOrtSichtProj = gl.GetUniformLocationString(_instanzProgramm, "uSichtProj");
        _instanzOrtHell = gl.GetUniformLocationString(_instanzProgramm, "uHell");
        _instanzOrtDurch = gl.GetUniformLocationString(_instanzProgramm, "uDurch");
        _instanzLichtOrte = [.. new[] { "uTag", "uTonR", "uTonG", "uTonB", "uNacht", "uAugeX", "uAugeY", "uAugeZ" }.Select(n => gl.GetUniformLocationString(_instanzProgramm, n))];
        _instanzDunstOrte = DunstOrte(gl, _instanzProgramm);
    }

    /// <summary>Saetze hochladen (alte vorher frei).</summary>
    private void InstanzenLaden(GlInterface gl, Instanzplan? plan)
    {
        InstanzenFrei(gl);
        foreach (var s in plan?.Saetze ?? [])
        {
            var (nah, pn) = SatzNetz(gl, s.Nah);
            var (fern, pf) = s.Fern is { } f ? SatzNetz(gl, f) : (null, 0);
            _instanzen.Add(new GlSatz(s, nah, fern, pn, pf));
            if (_instanzLagen.Length < s.Lagen.Length)
            {
                _instanzLagen = new Matrix4x4[s.Lagen.Length];
            }
        }
    }

    private void InstanzenFrei(GlInterface gl)
    {
        foreach (var s in _instanzen)
        {
            s.Nah.Freigeben(gl);
            s.Fern?.Freigeben(gl);
            foreach (var p in new[] { s.LagenNah, s.LagenFern })
            {
                if (p != 0)
                {
                    gl.DeleteBuffer(p);
                }
            }
        }
        _instanzen.Clear();
    }

    /// <summary>
    /// Welle A 0.92.0: die ersten <paramref name="zahl"/> Lagen ordnen: zuerst die Stuecke bis <see cref="IchSicht.KachelnVollBis"/>
    /// vom Auge (waagrecht), dann die bis <see cref="IchSicht.Sichtweite"/>; weiter weg (im vollen Dunst) faellt weg.
    /// Rueckgabe = (Zahl der nahen, Zahl der nahen und mittleren).
    /// </summary>
    private (int Nah, int Sicht) IchNahZuerst(int zahl, Vector2 auge)
    {
        var nah = 0;
        for (var i = 0; i < zahl; i++)
        {
            var t = _instanzLagen[i].Translation;
            if (Vector2.Distance(new Vector2(t.X, t.Z), auge) <= IchSicht.KachelnVollBis)
            {
                (_instanzLagen[nah], _instanzLagen[i]) = (_instanzLagen[i], _instanzLagen[nah]);
                nah++;
            }
        }
        var sicht = nah;
        for (var i = nah; i < zahl; i++)
        {
            var t = _instanzLagen[i].Translation;
            if (Vector2.Distance(new Vector2(t.X, t.Z), auge) <= IchSicht.Sichtweite)
            {
                (_instanzLagen[sicht], _instanzLagen[i]) = (_instanzLagen[i], _instanzLagen[sicht]);
                sicht++;
            }
        }
        return (nah, sicht);
    }

    /// <summary>Netz eines Satzes; mit Instancing im eigenen Vertex-Array mit Puffer fuer die Lagen (Attribute 4-7, eins je Stueck).</summary>
    private unsafe (GlNetz Netz, int Lagen) SatzNetz(GlInterface gl, Netz netz)
    {
        var g = GlNetz.Laden(gl, netz);
        if (!InstancingDa || InstanzenEinzelnFuerTests)
        {
            return (g, 0);
        }
        g.Binden(gl);
        var puffer = gl.GenBuffer();
        gl.BindBuffer(GL_ARRAY_BUFFER, puffer);
        gl.BufferData(GL_ARRAY_BUFFER, new IntPtr(64), IntPtr.Zero, GlDynamicDraw);
        var teiler = (delegate* unmanaged[Stdcall]<uint, uint, void>)_glTeiler;
        for (var k = 0; k < 4; k++)
        {
            gl.VertexAttribPointer(4 + k, 4, GL_FLOAT, 0, 64, new IntPtr(k * 16));
            gl.EnableVertexAttribArray(4 + k);
            teiler((uint)(4 + k), 1);
        }
        gl.BindVertexArray(0);
        return (g, puffer);
    }

    /// <summary>
    /// Alle Saetze dieses Bilds: Stadt immer, Kleingruen nur bis <see cref="GruenWeite"/>; fern die Fernform. Jeder Satz mit
    /// Stuecken im Bild ist ein Aufruf und zaehlt Netz mal Stuecke Dreiecke.
    /// </summary>
    private unsafe void InstanzenZeichnen(GlInterface gl, Matrix4x4 sichtProj, bool fernBild, float abstand, Tageslicht.Licht licht, Vector3 auge)
    {
        if (_instanzen.Count == 0)
        {
            return;
        }
        var mitProgramm = false;
        foreach (var s in _instanzen)
        {
            if (s.Satz.Ziel == Instanzen.Gruen && !_gw.Gruen(abstand) && _ichFern is null)
            {
                continue;
            }
            var zahl = s.Satz.ImBild(sichtProj, _instanzLagen, alle: OhneSparenFuerTests);
            if (zahl == 0)
            {
                continue;
            }
            var nahZahl = zahl;
            if (_ichFern is { } ich && !OhneSparenFuerTests)
            {
                // Welle A 0.92.0: in der Ich-Sicht (Blick waagrecht weit) nur Stuecke nahe am Auge in voller Form; weiter weg
                // die Fernform, hinter der Sichtweite (voller Dunst) nichts; Kleingruen nur nah (wie sonst bis GruenBis).
                (nahZahl, zahl) = IchNahZuerst(zahl, ich);
                if (s.Satz.Ziel == Instanzen.Gruen)
                {
                    zahl = nahZahl;
                }
                else if (s.Fern is null)
                {
                    nahZahl = zahl;
                }
            }
            var fernAlle = fernBild && s.Fern is not null;
            if (fernAlle)
            {
                nahZahl = 0;
            }
            foreach (var (fern, von, anzahl) in new[] { (false, 0, nahZahl), (true, nahZahl, zahl - nahZahl) })
            {
                if (anzahl <= 0)
                {
                    continue;
                }
                var netz = fern ? s.Fern! : s.Nah;
                var puffer = fern ? s.LagenFern : s.LagenNah;
                if (puffer == 0)
                {
                    // ohne Instancing: jedes Stueck ein Aufruf mit dem Programm der Szene
                    for (var i = von; i < von + anzahl; i++)
                    {
                        Zeichnen(gl, netz, _instanzLagen[i], sichtProj);
                        _festAufrufe++;
                        _festDreiecke += netz.Anzahl / 3;
                    }
                    continue;
                }
                if (!mitProgramm)
                {
                    gl.UseProgram(_instanzProgramm);
                    gl.UniformMatrix4fv(_instanzOrtSichtProj, 1, false, &sichtProj);
                    gl.Uniform1f(_instanzOrtHell, (IstDunkel() ? 0.72f : 1f) * licht.Hell);
                    LichtSetzen(gl, _instanzLichtOrte, licht, auge);
                    DunstSetzen(gl, _instanzDunstOrte);
                    gl.Uniform1f(_instanzOrtDurch, 0f);
                    mitProgramm = true;
                }
                netz.Binden(gl);
                gl.BindBuffer(GL_ARRAY_BUFFER, puffer);
                fixed (Matrix4x4* lagen = _instanzLagen)
                {
                    gl.BufferData(GL_ARRAY_BUFFER, new IntPtr(anzahl * 64), new IntPtr(lagen + von), GlDynamicDraw);
                }
                ((delegate* unmanaged[Stdcall]<int, int, int, IntPtr, int, void>)_glInstanziert)(GL_TRIANGLES, netz.Anzahl, GlUnsignedInt, IntPtr.Zero, anzahl);
                _aufrufe++;
                _dreiecke += (long)(netz.Anzahl / 3) * anzahl;
                _festAufrufe++;
                _festDreiecke += (long)(netz.Anzahl / 3) * anzahl;
            }
        }
        if (mitProgramm)
        {
            gl.UseProgram(_programm);
        }
    }
}
