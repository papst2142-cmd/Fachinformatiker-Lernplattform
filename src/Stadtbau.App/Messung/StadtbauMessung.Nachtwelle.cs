using Avalonia.Controls;
using Avalonia.Interactivity;
using Lernplattform.Messung;
using Lernplattform.Views;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Messung;

/// <summary>
/// Welle S 0.47.0 (Weisung P12-226): W1 das Moebelhaus aus Stadt 1 kommt ueber die Zwischenablage in eine leere Stadt
/// (Knopf „Kopieren (für andere Projekte)“, Knopf „Einfügen“, Vorschau, Tipp = ein Schritt; Gegenprobe Tipp am Rand: Grund,
/// kein Schritt), Messpunkt „Einfuegen (bis zum Bild)“. W2 am roten Fund der Laterne 20 von Stadt 1 „Laterne versetzen“:
/// Vorschlag, Vorschau, „Übernehmen“ = ein Schritt, danach kein roter Fund der Laterne; Messpunkte „Laterne versetzen
/// Knopf (bis zum Bild)“ (die Suche laeuft im Hintergrund, ihre Zeit steht als Angabe) und „Laterne versetzen uebernehmen (bis zum Bild)“. W6 „Frühere Fassungen“ im Fenster „Projekt
/// öffnen“: Fenster mit Bild, „Diese Fassung als neues Projekt öffnen“, Projekt und Sicherung bleiben bytegleich; Messpunkt
/// „Fruehere Fassungen oeffnen (bis zum Bild)“ (einmalig). Layout je Groesse hell und dunkel.
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task NachtwelleAsync(Hauptansicht a, Window? f)
    {
        await EinfuegenAsync(a, f);
        await LaterneVersetzenAsync(a, f);
        await FruehereFassungenAsync(a, f);
    }

    private static async Task EinfuegenAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Kopieren Quelle", null);
        a.ReiterZeigen(innen: false);
        await a.AussenGeprueft;
        a.Karte.Waehlen(OrtStelle("moebelhaus"));
        await BildAsync(a);
        if (KnopfMitText(a, Texte.InZwischenablage) is not { IsEffectivelyEnabled: true } kopieren)
        {
            Fund("Einfuegen: Knopf „" + Texte.InZwischenablage + "“ fehlt am Moebelhaus");
            return;
        }
        kopieren.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (Zwischenablage.Laden(a.Ablage) is not { Anzahl: 1 } z)
        {
            Fund("Einfuegen: nach „Kopieren“ liegt nicht genau ein Stueck in der Zwischenablage");
            return;
        }

        a.NeuAnlegen("Starttest Einfuegen", null, (40, 30));
        a.ReiterZeigen(innen: false);
        await a.AussenGeprueft;
        var p = a.Projekt!;
        a.EinfuegenKnopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (!a.EinfuegenOffen)
        {
            Fund("Einfuegen: nach dem Knopf wartet die Draufsicht nicht auf den Tipp");
            return;
        }
        a.Karte.EinfuegenSchweben(20, 15);
        await BildAsync(a);
        await AlleGroessenAsync(a, f, "Einfuegen Vorschau");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);

        // Gegenprobe: am Rand ragt das Haus ueber die Karte, kein Schritt, die Vorschau bleibt
        var schritte = p.Verlauf.Count;
        if (a.EinfuegenAn(0.5, 0.5) != Texte.EinfuegenAusserhalb || p.Verlauf.Count != schritte || !a.EinfuegenOffen)
        {
            Fund("Einfuegen: Tipp am Rand fuegt ein oder beendet das Einfuegen (Gegenprobe)");
        }

        await MessenAsync("Einfuegen (bis zum Bild)", a, async () =>
        {
            if (!a.EinfuegenOffen)
            {
                a.EinfuegenStarten();
            }
            if (a.EinfuegenAn(20, 15) is { } grund)
            {
                Fund("Einfuegen: scheitert: " + grund);
            }
            await BildAsync(a);
        }, async () =>
        {
            p.Rueckgaengig();
            await a.AussenGeprueft;
        });
        if (p.Verlauf.Count != schritte + 1 || a.Karte.Gewaehlt.Count == 0 || a.EinfuegenOffen)
        {
            Fund("Einfuegen: nicht genau ein Schritt, nichts gewaehlt oder Einfuegen bleibt offen");
        }
        await a.AussenGeprueft;
        await AlleGroessenAsync(a, f, "Eingefuegt");
        Messwerte.Notiere("Einfuegen: Moebelhaus aus Stadt 1 in leere Stadt 40 x 30 (" + z.Anzahl + " Stueck, ein Schritt), Gegenprobe am Rand ohne Schritt");
    }

    private static async Task LaterneVersetzenAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Laterne versetzen", null);
        a.ReiterZeigen(innen: false);
        await a.AussenGeprueft;
        var p = a.Projekt!;
        var fund = a.AussenFunde.FirstOrDefault(x => x.Rot && x.Text == Laterne20Fund);
        if (fund is null || LaterneVersetzen.Nummer(p.AussenStand, fund) is not { } nummer)
        {
            Fund("Laterne versetzen: roter Fund der Laterne 20 fehlt in Stadt 1");
            return;
        }
        await BildAsync(a);
        if (KnopfMitText(a, Texte.LaterneVersetzen) is not { } knopf)
        {
            Fund("Laterne versetzen: Knopf am roten Fund fehlt");
            return;
        }
        knopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (a.LaterneSuche is not { } suche || (await suche).Vorschlag is null || !await BisAsync(a, () => a.Karte.LaterneVorschlag is not null))
        {
            Fund("Laterne versetzen: kein Vorschlag fuer Laterne 20 (" + (a.LaterneSuche is { } s ? (await s).Grund : "keine Suche") + ")");
            return;
        }
        await BildAsync(a);
        await AlleGroessenAsync(a, f, "Laterne versetzen Vorschlag");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);

        // Der Knopf antwortet sofort („Suche freie Stelle …“), die Suche laeuft im Hintergrund (je Kandidat die ganze Pruefung)
        await MessenAsync("Laterne versetzen Knopf (bis zum Bild)", a, async () =>
        {
            knopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await BildAsync(a);
            if (a.LaterneStatus is not { IsEffectivelyVisible: true, Text: Texte.LaterneVersetzenSucht })
            {
                Fund("Laterne versetzen: nach dem Knopf steht nicht sofort „" + Texte.LaterneVersetzenSucht + "“");
            }
        }, async () =>
        {
            if (a.LaterneSuche is { } s)
            {
                await s;
            }
            await BisAsync(a, () => a.Karte.LaterneVorschlag is not null);
        });
        var suchUhr = System.Diagnostics.Stopwatch.StartNew();
        if (a.LaterneSuche is { } letzte)
        {
            await letzte;
        }
        await BisAsync(a, () => a.Karte.LaterneVorschlag is not null);
        Messwerte.Notiere(string.Create(Inv, $"Laterne versetzen: Suche im Hintergrund bis zur Vorschau (Angabe) {suchUhr.Elapsed.TotalMilliseconds:F0} ms nach dem Bild des Knopfs"));
        var suchZeiten = new List<double>();
        for (var i = 0; i < 3; i++)
        {
            suchUhr.Restart();
            await a.LaterneVersetzenSuchenAsync(nummer, null, null);
            await BildAsync(a);
            suchZeiten.Add(suchUhr.Elapsed.TotalMilliseconds);
        }
        Messwerte.Notiere("Laterne versetzen: Knopf bis Vorschau (Angabe) " + string.Join(", ", suchZeiten.Select(z => z.ToString("F0", Inv))) + " ms");
        var schritte = p.Verlauf.Count;
        await MessenAsync("Laterne versetzen uebernehmen (bis zum Bild)", a, async () =>
        {
            if (a.Karte.LaterneVorschlag is null)
            {
                await a.LaterneVersetzenSuchenAsync(nummer, null, null);
            }
            if (a.Karte.LaterneVorschlag is not { } v || !a.LaterneUebernehmen(v))
            {
                Fund("Laterne versetzen: Uebernehmen legt keinen Schritt an");
            }
            await BildAsync(a);
        }, async () =>
        {
            p.Rueckgaengig();
            await a.AussenGeprueft;
            await a.LaterneVersetzenSuchenAsync(nummer, null, null);
        });
        await a.AussenGeprueft;
        if (p.Verlauf.Count != schritte + 1 || a.AussenFunde.Any(x => x.Rot && LaterneVersetzen.Nummer(p.AussenStand, x) == nummer))
        {
            Fund("Laterne versetzen: nicht genau ein Schritt oder die Laterne hat noch einen roten Fund");
        }
        // Gegenprobe: derselbe Vorschlag ein zweites Mal aendert nichts (veraltet)
        if (a.LaterneUebernehmen(new LaterneVersetzen.Vorschlag(nummer, new(0, 0), new(1, 1))) || p.Verlauf.Count != schritte + 1)
        {
            Fund("Laterne versetzen: veralteter Vorschlag legt einen Schritt an (Gegenprobe)");
        }
        await AlleGroessenAsync(a, f, "Laterne versetzt");
        Messwerte.Notiere("Laterne versetzen: Laterne 20 in Stadt 1, Vorschlag und Uebernehmen (ein Schritt), Gegenprobe veraltet");
    }

    private static async Task FruehereFassungenAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Fassungen", null);
        var p = a.Projekt!;
        a.ReiterZeigen(innen: false);
        await a.AussenGeprueft;
        p.Speichern(vonHand: true);
        var zeit = new DateTime(2026, 11, 12, 23, 5, 0);
        var sicherung = new Sicherungen(a.Ablage, p.Name).Anlegen(File.ReadAllBytes(p.Datei), zeit);
        File.SetLastWriteTime(sicherung, zeit);
        var original = File.ReadAllBytes(p.Datei);
        var gesichert = File.ReadAllBytes(sicherung);

        var oeffnen = a.OeffnenAsync();
        if (!await BisAsync(a, () => a.ProjektKaesten.Any(k => k.Datei == p.Datei)))
        {
            Fund("Fruehere Fassungen: Fenster „Projekt öffnen“ zeigt das Projekt nicht");
            Dialog.AlleSchliessen();
            await oeffnen;
            return;
        }
        var knopf = a.ProjektKaesten.First(k => k.Datei == p.Datei).Knoepfe.FirstOrDefault(k => k.Content is TextBlock { Text: var t } && t == Texte.FruehereFassungen);
        if (knopf is null)
        {
            Fund("Fruehere Fassungen: Knopf im Kasten des Projekts fehlt");
            Dialog.AlleSchliessen();
            await oeffnen;
            return;
        }
        await MessenAsync("Fruehere Fassungen oeffnen (bis zum Bild)", a, async () =>
        {
            knopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await BisAsync(a, () => a.FassungKaesten.Count > 0 && a.FassungBilderGeladen is not null);
            if (a.FassungBilderGeladen is { } t)
            {
                await t;
            }
            await BildAsync(a);
        }, einmalig: true);
        var eintrag = a.FassungKaesten.FindIndex(k => k.Datei == sicherung);
        if (eintrag < 0 || a.FassungKaesten.Any(k => k.Bild.Projekt is null || !k.Oeffnen.IsEnabled || k.Bild.Bounds.Width < Hauptansicht.FassungBildBreite - 1))
        {
            Fund("Fruehere Fassungen: Fenster zeigt die Sicherung nicht oder eine Fassung ohne Bild und Knopf ("
                + a.FassungKaesten.Count + " Eintraege)");
            Dialog.AlleSchliessen();
            await oeffnen;
            return;
        }
        var anzahl = a.FassungKaesten.Count;
        await AlleGroessenAsync(a, f, "Fruehere Fassungen");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var neuName = Projekt.NameAusSicherung(p.Name, zeit, a.Ablage);
        await MessenAsync("Fassung als Projekt oeffnen (bis zum Bild)", a, async () =>
        {
            a.FassungKaesten[eintrag].Oeffnen.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await oeffnen;
            await BildAsync(a);
        }, einmalig: true);
        if (a.Projekt?.Name != neuName)
        {
            Fund("Fruehere Fassungen: offen ist „" + a.Projekt?.Name + "“ statt „" + neuName + "“");
        }
        if (!original.AsSpan().SequenceEqual(File.ReadAllBytes(p.Datei)) || !gesichert.AsSpan().SequenceEqual(File.ReadAllBytes(sicherung)))
        {
            Fund("Fruehere Fassungen: Projekt oder Sicherung hat sich geaendert");
        }
        Messwerte.Notiere("Fruehere Fassungen: " + anzahl + " Sicherung(en) mit Bild, als „" + neuName + "“ geoeffnet, Projekt und Sicherung bytegleich");
    }
}
