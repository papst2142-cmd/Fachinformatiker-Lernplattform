using Avalonia.Threading;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Texte;

namespace Lernplattform.Programm;

/// <summary>
/// Phase 2, L10: verbindet die Abgleich-Steuerung (Core.Daten.AbgleichSteuerung) mit dem
/// Programm. PC: Start nach 1,5 s, 60 s nach dem Lernen, beim Beenden (höchstens 12 s).
/// Handy: beim Start, 30 s nach dem Lernen, beim Verlassen der App. Im Start ohne Bedienung
/// (LERNPLATTFORM_MESSUNG_AUTO=1) geht das Programm nie ins Netz (wie FISI_SELFTEST).
/// </summary>
public static class AbgleichDienst
{
    private static readonly bool Android = OperatingSystem.IsAndroid();
    private static AbgleichSteuerung? _steuerung;

    /// <summary>Nach jedem Abgleich (Ergebnis oder Fehler), auf dem Oberflächen-Faden.</summary>
    public static event Action<AbgleichErgebnis?, AbgleichAusnahme?>? Fertig;

    /// <summary>Letztes Ergebnis, falls die Optionen erst danach gebaut werden (Python _sync_status).</summary>
    public static (AbgleichErgebnis? Ergebnis, AbgleichAusnahme? Fehler)? Letztes { get; private set; }

    private static bool Aus => Views.MainView.AutoModus;

    /// <summary>Gerät in der Commit-Nachricht und in der Sicherung: „PC &lt;Rechnername&gt;“ bzw. „Handy“.</summary>
    public static string Geraet()
    {
        if (Android)
        {
            return Geraetetexte.Geraet(false, "");
        }
        try
        {
            return Geraetetexte.Geraet(true, System.Net.Dns.GetHostName());
        }
        catch (System.Net.Sockets.SocketException)
        {
            return Geraetetexte.Geraet(true, "");
        }
    }

    private static AbgleichSteuerung Steuerung => _steuerung ??= new AbgleichSteuerung(
        Android ? AbgleichSteuerung.Handy : AbgleichSteuerung.Pc,
        () => Abgleich.AutoAn(Zustand.Einstellungen.Laden()),
        Arbeit,
        (ergebnis, fehler) => Dispatcher.UIThread.Post(() =>
        {
            Zustand.EinstellungenNeuLesen();       // sync_last
            Letztes = (ergebnis, fehler);
            Fertig?.Invoke(ergebnis, fehler);
        }));

    private static AbgleichErgebnis Arbeit()
    {
        Core.Spiel.Spielschlange.AlleLeeren();   // Weisung P7-17: Spielschritte zuerst fertig schreiben
        using var netz = new GitHubZugang();
        return Abgleich.Ausfuehren(Zustand.Lernstand, Zustand.Einstellungen, Geraet(), netz, Zustand.Umbenennungen);
    }

    /// <summary>auto_start.</summary>
    public static void Starten()
    {
        if (!Aus)
        {
            Steuerung.Starten();
        }
    }

    /// <summary>„Speichern und abgleichen“ (auch ohne Automatik).</summary>
    public static void Jetzt()
    {
        if (!Aus)
        {
            Steuerung.Abgleichen();
        }
    }

    /// <summary>schedule: nach jeder gespeicherten Antwort.</summary>
    public static void NachDemLernen()
    {
        if (!Aus)
        {
            Steuerung.NachDemLernen();
        }
    }

    /// <summary>Handy: App geht in den Hintergrund.</summary>
    public static void Verlassen()
    {
        if (!Aus)
        {
            Steuerung.Verlassen();
        }
    }

    /// <summary>PC: beim Beenden, nachdem die Eingaben gespeichert sind (wartet höchstens 12 s).</summary>
    public static void VorDemBeenden()
    {
        if (!Aus && _steuerung is { } s)
        {
            s.VorDemBeenden();
        }
    }

    public static bool Laeuft => _steuerung?.Laeuft ?? false;
}
