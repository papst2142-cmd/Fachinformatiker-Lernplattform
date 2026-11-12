using System.Diagnostics;
using Avalonia.Controls;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// S 0.45.0 (Weisung P12-212 Punkte 4 und 5) mit Nicos Teststadt (leere Stadt, Gebaeude wie in Stadt 1): „Als Spielort
/// festlegen“ macht das Moebelhaus zum Ort der Stadt 1 (ein Schritt, Rueckgaengig geht), danach ist „Spielort fehlt“ rot. Gegenproben:
/// der Bahnsteig kann kein Spielort werden; in Stadt 1 fehlt nichts (kein Knopf). Karte „Laufzeiten zu Fuß“ in Stadt 1 mit
/// gelben Wegen ab 60 Minuten. Messpunkte „Spielort festlegen (bis zum Bild)“ und „Laufzeiten zeigen (bis zum Bild)“
/// (wiederholbar, 400 ms). Layout PC und Tablet, hell und dunkel.
/// </summary>
public static partial class StadtbauMessung
{
    /// <summary>Nicos Teststadt (liegt im Repo; der Start-Test laeuft im Repo-Ordner).</summary>
    private const string NicoTeststadt = "tests/Stadtbau.Tests/Testdaten/nico/teststadt_S0.43.0.fistadt";

    private static async Task SpielortAsync(Hauptansicht a, Window? f)
    {
        if (!File.Exists(NicoTeststadt) || a.Daten is not { } daten)
        {
            Fund("Spielort: Nicos Teststadt fehlt (" + Path.GetFullPath(NicoTeststadt) + ")");
            return;
        }
        var p = Projekt.AusStadtDatei(File.ReadAllBytes(NicoTeststadt), daten, a.Ablage, "Starttest Spielort");
        a.ProjektSetzen(p, null, innen: false);
        a.ReiterZeigen(innen: false);
        await a.AussenGeprueft;
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);

        // Gegenprobe: der Bahnsteig wird kein Spielort (Knopf grau mit Grund, Liste bleibt zu)
        var bahnsteig = GebaeudeStelle(p.AussenStand, "bahnsteig_bahnhof_2");
        a.Karte.Waehlen(bahnsteig);
        a.SpielortAuf(bahnsteig);
        if (a.SpielortOffen is not null)
        {
            Fund("Spielort: der Bahnsteig laesst sich als Spielort festlegen (Gegenprobe)");
        }

        // Moebelhaus: Tuer dazu, dann Liste der fehlenden Orte
        var s = GebaeudeStelle(p.AussenStand, "moebelhaus");
        if (Blueprint.ZierwegVon(p.AussenStand, s) is null)
        {
            var platz = GebaeudeTuer.DazuPlatz(p, (PyObjekt)Py.Kopie(p.AussenStand)!, s, true);
            p.AussenAendern(Texte.TuerDazu, aa => GebaeudeTuer.DazuAn(aa, s, true, platz));
        }
        a.Karte.Waehlen(s);
        a.SpielortAuf(s);
        if (a.SpielortOffen != s)
        {
            Fund("Spielort: Liste fuer das Moebelhaus geht nicht auf");
            return;
        }
        await BildAsync(a);
        await AlleGroessenAsync(a, f, "Spielort Liste");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var schritte = p.Verlauf.Count;
        await MessenAsync("Spielort festlegen (bis zum Bild)", a, async () =>
        {
            if (a.SpielortFestlegen(GebaeudeStelle(p.AussenStand, "moebelhaus"), "moebelhaus") is { } grund)
            {
                Fund("Spielort: Festlegen scheitert: " + grund);
            }
            await BildAsync(a);
        }, async () =>
        {
            p.Rueckgaengig();
            await a.AussenGeprueft;
        });
        if (AussenBearbeitung.Ort(p.AussenStand, "moebelhaus") is null || p.Verlauf.Count != schritte + 1)
        {
            Fund("Spielort: nach dem Festlegen fehlt der Ort oder es ist nicht genau ein Schritt");
        }
        if (!p.Orte.Contains("moebelhaus"))
        {
            Fund("Spielort: das Moebelhaus hat keinen Innenraum aus Stadt 1");
        }
        await a.AussenGeprueft;
        await BildAsync(a);
        if (!a.AussenFunde.Any(x => x.Rot && x.Text.Contains("Spielorte fehlen noch", StringComparison.Ordinal)))
        {
            Fund("Spielort: kein roter Fund „Spielorte fehlen“ (Gegenprobe)");
        }
        await AlleGroessenAsync(a, f, "Spielort festgelegt");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        p.Rueckgaengig();
        if (AussenBearbeitung.Ort(p.AussenStand, "moebelhaus") is not null || p.Orte.Contains("moebelhaus"))
        {
            Fund("Spielort: Rueckgaengig laesst Ort oder Innenraum stehen");
        }

        // Stadt 1: kein Knopf (alle Orte da), Laufzeiten mit gelben Wegen
        a.NeuAnlegen("Starttest Laufzeiten", null);
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var q = a.Projekt!;
        if (Spielort.KnopfZeigen(q.AussenStand, GebaeudeStelle(q.AussenStand, "stellwerk"), daten.Fassung))
        {
            Fund("Spielort: in Stadt 1 zeigt das Stellwerk den Knopf (Gegenprobe)");
        }
        var uhr = Stopwatch.StartNew();
        var wege = Laufzeiten.Rechnen(q.Stand(), daten.Inhalt, daten.Grafik);
        Messwerte.Reihe("Laufzeiten rechnen Stadt 1 (Angabe)", uhr.Elapsed.TotalMilliseconds);
        if (a.LaufzeitenListe is not { } liste || liste.Count != wege.Count || wege.Count == 0)
        {
            Fund("Laufzeiten: Liste " + a.LaufzeitenListe?.Count + " Wege statt " + wege.Count);
        }
        if (a.LaufzeitenGruppe is not { } gruppe)
        {
            Fund("Laufzeiten: Karte fehlt");
            return;
        }
        await MessenAsync("Laufzeiten zeigen (bis zum Bild)", a, () =>
        {
            gruppe.Offen = true;
            return Task.CompletedTask;
        }, () =>
        {
            gruppe.Offen = false;
            return BildAsync(a);
        });
        if (!a.AussenFunde.Any(x => !x.Rot && x.Text.Contains("Spielminuten oder länger", StringComparison.Ordinal)))
        {
            Fund("Laufzeiten: kein gelber Hinweis in Stadt 1 (Wege bis " + wege.Max(w => w.Minuten ?? 0) + " Minuten)");
        }
        await AlleGroessenAsync(a, f, "Laufzeiten");
        gruppe.Offen = false;
        Messwerte.Notiere("Spielort und Laufzeiten: Nicos Teststadt, Moebelhaus festgelegt und zurueck, Gegenproben Bahnsteig, Stadt 1 ohne Knopf; "
            + wege.Count + " Wege, " + wege.Count(w => w.Kuerzer >= Laufzeiten.GelbAb) + " ab 60 Minuten");
    }
}
