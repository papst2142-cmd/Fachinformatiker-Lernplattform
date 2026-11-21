using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Pakete;

namespace Lernplattform.Tests;

/// <summary>
/// Welle „Ablauf stabil“ (Weisungen P12-338/P12-339): Wackelfehler der CI an der Wurzel behoben, je Fund ein Test mit Gegenprobe.
/// Sammelliste 413 (Paket-Probe am Mac): (a) der Abdruck „Datenordner wie vorher“ zaehlte haenger.log mit, in die macOS bei
/// verdecktem Fenster eine Zeile „verdeckt“ schreibt; (b) der Abbruch kam auf langsamen Rechnern erst nach dem Ende des Ladens.
/// </summary>
[Collection("Haenger")]
public sealed class WackelfehlerTests : IDisposable
{
    private readonly string _tmp = Path.Combine(Path.GetTempPath(), "lp_wackel_" + Guid.NewGuid().ToString("N"));

    public WackelfehlerTests() => Directory.CreateDirectory(_tmp);

    public void Dispose()
    {
        OrdnerQuelle.HaltNachErstemBlock = TimeSpan.Zero;
        Directory.Delete(_tmp, true);
    }

    private static string Eintrag(string text) => "=== 2026-11-21 16:24:50 | " + text + " ===\n";

    [Fact]
    public void Abdruck413_HaengerLogVerdecktZaehltNicht_AndereDateiSchon()
    {
        File.WriteAllText(Path.Combine(_tmp, "lernplattform.db"), "db");
        Directory.CreateDirectory(Path.Combine(_tmp, "pakete"));
        var vorher = OrdnerAbdruck.Abdruck(_tmp);
        var haengerVorher = OrdnerAbdruck.HaengerText(_tmp);
        // macOS App Nap: nur haenger.log bekommt einen Eintrag mit Zusatz „verdeckt“ (wie Lauf 1147)
        var verdeckt = Eintrag("Hänger-Diagnose: Hauptfaden seit 5,2 s ohne Rückruf (Seite: optionen, Vorgang: bereit)" + Haenger.VerdecktZusatz);
        File.WriteAllText(Path.Combine(_tmp, Haenger.LogName), verdeckt);
        Assert.Equal(vorher, OrdnerAbdruck.Abdruck(_tmp));
        var neu = OrdnerAbdruck.NeueHaengerEintraege(haengerVorher, OrdnerAbdruck.HaengerText(_tmp));
        Assert.Single(neu);
        Assert.True(OrdnerAbdruck.NurVerdeckt(neu));
        // Gegenprobe 1: der alte Abdruck (alle Dateien) waere ANDERS gewesen (= der rote 1. Lauf)
        Assert.NotEqual(AlterAbdruck(_tmp, vorher), vorher);
        // Gegenprobe 2: eine echte Blockade im sichtbaren Fenster (ohne Zusatz) bleibt rot
        File.AppendAllText(Path.Combine(_tmp, Haenger.LogName), Eintrag("Hänger-Diagnose: Hauptfaden seit 6,0 s ohne Rückruf (Seite: optionen, Vorgang: bereit)"));
        var zwei = OrdnerAbdruck.NeueHaengerEintraege(haengerVorher, OrdnerAbdruck.HaengerText(_tmp));
        Assert.Equal(2, zwei.Count);
        Assert.False(OrdnerAbdruck.NurVerdeckt(zwei));
        // Gegenprobe 3: jede andere Datei (auch ein Rest eines Pakets) aendert den Abdruck weiter
        File.WriteAllText(Path.Combine(_tmp, "pakete", "rest.fipaket"), "x");
        Assert.NotEqual(vorher, OrdnerAbdruck.Abdruck(_tmp));
        // haenger.log tiefer im Baum (nicht die Diagnose-Datei des Programms) zaehlt mit
        File.Delete(Path.Combine(_tmp, "pakete", "rest.fipaket"));
        File.WriteAllText(Path.Combine(_tmp, "pakete", Haenger.LogName), "x");
        Assert.NotEqual(vorher, OrdnerAbdruck.Abdruck(_tmp));
    }

    /// <summary>Abdruck wie bis A 1.2.0 (AutoMessung.DatenAbdruck): alle Dateien.</summary>
    private static string AlterAbdruck(string ordner, string _) => string.Join("\n",
        Directory.GetFileSystemEntries(ordner, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
            .Select(p => Path.GetRelativePath(ordner, p)));

    [Fact]
    public async Task Abbruch413_HaltNachErstemBlock_AbbruchTrifftImmerMittenDrin()
    {
        var quelle = Path.Combine(_tmp, "quelle");
        Directory.CreateDirectory(quelle);
        var datei = Path.Combine(quelle, "probe_testkarte_1.fipaket");
        File.WriteAllBytes(datei, new byte[200_000]);
        var angebot = new PaketAngebot("probe_testkarte", 1, "Probe", "", 200_000, datei, null);
        var ziel = Path.Combine(_tmp, "ziel.fipaket");
        // Mit Halt: auch wenn der Abbruch erst spaet kommt (langsamer Rechner, hier 1,5 s), ist das Laden noch nicht fertig
        OrdnerQuelle.HaltNachErstemBlock = TimeSpan.FromSeconds(60);
        using (var abbruch = new CancellationTokenSource())
        {
            long gesehen = 0;
            var laden = new OrdnerQuelle(quelle).LadenAsync(angebot, ziel, new SofortFortschritt(n => gesehen = n), abbruch.Token);
            await Task.Delay(1500, TestContext.Current.CancellationToken);
            Assert.False(laden.IsCompleted);
            Assert.True(gesehen > 0 && gesehen < 200_000, "erster Block gemeldet, nicht alles: " + gesehen);
            abbruch.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => laden);
        }
        // Gegenprobe: ohne Halt (wie bis A 1.2.0) ist dasselbe Laden nach 1,5 s schon fertig, der Abbruch kaeme zu spaet
        OrdnerQuelle.HaltNachErstemBlock = TimeSpan.Zero;
        using (var spaet = new CancellationTokenSource())
        {
            var laden = new OrdnerQuelle(quelle).LadenAsync(angebot, ziel, null, spaet.Token);
            await Task.Delay(1500, TestContext.Current.CancellationToken);
            Assert.True(laden.IsCompletedSuccessfully);
        }
    }

    private sealed class SofortFortschritt(Action<long> melden) : IProgress<long>
    {
        public void Report(long value) => melden(value);
    }
}
