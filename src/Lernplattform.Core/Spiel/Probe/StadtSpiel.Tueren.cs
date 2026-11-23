using System.Numerics;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle A 1.6.0 (Sammelliste 443, Weisung P12-360 Antwort 1c, Nico: „1c“): jede Raumtuer ist zu und geht nur mit der Handlung
/// (Taste F am PC, Knopf ueber dem linken Stick am Handy und Tablet) auf; eine geschlossene Tuer ist ein Hindernis, die Figur geht
/// nicht hindurch (frei gehen und Antippen: sie bleibt davor stehen). E1 (Chat): eine offene Tuer geht wieder zu, sobald die Figur
/// den Tuerbereich verlassen hat (weiter als <see cref="TuerZuAb"/> von ihrer Mitte). Nur mit 3D (<see cref="TuerenAn"/>; ohne 3D
/// gibt es keine gezeichneten Tueren und die Figur geht wie bisher). Nur Darstellung: kein Ereignis, kein Spielstand. Im Werkzeug
/// (<see cref="TuerenOeffnenSelbst"/>) oeffnet die Figur jede Tuer auf ihrem Weg von selbst, dort gibt es keine Handlung.
/// </summary>
public sealed partial class StadtSpiel
{
    public const string AktionTuerAuf = "tuer_auf";

    public const string AktionTuerZu = "tuer_zu";

    /// <summary>So weit (Einheiten) darf die Mitte einer Tuer von der Figur weg sein, damit sie die Tuer oeffnen kann.</summary>
    public const float TuerReichweite = 1.6f;

    /// <summary>Weiter als so weit von der Mitte der Tuer weg geht eine offene Tuer wieder zu (E1 Chat).</summary>
    public const float TuerZuAb = 2.2f;

    /// <summary>Werkzeug: die Figur oeffnet jede Tuer auf ihrem Weg selbst (das Werkzeug hat keine Handlung).</summary>
    public bool TuerenOeffnenSelbst { get; set; }

    private Innenraum? _tuerenInnen;
    private readonly Dictionary<int, TuerOffen> _offeneTueren = [];

    /// <summary>Gibt es Tueren (3D und Haltungen moeglich, nicht die Gegenprobe ohne Tueren)?</summary>
    public bool TuerenAn => HaltungAn && !Probe.Innenraum.OhneTuerenFuerTests;

    /// <summary>Die offenen Raumtueren (Nummer und Lage); eine Tuer, von der die Figur weit genug weg ist, geht dabei zu.</summary>
    public IReadOnlyDictionary<int, TuerOffen> OffeneTueren
    {
        get
        {
            if (!ReferenceEquals(_tuerenInnen, Innen))
            {
                _tuerenInnen = Innen;
                _offeneTueren.Clear();
            }
            if (Innen is { } innen && _offeneTueren.Count > 0)
            {
                var figur = new Vector2(FigurX, FigurZ);
                foreach (var t in innen.RaumTueren)
                {
                    if (_offeneTueren.ContainsKey(t.Nr) && Vector2.Distance(t.Mitte, figur) > TuerZuAb && !InSperre(t, FigurX, FigurZ))
                    {
                        _offeneTueren.Remove(t.Nr);
                    }
                }
            }
            return _offeneTueren;
        }
    }

    private static bool InSperre(RaumTuer t, float x, float z)
    {
        var (x1, z1, x2, z2) = Probe.Innenraum.TuerSperre(t);
        return x >= x1 && x <= x2 && z >= z1 && z <= z2;
    }

    /// <summary>Ist diese Raumtuer gerade zu (und sperrt)?</summary>
    public bool TuerZu(RaumTuer t) => TuerenAn && !OffeneTueren.ContainsKey(t.Nr);

    /// <summary>Darf die Figur auf (x, z) treten, ohne in eine geschlossene Tuer zu laufen (Schritt von (vonX, vonZ) aus)?</summary>
    private bool TuerFrei(double x, double z, double vonX, double vonZ)
    {
        if (!TuerenAn || Innen is not { } innen)
        {
            return true;
        }
        if (TuerenOeffnenSelbst)
        {
            TuerenAufStrecke(innen, (float)vonX, (float)vonZ, (float)x, (float)z);
            return true;
        }
        return innen.TuerAufStrecke((float)vonX, (float)vonZ, (float)x, (float)z, TuerZu) is null;
    }

    /// <summary>Werkzeug: jede geschlossene Tuer auf der Strecke oeffnen (in ihrer freien Lage).</summary>
    private void TuerenAufStrecke(Innenraum innen, float ax, float az, float bx, float bz)
    {
        // nur Tueren nah an der Figur (weiter weg ginge sie gleich wieder zu); die Figur geht im Werkzeug ohne Sperre weiter
        var figur = new Vector2(FigurX, FigurZ);
        bool Nah(RaumTuer t) => TuerZu(t) && Vector2.Distance(t.Mitte, figur) <= TuerZuAb;
        while (innen.TuerAufStrecke(ax, az, bx, bz, Nah) is not null)
        {
            var t = innen.RaumTueren.Where(Nah).OrderBy(t => Vector2.Distance(t.Mitte, new Vector2(ax, az)))
                .FirstOrDefault(t => innen.TuerAufStrecke(ax, az, bx, bz, u => u == t) is not null);
            if (t is null)
            {
                return;
            }
            _offeneTueren[t.Nr] = innen.OffenLage(t) ?? new TuerOffen(true, t.Innen);
        }
    }

    /// <summary>
    /// Weg der Figur (Antippen) an der ersten geschlossenen Tuer kuerzen: sie bleibt davor stehen (der Rest des Wegs entfaellt).
    /// true = gekuerzt.
    /// </summary>
    private bool WegVorTuerKuerzen()
    {
        if (!TuerenAn || Innen is not { } innen || _weg.Count == 0)
        {
            return false;
        }
        var (ax, az) = ((float)FigurX, (float)FigurZ);
        for (var i = 0; i < _weg.Count; i++)
        {
            var (bx, bz) = ((float)_weg[i].X, (float)_weg[i].Y);
            if (TuerenOeffnenSelbst)
            {
                TuerenAufStrecke(innen, ax, az, bx, bz);
            }
            else if (innen.TuerAufStrecke(ax, az, bx, bz, TuerZu) is { } anteil)
            {
                // knapp vor der Sperre stehen bleiben
                var laenge = MathF.Sqrt(((bx - ax) * (bx - ax)) + ((bz - az) * (bz - az)));
                var bis = laenge > 1e-6f ? MathF.Max(0, anteil - (0.02f / laenge)) : 0;
                _weg.RemoveRange(i, _weg.Count - i);
                if (bis > 0)
                {
                    _weg.Add((ax + ((bx - ax) * bis), az + ((bz - az) * bis)));
                }
                _vorTuer = true;
                return true;
            }
            (ax, az) = (bx, bz);
        }
        return false;
    }

    /// <summary>Die Figur blieb vor einer geschlossenen Tuer stehen (Hinweis in der Zeile, bis sie wieder geht).</summary>
    private bool _vorTuer;

    /// <summary>Knopf an einer Raumtuer in Reichweite: „Tür öffnen“ oder „Tür schließen“ (nur mit Tueren, stehend).</summary>
    private List<(string Aktion, string Name)> TuerKnoepfe(Innenraum innen, List<(string Aktion, string Name)> knoepfe)
    {
        if (!TuerenAn || TuerenOeffnenSelbst || MeinPlatz is not null || innen.RaumTuerNahe(FigurX, FigurZ, TuerReichweite) is not { } t)
        {
            _vorTuer = false;
            return knoepfe;
        }
        var offen = OffeneTueren.ContainsKey(t.Nr);
        if (_vorTuer && !offen)
        {
            Zeile = SpielProbeTexte.TuerIstZu + "\n" + Zeile;
        }
        _vorTuer = false;
        if (!offen && innen.OffenLage(t) is null)
        {
            // kein Platz zum Aufgehen (sollte es nie geben, Test ueber alle Grundrisse): die Tuer bleibt offen gezeichnet
            return knoepfe;
        }
        return [(offen ? AktionTuerZu : AktionTuerAuf, offen ? SpielProbeTexte.KnopfTuerSchliessen : SpielProbeTexte.KnopfTuerOeffnen), .. knoepfe];
    }

    /// <summary>Knopf „Tür öffnen“/„Tür schließen“; true = die Aktion gehoert hierher.</summary>
    private bool TuerAktion(string aktion)
    {
        if (aktion is not (AktionTuerAuf or AktionTuerZu))
        {
            return false;
        }
        if (Innen is { } innen && innen.RaumTuerNahe(FigurX, FigurZ, TuerReichweite) is { } t)
        {
            if (aktion == AktionTuerAuf && innen.OffenLage(t) is { } lage)
            {
                _offeneTueren[t.Nr] = lage;
            }
            else if (aktion == AktionTuerZu && !InSperre(t, FigurX, FigurZ))
            {
                _offeneTueren.Remove(t.Nr);
            }
            InnenText();
            Geaendert?.Invoke();
        }
        return true;
    }
}
