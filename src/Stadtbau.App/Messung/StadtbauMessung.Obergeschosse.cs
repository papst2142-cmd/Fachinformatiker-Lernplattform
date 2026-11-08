using Avalonia.Controls;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Messung;

/// <summary>
/// S 0.19.0 „Obergeschosse eigener Gebaeude“ (Weisung P12-80, Plan P12-51 Abschnitt 2 Punkt 7): Probehaus mit EG und zwei
/// Obergeschossen im Editor bauen, als Blueprint speichern, in Stadt 1 setzen. Jede Etage hat einen Grundriss mit Treppe und
/// keinen roten Fund. Messpunkt „Etagen aus Entwurf (bis zum Bild)“ (wiederholbar, 400 ms): „Vorschlag neu“ an einem
/// Innenraum ohne Etagen bis zum Bild. Gegenproben: Etage groesser als ihre Ebene ist rot; Entwurf geaendert zeigt im Editor
/// den gelben Hinweis mit „Vorschlag neu“. Layout PC und Tablet, hell und dunkel.
/// </summary>
public static partial class StadtbauMessung
{
    private const string TurmName = "Starttest Turm";

    private static async Task ObergeschosseAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Obergeschosse", null);
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var e = a.EntwurfOeffnen(null);
        if (e is null)
        {
            Fund("Obergeschosse: Editor oeffnet nicht");
            return;
        }
        var haus = Entwurf.Probehaus(3);
        foreach (var t in haus.Teile)
        {
            e.Entwurf.Setzen(t.Id, t.X, t.Y, t.Stockwerk, t.Dreh);
        }
        e.Entwurf.EingangFestlegen(e.Entwurf.Teile.FindIndex(t => t.Name == "building-door-window"));
        await e.Fertig;
        if (e.Funde.Count > 0)
        {
            Fund("Obergeschosse: Probehaus mit 2 OG hat Funde " + string.Join(" | ", e.Funde.Select(x => x.Text)));
        }
        e.GanzZeigen();
        await BildAsync(a);
        await AlleGroessenAsync(a, f, "Obergeschosse Entwurf");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        if (a.EntwurfSpeichern(TurmName) is { } grund)
        {
            Fund("Obergeschosse: Speichern ging nicht: " + grund);
            a.EntwurfSchliessen();
            return;
        }
        a.EntwurfSchliessen();
        var bp = a.EigeneBlueprints().FirstOrDefault(b => b.Name == TurmName);
        if (bp?.Innenraum is not { } innen || Etagen.Anzahl(innen) != 2 || Etagen.Uebergaenge(innen, "").Count != 2)
        {
            Fund("Obergeschosse: der Blueprint hat nicht 2 Etagen mit 2 Treppen");
            return;
        }

        // in Stadt 1 setzen (erster Platz ohne neuen roten Fund draussen)
        var p = a.Projekt!;
        AussenStelle? gesetzt = null;
        foreach (var (x, y) in new[] { (18.0, 6.0), (18.0, 4.0), (20.0, 6.0) })
        {
            p.AussenAendern(Texte.BlueprintSetzen, aa => gesetzt = bp.Setzen(aa, x, y, out _));
            if (gesetzt is null)
            {
                continue;
            }
            await a.AussenGeprueft;
            if (!a.AussenFunde.Any(z => z.Rot && z.Text != Laterne20Fund))
            {
                break;
            }
            a.Rueckgaengig();
            await a.AussenGeprueft;
            gesetzt = null;
        }
        if (gesetzt is null)
        {
            Fund("Obergeschosse: kein freier Platz in Stadt 1 ohne roten Fund");
            return;
        }
        var bau = Projekt.BauPraefix + bp.Modell;
        a.ReiterZeigen(innen: true);
        a.OrtOeffnen(bau);
        await BildAsync(a);
        if (p.EtagenVon(bau).Count != 3 || a.EtagenKnoepfe.Count != 3)
        {
            Fund($"Obergeschosse: Innenraum zeigt {a.EtagenKnoepfe.Count} statt 3 Etagen");
        }
        foreach (var o in p.EtagenVon(bau))
        {
            a.OrtOeffnen(o);
            await a.Geprueft;
            await BildAsync(a);
            if (a.Funde.Any(z => z.Rot))
            {
                Fund("Obergeschosse " + p.OrtName(o) + ": rote Funde " + string.Join(" | ", a.Funde.Where(z => z.Rot).Select(z => z.Text)));
            }
            if (Etagen.Einrichtung(p.Grundriss(o)).Count(d => Py.Gleich(d["typ"], Etagen.Treppe)) == 0)
            {
                Fund("Obergeschosse " + p.OrtName(o) + ": keine Treppe");
            }
            await AlleGroessenAsync(a, f, "Obergeschosse " + (Etagen.Teilen(o).Etage == 0 ? "EG" : Etagen.Teilen(o).Etage + ". OG"));
            await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        }

        // Innenraum ohne Etagen (Projekt aus S 0.18.0): gelber Hinweis mit „Vorschlag neu“
        a.OrtOeffnen(bau);
        p.EtagenAendern(bau, "Starttest ohne Etagen", (eg, _) =>
        {
            foreach (var d in Etagen.Einrichtung(eg).Where(d => Py.Hat(d, Etagen.FeldLink)).ToList())
            {
                Liste(Objekt(eg["flur"])["deko"]).Remove(d);
            }
            eg.RemoveAll(x => x.Key is Etagen.FeldEtagen or Etagen.FeldUebergaenge);
        });
        a.OrtOeffnen(bau);
        await a.Geprueft;
        await BildAsync(a);
        if (a.VorschlagNeuKnopf is null)
        {
            Fund("Obergeschosse: ohne Etagen fehlt der Knopf „" + Texte.EtagenVorschlagNeu + "“");
        }
        if (a.Funde.Count(z => !z.Rot && z.Text.Contains("noch nicht begehbar", StringComparison.Ordinal)) != 2)
        {
            Fund("Obergeschosse: ohne Etagen fehlen die gelben Funde „noch nicht begehbar“: " + string.Join(" | ", a.Funde.Select(z => z.Text)));
        }
        await AlleGroessenAsync(a, f, "Obergeschosse Hinweis");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        await MessenAsync("Etagen aus Entwurf (bis zum Bild)", a, async () =>
        {
            await a.EtagenVorschlagNeuFuerTests();
        }, () =>
        {
            a.Rueckgaengig();
            a.OrtOeffnen(bau);
            return Task.CompletedTask;
        });
        await a.Geprueft;
        if (p.EtagenVon(bau).Count != 3 || a.VorschlagNeuKnopf is not null || a.Funde.Any(z => z.Rot))
        {
            Fund($"Obergeschosse: nach „{Texte.EtagenVorschlagNeu}“ {p.EtagenVon(bau).Count} statt 3 Etagen oder Funde: "
                + string.Join(" | ", a.Funde.Select(z => z.Text)));
        }

        // Gegenprobe Regel 5a: das 2. OG groesser als seine Ebene ist rot
        p.EtagenAendern(bau, "Starttest Gegenprobe", (eg, _) => Py.Setzen(Etagen.Gebaeude(eg, 2), "hoehe", Py.F(eg["hoehe"]) + 4));
        if (!p.Pruefen(bau + "@2").Any(z => z.Rot && z.Text.Contains("ragt über die Grundfläche", StringComparison.Ordinal)))
        {
            Fund("Obergeschosse: Etage groesser als ihre Ebene ist nicht rot (Gegenprobe)");
        }
        a.Rueckgaengig();
        if (p.Pruefen(bau + "@2").Any(z => z.Rot))
        {
            Fund("Obergeschosse: nach Rueckgaengig bleibt das 2. OG rot");
        }

        // Editor: Entwurf geaendert (2. OG schmaler), gelber Hinweis mit „Vorschlag neu“, danach keiner
        var weiter = a.EntwurfOeffnen(a.EigeneBlueprints().First(b => b.Name == TurmName));
        if (weiter is null)
        {
            Fund("Obergeschosse: Weiterbauen oeffnet nicht");
            return;
        }
        await weiter.Fertig;
        foreach (var i in Enumerable.Range(0, weiter.Entwurf.Teile.Count).Reverse().Where(i => weiter.Entwurf.Teile[i].Stockwerk >= 2 && weiter.Entwurf.Teile[i].X == 0))
        {
            weiter.Entwurf.Loeschen(i);
        }
        weiter.Entwurf.Setzen(Bauteile.Praefix + "roof-flat-center", 0, 0, 2, 0);
        weiter.Entwurf.Setzen(Bauteile.Praefix + "roof-flat-center", 0, 1, 2, 0);
        await weiter.Fertig;
        await BildAsync(a);
        if (weiter.VorschlagNeuKnopf is null || weiter.Funde.Any(z => z.Rot))
        {
            Fund("Obergeschosse Editor: kein gelber Hinweis mit „" + Texte.EtagenVorschlagNeu + "“ oder rote Funde: " + string.Join(" | ", weiter.Funde.Select(z => z.Text)));
        }
        await AlleGroessenAsync(a, f, "Obergeschosse Entwurf Hinweis");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        weiter.VorschlagNeu();
        await weiter.Fertig;
        if (weiter.VorschlagNeuKnopf is not null)
        {
            Fund("Obergeschosse Editor: nach „" + Texte.EtagenVorschlagNeu + "“ bleibt der Hinweis");
        }
        a.EntwurfSchliessen();
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
    }
}
