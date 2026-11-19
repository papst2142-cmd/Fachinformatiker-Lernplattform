using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle A 0.88.0 (Inhaltspakete W2, Weisung P12-296, Plan P12-175 Abschnitt 3, Nico 1a): am Bahnhof steht draussen der Fahrplan
/// mit allen Staedten (<see cref="Fahrplan"/> liefert die Oberflaeche aus der Liste der Pakete). Fahrbar = Knopf „Zug nach …“
/// (<see cref="AktionZug"/>); fehlt ein Paket, der Knopf „Paket fehlt (…) – Laden“ ueber denselben Weg wie die Karte in den
/// Optionen (die Oberflaeche fuehrt ihn aus). Nichts laedt von allein. Die Fahrt selbst ist <see cref="ZugFahren"/> (ein Ereignis
/// <see cref="Ereignisse.ZugGefahren"/>); danach baut die Sitzung die andere Stadt.
/// </summary>
public sealed partial class StadtSpiel
{
    /// <summary>Aktion „mit dem Zug fahren“: <c>zug:&lt;kennung&gt;</c> (Stadt 1: <see cref="ZugStadt1"/>).</summary>
    public const string AktionZug = "zug:";

    /// <summary>Kennung von Stadt 1 in der Aktion (im Ereignis steht Stadt 1 ohne Vorsatz).</summary>
    public const string ZugStadt1 = "stadt1";

    /// <summary>Aktionen der Oberflaeche am Bahnhof: Paket laden, aktualisieren, suchen.</summary>
    public const string AktionPaketLaden = "paket:";

    public const string AktionPaketSuchen = "paketsuche";

    /// <summary>Staedte im Fahrplan (von der Oberflaeche gesetzt; null = kein Fahrplan, z. B. im Werkzeug und in Tests).</summary>
    public Func<IReadOnlyList<FahrplanStadt>>? Fahrplan { get; set; }

    /// <summary>Steht die Figur draussen vor dem Bahnhof dieser Stadt (mit Spielfigur)?</summary>
    public bool AmBahnhof => Fahrplan is not null && !ImInnenraum && !ImFreien && !Geht && Ablauf.Stand.Profil is not null
        && Jetzt.Ort == Stadt.Bahnhof && Stadt.Tuer(Stadt.Bahnhof) is not null;

    /// <summary>Zeile und Knoepfe des Fahrplans an die Ansicht vor dem Bahnhof haengen.</summary>
    private void FahrplanKnoepfe()
    {
        if (!AmBahnhof || Fahrplan!() is not { Count: > 0 } staedte)
        {
            return;
        }
        var knoepfe = Knoepfe.ToList();
        var zeilen = new List<string> { ZugTexte.Format(ZugTexte.Fahrplan, string.Join(ZugTexte.FahrplanTrenner, staedte.Select(s => s.Name))) };
        foreach (var s in staedte)
        {
            if (s.Kennung != Stadt.Kennung && s.Fahrbar)
            {
                knoepfe.Add((AktionZug + (s.Kennung ?? ZugStadt1), ZugTexte.Format(ZugTexte.ZugNach, s.Name)));
            }
            if (s.Knopf is { } k && !knoepfe.Contains(k))
            {
                knoepfe.Add(k);
            }
            if (s.Hinweis is { } h)
            {
                zeilen.Add(h);
            }
        }
        Zeile = Zeile + "\n" + string.Join("\n", zeilen);
        Knoepfe = knoepfe;
    }

    /// <summary>Fahrplan neu zeigen (nach Laden oder Suchen eines Pakets), wenn die Figur vor dem Bahnhof steht.</summary>
    public void FahrplanNeu(string? zeile = null)
    {
        if (!AmBahnhof)
        {
            return;
        }
        Zeile = OrtZeile(Jetzt.Ort);
        AussenKnoepfe(Jetzt.Ort);
        if (zeile is not null)
        {
            Zeile = zeile + "\n" + Zeile;
        }
        Geaendert?.Invoke();
    }

    /// <summary>
    /// Mit dem Zug zum Bahnhof einer anderen Stadt fahren (ein Ereignis). Danach gehoert die Figur zur anderen Stadt: die Sitzung
    /// baut sie neu (<c>SpielSitzung.Abgleichen</c>). Geht es nicht, steht der Grund in der Zeile.
    /// </summary>
    public StadtGrund ZugFahren(string kennung, string name)
    {
        FreiHalt();
        if (!AmBahnhof)
        {
            Zeile = ZugTexte.NichtAmBahnhof;
            Geaendert?.Invoke();
            return StadtGrund.NichtAmBahnhof;
        }
        var ziel = Stadt.BahnhofVon(kennung == ZugStadt1 ? null : kennung);
        var ergebnis = Schreiben(() => Stadt.Zug(Ablauf, ziel, name));
        if (ergebnis is not { Ok: true })
        {
            if (ergebnis is { } e)
            {
                Zeile = StadtTexte.Grund(e.Grund);
                Geaendert?.Invoke();
            }
            return ergebnis?.Grund ?? StadtGrund.KeinWeg;
        }
        return StadtGrund.Ok;
    }

    /// <summary>
    /// Die Figur ist in einer Stadt, deren Paket hier fehlt (zweites Geraet, Plan Abschnitt 5): nur der Hinweis und „Paket laden“,
    /// jeder Weg geht nicht (der Ort ist hier <see cref="Stadt.Fremd"/>).
    /// </summary>
    public void PaketFehlt(string kennung, string name, string? zusatz = null)
    {
        // nur Darstellung: die Figur steht hier am Bahnhof (dort kaeme sie mit dem Zug an), nicht an einer leeren Stelle der Karte;
        // ihr Ort bleibt der in der anderen Stadt (kein Schritt moeglich)
        if (Szene.Ort(Stadt.Bahnhof) is { } bahnhof && !Geht)
        {
            FigurX = bahnhof.StehX;
            FigurZ = bahnhof.StehZ;
        }
        Zeile = (zusatz is null ? "" : zusatz + "\n") + ZugTexte.Format(ZugTexte.PaketFehltHier, name);
        Knoepfe = [(AktionPaketLaden + kennung, ZugTexte.PaketLaden)];
        Geaendert?.Invoke();
    }

    /// <summary>true, solange die Figur in einer anderen Stadt ist (hier geht kein Schritt).</summary>
    public bool InAndererStadt => Stadt.Fremd(Jetzt.Ort);
}
