using System.Numerics;
using Lernplattform.Core.Texte;
using GlastuerLage = Lernplattform.Core.Spiel.Probe.Innenraum.GlastuerLage;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle A 0.99.0 (Weisung P12-317 F2, Nico: „Erst zu und durch Oeffnen wird sie so geoeffnet wie auf dem Bild.“): die Glastuer
/// des Serverschranks ist zu, bis die Figur sie vor dem Schrank oeffnet (Knopf „Glastür öffnen“); dann steht sie 90 Grad offen
/// zu der Seite, auf der sie weder die Figur noch ein anderes Stueck beruehrt; die Figur dreht sich dabei zur Tuer. Nur Darstellung: kein Ereignis, kein Spielstand,
/// keine Bewertung. E1: sie geht wieder zu mit „Glastür schließen“ und sobald die Figur sich bewegt, dreht oder die Haltung
/// wechselt (so kann die offene Tuer die Figur nie beruehren), beim Verlassen des Raums ebenso.
/// </summary>
public sealed partial class StadtSpiel
{
    public const string AktionGlastuer = "glastuer";

    private (Innenraum Innen, int Nummer, GlastuerLage Lage, Vector2 Ort, float Richtung, Koerperhaltung Koerper)? _glastuer;

    /// <summary>Hinweis, wenn die Tuer keinen Platz fand (Zeile der Tafel), sonst null.</summary>
    private string? _glastuerHinweis;

    /// <summary>Die offene Glastuer (Nummer im Raum und Lage), sonst null; geht zu, sobald die Figur sich bewegt oder dreht.</summary>
    public (int Nummer, GlastuerLage Lage)? GlastuerOffen
    {
        get
        {
            if (_glastuer is not { } g)
            {
                return null;
            }
            if (!ReferenceEquals(g.Innen, Innen) || Vector2.Distance(g.Ort, new Vector2(FigurX, FigurZ)) > 0.01f
                || MathF.Abs(g.Richtung - FigurRichtung) > 0.5f || g.Koerper != Koerper || MeinPlatz is not null)
            {
                _glastuer = null;
                return null;
            }
            return (g.Nummer, g.Lage);
        }
    }

    /// <summary>Knopf vor einer Glastuer (nur mit 3D, stehend): oeffnen oder schliessen.</summary>
    private List<(string Aktion, string Name)> GlastuerKnoepfe(Innenraum innen, List<(string Aktion, string Name)> knoepfe)
    {
        if (!HaltungAn || MeinPlatz is not null || innen.GlastuerNahe(FigurX, FigurZ) is not { } nr)
        {
            return knoepfe;
        }
        if (_glastuerHinweis is { } hinweis)
        {
            Zeile += "\n" + hinweis;
        }
        var offen = GlastuerOffen is { } g && g.Nummer == nr;
        return [.. knoepfe, (AktionGlastuer, offen ? SpielProbeTexte.KnopfGlastuerSchliessen : SpielProbeTexte.KnopfGlastuerOeffnen)];
    }

    /// <summary>Knopf „Glastür öffnen“/„Glastür schließen“; true = die Aktion gehoert hierher.</summary>
    private bool GlastuerAktion(string aktion)
    {
        if (aktion != AktionGlastuer || Innen is not { } innen || innen.GlastuerNahe(FigurX, FigurZ) is not { } nr)
        {
            return aktion == AktionGlastuer;
        }
        _glastuerHinweis = null;
        if (GlastuerOffen is { } g && g.Nummer == nr)
        {
            _glastuer = null;
        }
        else
        {
            // wer eine Tuer oeffnet, schaut zu ihr: die Figur dreht sich zur Front des Schranks (nur Darstellung)
            var blick = innen.GlastuerBlick(nr);
            var lage = innen.GlastuerWahl(nr, new Vector2(FigurX, FigurZ), blick * MathF.PI / 180f, Koerper);
            if (lage != GlastuerLage.Zu)
            {
                FigurRichtung = blick;
            }
            _glastuer = lage == GlastuerLage.Zu ? null : (innen, nr, lage, new Vector2(FigurX, FigurZ), FigurRichtung, Koerper);
            _glastuerHinweis = lage == GlastuerLage.Zu ? SpielProbeTexte.GlastuerKeinPlatz : null;
        }
        InnenText();
        return true;
    }

    /// <summary>Lage jeder Glastuer im Raum (Darstellung): alle zu, ausser der offenen.</summary>
    public IReadOnlyList<GlastuerLage> GlastuerLagen()
    {
        if (Innen is not { } innen || innen.GlastuerZahl == 0)
        {
            return [];
        }
        var lagen = Enumerable.Repeat(GlastuerLage.Zu, innen.GlastuerZahl).ToArray();
        if (GlastuerOffen is { } g)
        {
            lagen[g.Nummer] = g.Lage;
        }
        return lagen;
    }
}
