using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// S 0.31.0 (Weisung P12-140): Farbwahl eines Gebaeudes (Knopf „Farbe“, 12 Farben, eigene Farbe, Tuer mit vier Farben,
/// ein Schritt je Wahl; Gegenprobe Rueckgaengig), Industrie im Gebaeude-Editor (Hallenwand auf dem Drittel-Raster ueber den
/// Strahl, Probehalle ohne Fund; Gegenprobe Wohnbau-Teil in der Halle rot) und „Als Gebäude übernehmen“ (Messpunkt
/// „Als Gebaeude uebernehmen (bis zum Bild)“, wiederholbar, 400 ms; Gegenprobe: ein Effekt hat keinen Knopf).
/// Layout PC und Tablet, hell und dunkel.
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task FarbeAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Farbe", null, (40, 40));
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;
        var s = p.AussenAendern("Wohnung", aa => AussenBearbeitung.Setzen(aa, AussenArt.Gebaeude, 20, 15, raster: true, modell: "wohnung")!);
        a.Karte.Waehlen(s);
        await BildAsync(a);
        if (a.FarbeKnopf is not { } knopf)
        {
            Fund("Farbe: Knopf „" + Texte.FarbeKnopf + "“ fehlt unter „Ausgewähltes Stück“");
            return;
        }
        knopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await BildAsync(a);
        if (a.Farbwahl is not { } w)
        {
            Fund("Farbe: Knopf klappt die Farbwahl nicht auf");
            return;
        }
        w.TeilWaehlen(Gebaeudefarben.Teile[0]);
        await BildAsync(a);
        var kennung = AussenBearbeitung.FarbKennung(p.AussenStand, s)!;
        if (a.Farbwahl!.Felder.Count != Gebaeudefarben.Feste.Length || a.Farbwahl.UebernehmenKnopf is null)
        {
            Fund("Farbe: " + a.Farbwahl.Felder.Count + " Farbfelder statt " + Gebaeudefarben.Feste.Length + " oder kein Knopf fuer die eigene Farbe");
        }
        var schritte = p.SchritteZurueck;
        a.Farbwahl.Felder[3].Knopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await BildAsync(a);
        if (p.SchritteZurueck != schritte + 1 || AussenBearbeitung.FarbeVon(p.AussenStand, kennung, Gebaeudefarben.Teile[0]) is null)
        {
            Fund("Farbe: Wahl einer Farbe ist nicht genau ein Schritt oder wird nicht gespeichert");
        }
        await AlleGroessenAsync(a, f, "Farbe Fassade");
        // Tuer: die vier Tuer-Farben (Nico 10.11.2026), keine eigene Farbe
        a.Farbwahl!.TeilWaehlen(Gebaeudefarben.TuerSchluessel);
        await BildAsync(a);
        if (a.Farbwahl.Felder.Count != Gebaeudefarben.TuerVarianten.Length || a.Farbwahl.UebernehmenKnopf is not null)
        {
            Fund("Farbe Tuer: " + a.Farbwahl.Felder.Count + " Felder statt " + Gebaeudefarben.TuerVarianten.Length);
        }
        await AlleGroessenAsync(a, f, "Farbe Tuer");
        // Gegenprobe: Rueckgaengig nimmt die Farbe wieder weg
        a.Rueckgaengig();
        await BildAsync(a);
        if (AussenBearbeitung.FarbeVon(p.AussenStand, kennung, Gebaeudefarben.Teile[0]) is not null)
        {
            Fund("Farbe: Rueckgaengig nimmt die Farbe nicht weg (Gegenprobe)");
        }
        a.Farbwahl?.TeilWaehlen(Gebaeudefarben.Teile[0]);
        a.Karte.Waehlen(null);
        await BildAsync(a);
    }

    /// <summary>Bildpunkt der Mitte eines Felds der Halle (1/3 Feld) im Editor.</summary>
    private static Point? HallenpunktVon(EntwurfAnsicht e, int x, int y, int ebene)
    {
        if (e.Drei.AktuelleKamera is not { } k)
        {
            return null;
        }
        var fe = Bauteile.HalleMassstab;
        var mitte = new Vector3(((x + 0.5f) * fe) + EntwurfAnsicht3D.Rand, ebene * Bauteile.HalleEbene, ((y + 0.5f) * fe) + EntwurfAnsicht3D.Rand);
        return k.Bildpunkt(mitte, (float)e.Bedienung.Bounds.Width, (float)e.Bedienung.Bounds.Height) is { } b ? new Point(b.X, b.Y) : null;
    }

    private static async Task IndustrieAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Halle", null);
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        if (a.EntwurfOeffnen(null) is not { } e)
        {
            Fund("Industrie: Editor oeffnet nicht");
            return;
        }
        await e.Fertig;
        await BildAsync(a);
        if (e.Drei.GlBereit && e.Drei.AktuelleKamera is not null)
        {
            // Hallenwand ueber den Strahl auf Feld 10,10 des Drittel-Rasters
            e.TeilWaehlen(Bauteile.PraefixHalle + "structure-wall");
            await BildAsync(a);
            if (HallenpunktVon(e, 10, 10, 0) is not { } q || e.Bedienung.SetzenAn(q) < 0)
            {
                Fund("Industrie: Hallenwand laesst sich nicht setzen");
            }
            await e.Fertig;
            var t = e.Entwurf.Teile.LastOrDefault();
            if (!e.Entwurf.Halle || t is null || (t.X, t.Y) != (10, 10))
            {
                Fund("Industrie: Hallenwand nicht auf Feld 10,10 des Drittel-Rasters (" + (t is null ? "keins" : t.X + "," + t.Y) + ")");
            }
            await AlleGroessenAsync(a, f, "Gebaeude entwerfen Halle Wand");
            a.Rueckgaengig();
            await e.Fertig;
            e.TeilWaehlen(null);
        }
        else
        {
            Messwerte.Notiere("Industrie: kein OpenGL auf diesem Rechner, Setzen ueber den Strahl nicht geprueft");
        }
        var halle = Entwurf.Probehalle();
        e.Entwurf.HalleWunsch = true;
        foreach (var t in halle.Teile)
        {
            e.Entwurf.Setzen(t.Id, t.X, t.Y, t.Stockwerk, t.Dreh);
        }
        e.Entwurf.EingangFestlegen(e.Entwurf.Teile.FindIndex(t => t.Name == Bauteile.PraefixHalle + "structure-doorway"));
        await e.Fertig;
        if (e.Funde.Any(x => x.Rot))
        {
            Fund("Probehalle: rote Funde " + string.Join(" | ", e.Funde.Where(x => x.Rot).Select(x => x.Text)));
        }
        Messwerte.Notiere(string.Create(Inv, $"Probehalle: {e.Entwurf.Teile.Count} Teile, {e.Dreiecke} Dreiecke (Grenze {Bauteile.GrenzeDreiecke})"));
        if (e.Dreiecke > Bauteile.GrenzeDreiecke)
        {
            Fund(string.Create(Inv, $"Probehalle: {e.Dreiecke} Dreiecke ueber der Grenze"));
        }
        await AlleGroessenAsync(a, f, "Gebaeude entwerfen Probehalle");
        // Gegenprobe: ein Teil des Wohnbaus in der Halle ist rot
        e.Entwurf.Setzen(Bauteile.Praefix + "building-block", 12, 12, 0, 0);
        await e.Fertig;
        if (!e.Funde.Any(x => x.Rot && x.Text.EndsWith(Texte.EntwurfGemischt, StringComparison.Ordinal)))
        {
            Fund("Industrie: Wohnbau-Teil in der Halle ist nicht rot (Gegenprobe)");
        }
        a.EntwurfSchliessen();
        await BildAsync(a);
    }

    private static KatalogEintrag KatalogHaus => Katalog.Aktiv!.Eintraege.First(k => k.Art == KatalogArt.Modell && k.Paket == "industrial"
        && k.Name.StartsWith("building", StringComparison.Ordinal) && k.Aussen[0] >= 1.5 && k.Aussen[1] >= 1.5);

    private static async Task AlsGebaeudeAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Als Gebaeude", null, (40, 40));
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;
        var k = a.Karte;
        p.AussenAendern("Strasse", aa => Aussen.Landschaft(aa).Add(new PyObjekt
        {
            { "typ", "strasse" }, { "punkte", new List<object?> { new List<object?> { 2.0, 25.0 }, new List<object?> { 38.0, 25.0 } } }, { "gehweg", 0.35 },
        }));
        var e = KatalogHaus;
        var s = p.AussenAendern("Stueck", aa => AussenBearbeitung.Setzen(aa, AussenArt.Katalog, 20, 21, raster: false, modell: e.Id)!);
        await a.AussenGeprueft;
        k.Waehlen(s);
        await BildAsync(a);
        if (TuerKnopfSuchen(a, Texte.AlsGebaeude) is not { } knopf)
        {
            Fund("Als Gebaeude: Knopf „" + Texte.AlsGebaeude + "“ fehlt unter „Ausgewähltes Stück“");
            return;
        }
        await AlleGroessenAsync(a, f, "Als Gebaeude Knopf");
        await MessenAsync("Als Gebaeude uebernehmen (bis zum Bild)", a, () =>
        {
            k.Waehlen(s);
            if (a.AlsGebaeudeUebernehmen(s) is null)
            {
                Fund("Als Gebaeude: Uebernehmen gibt keine Meldung");
            }
            return Task.CompletedTask;
        }, () =>
        {
            a.Rueckgaengig();
            return Task.CompletedTask;
        });
        // MessenAsync nimmt nur zwischen den Versuchen zurueck: der letzte Versuch bleibt stehen
        a.Rueckgaengig();
        await a.AussenGeprueft;
        if (Aussen.KatalogStuecke(p.AussenStand).Count != 1)
        {
            Fund("Als Gebaeude: Rueckgaengig stellt das Katalog-Stueck nicht wieder her");
            return;
        }
        knopf = TuerKnopfSuchen(a, Texte.AlsGebaeude);
        k.Waehlen(s);
        await BildAsync(a);
        (TuerKnopfSuchen(a, Texte.AlsGebaeude) ?? knopf)?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await a.AussenGeprueft;
        await BildAsync(a);
        if (k.Auswahl is not { Art: AussenArt.Gebaeude } g || Aussen.KatalogStuecke(p.AussenStand).Count != 0)
        {
            Fund("Als Gebaeude: nach dem Knopf ist kein Gebaeude gewaehlt oder das Stueck steht noch");
            return;
        }
        if (!GebaeudeTuer.HatTuer(p.AussenStand, g) || p.InnenortVon(g) is null)
        {
            Fund("Als Gebaeude: das Gebaeude hat keine Tuer oder keinen Innenraum");
        }
        if (a.AussenFunde.FirstOrDefault(x => x.Rot) is { } rot)
        {
            Fund("Als Gebaeude: roter Fund: " + rot.Text);
        }
        if (TuerKnopfSuchen(a, Texte.TuerSetzenKnopf) is null || TuerKnopfSuchen(a, Texte.AlsBlueprintSpeichern) is null)
        {
            Fund("Als Gebaeude: „" + Texte.TuerSetzenKnopf + "“ oder „" + Texte.AlsBlueprintSpeichern + "“ fehlt am neuen Gebaeude");
        }
        await AlleGroessenAsync(a, f, "Als Gebaeude uebernommen");
        // Gegenprobe: ein Effekt hat keinen Knopf
        var eff = Katalog.Aktiv!.Eintraege.First(x => x.Art == KatalogArt.Effekt);
        var se = p.AussenAendern("Effekt", aa => AussenBearbeitung.Setzen(aa, AussenArt.Katalog, 8, 8, raster: false, modell: eff.Id)!);
        k.Waehlen(se);
        await BildAsync(a);
        if (TuerKnopfSuchen(a, Texte.AlsGebaeude) is not null)
        {
            Fund("Als Gebaeude: ein Effekt zeigt den Knopf (Gegenprobe)");
        }
        k.Waehlen(null);
        await BildAsync(a);
    }
}
