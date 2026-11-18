using Lernplattform.Core.Pakete;
using Lernplattform.Core.Texte;
using Lernplattform.Views;

namespace Lernplattform.Programm;

/// <summary>
/// Welle A 0.91.0 (Weisung P12-300, Nico 1b 2c 3a, Zusatz a): Ablaeufe der Fachrichtungs-Pakete ausserhalb der Karte
/// „Inhaltspakete“: Laden aus dem Hinweis oben in der Lernansicht und aus der Rueckfrage, wenn einer Fachrichtung alle Inhalte
/// fehlen (Vorbereitung 386; die App 0.91.0 bringt AE, DPA und DV noch selbst mit), und der Hinweis nach einem Abgleich. Nichts
/// laedt von allein; am Handy ohne WLAN zuerst die Rueckfrage mit der Groesse. Der Lernstand wird nie geaendert.
/// </summary>
public static class FachrichtungsPaketDienst
{
    private static readonly HashSet<string> Gezeigt = new(StringComparer.Ordinal);

    /// <summary>
    /// Hinweis oben in der Lernansicht (Nico 3a): ein neuerer Stand des Pakets der Sicht ist angeboten (letzte Suche) und der
    /// Hinweis zu diesem Stand wurde in diesem Programmlauf noch nicht gezeigt. null = kein Hinweis.
    /// </summary>
    public static (string Fachrichtung, PaketAngebot Angebot)? Hinweis()
    {
        var sicht = Zustand.Sicht;
        foreach (var (fr, angebot) in PaketDienst.Verwaltung.NeueFachrichtungsStaende())
        {
            if (fr == sicht && !Gezeigt.Contains(fr + ":" + angebot.Stand))
            {
                return (fr, angebot);
            }
        }
        return null;
    }

    /// <summary>Der Hinweis zu diesem Stand ist gezeigt (einmal je Programmlauf).</summary>
    public static void HinweisGezeigt(string fachrichtung, long stand) => Gezeigt.Add(fachrichtung + ":" + stand);

    /// <summary>Text des Hinweises (Nico 3a).</summary>
    public static string HinweisText(string fachrichtung, PaketAngebot angebot) =>
        PaketTexte.Format(PaketTexte.FrHinweisNeu, PaketTexte.Fr(fachrichtung), angebot.Stand, PaketTexte.Mb(angebot.Groesse));

    /// <summary>
    /// Laedt das Fachrichtungs-Paket (Angebot der letzten Suche, sonst wird erst gesucht); am Handy ohne WLAN vorher die
    /// Rueckfrage mit der Groesse. Danach werden die Inhalte neu gebildet. null = geladen, sonst der Grund.
    /// </summary>
    public static async Task<string?> LadenAsync(string fachrichtung)
    {
        var v = PaketDienst.Verwaltung;
        var kennung = FachrichtungsPaket.Kennung(fachrichtung);
        if (v.Angebote.All(a => a.Kennung != kennung))
        {
            await Task.Run(() => v.SuchenAsync());
        }
        if (v.Zeilen().FirstOrDefault(z => z.Kennung == kennung && z.Angebot is not null) is not { Angebot: { } angebot } zeile)
        {
            return v.SuchFehler ?? PaketTexte.KeineAngebote;
        }
        if (PaketDienst.MobilFrage(zeile) is { } frage && !await frage)
        {
            return PaketTexte.Abgebrochen;
        }
        var grund = await Task.Run(() => v.LadenAsync(angebot, null, CancellationToken.None));
        if (grund is null)
        {
            await Zustand.PaketeNeu(fachrichtung);
        }
        return grund;
    }

    /// <summary>
    /// Rueckfrage, wenn der Fachrichtung alle Inhalte fehlen (Weisung P12-300 Punkt 4, wortgleich): „Für AE fehlt das
    /// Fachrichtungs-Paket (1,2 MB). Jetzt laden?“ mit „Laden“ und „Später“. Ohne Verbindung ein Hinweis. Nie ein Absturz.
    /// </summary>
    public static async Task FehltAsync(string fachrichtung)
    {
        try
        {
            var v = PaketDienst.Verwaltung;
            var kennung = FachrichtungsPaket.Kennung(fachrichtung);
            if (v.Angebote.All(a => a.Kennung != kennung))
            {
                await Task.Run(() => v.SuchenAsync());
            }
            var fr = PaketTexte.Fr(fachrichtung);
            if (v.Angebote.FirstOrDefault(a => a.Kennung == kennung) is not { } angebot)
            {
                await Dialog.Zeigen(PaketTexte.FrFehltTitel, PaketTexte.Format(PaketTexte.FrFehltOhneNetz, fr), Dialog.Ok);
                return;
            }
            if (await Dialog.Zeigen(PaketTexte.FrFehltTitel, PaketTexte.Format(PaketTexte.FrFehltFrage, fr, PaketTexte.Mb(angebot.Groesse)),
                PaketTexte.Laden, PaketTexte.Spaeter) != 0)
            {
                return;
            }
            if (await LadenAsync(fachrichtung) is { } grund && grund != PaketTexte.Abgebrochen)
            {
                await Dialog.Zeigen(PaketTexte.FrFehltTitel, grund, Dialog.Ok);
            }
        }
        catch (Exception fehler)
        {
            Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
        }
    }

    private static string? _nachAbgleichGezeigt;

    /// <summary>
    /// Nach einem Abgleich (Kurzplan P12-180 Abschnitt 4): kennt der Lernstand Szenarien oder Testprojekte, die auf diesem
    /// Geraet fehlen, oder fehlen der Sicht alle Inhalte, einmal der Hinweis aufs Fachrichtungs-Paket. Nur lesen.
    /// </summary>
    public static async Task NachAbgleichAsync()
    {
        try
        {
            if (await Zustand.AlleInhalte is not { } alle)
            {
                return;
            }
            var fehlen = await Task.Run(() => FachrichtungsPaket.FehlenNachAbgleich(Zustand.Lernstand, alle, Zustand.Aktuell, Zustand.Sicht));
            if (fehlen.Count == 0)
            {
                return;
            }
            var text = PaketTexte.Format(PaketTexte.FrNachAbgleich, string.Join(", ", fehlen.Select(PaketTexte.Fr)));
            if (text == _nachAbgleichGezeigt)
            {
                return;
            }
            _nachAbgleichGezeigt = text;
            await Dialog.Zeigen(KartenTexte.HinweisTitel, text, Dialog.Ok);
        }
        catch (Exception fehler)
        {
            Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
        }
    }
}
