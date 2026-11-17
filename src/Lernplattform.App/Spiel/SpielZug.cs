using Lernplattform.Core.Pakete;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;
using Lernplattform.Programm;

namespace Lernplattform.Spiel;

/// <summary>
/// Welle A 0.88.0 (Inhaltspakete W2, Weisung P12-296, Plan P12-175 Abschnitte 3 und 5, Nico 1a): Fahrplan am Bahnhof und seine
/// Knoepfe. Der Fahrplan nennt Stadt 1 und jede Stadt der Paketliste (geladen oder angeboten). Fehlt ein Paket, laedt der Knopf
/// „Paket fehlt (…) – Laden“ es ueber denselben Weg wie die Karte in den Optionen (<see cref="PaketDienst.MobilFrage"/>, dann
/// <see cref="PaketVerwaltung.LadenAsync"/>); nichts laedt von allein, gesucht wird nur auf Knopfdruck.
/// </summary>
public static class SpielZug
{
    /// <summary>Die Staedte im Fahrplan (Stadt 1 zuerst).</summary>
    public static IReadOnlyList<FahrplanStadt> Fahrplan(SpielSitzung sitzung)
    {
        var verwaltung = PaketDienst.Verwaltung;
        var liste = new List<FahrplanStadt>
        {
            new(null, ZugTexte.Stadt1, true, verwaltung.Gesucht is null && !Laedt ? (StadtSpiel.AktionPaketSuchen, ZugTexte.Suchen) : null),
        };
        // Sammelliste 376: nur Pakete, deren Art als Stadt bekannt ist (sonst nur in den Optionen)
        foreach (var z in verwaltung.Zeilen().Where(z => z.ImFahrplan))
        {
            switch (z.Zustand)
            {
                case PaketZustand.Geladen:
                    liste.Add(sitzung.PaketStadt(z.Kennung) is not null ? new(z.Kennung, z.Name, true)
                        : new(z.Kennung, z.Name, false, null, ZugTexte.Format(ZugTexte.Fehlerhaft, z.Name)));
                    break;
                case PaketZustand.NeuerStand:
                    liste.Add(new(z.Kennung, z.Name, sitzung.PaketStadt(z.Kennung) is not null,
                        Laedt ? null : (StadtSpiel.AktionPaketLaden + z.Kennung, ZugTexte.Format(ZugTexte.NeuerStand, z.Name))));
                    break;
                case PaketZustand.NichtGeladen:
                    liste.Add(new(z.Kennung, z.Name, false,
                        Laedt ? null : (StadtSpiel.AktionPaketLaden + z.Kennung, ZugTexte.Format(ZugTexte.PaketFehlt, z.Name, PaketTexte.Mb(z.Groesse)))));
                    break;
                default:
                    // fehlerhaft: Grund steht in den Optionen
                    liste.Add(new(z.Kennung, z.Name, false, null, ZugTexte.Format(ZugTexte.Fehlerhaft, z.Name)));
                    break;
            }
        }
        return liste;
    }

    /// <summary>Laedt gerade ein Paket vom Bahnhof aus (dann keine Lade-Knoepfe).</summary>
    public static bool Laedt { get; private set; }

    /// <summary>Knopf am Bahnhof oder im Hinweis „Paket laden?“; true = war einer davon.</summary>
    public static bool Aktion(SpielSitzung sitzung, string aktion)
    {
        if (aktion.StartsWith(StadtSpiel.AktionZug, StringComparison.Ordinal))
        {
            sitzung.ZugFahren(aktion[StadtSpiel.AktionZug.Length..]);
            return true;
        }
        if (aktion.StartsWith(StadtSpiel.AktionPaketLaden, StringComparison.Ordinal))
        {
            _ = LadenAsync(sitzung, aktion[StadtSpiel.AktionPaketLaden.Length..]);
            return true;
        }
        if (aktion == StadtSpiel.AktionPaketSuchen)
        {
            _ = SuchenAsync(sitzung);
            return true;
        }
        return false;
    }

    private static async Task SuchenAsync(SpielSitzung sitzung)
    {
        Laedt = true;
        sitzung.Spiel.FahrplanNeu();
        try
        {
            await Task.Run(() => PaketDienst.Verwaltung.SuchenAsync());
        }
        finally
        {
            Laedt = false;
        }
        sitzung.Spiel.FahrplanNeu(PaketDienst.Verwaltung.SuchFehler);
    }

    /// <summary>
    /// Laedt das Paket <paramref name="kennung"/> wie die Karte in den Optionen (am Handy ohne WLAN erst die Rueckfrage mit der
    /// Groesse). Danach zeigt die Sitzung die Stadt, in der die Figur ist (fehlte das Paket, geht es dort weiter).
    /// </summary>
    public static async Task<string?> LadenAsync(SpielSitzung sitzung, string kennung)
    {
        if (Laedt)
        {
            return null;
        }
        var verwaltung = PaketDienst.Verwaltung;
        if (verwaltung.Zeilen().FirstOrDefault(z => z.Kennung == kennung && z.Angebot is not null) is null)
        {
            await Task.Run(() => verwaltung.SuchenAsync());
        }
        if (verwaltung.Zeilen().FirstOrDefault(z => z.Kennung == kennung) is not { Angebot: { } angebot } zeile)
        {
            var grund = verwaltung.SuchFehler ?? PaketTexte.KeineVerbindung;
            Zeigen(sitzung, grund);
            return grund;
        }
        if (PaketDienst.MobilFrage(zeile) is { } frage && !await frage)
        {
            return PaketTexte.Abgebrochen;
        }
        Laedt = true;
        string? ergebnis;
        try
        {
            Zeigen(sitzung, ZugTexte.Format(ZugTexte.Laedt, zeile.Name));
            ergebnis = await Task.Run(() => verwaltung.LadenAsync(angebot, null, CancellationToken.None));
        }
        finally
        {
            Laedt = false;
        }
        sitzung.Abgleichen();
        Zeigen(sitzung, ergebnis ?? ZugTexte.Format(ZugTexte.Geladen, zeile.Name));
        return ergebnis;
    }

    private static void Zeigen(SpielSitzung sitzung, string zeile)
    {
        if (sitzung.Fehlt is { } f)
        {
            sitzung.Spiel.PaketFehlt(f.Kennung, f.Name, zeile);
            return;
        }
        sitzung.Spiel.FahrplanNeu(zeile);
    }
}
