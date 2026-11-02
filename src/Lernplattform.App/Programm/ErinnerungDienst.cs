using Avalonia.Threading;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Lernen;
using Lernplattform.Views;

namespace Lernplattform.Programm;

/// <summary>
/// Phase 11 W1 (Sammelliste 180): Erinnerung ans Tagesziel wie check_reminder in Python v0.63.2. PC: 8 s nach
/// dem Start und danach alle 5 Minuten, solange das Programm laeuft (app_gui 11071). Handy: 8 s nach dem Start
/// und beim Zurueckholen der App (main.py 6044, 5983); eine Benachrichtigung bei geschlossener App gibt es wie in
/// Python nicht. Faellig ist sie nach <see cref="Erinnerung.Faellig"/>; dann wird zuerst der Tag gemerkt
/// (einstellungen.json nur ueber <see cref="Zustand.Aendere"/>) und danach der Hinweis gezeigt. Laesst sich der Tag
/// nicht speichern, kommt kein Hinweis (sonst kaeme er alle 5 Minuten wieder). Ein Fehler zeigt nichts, die
/// naechste Pruefung versucht es neu. Im Start ohne Bedienung (Start-Test) laeuft die Erinnerung nicht.
/// </summary>
public static class ErinnerungDienst
{
    private static int _gestartet;
    private static int _laeuft;

    private static bool Aus => MainView.AutoModus;

    /// <summary>Beim Start (MainView, erstes Bild); gestartet wird einmal je Programmlauf.</summary>
    public static void Starten()
    {
        if (Aus || Interlocked.Exchange(ref _gestartet, 1) == 1)
        {
            return;
        }
        DispatcherTimer.RunOnce(() =>
        {
            _ = PruefenAsync();
            if (UpdateDienst.Pc) // alle 5 Minuten nur am PC
            {
                var uhr = new DispatcherTimer { Interval = Erinnerung.PcAbstand };
                uhr.Tick += (_, _) => _ = PruefenAsync();
                uhr.Start();
            }
        }, Erinnerung.StartNach);
    }

    /// <summary>Handy: App kommt aus dem Hintergrund zurueck (nur aus dem Android-Lebenszyklus gerufen).</summary>
    public static void Zurueck()
    {
        if (!Aus && Volatile.Read(ref _gestartet) == 1)
        {
            _ = PruefenAsync();
        }
    }

    /// <summary>Eine Pruefung (Oberflaechen-Faden): faellig, dann Tag merken und Hinweis zeigen.</summary>
    public static async Task PruefenAsync()
    {
        if (Interlocked.Exchange(ref _laeuft, 1) == 1)
        {
            return;
        }
        try
        {
            var werte = Zustand.Einstellungen.Laden();
            var ziel = Einstellungen.Ziel(werte);
            var jetzt = DateTime.Now;
            if (!ziel.ZielAn || !ziel.ErinnerungAn)
            {
                return;
            }
            var pfad = Path.Combine(Ablage.Datenordner(), Lernstand.DateiName);
            if (await Task.Run(() => DashboardWerte.ErledigtAm(pfad, DateOnly.FromDateTime(jetzt))) is not { } erledigt)
            {
                return;
            }
            if (!Erinnerung.Faellig(ziel, erledigt, jetzt, Erinnerung.GezeigtAm(werte)))
            {
                return;
            }
            if (!Zustand.Aendere(w => w[Erinnerung.GezeigtSchluessel] = Erinnerung.Tag(jetzt)))
            {
                return;
            }
            Kurzhinweis.Zeigen(Erinnerung.Text(erledigt, ziel.ZielAnzahl), dauer: Erinnerung.Dauer);
        }
        catch (Exception fehler)
        {
            // wie Python (except Exception): nichts zeigen, beim naechsten Mal neu
            Core.Diagnose.Fehlerprotokoll.Schreiben("Erinnerung: " + fehler.GetType().Name);
        }
        finally
        {
            Volatile.Write(ref _laeuft, 0);
        }
    }
}
