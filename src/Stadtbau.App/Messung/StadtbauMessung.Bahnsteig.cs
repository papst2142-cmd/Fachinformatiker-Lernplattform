using Avalonia.Controls;
using Avalonia.Interactivity;
using Lernplattform.Core.Daten;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Welle A 0.70.0 / S 0.46.0 (Weisungen P12-220 und P12-221) mit Nicos RC-Kandidat fuer Stadt 1: am Bahnsteig neben dem
/// Bahnhof Talheim steht „Mit Bahnhof verknüpfen“; Knopf, dann Tipp auf den Bahnhof = ein Schritt, der gelbe Hinweis „hat keinen
/// Bahnsteig mehr“ ist weg (Gegenprobe: Tipp daneben aendert nichts). Messpunkt „Bahnsteig verknuepfen (bis zum Bild)“
/// (wiederholbar, 400 ms). Karte „Laufzeiten“ mit den Buszeiten der sechs Haltestellen. Layout PC und Tablet, hell und dunkel.
/// </summary>
public static partial class StadtbauMessung
{
    /// <summary>Nicos RC-Kandidat fuer Stadt 1 (liegt im Repo).</summary>
    private const string NicoRc = "tests/Stadtbau.Tests/Testdaten/nico/rc_S0.45.0.fistadt";

    private const string KeinBahnsteigTalheim = "„Bahnhof Talheim“ hat keinen Bahnsteig mehr.";

    private static async Task BahnsteigAsync(Hauptansicht a, Window? f)
    {
        if (!File.Exists(NicoRc) || a.Daten is not { } daten)
        {
            Fund("Bahnsteig: Nicos RC-Stadt fehlt (" + Path.GetFullPath(NicoRc) + ")");
            return;
        }
        var p = Projekt.AusStadtDatei(File.ReadAllBytes(NicoRc), daten, a.Ablage, "Starttest Bahnsteig");
        a.ProjektSetzen(p, null, innen: false);
        a.ReiterZeigen(innen: false);
        await a.AussenGeprueft;
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        if (!a.AussenFunde.Any(x => !x.Rot && x.Text == KeinBahnsteigTalheim && x.Tipp.Contains(Texte.BahnsteigVerknuepfenKnopf, StringComparison.Ordinal)))
        {
            Fund("Bahnsteig: der gelbe Hinweis am Bahnhof Talheim nennt den Knopf nicht");
        }
        await AlleGroessenAsync(a, f, "Bahnsteig Pruefliste vorher");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);

        var bahnsteig = GebaeudeStelle(p.AussenStand, "bahnsteig_bahnhof_2");
        a.Karte.Waehlen(bahnsteig);
        await BildAsync(a);
        if (KnopfMitText(a, Texte.BahnsteigVerknuepfenKnopf) is not { IsEffectivelyEnabled: true, IsEffectivelyVisible: true } knopf)
        {
            Fund("Bahnsteig: Knopf „Mit Bahnhof verknüpfen“ fehlt am Bahnsteig");
            return;
        }
        await AlleGroessenAsync(a, f, "Bahnsteig Knopf");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);

        // ein Punkt im Bahnhof Talheim (Tipp auf das Gegenstueck)
        (double X, double Y)? bahnhof = null;
        for (var x = 36.0; x < 46 && bahnhof is null; x += 0.25)
        {
            for (var y = 2.0; y < 9 && bahnhof is null; y += 0.25)
            {
                if (BahnsteigVerknuepfung.Treffer(p.AussenStand, x, y, sucheBahnsteig: false, daten.Fassung) is { Ort: "talheim" })
                {
                    bahnhof = (x, y);
                }
            }
        }
        if (bahnhof is not { } punkt)
        {
            Fund("Bahnsteig: kein Punkt im Bahnhof Talheim gefunden");
            return;
        }

        // Gegenprobe: Tipp daneben aendert nichts, die Wahl bleibt offen
        var schritte = p.Verlauf.Count;
        knopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (!a.BahnsteigWahlOffen)
        {
            Fund("Bahnsteig: nach dem Knopf wartet die Draufsicht nicht auf den Tipp");
        }
        if (a.BahnsteigGetippt(2, 60) != Texte.BahnsteigTippDaneben || p.Verlauf.Count != schritte || !a.BahnsteigWahlOffen)
        {
            Fund("Bahnsteig: Tipp daneben verknuepft oder beendet die Wahl (Gegenprobe)");
        }

        await MessenAsync("Bahnsteig verknuepfen (bis zum Bild)", a, async () =>
        {
            if (!a.BahnsteigWahlOffen)
            {
                a.BahnsteigWahlStarten(GebaeudeStelle(p.AussenStand, "bahnsteig_bahnhof_2"));
            }
            if (a.BahnsteigGetippt(punkt.X, punkt.Y) is { } grund)
            {
                Fund("Bahnsteig: Verknuepfen scheitert: " + grund);
            }
            await BildAsync(a);
        }, async () =>
        {
            p.Rueckgaengig();
            await a.AussenGeprueft;
        });
        if (p.Verlauf.Count != schritte + 1 || BahnsteigVerknuepfung.BahnhofVon(p.AussenStand, GebaeudeStelle(p.AussenStand, "bahnsteig_bahnhof_2")) != "talheim")
        {
            Fund("Bahnsteig: nach dem Verknuepfen nicht genau ein Schritt oder keine Verknuepfung");
        }
        await a.AussenGeprueft;
        await BildAsync(a);
        if (a.AussenFunde.Any(x => x.Text == KeinBahnsteigTalheim))
        {
            Fund("Bahnsteig: nach dem Verknuepfen steht noch „hat keinen Bahnsteig mehr“");
        }
        if (KnopfMitText(a, Texte.BahnsteigLoesenKnopf) is null)
        {
            Fund("Bahnsteig: nach dem Verknuepfen fehlt „Verknüpfung lösen“");
        }
        await AlleGroessenAsync(a, f, "Bahnsteig verknuepft");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);

        // P12-221: Karte „Laufzeiten“ mit Buszeiten (sechs Haltestellen)
        if (a.LaufzeitenListe is not { } wege || !wege.Any(w => w.BusHin is not null || w.BusZurueck is not null))
        {
            Fund("Laufzeiten: in Nicos RC-Stadt nennt die Karte keine Buszeit");
        }
        if (a.LaufzeitenGruppe is { } gruppe)
        {
            gruppe.Offen = true;
            await BildAsync(a);
            await AlleGroessenAsync(a, f, "Laufzeiten mit Bus");
            gruppe.Offen = false;
        }
        Messwerte.Notiere("Bahnsteig und Laufzeiten: Nicos RC-Stadt, Bahnsteig mit Bahnhof Talheim verknuepft (ein Schritt), Gegenprobe Tipp daneben; "
            + (a.LaufzeitenListe?.Count(w => w.BusHin is not null || w.BusZurueck is not null) ?? 0) + " Wege mit Bus");
    }
}
