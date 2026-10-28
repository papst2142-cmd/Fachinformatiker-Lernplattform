using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Ton;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 8 W9 (Weisungen P8-29 und P8-32, Nico 4b): Hoerprobe an den Ton-Reglern. Je Regler das passende Beispiel,
/// nie zwei zugleich, Nachlauf und Ausklang, Geraet ausserhalb des Spiels nur waehrend der Probe, Musik = erstes
/// lesbares eigenes Lied (kaputte Datei uebersprungen mit Zeile in fehler.log), sonst die Melodien; 0 % bleibt still.
/// </summary>
public sealed class SpielHoerprobeTests
{
    private static string NeuerOrdner()
    {
        var ordner = Path.Combine(Path.GetTempPath(), "lp_probe_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ordner);
        return ordner;
    }

    [Fact]
    public void ReglerUndSchluessel()
    {
        Assert.Equal(Tonregler.Gesamt, Hoerprobe.Fuer(TonEinstellungen.Gesamt));
        Assert.Equal(Tonregler.Musik, Hoerprobe.Fuer(TonEinstellungen.Musik));
        Assert.Equal(Tonregler.Umgebung, Hoerprobe.Fuer(TonEinstellungen.Umgebung));
        Assert.Null(Hoerprobe.Fuer(TonEinstellungen.MusikOrte));
        Assert.Null(Hoerprobe.Fuer("vollbild"));
        Assert.Equal("hoerprobe:gesamt", Hoerprobe.Schluessel(Tonregler.Gesamt));
        Assert.Equal("hoerprobe:musik", Hoerprobe.Schluessel(Tonregler.Musik));
        Assert.Equal("hoerprobe:umgebung", Hoerprobe.Schluessel(Tonregler.Umgebung));
    }

    /// <summary>Gesamt = Stadtklang und Melodien, Musik = eigenes Lied, Umgebung = Stadtklang (Vogelruf nur Fruehling/Sommer).</summary>
    [Fact]
    public void BeispielJeRegler()
    {
        foreach (var j in Enum.GetValues<Jahreszeit>())
        {
            var gesamt = Hoerprobe.BeispielFuer(Tonregler.Gesamt, j);
            Assert.Equal([Tongruppe.Umgebung, Tongruppe.Musik], gesamt.Select(t => t.Gruppe));
            Assert.Equal(Tonplan.StadtGrundFuer(j), gesamt[0].Schluessel);
            Assert.Equal(Hoerprobe.Melodien, gesamt[1].Schluessel);
            Assert.All(gesamt, t => Assert.True(t.Schleife));

            var musik = Assert.Single(Hoerprobe.BeispielFuer(Tonregler.Musik, j));
            Assert.Equal(Hoerprobe.EigenesLied, musik.Schluessel);
            Assert.Equal(Tongruppe.Musik, musik.Gruppe);

            var umgebung = Hoerprobe.BeispielFuer(Tonregler.Umgebung, j);
            Assert.All(umgebung, t => Assert.Equal(Tongruppe.Umgebung, t.Gruppe));
            Assert.Equal(Tonplan.StadtGrundFuer(j), umgebung[0].Schluessel);
            Assert.Equal(j is Jahreszeit.Fruehling or Jahreszeit.Sommer, umgebung.Any(t => t.Schluessel == "vogelruf"));
        }
    }

    /// <summary>Gleicher Regler stapelt nicht, anderer Regler wechselt; nach dem Ausklang startet ein neues Beispiel.</summary>
    [Fact]
    public void KeinStapelnUndWechsel()
    {
        var lauf = new Probenlauf();
        Assert.Null(lauf.Regler);
        Assert.Equal(Probenwechsel.Neu, lauf.Aenderung(Tonregler.Musik, 0));
        for (var t = 0.1; t < 5; t += 0.1)
        {
            Assert.Equal(Probenwechsel.Weiter, lauf.Aenderung(Tonregler.Musik, t));
        }
        Assert.Equal(1, lauf.Starts);
        Assert.Equal(Probenwechsel.Wechsel, lauf.Aenderung(Tonregler.Umgebung, 5.1));
        Assert.Equal(Tonregler.Umgebung, lauf.Regler);
        Assert.Equal(2, lauf.Starts);
        // ganz ausgeklungen: derselbe Regler startet neu
        var spaeter = 5.1 + Hoerprobe.Nachlauf + Hoerprobe.Ausklang + 0.01;
        Assert.True(lauf.Ausgeklungen(spaeter));
        Assert.Equal(Probenwechsel.Neu, lauf.Aenderung(Tonregler.Umgebung, spaeter));
        Assert.Equal(3, lauf.Starts);
        lauf.Ende();
        Assert.Null(lauf.Regler);
        Assert.Equal(Probenwechsel.Neu, lauf.Aenderung(Tonregler.Gesamt, spaeter + 1));
    }

    /// <summary>Einblenden 0,15 s, voll waehrend der Aenderungen und 1,5 s danach, dann 0,5 s Ausklang; Geraet 3 s spaeter frei.</summary>
    [Fact]
    public void HuelleUndGeraet()
    {
        var lauf = new Probenlauf();
        Assert.Equal(0, lauf.Huelle(0));
        Assert.True(lauf.GeraetFrei(0));
        lauf.Aenderung(Tonregler.Gesamt, 10);
        Assert.Equal(0, lauf.Huelle(10));
        Assert.Equal(0.5, lauf.Huelle(10 + (Hoerprobe.Einblenden / 2)), 6);
        Assert.Equal(1, lauf.Huelle(10.2));
        lauf.Aenderung(Tonregler.Gesamt, 11);
        Assert.Equal(1, lauf.Huelle(11 + Hoerprobe.Nachlauf));
        Assert.Equal(0.5, lauf.Huelle(11 + Hoerprobe.Nachlauf + (Hoerprobe.Ausklang / 2)), 6);
        var aus = 11 + Hoerprobe.Nachlauf + Hoerprobe.Ausklang;
        Assert.Equal(0, lauf.Huelle(aus));
        Assert.True(lauf.Ausgeklungen(aus));
        Assert.False(lauf.GeraetFrei(aus));
        Assert.False(lauf.GeraetFrei(aus + Hoerprobe.GeraetNach - 0.01));
        Assert.True(lauf.GeraetFrei(aus + Hoerprobe.GeraetNach));
    }

    /// <summary>Hoerprobe oeffnet das Geraet auch ausserhalb des Spiels, nie im Hintergrund und nie bei Gesamt 0.</summary>
    [Fact]
    public void SchalterMitHoerprobe()
    {
        var schalter = new Tonschalter();
        Assert.True(schalter.Setzen(hoerprobe: true));
        Assert.Null(schalter.Setzen(imSpiel: true));
        Assert.Null(schalter.Setzen(imSpiel: false));
        Assert.False(schalter.Setzen(hoerprobe: false));
        // 0 %: kein Geraet, still
        Assert.Null(schalter.Setzen(gesamt: 0, hoerprobe: true));
        Assert.False(schalter.Laeuft);
        Assert.True(schalter.Setzen(gesamt: 30));
        Assert.False(schalter.Setzen(hintergrund: true));
        Assert.Equal(0, schalter.Wiederanlaeufe);
    }

    /// <summary>Melodien im Wechsel: erste, Pause, zweite, Pause; andere Rate wird angepasst.</summary>
    [Fact]
    public void MelodienKette()
    {
        var a = new TonDaten([.. Enumerable.Repeat(0.5f, 100)], Mischer.Rate);
        var b = new TonDaten([.. Enumerable.Repeat(-0.25f, 50)], Mischer.Rate / 2);
        var kette = Hoerprobe.Kette(a, b);
        var pause = (int)Math.Round(Hoerprobe.MelodiePause * Mischer.Rate);
        Assert.Equal(Mischer.Rate, kette.Rate);
        Assert.Equal(100 + pause + 100 + pause, kette.Proben.Length);
        Assert.All(kette.Proben.Take(100), p => Assert.Equal(0.5f, p));
        Assert.All(kette.Proben.Skip(100).Take(pause), p => Assert.Equal(0f, p));
        Assert.All(kette.Proben.Skip(100 + pause).Take(100), p => Assert.Equal(-0.25f, p));
        Assert.All(kette.Proben.Skip(200 + pause), p => Assert.Equal(0f, p));
    }

    /// <summary>Erstes eigenes Lied: immer von vorn, kaputte Datei uebersprungen (Zeile in fehler.log), Reihe der Musik unveraendert.</summary>
    [Fact]
    public void ErstesLiedUeberspringtKaputte()
    {
        var ordner = NeuerOrdner();
        try
        {
            var lieder = new EigeneLieder(ordner);
            Assert.Null(lieder.Erstes());
            Directory.CreateDirectory(lieder.Ordner);
            File.WriteAllBytes(Path.Combine(lieder.Ordner, "a_kaputt.mp3"), Encoding.ASCII.GetBytes("kein Ton, nur Text"));
            foreach (var name in new[] { "b.wav", "c.wav" })
            {
                using var wav = File.Create(Path.Combine(lieder.Ordner, name));
                Tondekoder.WavSchreiben(wav, Tondekoder.Sinus(440, 0.05, 44100), 44100);
            }
            for (var i = 0; i < 3; i++)
            {
                var erstes = lieder.Erstes()!.Value;
                erstes.Strom.Dispose();
                Assert.Equal("b.wav", erstes.Name);
            }
            var log = File.ReadAllText(Path.Combine(ordner, Fehlerprotokoll.DateiName));
            Assert.Contains("Ton: eigenes Lied übersprungen (a_kaputt.mp3)", log, StringComparison.Ordinal);
            // die Hoerprobe verschiebt die Reihe der Musik im Spiel nicht
            var naechstes = lieder.Naechstes()!.Value;
            naechstes.Strom.Dispose();
            Assert.Equal("b.wav", naechstes.Name);
            // nur kaputte Dateien: kein Lied, dann spielt die Hoerprobe die Melodien
            File.Delete(Path.Combine(lieder.Ordner, "b.wav"));
            File.Delete(Path.Combine(lieder.Ordner, "c.wav"));
            Assert.Null(lieder.Erstes());
        }
        finally
        {
            Directory.Delete(ordner, true);
        }
    }

    /// <summary>Gesamt 0 % = still, auch waehrend einer Hoerprobe (der Mischer gibt nichts aus).</summary>
    [Fact]
    public void NullProzentStill()
    {
        var mischer = new Mischer();
        mischer.Regler(0f, 0.5f, 0.8f);
        mischer.Starten(new TonDaten([.. Enumerable.Repeat(0.5f, 4096)], Mischer.Rate), Tongruppe.Musik, Tonplan.Melodie, true);
        var puffer = new float[2048];
        mischer.Fuellen(puffer, 2);
        Assert.All(puffer, p => Assert.Equal(0f, p));
    }
}
