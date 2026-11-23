using Lernplattform.Core.Daten;
using Lernplattform.Core.Pakete;
using Lernplattform.Core.Update;

namespace Lernplattform.Programm;

/// <summary>
/// Welle A 0.87.0 (Weisung P12-294): die eine Paket-Verwaltung des Programms (Datenordner, Quelle GitHub oder Testquelle
/// <c>LERNPLATTFORM_PAKET_QUELLE</c>, Probe-APK <see cref="PaketQuelle.ProbeOrdner"/>). Wiederholungen bei Stoerungen von GitHub
/// stehen in update.log (wie beim Update). Der erste Zugriff raeumt Reste eines abgebrochenen Ladens weg.
/// </summary>
public static class PaketDienst
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(30) };
    private static PaketVerwaltung? _verwaltung;

    private static readonly Lock VerwaltungSperre = new();

    // Welle A 1.5.0: der erste Zugriff kann beim Start im Hintergrund kommen (Pruefung der Fachrichtungs-Pakete, waehrend die
    // bisherigen Inhalte laden): nur eine Verwaltung, auch bei gleichzeitigem Zugriff
    public static PaketVerwaltung Verwaltung
    {
        get
        {
            if (Volatile.Read(ref _verwaltung) is { } v)
            {
                return v;
            }
            lock (VerwaltungSperre)
            {
                return _verwaltung ??= Neu();
            }
        }
    }

    // Welle A 0.88.0 (Nico 5a): Entfernen gesperrt, solange die Figur in der Stadt des Pakets ist
    // Welle A 0.91.0 (Weisung P12-300): Fachrichtungs-Pakete werden gegen die Inhalte der App geprueft („nur angehaengt“);
    // Entfernen gesperrt, solange ein Pruefungssatz im echten Format dieser Fachrichtung offen ist
    // Welle A 1.5.0 (Lauf 1193): ganz gepruefte Fachrichtungs-Pakete gemerkt je Programmstand (PruefKennung)
    private static PaketVerwaltung Neu() => new(new PaketAblage(Ablage.Datenordner(), Zustand.Eingebaut, PruefKennung),
        () => PaketQuelle.Fuer(Http, zeile => Updateprotokoll.Schreiben(zeile)), Aktualisierung.Aktualisierung.Anzeige)
    {
        EntfernenSperre = kennung => Spiel.SpielStart.EntfernenSperre(kennung) ?? FachrichtungsPaket.EntfernenSperre(kennung, OffenerSatz()),
    };

    /// <summary>
    /// Welle A 1.5.0 (Lauf 1193): Kennung des Programmstands fuer <see cref="PaketAblage.PruefKennung"/>: die Kennungen der
    /// Module von Core (Pruefung, Ladepruefung) und App (eingebaute Inhalte); jeder andere Bau ergibt eine andere Kennung.
    /// </summary>
    internal static string PruefKennung =>
        typeof(Paket).Assembly.ManifestModule.ModuleVersionId.ToString("N") + "/" + typeof(Zustand).Assembly.ManifestModule.ModuleVersionId.ToString("N");

    /// <summary>Kennung des offenen Pruefungssatzes im echten Format (null = keiner); nur lesen.</summary>
    internal static string? OffenerSatz()
    {
        try
        {
            return new Core.Pruefung.Echt.EchtAblage(() => Zustand.Einstellungen.Laden(), _ => false).Laden() is { } z
                ? Core.Pruefung.Json.Text(z["satz"]) : null;
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Rueckfrage vor dem Laden ueber mobile Daten (nur am Handy ohne WLAN, sonst null); true = laden. Ein Weg fuer die Karte in den
    /// Optionen und den Fahrplan am Bahnhof (Welle A 0.88.0).
    /// </summary>
    public static Task<bool>? MobilFrage(PaketZeile zeile)
    {
        if (!MobilFuerTests && Aktualisierung.Aktualisierung.Handy?.ImWlan != false)
        {
            return null;
        }
        return Frage();
        async Task<bool> Frage() => await Views.Dialog.Zeigen(Core.Texte.PaketTexte.MobilTitel,
            Core.Texte.PaketTexte.Format(Core.Texte.PaketTexte.MobilFrage, zeile.Name, Core.Texte.PaketTexte.Mb(zeile.Angebot?.Groesse ?? zeile.Groesse)),
            Core.Texte.PaketTexte.MobilJa, Core.Texte.PaketTexte.MobilNein) == 0;
    }

    /// <summary>Start-Test und Fotos: verhaelt sich wie am Handy ohne WLAN (Rueckfrage vor dem Laden).</summary>
    internal static bool MobilFuerTests { get; set; }

    /// <summary>Start-Test: eine neue Verwaltung (anderer Datenordner oder andere Quelle).</summary>
    internal static void NeuFuerTests() => _verwaltung = null;
}
