using System.Numerics;
using Avalonia.OpenGL;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Spiel;

/// <summary>
/// Welle A 0.99.0 (Weisung P12-317 F2): Glastueren der Serverschraenke. Rahmen deckend, die Scheibe halb durchsichtig wie
/// verdeckende Waende (Schachbrett im Shader, auf jeder Grafikstufe gleich, kein Mischen noetig). Zwei Aufrufe mehr, nur in
/// Raeumen mit Serverschraenken; neu gebaut nur, wenn eine Tuer auf- oder zugeht.
/// </summary>
public sealed partial class StadtFlaeche
{
    private (Innenraum Raum, string Schluessel, GlNetz Rahmen, GlNetz Glas)? _glastueren;

    private void GlastuerenZeichnen(GlInterface gl, Innenraum innen, Matrix4x4 sichtProj)
    {
        if (_spiel is not { } spiel || innen.GlastuerZahl == 0)
        {
            return;
        }
        var lagen = spiel.GlastuerLagen();
        var schluessel = string.Join(",", lagen);
        if (_glastueren is not { } t || !ReferenceEquals(t.Raum, innen) || t.Schluessel != schluessel)
        {
            GlastuerenFrei(gl);
            t = (innen, schluessel, GlNetz.Laden(gl, innen.GlastuerNetz(lagen, false)), GlNetz.Laden(gl, innen.GlastuerNetz(lagen, true)));
            _glastueren = t;
        }
        gl.Uniform1f(_ortDurch, 0f);
        Zeichnen(gl, t.Rahmen, Matrix4x4.Identity, sichtProj);
        gl.Uniform1f(_ortDurch, 1f);
        Zeichnen(gl, t.Glas, Matrix4x4.Identity, sichtProj);
        gl.Uniform1f(_ortDurch, 0f);
    }

    private void GlastuerenFrei(GlInterface? gl)
    {
        if (gl is not null && _glastueren is { } t)
        {
            t.Rahmen.Freigeben(gl);
            t.Glas.Freigeben(gl);
        }
        _glastueren = null;
    }
}
