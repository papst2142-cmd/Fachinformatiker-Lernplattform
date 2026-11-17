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

    public static PaketVerwaltung Verwaltung => _verwaltung ??= Neu();

    private static PaketVerwaltung Neu() => new(new PaketAblage(Ablage.Datenordner()),
        () => PaketQuelle.Fuer(Http, zeile => Updateprotokoll.Schreiben(zeile)), Aktualisierung.Aktualisierung.Anzeige);

    /// <summary>Start-Test: eine neue Verwaltung (anderer Datenordner oder andere Quelle).</summary>
    internal static void NeuFuerTests() => _verwaltung = null;
}
