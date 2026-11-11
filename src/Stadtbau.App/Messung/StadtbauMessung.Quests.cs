using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Welle Q1 (S 0.40.0, Weisung P12-186): Reiter „Quests“ und „Questgeber“, Verkettungen. Messpunkte (400 ms): „Quests oeffnen
/// (erstes Oeffnen, bis zum Bild)“ und „Questgeber oeffnen (erstes Oeffnen, bis zum Bild)“ einmalig; „Quest anlegen (bis zum
/// Bild)“, „Questgeber setzen (bis zum Bild)“ und „Verkettungen je Questgeber (bis zum Bild)“ wiederholbar. Geprueft: Liste
/// zeigt jede Quest, neue Quest ist rot bis sie fertig ist und danach gruen (Gegenprobe), ein Textfeld uebernimmt beim
/// Verlassen als ein Schritt, vorhandene Quest nur lesen + kopieren (Quelle bleibt), Person per Tipp in die Karte und in den
/// Grundriss stellen (Marke sichtbar), auf ein Haus = rot (Gegenprobe), Escape bricht „Platz wählen“ ab, je Questgeber ohne
/// Kreuzung, Pfeil ziehen setzt „erst nach“ als ein Schritt (Gegenprobe auf eine vorhandene Quest: nichts). Layout je Groesse
/// hell und dunkel.
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task QuestsAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Quests", null);
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;

        // Reiter „Quests“: erstes Oeffnen
        await MessenAsync("Quests oeffnen (erstes Oeffnen, bis zum Bild)", a, () =>
        {
            a.QuestSeiteZeigen(Texte.Quests);
            return Task.CompletedTask;
        }, einmalig: true);
        if (a.QuestSeite != Texte.Quests || a.QuestListeIds.Count != p.Quests().Count || p.Quests().Count == 0
            || a.QuestEditor.Parent is null)
        {
            Fund("Quests: Reiter zeigt " + a.QuestListeIds.Count.ToString(Inv) + " statt " + p.Quests().Count.ToString(Inv) + " Quests");
        }
        await QuestPruefungAbwartenAsync(a);
        await AlleGroessenAsync(a, f, "Quests leer");

        // „Quest anlegen“ (wiederholbar, zuruecksetzen = Rueckgaengig)
        string? neu = null;
        await MessenAsync("Quest anlegen (bis zum Bild)", a, () =>
        {
            a.QuestNeuAnlegen("auswahl");
            neu = a.QuestAuswahl.Quest;
            return Task.CompletedTask;
        }, () =>
        {
            a.Rueckgaengig();
            return Task.CompletedTask;
        });
        if (neu is null || p.Quests().FirstOrDefault(q => q.Id == neu) is not { Bestand: false })
        {
            Fund("Quests: „Neue Quest“ legt keine neue Quest an");
            return;
        }
        await QuestPruefungAbwartenAsync(a);
        if (!a.QuestFundeStand.Any(x => x.Quest == neu && x.Inhalt.Rot))
        {
            Fund("Quests: neue leere Quest ohne roten Fund (Gegenprobe)");
        }

        // Textfeld: Verlassen uebernimmt als ein Schritt, der Editor bleibt
        var schritte = p.SchritteZurueck;
        if (FeldMitNamen(a.QuestEditor, Texte.QuestFeldTitel) is not { } titel)
        {
            Fund("Quests: Feld „Titel“ fehlt im Editor");
        }
        else
        {
            // echter Fokuswechsel: ins Feld, Text aendern, dann auf den naechsten Knopf
            titel.Focus();
            await BildAsync(a);
            titel.Text = "Starttest Netzwerk";
            a.QuestEditor.GetLogicalDescendants().OfType<Button>().First().Focus();
            await BildAsync(a);
            if (p.Quests().First(q => q.Id == neu).Titel != "Starttest Netzwerk" || p.SchritteZurueck != schritte + 1
                || FeldMitNamen(a.QuestEditor, Texte.QuestFeldTitel) != titel)
            {
                Fund("Quests: Titel nicht als ein Schritt uebernommen oder Editor neu gebaut");
            }
        }

        // fertig ausfuellen (Daten), danach ohne roten Fund
        var qg = p.Questgeber().First(q => q.Bestand).Id;
        p.QuestFeld(neu, "frage", "Welches Protokoll vergibt IP-Adressen automatisch?");
        p.QuestFeld(neu, "ticket", new List<object?> { "Der Drucker hat keine Adresse." });
        p.QuestFeld(neu, "hilfe", new List<object?> { "Denk an die automatische Vergabe im Netz." });
        p.QuestFeld(neu, "erklaerung", new List<object?> { "DHCP vergibt Adressen automatisch." });
        p.QuestAendern(neu, Texte.QuestFeldOption, e =>
        {
            Py.Setzen(e, "optionen", new List<object?> { "DHCP", "DNS", "FTP" });
            Py.Setzen(e, "antwort", "DHCP");
        });
        p.QuestUmhaengen(neu, qg);
        await QuestPruefungAbwartenAsync(a);
        foreach (var x in a.QuestFundeStand.Where(x => x.Quest == neu && x.Inhalt.Rot))
        {
            Fund("Quests: fertige Quest noch rot: " + x.Inhalt.Text);
        }
        await AlleGroessenAsync(a, f, "Quests Auswahl");
        // Weisung P12-187 Punkt 3: nichts liegt ueber einem Eingabefeld (kein Tipp-Schild am Feld, kein Text auf dem Feld);
        // Gegenprobe: Hinweise wie vorher als Tipp am Feld
        FelderFrei(a.QuestEditor, "Quests Auswahl");
        Hauptansicht.FeldTippAltFuerTests = true;
        a.QuestOeffnen(neu);
        await BildAsync(a);
        if (FelderFreiFunde(a.QuestEditor).Count == 0)
        {
            Fund("Quests: Gegenprobe Hinweis als Schild am Eingabefeld greift nicht");
        }
        Hauptansicht.FeldTippAltFuerTests = false;
        a.QuestOeffnen(neu);
        await BildAsync(a);
        if (!a.QuestEditor.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == Texte.QuestZeilenTipp))
        {
            Fund("Quests: Hinweis „" + Texte.QuestZeilenTipp + "“ fehlt unter der Beschriftung");
        }
        a.QuestNeuAnlegen("zuordnung");
        await BildAsync(a);
        await AlleGroessenAsync(a, f, "Quests Zuordnung");

        // vorhandene Quest: nur lesen, kopieren legt eine neue an, die Quelle bleibt
        var bestand = p.Quests().FirstOrDefault(q => q.Bestand && QuestDaten.Typen.Contains(q.Typ)) ?? p.Quests().First(q => q.Bestand);
        a.QuestOeffnen(bestand.Id);
        await BildAsync(a);
        if (FeldMitNamen(a.QuestEditor, Texte.QuestFeldTitel) is not null)
        {
            Fund("Quests: vorhandene Quest hat ein Textfeld (soll nur lesbar sein)");
        }
        await AlleGroessenAsync(a, f, "Quests vorhanden");
        if (KnopfMitText(a.QuestEditor, Texte.QuestKopieren) is { IsEnabled: true } kopieren)
        {
            var vorher = Lernplattform.Core.Daten.PythonJson.Standard(bestand.Daten);
            kopieren.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await BildAsync(a);
            if (a.QuestAuswahl.Quest == bestand.Id || p.Quests().FirstOrDefault(q => q.Id == a.QuestAuswahl.Quest) is not { Bestand: false }
                || Lernplattform.Core.Daten.PythonJson.Standard(p.Quests().First(q => q.Id == bestand.Id).Daten) != vorher)
            {
                Fund("Quests: „Als neue Quest kopieren“ legt keine neue Quest an oder aendert die vorhandene");
            }
        }
        else if (QuestDaten.Typen.Contains(bestand.Typ))
        {
            Fund("Quests: „Als neue Quest kopieren“ fehlt bei einer vorhandenen Quest vom Typ " + bestand.Typ);
        }

        // Reiter „Questgeber“
        await MessenAsync("Questgeber oeffnen (erstes Oeffnen, bis zum Bild)", a, () =>
        {
            a.QuestSeiteZeigen(Texte.Questgeber);
            return Task.CompletedTask;
        }, einmalig: true);
        await QuestPruefungAbwartenAsync(a);
        // Stelle im Spiel mit ihrem Namen, nie der Kennung (buero = Buero-Ort der Karte); Gegenprobe alte Anzeige
        var stellen = p.Questgeber().Where(q => q.Bestand && q.Stelle is not null).Select(q => q.Stelle!).Distinct().ToList();
        foreach (var s in stellen.Where(s => a.StelleName(s) == s))
        {
            Fund("Questgeber: Stelle im Spiel zeigt die Kennung „" + s + "“ statt des Namens");
        }
        Hauptansicht.StelleAltFuerTests = true;
        if (!stellen.Any(s => a.StelleName(s) == s))
        {
            Fund("Questgeber: Gegenprobe Kennung statt Name greift nicht");
        }
        Hauptansicht.StelleAltFuerTests = false;
        var person = p.QuestgeberNeu();
        a.QgOeffnen(person);
        await BildAsync(a);
        await AlleGroessenAsync(a, f, "Questgeber eigen");
        FelderFrei(a.QgEditor, "Questgeber eigen");

        // Platz draussen per Tipp in die Karte (Messpunkt, zuruecksetzen = Rueckgaengig)
        var draussen = QuestDraussenPlatz(p);
        await MessenAsync("Questgeber setzen (bis zum Bild)", a, async () =>
        {
            a.QgPlatzWaehlen(person, null);
            await BildAsync(a);
            FingerTipp(a.Karte, a.Karte.Bildpunkt(draussen.X, draussen.Y));
        }, () =>
        {
            a.Rueckgaengig();
            return Task.CompletedTask;
        });
        var gesetzt = p.Questgeber().First(q => q.Id == person).Platz;
        if (gesetzt is not { Draussen: true } || Math.Abs(gesetzt.X - draussen.X) > 0.3 || Math.Abs(gesetzt.Y - draussen.Y) > 0.3
            || !a.Karte.QuestgeberMarken.Any(m => m.Id == person) || a.QuestSeite != Texte.Questgeber)
        {
            Fund("Questgeber: Tipp in die Karte stellt die Person nicht hin oder zeigt keine Marke");
        }
        await QuestPruefungAbwartenAsync(a);
        foreach (var x in a.QuestFundeStand.Where(x => x.Questgeber == person && x.Inhalt.Rot))
        {
            Fund("Questgeber: Person draussen am Weg rot: " + x.Inhalt.Text);
        }
        // Gegenprobe: ausserhalb der Karte = rot
        p.QuestgeberPlatz(person, new QuestgeberDaten.Platz(true, "", 0, -5, -5, 0));
        await QuestPruefungAbwartenAsync(a);
        if (!a.QuestFundeStand.Any(x => x.Questgeber == person && x.Inhalt.Rot))
        {
            Fund("Questgeber: Person ausserhalb der Karte ohne roten Fund (Gegenprobe)");
        }
        a.Rueckgaengig();

        // Escape bricht „Platz wählen“ ab
        a.QgPlatzWaehlen(person, null);
        await BildAsync(a);
        a.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape, Source = a });
        await BildAsync(a);
        if (a.QgSetzenFuer is not null || a.Karte.QuestgeberSetzen is not null || a.QuestSeite != Texte.Questgeber)
        {
            Fund("Questgeber: Escape bricht „Platz wählen“ nicht ab");
        }

        // Platz drinnen per Tipp in den Grundriss
        var innen = QuestInnenPlatz(p);
        if (innen is null)
        {
            Fund("Questgeber: kein freier Platz im Moebelhaus fuer die Probe");
        }
        else
        {
            var zweite = p.QuestgeberNeu();
            a.QgOeffnen(zweite);
            a.QgPlatzWaehlen(zweite, "moebelhaus");
            await BildAsync(a);
            FingerTipp(a.Flaeche, a.Flaeche.BildPunkt(innen.X, innen.Y));
            await BildAsync(a);
            if (p.Questgeber().First(q => q.Id == zweite).Platz is not { Draussen: false, Ort: "moebelhaus" })
            {
                Fund("Questgeber: Tipp in den Grundriss stellt die Person nicht hin");
            }
            a.OrtOeffnen("moebelhaus");
            await BildAsync(a);
            if (!a.Flaeche.QuestgeberMarken.Any(m => m.Id == zweite))
            {
                Fund("Questgeber: keine Marke im Grundriss");
            }
            await AlleGroessenAsync(a, f, "Questgeber im Grundriss");
            a.QgOeffnen(zweite);
            await BildAsync(a);
        }
        p.QuestUmhaengen(neu, person);

        // Verkettungen je Questgeber
        a.GeschichteZeigen();
        await BildAsync(a);
        var z = a.Zusammenhang;
        await MessenAsync("Verkettungen je Questgeber (bis zum Bild)", a, () =>
        {
            z.AnsichtSetzen(true);
            return Task.CompletedTask;
        }, () =>
        {
            z.AnsichtSetzen(false);
            return Task.CompletedTask;
        });
        await BildAsync(a);
        await LesbarJeQuestgeberAsync(a, f, z);
        if (!z.JeQuestgeber || z.Plan is not { } plan || !plan.Kaesten.Any(k => k.Ort == Spielgeschichte.QgGruppe + person))
        {
            Fund("Verkettungen: je Questgeber zeigt die Person nicht");
        }
        else
        {
            z.OffenSetzen(plan.Kaesten.Select(k => k.Ort));
            await BildAsync(a);
            // E1: Personen verweisen kreuz und quer aufeinander (17 Personen, Ketten ueber mehrere Personen); ohne Kreuzung
            // laesst sich das in Spalten nicht immer zeichnen. Kreuzungen sind deshalb nur eine Angabe, Linien durch Kaesten
            // und abgeschnittene Texte bleiben Funde.
            Messwerte.Notiere("Verkettungen je Questgeber alle auf (Angabe): " + z.Plan!.Kreuzungen().Count.ToString(Inv) + " Kreuzungen, "
                + z.Plan.Linien.Count.ToString(Inv) + " Linien");
            foreach (var k in z.Plan.DurchKaesten())
            {
                Fund("Verkettungen je Questgeber: Linie durch einen Kasten: " + k);
            }
            foreach (var t in z.KastenTexte.Select(Abgeschnitten).OfType<string>().Distinct())
            {
                Fund("Verkettungen je Questgeber: Text im Kasten abgeschnitten: " + t);
            }
        }
        await AlleGroessenAsync(a, f, "Verkettungen je Questgeber");

        // Pfeil ziehen: von einer vorhandenen Quest auf die neue = „erst nach“ (ein Schritt); Gegenprobe auf eine vorhandene: nichts
        z.Einpassen();
        await BildAsync(a);
        var zeilen = z.ZeilenOrte;
        var zielZeile = zeilen.FirstOrDefault(r => r.Id == neu);
        var vonZeile = zeilen.FirstOrDefault(r => r.Id != neu && p.Quests().Any(q => q.Id == r.Id && q.Bestand));
        if (zielZeile.Id is null || vonZeile.Id is null)
        {
            Fund("Verkettungen: neue oder vorhandene Quest nicht in den Kaesten fuer „Pfeil ziehen“");
        }
        else
        {
            var vorher = p.SchritteZurueck;
            Ziehen(z, vonZeile.Ort, zielZeile.Ort);
            await BildAsync(a);
            if (p.Quests().First(q => q.Id == neu).Nach != vonZeile.Id || p.SchritteZurueck != vorher + 1 || z.PfeilLaeuft)
            {
                Fund("Verkettungen: Pfeil ziehen setzt „erst nach“ nicht als einen Schritt");
            }
            var bestandNach = p.Quests().First(q => q.Id == vonZeile.Id).Nach;
            Ziehen(z, zielZeile.Ort, vonZeile.Ort);
            await BildAsync(a);
            if (p.Quests().First(q => q.Id == vonZeile.Id).Nach != bestandNach || p.SchritteZurueck != vorher + 1)
            {
                Fund("Verkettungen: Pfeil auf eine vorhandene Quest aendert sie (Gegenprobe)");
            }
        }
        z.AnsichtSetzen(false);
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
        Messwerte.Notiere("Quests: " + p.Quests().Count.ToString(Inv) + " Quests, " + p.Questgeber().Count.ToString(Inv) + " Questgeber geprueft");
    }

    /// <summary>
    /// Weisung P12-187 Punkte 1 und 2: „je Questgeber“ ist beim Oeffnen und nach „Alle aufklappen“ lesbar: kleinste gezeichnete
    /// Schrift mindestens <see cref="ZusammenhangAnsicht.SchriftMindestPx"/> und mindestens so gross wie „je Ort“ bei „Alles
    /// zeigen“ (je Groesse); eigene Erklaerung je Ansicht. Gegenprobe: heutiger Aufbau (eine Spalte, alles einpassen) ist zu klein.
    /// </summary>
    private static async Task LesbarJeQuestgeberAsync(Hauptansicht a, Window? f, ZusammenhangAnsicht z)
    {
        foreach (var (name, b, h) in Groessen)
        {
            await GroesseAsync(a, f, b, h);
            z.AnsichtSetzen(false);
            await BildAsync(a);
            if (z.Erklaerung != Texte.GeschichteZusammenhaengeZeile)
            {
                Fund("Verkettungen je Ort: Erklaerung oben passt nicht zur Ansicht");
            }
            z.Einpassen();
            await BildAsync(a);
            var jeOrt = z.KleinsteSchriftPx;
            z.AlleUmschalten();
            z.Einpassen();
            await BildAsync(a);
            var jeOrtAuf = z.KleinsteSchriftPx;
            z.AlleUmschalten();
            z.AnsichtSetzen(true);
            await BildAsync(a);
            var start = z.KleinsteSchriftPx;
            if (z.Erklaerung != Texte.VerkettungZeileJeQuestgeber)
            {
                Fund("Verkettungen je Questgeber: Erklaerung oben sagt noch „je Ort“");
            }
            z.AlleUmschalten();
            await BildAsync(a);
            var auf = z.KleinsteSchriftPx;
            z.AlleUmschalten();
            await BildAsync(a);
            Messwerte.Notiere("Verkettungen kleinste Schrift " + name + " (Angabe, Grenze " + ZusammenhangAnsicht.SchriftMindestPx.ToString("0.0", Inv)
                + " / aufgeklappt " + ZusammenhangAnsicht.AufgeklapptMindestPx.ToString("0.0", Inv) + " px): je Ort alles " + jeOrt.ToString("0.0", Inv) + " (alle aufgeklappt " + jeOrtAuf.ToString("0.0", Inv)
                + "), je Questgeber Start " + start.ToString("0.0", Inv) + ", alle aufgeklappt " + auf.ToString("0.0", Inv));
            foreach (var (was, px, ort, fest) in new[] { ("beim Oeffnen", start, jeOrt, ZusammenhangAnsicht.SchriftMindestPx),
                ("nach „Alle aufklappen“", auf, jeOrtAuf, ZusammenhangAnsicht.AufgeklapptMindestPx) })
            {
                var grenze = Math.Max(fest, ort);
                if (px < grenze - 0.01)
                {
                    Fund("Verkettungen je Questgeber " + name + " " + was + ": kleinste Schrift " + px.ToString("0.0", Inv) + " px, Grenze "
                        + grenze.ToString("0.0", Inv) + " px");
                }
            }
            if (z.Plan is { } qp && qp.Breite * z.Kamera.Zoom > z.Sicht.Bounds.Width + 0.5)
            {
                Fund("Verkettungen je Questgeber " + name + ": Startansicht breiter als das Fenster");
            }
        }
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        // Gegenprobe: so wie vorher (eine lange Spalte, Start = alles einpassen) ist die Schrift zu klein
        ZusammenhangAnsicht.QuestgeberAltFuerTests = true;
        z.AnsichtSetzen(false);
        z.AnsichtSetzen(true);
        await BildAsync(a);
        var alt = z.KleinsteSchriftPx;
        ZusammenhangAnsicht.QuestgeberAltFuerTests = false;
        Messwerte.Notiere("Verkettungen je Questgeber Gegenprobe alter Aufbau (Angabe): " + alt.ToString("0.0", Inv) + " px");
        if (alt >= ZusammenhangAnsicht.SchriftMindestPx)
        {
            Fund("Verkettungen: Gegenprobe alter Aufbau je Questgeber ist nicht zu klein");
        }
        z.AnsichtSetzen(false);
        z.AnsichtSetzen(true);
        await BildAsync(a);
    }

    /// <summary>Weisung P12-187 Punkt 3: Funde „etwas liegt ueber einem Eingabefeld“ im Editor.</summary>
    private static List<string> FelderFreiFunde(Control wurzel)
    {
        var funde = new List<string>();
        var felder = wurzel.GetVisualDescendants().OfType<TextBox>().Where(t => t.IsEffectivelyVisible && t.Bounds.Width > 0).ToList();
        var texte = wurzel.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.IsEffectivelyVisible && t.Bounds.Width > 0 && !t.GetVisualAncestors().OfType<TextBox>().Any()).ToList();
        foreach (var feld in felder)
        {
            var name = AutomationProperties.GetName(feld) ?? "?";
            if (ToolTip.GetTip(feld) is not null)
            {
                funde.Add("Schild (Tipp) am Eingabefeld „" + name + "“");
            }
            if (feld.TransformToVisual(wurzel) is not { } mf)
            {
                continue;
            }
            var rf = new Rect(feld.Bounds.Size).TransformToAABB(mf);
            foreach (var t in texte)
            {
                if (t.TransformToVisual(wurzel) is { } mt && new Rect(t.Bounds.Size).TransformToAABB(mt).Intersect(rf) is { Width: > 0.5, Height: > 0.5 })
                {
                    funde.Add("Text „" + t.Text + "“ liegt auf dem Eingabefeld „" + name + "“");
                }
            }
        }
        return funde;
    }

    private static void FelderFrei(Control wurzel, string seite)
    {
        foreach (var x in FelderFreiFunde(wurzel).Distinct())
        {
            Fund(seite + ": " + x);
        }
    }

    private static async Task QuestPruefungAbwartenAsync(Hauptansicht a)
    {
        for (var i = 0; i < 600 && a.QuestPruefungLaeuft; i++)
        {
            await Task.Delay(50);
        }
        if (a.QuestPruefungLaeuft)
        {
            Fund("Quests: Pruefung nach 30 s nicht fertig");
        }
        await BildAsync(a);
    }

    private static TextBox? FeldMitNamen(Control wurzel, string name) =>
        wurzel.GetLogicalDescendants().OfType<TextBox>().FirstOrDefault(t => AutomationProperties.GetName(t) == name);

    private static Button? KnopfMitText(Control wurzel, string text) =>
        wurzel.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => b.Content is TextBlock t ? t.Text == text : Equals(b.Content, text));

    /// <summary>Ein Finger zieht von der Mitte einer Zeile auf die Mitte einer anderen (Zusammenhaenge).</summary>
    private static void Ziehen(ZusammenhangAnsicht z, Zusammenhaenge.Flaeche von, Zusammenhaenge.Flaeche auf)
    {
        var a = z.InSicht(new Point(von.X + (von.B / 2), von.MitteY));
        var b = z.InSicht(new Point(auf.X + (auf.B / 2), auf.MitteY));
        var finger = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        Druck(z.Sicht, finger, a);
        for (var i = 1; i <= 5; i++)
        {
            Bewegung(z.Sicht, finger, a + ((b - a) * (i / 5.0)));
        }
        Los(z.Sicht, finger, b);
    }

    /// <summary>Platz draussen: wo die Figur am Start-Ort steht (frei und erreichbar, wie in den Unit-Tests).</summary>
    private static QuestgeberDaten.Platz QuestDraussenPlatz(Projekt p)
    {
        var (sz, start) = Projekt.QuestSzene(p.AussenStand, p.Basis, p.Innenraeume(), p.Daten)!.Value;
        var o = sz.Ort(start)!;
        return new QuestgeberDaten.Platz(true, "", 0, Math.Round(o.StehX, 2), Math.Round(o.StehZ, 2), 0);
    }

    /// <summary>Freier Platz neben der ersten Person im Moebelhaus (ohne Stueck, ohne roten Fund in einer Probe).</summary>
    private static QuestgeberDaten.Platz? QuestInnenPlatz(Projekt p)
    {
        const string ort = "moebelhaus";
        var g = p.Grundriss(ort);
        var personen = p.Personen(ort).Select(Wege.Platz).OfType<(double X, double Y)>().ToList();
        if (personen.Count == 0)
        {
            return null;
        }
        var (px, py) = personen[0];
        foreach (var (dx, dy) in new[] { (1.0, 0.0), (-1.0, 0.0), (0.0, 1.0), (0.0, -1.0), (1.5, 1.5), (-1.5, 1.5), (1.5, -1.5), (-1.5, -1.5), (2.0, 0.0), (0.0, 2.0) })
        {
            if (Bearbeitung.StueckBei(g, px + dx, py + dy) is null)
            {
                return new QuestgeberDaten.Platz(false, ort, 0, Math.Round(px + dx, 2), Math.Round(py + dy, 2), 0);
            }
        }
        return null;
    }
}
