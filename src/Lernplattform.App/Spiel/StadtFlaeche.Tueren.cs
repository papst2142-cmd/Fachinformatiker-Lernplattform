using System.Numerics;
using Avalonia.OpenGL;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Spiel;

/// <summary>
/// Welle A 1.6.0 (Sammelliste 443, Weisung P12-360 Antwort 1c): die Blaetter der Raumtueren (zu oder offen) in einem Netz, ein
/// Zeichenaufruf mehr; neu gebaut nur, wenn eine Tuer auf- oder zugeht. Zarge und Sturz gehoeren zu den Wandstuecken. In der
/// Ich-Sicht wie die Waende um <see cref="Innenraum.IchWandStreckung"/> gestreckt. Ohne Spiel (Werkzeug ohne Figur) sind alle zu.
/// </summary>
public sealed partial class StadtFlaeche
{
    private (Innenraum Raum, string Schluessel, GlNetz Netz)? _tuerBlaetter;

    private static readonly IReadOnlyDictionary<int, TuerOffen> AlleZu = new Dictionary<int, TuerOffen>();

    private void TuerBlaetterZeichnen(GlInterface gl, Innenraum innen, Matrix4x4 sichtProj, bool ich)
    {
        if (innen.RaumTueren.Count == 0)
        {
            return;
        }
        var offen = _spiel is { TuerenAn: true } spiel ? spiel.OffeneTueren : AlleZu;
        var schluessel = string.Join(",", offen.OrderBy(o => o.Key).Select(o => $"{o.Key}:{o.Value.AngelVon}:{o.Value.Seite}"));
        if (_tuerBlaetter is not { } t || !ReferenceEquals(t.Raum, innen) || t.Schluessel != schluessel)
        {
            TuerBlaetterFrei(gl);
            t = (innen, schluessel, GlNetz.Laden(gl, innen.TuerBlattNetz(offen)));
            _tuerBlaetter = t;
        }
        gl.Uniform1f(_ortDurch, 0f);
        Zeichnen(gl, t.Netz, ich ? Matrix4x4.CreateScale(1f, Innenraum.IchWandStreckung, 1f) : Matrix4x4.Identity, sichtProj);
    }

    private void TuerBlaetterFrei(GlInterface? gl)
    {
        if (gl is not null && _tuerBlaetter is { } t)
        {
            t.Netz.Freigeben(gl);
        }
        _tuerBlaetter = null;
    }
}
