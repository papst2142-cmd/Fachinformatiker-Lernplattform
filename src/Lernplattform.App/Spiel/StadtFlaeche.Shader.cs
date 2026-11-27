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
using Lernplattform.Core.Texte;
using static Avalonia.OpenGL.GlConsts;

namespace Lernplattform.Spiel;

/// <summary>
/// Engine festigen 3 (A 1.16.0, Weisung P12-395/P12-399): Teil von <see cref="StadtFlaeche"/> mit den Shader-Quellen und dem
/// Uebersetzen der Programme. Reiner Umzug aus StadtFlaeche.cs, nichts geaendert.
/// </summary>
public sealed partial class StadtFlaeche
{
    /// <summary>
    /// Kopf eines Shaders. Patch A 0.75.1 (Sammelliste 340, Pixelfehler Tab A8 mit Mali-G52): unter OpenGL ES rechnet der
    /// Ecken-Shader mit <c>highp</c> (32 Bit). Bis A 0.75.0 stand <c>precision mediump float;</c> auch vor dem Ecken-Shader;
    /// ein Mali-Chip rechnet mediump wirklich mit 16 Bit (10 Bit Mantisse), eine Weltkoordinate um 100 liegt dann bis
    /// 2^6 x 2^-10 = 0,0625 daneben, lange Flaechen der Stadt (Strassen, Kreisverkehr, Teich) zerfallen in Scherben.
    /// Der Pixel-Shader bleibt mediump (Farben, nichts an Lagen). Am PC (Desktop-GLSL) aendert sich nichts.
    /// </summary>
    internal static string Kopf(bool es, GlVersion v, bool ecke) =>
        es ? (ecke ? "#version 300 es\nprecision highp float;\n" : "#version 300 es\nprecision mediump float;\n")
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

    /// <summary>Patch A 0.75.1: ganze Quelltexte (Kopf plus Rumpf) von Ecken- und Pixel-Shader, wie sie der Treiber bekommt (Tests pruefen den Kopf je Art).</summary>
    internal static (string Ecke, string Pixel) Quellen(bool es, GlVersion v, bool autos = false, bool instanzen = false)
    {
        var ecke = autos ? AutoEcke : instanzen ? InstanzEcke : """
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
            uniform float uDunst;
            uniform float uDunstVon;
            uniform float uDunstBis;
            uniform float uDunstR;
            uniform float uDunstG;
            uniform float uDunstB;
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
                // Welle A 0.60.0: Lampen der Ampeln (Ampeln.LichtRot, LichtGelb, LichtGruen) leuchten in ihrer Farbe, auch am Tag
                float nacht = uNacht;
                if (distance(farbe, vec3(0.92, 0.10, 0.08)) < 0.02) { leuchten = vec3(1.0, 0.16, 0.10); nacht = max(uNacht, 0.6); }
                else if (distance(farbe, vec3(0.95, 0.66, 0.08)) < 0.02) { leuchten = vec3(1.0, 0.78, 0.18); nacht = max(uNacht, 0.6); }
                else if (distance(farbe, vec3(0.12, 0.80, 0.32)) < 0.02) { leuchten = vec3(0.30, 1.0, 0.50); nacht = max(uNacht, 0.6); }
                vec3 aus = mix(normal, leuchten, glut * nacht);
                // Welle Nachladen A 0.82.0 (Nico 3b): leichter Dunst am Rand der Sicht in der Farbe des Himmels (Dunst.cs);
                // ohne Nachladen ist uDunst 0 und das Bild wie vorher; naeher als uDunstVon ist der Anteil 0 (dort nichts
                // rechnen: das Bild bleibt gleich, die Rechnung je Bildpunkt entfaellt fast ueberall, P12-281 Punkt 3)
                vec2 vomAuge = vWelt.xz - vec2(uAugeX, uAugeZ);
                float weit2 = dot(vomAuge, vomAuge);
                if (uDunst > 0.0 && weit2 > uDunstVon * uDunstVon) {
                    aus = mix(aus, vec3(uDunstR, uDunstG, uDunstB), uDunst * smoothstep(uDunstVon, uDunstBis, sqrt(weit2)));
                }
                fFarbe = vec4(aus, 1.0);
            }
            """;
        return (Kopf(es, v, ecke: true) + ecke, Kopf(es, v, ecke: false) + pixel);
    }

    private static int Programm(GlInterface gl, bool es, GlVersion v, bool autos = false, bool instanzen = false)
    {
        var (eckeQuelle, pixelQuelle) = Quellen(es, v, autos, instanzen);
        var vs = gl.CreateShader(GL_VERTEX_SHADER);
        var fehler = gl.CompileShaderAndGetError(vs, eckeQuelle);
        if (fehler is not null)
        {
            throw new InvalidOperationException("Ecken-Shader: " + fehler);
        }
        var fs = gl.CreateShader(GL_FRAGMENT_SHADER);
        fehler = gl.CompileShaderAndGetError(fs, pixelQuelle);
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
        if (instanzen)
        {
            for (var k = 0; k < 4; k++)
            {
                gl.BindAttribLocationString(p, 4 + k, "aLage" + k);
            }
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
}
