using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Welle S 0.33.0 (Weisungen P12-146, P12-148, Sammelliste 272, 273). Stadt 1: am Kaufhaus vor das Schaufenster tippen, mit
/// „Einrasten“ rueckt die Tuer auf die naechste glatte Stelle, danach kein roter Tuer-Fund. Fund antippen: in der Draufsicht
/// (Messpunkt „Fund antippen (bis zum Bild)“, wiederholbar, 400 ms) steht die Stelle danach mittig, in 3D faehrt die Kamera
/// hin und die Neigung bleibt, im Grundriss (Elektromarkt, Fund 216) steht die Stelle mittig, im Editor zeigt er die Ebene des
/// Teils und waehlt es. Gegenprobe je Ansicht: Springen aus (<see cref="FundListe.SpringenAusFuerTests"/>) = Ansicht bleibt.
/// Ein Fund ohne Stelle ist kein Knopf und hat einen Namen.
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task TuerFassadeAsync(Hauptansicht a, Window? f)
    {
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        a.NeuAnlegen("Starttest Tuer frei setzen", null);
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
        var p = a.Projekt!;
        var k = a.Karte;

        // 272: Kaufhaus, Tipp vor das Schaufenster (Nicos Fund 06/07)
        var kaufhaus = new AussenStelle(AussenArt.Haus, -1, 0, "kaufhaus");
        var name = Lernplattform.Core.Spiel.Py.Str(AussenBearbeitung.Ort(p.AussenStand, "kaufhaus")!["name"]);
        k.Raster = true;
        k.Waehlen(kaufhaus);
        k.TuerSetzenStarten(kaufhaus, still: true);
        if (k.FassadeJetzt() is null)
        {
            Fund("Tuer frei setzen: keine Fassade fuer das Kaufhaus");
        }
        else
        {
            k.Hinfahren(29.6, 21.1);
            await BildAsync(a);
            var schritte = p.SchritteZurueck;
            FingerTipp(k, k.Bildpunkt(29.6, 21.1));
            await a.AussenGeprueft;
            if (p.SchritteZurueck != schritte + 1)
            {
                Fund("Tuer frei setzen: Tipp vor das Schaufenster setzt keine Tuer");
            }
            else if (a.AussenFunde.FirstOrDefault(x => x.Rot && x.Text.StartsWith("Die Tür von „" + name + "“", StringComparison.Ordinal)) is { } rot)
            {
                Fund("Tuer frei setzen: nach dem Einrasten roter Fund: " + rot.Text);
            }
            await AlleGroessenAsync(a, f, "Tuer frei setzen");
            await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
            a.Rueckgaengig();
            await a.AussenGeprueft;
        }
        k.TuerSetzenBeenden();
        k.Waehlen(null);
        await BildAsync(a);

        // 273: Fund ohne Stelle ist kein Knopf und hat einen Namen
        var ohne = FundListe.Eintrag(new StadtbauFund("Probe ohne Stelle", "Tipp", true), null);
        if (ohne is Button || string.IsNullOrEmpty(Avalonia.Automation.AutomationProperties.GetName(ohne)))
        {
            Fund("Fund antippen: Fund ohne Stelle ist ein Knopf oder hat keinen Namen");
        }

        // Draufsicht: Laterne 20 der Stadt 1 ist rot (Sammelliste 220) und hat eine Stelle
        if (FundKnopf(a.AussenFundEintraege) is not { } knopf)
        {
            Fund("Fund antippen: draussen kein antippbarer Fund in Stadt 1");
            return;
        }
        var fund = (StadtbauFund)knopf.Tag!;
        if (string.IsNullOrEmpty(Avalonia.Automation.AutomationProperties.GetName(knopf)))
        {
            Fund("Fund antippen: Eintrag ohne Namen fuer den Screenreader");
        }
        await MessenAsync("Fund antippen (bis zum Bild)", a, () =>
        {
            knopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            return Task.CompletedTask;
        }, () =>
        {
            k.Einpassen();
            return Task.CompletedTask;
        });
        if (!Mittig(k, fund.X!.Value, fund.Y!.Value))
        {
            Fund(string.Create(Inv, $"Fund antippen Draufsicht: Stelle {fund.X:F1}/{fund.Y:F1} steht nicht mittig"));
        }
        if (AussenBearbeitung.StueckBei(p.AussenStand, fund.X.Value, fund.Y.Value, 0.6) is { } st && k.Auswahl != st)
        {
            Fund("Fund antippen Draufsicht: Stueck an der Stelle nicht gewaehlt");
        }
        await AlleGroessenAsync(a, f, "Fund antippen");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        // Gegenprobe: Springen aus = Ansicht bleibt
        k.Einpassen();
        await BildAsync(a);
        var vorher = k.Versatz;
        FundListe.SpringenAusFuerTests = true;
        try
        {
            knopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await BildAsync(a);
            if (k.Versatz != vorher || Mittig(k, fund.X.Value, fund.Y.Value))
            {
                Fund("Fund antippen Draufsicht: Gegenprobe (Springen aus) hat die Ansicht bewegt");
            }
        }
        finally
        {
            FundListe.SpringenAusFuerTests = false;
        }

        // 3D: Kamera faehrt hin, Neigung bleibt
        a.DreiZeigen();
        await a.DreiFertig;
        if (!a.Drei.GlBereit || a.Drei.AktuelleKamera is null)
        {
            Messwerte.Notiere("Fund antippen 3D: kein OpenGL auf diesem Rechner, nicht geprueft");
        }
        else
        {
            a.Bedienung3D.GanzeStadt();
            await BildAsync(a);
            var kam = a.Drei.AktuelleKamera;
            var neigung = kam.Neigung;
            var ziel = kam.Ziel;
            FundListe.SpringenAusFuerTests = true;
            try
            {
                knopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await BildAsync(a);
                if (Vector3.Distance(kam.Ziel, ziel) > 0.001f)
                {
                    Fund("Fund antippen 3D: Gegenprobe (Springen aus) hat die Kamera bewegt");
                }
            }
            finally
            {
                FundListe.SpringenAusFuerTests = false;
            }
            knopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await BildAsync(a);
            if (Math.Abs(kam.ZielX - fund.X.Value) > 0.01 || Math.Abs(kam.ZielZ - fund.Y.Value) > 0.01)
            {
                Fund(string.Create(Inv, $"Fund antippen 3D: Kamera steht bei {kam.ZielX:F2}/{kam.ZielZ:F2} statt {fund.X:F2}/{fund.Y:F2}"));
            }
            if (Math.Abs(kam.Neigung - neigung) > 0.001f)
            {
                Fund("Fund antippen 3D: Neigung hat sich geaendert");
            }
            await AlleGroessenAsync(a, f, "Fund antippen 3D");
            await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        }
        a.ReiterZeigen(innen: false);
        await BildAsync(a);

        // Grundriss: Elektromarkt (Fund 216, Waren oben nicht erreichbar)
        a.NeuAnlegen("Starttest Fund innen", "elektromarkt");
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: true);
        a.OrtOeffnen("elektromarkt");
        await a.Geprueft;
        await BildAsync(a);
        if (FundKnopf(a.InnenFundEintraege) is not { } innen)
        {
            Fund("Fund antippen Grundriss: kein antippbarer Fund im Elektromarkt");
        }
        else
        {
            var fi = (StadtbauFund)innen.Tag!;
            a.Flaeche.Einpassen();
            await BildAsync(a);
            var vorherInnen = a.Flaeche.Versatz;
            FundListe.SpringenAusFuerTests = true;
            try
            {
                innen.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await BildAsync(a);
                if (a.Flaeche.Versatz != vorherInnen)
                {
                    Fund("Fund antippen Grundriss: Gegenprobe (Springen aus) hat die Ansicht bewegt");
                }
            }
            finally
            {
                FundListe.SpringenAusFuerTests = false;
            }
            innen.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await BildAsync(a);
            var g = a.Flaeche;
            var hoehe = Math.Max(48, g.Bounds.Height - g.UntenFrei);
            var px = g.Versatz.X + (fi.X!.Value * g.Zoom);
            var py = g.Versatz.Y + (fi.Y!.Value * g.Zoom);
            if (Math.Abs(px - (g.Bounds.Width / 2)) > 2 || Math.Abs(py - (hoehe / 2)) > 2)
            {
                Fund(string.Create(Inv, $"Fund antippen Grundriss: Stelle {fi.X:F1}/{fi.Y:F1} steht nicht mittig"));
            }
            await AlleGroessenAsync(a, f, "Fund antippen Grundriss");
            await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        }

        // Editor: Teil in der Luft auf dem 1. OG (rot), Antippen zeigt Ebene 1 und waehlt das Teil
        a.ReiterZeigen(innen: false);
        if (a.EntwurfOeffnen(null) is not { } e)
        {
            Fund("Fund antippen Editor: Editor oeffnet nicht");
            return;
        }
        await e.Fertig;
        var probe = Entwurf.Probehaus().Teile[0];
        e.Entwurf.Setzen(probe.Id, 3, 3, 1, 0);
        await e.Fertig;
        await BildAsync(a);
        e.EbeneSetzen(0);
        if (FundKnopf(e.FundEintraege) is not { } teilKnopf)
        {
            Fund("Fund antippen Editor: kein antippbarer Fund fuer ein Teil in der Luft");
        }
        else
        {
            FundListe.SpringenAusFuerTests = true;
            try
            {
                teilKnopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (e.Ebene != 0 || e.Auswahl >= 0)
                {
                    Fund("Fund antippen Editor: Gegenprobe (Springen aus) hat Ebene oder Auswahl geaendert");
                }
            }
            finally
            {
                FundListe.SpringenAusFuerTests = false;
            }
            teilKnopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await BildAsync(a);
            var i = e.Entwurf.Teile.FindIndex(t => t.X == 3 && t.Y == 3 && t.Stockwerk == 1);
            if (e.Ebene != 1 || e.Auswahl != i)
            {
                Fund(string.Create(Inv, $"Fund antippen Editor: Ebene {e.Ebene}, Auswahl {e.Auswahl} statt Ebene 1, Teil {i}"));
            }
            await AlleGroessenAsync(a, f, "Fund antippen Editor");
            await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        }
        a.EntwurfSchliessen();
        await BildAsync(a);
    }

    /// <summary>Erster Eintrag der Liste, der ein Knopf mit Fund und Stelle ist.</summary>
    private static Button? FundKnopf(IReadOnlyList<Control> eintraege) =>
        eintraege.OfType<Button>().FirstOrDefault(b => b.Tag is StadtbauFund { X: not null, Y: not null });

    /// <summary>Stelle (x, y) mittig zwischen Werkzeugleiste und Kamera-Karte (wie <see cref="KartenFlaeche.Hinfahren"/>), 2 px.</summary>
    private static bool Mittig(KartenFlaeche k, double x, double y)
    {
        var b = k.Bildpunkt(x, y);
        var hoehe = Math.Max(48, k.Bounds.Height - k.UntenFrei - k.ObenFrei);
        return Math.Abs(b.X - (k.Bounds.Width / 2)) <= 2 && Math.Abs(b.Y - (k.ObenFrei + (hoehe / 2))) <= 2;
    }
}
