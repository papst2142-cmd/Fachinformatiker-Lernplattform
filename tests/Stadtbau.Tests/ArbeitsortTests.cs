using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Tests;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Welle E4 „Aufgaben an Gebäuden“ (S 0.53.0, Weisung P12-247, Nico 1a 2a 3c 4b): Arbeitsort einer neuen Quest im Werkzeug.
/// Geprueft: setzen, wechseln (auch per Ziehen: dieselbe Methode), Raum, Stelle, Schalter „wartet“ und entfernen sind je
/// genau ein Schritt, gleicher Wert und ungueltiges Ziel sind keiner; nur Haeuser mit Ort und Innenraum (Buero, Kunden,
/// Geschaefte, Schalter) gehen, draussen und selbst gebaute nicht; vorhandene Quests bekommen keinen (1a, gesperrt mit
/// Grund); jede Pruefregel rot mit Gegenprobe gruen und rot sperrt den Export; die Zahl am Haus zaehlt den Arbeitsort; die
/// Projektdatei hat Format 4 nur mit Arbeitsort (Gegenprobe 3).
/// </summary>
public class ArbeitsortTests
{
    private static PyObjekt Inhalt => Daten.Spiel.Inhalt;

    private static (Projekt P, string Id) MitQuest(string qg = "petra")
    {
        var p = QuestTests.Stadt1();
        var id = QuestTests.GuteAuswahl(p, qg);
        return (p, id);
    }

    private static Questebene.Arbeitsort? Lesen(Projekt p, string id) =>
        QuestDaten.NeueQuest(p.AussenStand, id) is { } q ? Questebene.ArbeitsortLesen(q) : null;

    private static List<string> Rot(Projekt p, string id) => [.. p.QuestFunde().Where(f => f.Rot && f.Quest == id).Select(f => f.Text)];

    [Fact]
    public void Arbeitsorte_NurHaeuserMitOrtUndInnenraum()
    {
        var p = QuestTests.Stadt1();
        var orte = p.Arbeitsorte().Select(w => w.Ort).ToList();
        Assert.Contains(SpielDaten.OrtBuero, orte);
        Assert.Contains("talheim", orte);
        Assert.Contains("talheim_nord", orte);
        Assert.Contains("moebelhaus", orte);
        Assert.Contains("bank", orte);
        // nicht: draussen, Serverfarm, Wohnung, Filiale, selbst gebaute Gebaeude
        foreach (var nicht in new[] { Questebene.OrtDraussen, "serverfarm", SpielDaten.OrtZuhause, SpielDaten.OrtFiliale, "bau:irgendwas" })
        {
            Assert.DoesNotContain(nicht, orte);
        }
        Assert.Equal(Questebene.ArbeitsortArt.Kunde, p.Arbeitsorte().Single(w => w.Ort == "talheim_nord").Art);
        Assert.Equal(12, p.Arbeitsorte().Single(w => w.Ort == "talheim_nord").FreiAb);
    }

    [Fact]
    public void Setzen_EinSchritt_ErsteFreieStelleImErstenRaum_Rueckgaengig()
    {
        var (p, id) = MitQuest();
        Assert.Empty(Rot(p, id));
        var schritte = p.SchritteZurueck;
        Assert.True(p.QuestArbeitsort(id, SpielDaten.OrtBuero));
        Assert.Equal(schritte + 1, p.SchritteZurueck);
        var a = Lesen(p, id)!;
        Assert.Equal(SpielDaten.OrtBuero, a.Ort);
        Assert.True(a.Wartet);
        var g = p.ArbeitsortGrundriss(a.Ort, 0)!;
        var personen = Questebene.ArbeitsortPersonen(a.Ort, 0, Inhalt, p.Innenraeume(), Aussen.Stadtdaten(p.AussenStand), "petra");
        // der erste Raum mit einer freien Stelle (im Chefbuero steht Sabine Kranich mitten im Raum)
        Assert.Equal(Questebene.Raeume(g).Select(r => r.Id).First(r => Questebene.ErsteFreieStelle(g, r, personen) is not null), a.Raum);
        Assert.Equal(Questebene.ErsteFreieStelle(g, a.Raum, personen), (a.X, a.Y));
        Assert.Contains(Wege.Zelle(a.X, a.Y), Questebene.Stellen(g, personen));
        Assert.Empty(Rot(p, id));
        // gleicher Ort: kein Schritt
        Assert.True(p.QuestArbeitsort(id, SpielDaten.OrtBuero));
        Assert.Equal(schritte + 1, p.SchritteZurueck);
        // wechseln (wie Ziehen auf ein anderes Haus): Raum und Stelle springen auf den ersten Raum dort
        Assert.True(p.QuestArbeitsort(id, "talheim"));
        Assert.Equal(schritte + 2, p.SchritteZurueck);
        Assert.NotEqual("", Lesen(p, id)!.Raum);
        Assert.True(p.Rueckgaengig());
        Assert.Equal(SpielDaten.OrtBuero, Lesen(p, id)!.Ort);
        Assert.True(p.Rueckgaengig());
        Assert.Null(Lesen(p, id));
    }

    [Theory]
    [InlineData("draussen")]
    [InlineData("serverfarm")]
    [InlineData("zuhause")]
    [InlineData("bau:haus1")]
    [InlineData("gibtsnicht")]
    public void UngueltigesZiel_KeinSchritt(string ort)
    {
        var (p, id) = MitQuest();
        p.QuestArbeitsort(id, SpielDaten.OrtBuero);
        var vorher = PythonJson.Text(QuestDaten.NeueQuest(p.AussenStand, id)!);
        var schritte = p.SchritteZurueck;
        Assert.False(p.QuestArbeitsort(id, ort));
        Assert.Equal(schritte, p.SchritteZurueck);
        Assert.Equal(vorher, PythonJson.Text(QuestDaten.NeueQuest(p.AussenStand, id)!));
    }

    [Fact]
    public void Bestand_KeinArbeitsort_GesperrtMitGrund()
    {
        var p = QuestTests.Stadt1();
        var bestand = p.Quests().First(q => q.Bestand && QuestDaten.Typen.Contains(q.Typ));
        var anderer = p.Questgeber().First(x => x.Id != bestand.Questgeber).Id;
        var schritte = p.SchritteZurueck;
        Assert.False(p.QuestArbeitsort(bestand.Id, SpielDaten.OrtBuero));
        Assert.Equal(schritte, p.SchritteZurueck);
        Assert.False(Py.Hat(bestand.Daten, Questebene.FeldArbeitsort));
        Assert.Contains("nur bei neuen Quests", Texte.QuestArbeitsortBestand, StringComparison.Ordinal);
        // von Hand in die Datei geschrieben: rot (Umhaengen hat nur Kennung und Questgeber)
        p.AussenAendern("von Hand", a =>
        {
            QuestDaten.Umhaengen(Inhalt, a, bestand.Id, anderer);
            var e = QuestDaten.Liste(a).OfType<PyObjekt>().Single(x => Py.Gleich(x["id"], bestand.Id));
            Py.Setzen(e, Questebene.FeldArbeitsort, new PyObjekt { { "ort", "buero" }, { "raum", "lager" }, { "etage", 0L }, { "x", 1.0 }, { "y", 1.0 }, { "wartet", true } });
            return true;
        });
        Assert.Contains(p.QuestFunde(), f => f.Rot && f.Quest == bestand.Id);
    }

    [Fact]
    public void Raum_Stelle_Wartet_Entfernen_JeEinSchritt()
    {
        var (p, id) = MitQuest();
        p.QuestArbeitsort(id, SpielDaten.OrtBuero);
        var s = p.SchritteZurueck;
        Assert.True(p.QuestArbeitsortRaum(id, "werkstatt"));
        Assert.Equal(s + 1, p.SchritteZurueck);
        var a = Lesen(p, id)!;
        Assert.Equal("werkstatt", a.Raum);
        var g = p.ArbeitsortGrundriss(SpielDaten.OrtBuero, 0)!;
        Assert.Equal("werkstatt", Py.Str(Wege.RaumBei(a.X, a.Y, g)!["id"]));
        // Raum gibt es nicht: kein Schritt
        Assert.False(p.QuestArbeitsortRaum(id, "gibtsnicht"));
        Assert.Equal(s + 1, p.SchritteZurueck);
        // Stelle: naechste erreichbare Zelle im Raum
        Assert.True(p.QuestArbeitsortStelle(id, a.X + 0.6, a.Y));
        Assert.Contains(Wege.Zelle(Lesen(p, id)!.X, Lesen(p, id)!.Y), Questebene.Erreichbar(g));
        Assert.Equal("werkstatt", Py.Str(Wege.RaumBei(Lesen(p, id)!.X, Lesen(p, id)!.Y, g)!["id"]));
        s = p.SchritteZurueck;
        Assert.True(p.QuestArbeitsortWartet(id, false));
        Assert.Equal(s + 1, p.SchritteZurueck);
        Assert.False(Lesen(p, id)!.Wartet);
        Assert.True(p.QuestArbeitsortWartet(id, false));
        Assert.Equal(s + 1, p.SchritteZurueck);
        // Ortswechsel behaelt „wartet“
        p.QuestArbeitsort(id, "elektromarkt");
        Assert.False(Lesen(p, id)!.Wartet);
        Assert.Equal("", Lesen(p, id)!.Raum);
        Assert.Empty(Rot(p, id));
        s = p.SchritteZurueck;
        Assert.True(p.QuestArbeitsortEntfernen(id));
        Assert.Equal(s + 1, p.SchritteZurueck);
        Assert.Null(Lesen(p, id));
        Assert.False(p.QuestArbeitsortEntfernen(id));
        Assert.Equal(s + 1, p.SchritteZurueck);
    }

    [Fact]
    public void Etage_ImMoebelhaus()
    {
        var (p, id) = MitQuest();
        Assert.Null(p.ArbeitsortGrundriss("moebelhaus", 1));
        Assert.False(p.QuestArbeitsort(id, "moebelhaus", 1));
        p.EtagenAendern("moebelhaus", "Etage hinzufügen", (eg, _) => EtagenBearbeitung.Hinzufuegen(eg));
        p.EtagenAendern("moebelhaus", "Treppe", (eg, _) =>
        {
            var s = Bearbeitung.Setzen(eg, Etagen.Treppe, 9, 13)!;
            var d = Bearbeitung.Deko(eg, s);
            Py.Setzen(d, "x", 8.0);
            Py.Setzen(d, "y", 12.2);
            Py.Setzen(d, "w", 3.0);
            Py.Setzen(d, "h", 1.6);
            Py.Setzen(d, "dreh", 3L);
            Assert.True(EtagenBearbeitung.ZielSetzen(eg, 0, s, 1));
        });
        Assert.NotNull(p.ArbeitsortGrundriss("moebelhaus", 1));
        Assert.True(p.QuestArbeitsort(id, "moebelhaus", 1));
        Assert.Equal(1, Lesen(p, id)!.Etage);
        Assert.Empty(Rot(p, id));
        // Etage, die es nicht gibt: kein Schritt
        Assert.False(p.QuestArbeitsort(id, "moebelhaus", 9));
        Assert.Equal(1, Lesen(p, id)!.Etage);
    }

    [Fact]
    public void Kopieren_NimmtDenArbeitsortMit()
    {
        var (p, id) = MitQuest();
        p.QuestArbeitsort(id, "talheim");
        var kopie = p.QuestKopieren(id)!;
        Assert.Equal(Lesen(p, id), Lesen(p, kopie));
    }

    /// <summary>Jede Regel der Pruefung rot (mit der Quest zum Antippen) und die Gegenprobe gruen; rot sperrt den Export.</summary>
    [Theory]
    [InlineData("unlesbar", "nicht lesbar")]
    [InlineData("ort unbekannt", "gibt es im Spiel nicht")]
    [InlineData("bau", "erst mit dem Einbau")]
    [InlineData("etage", "Etage 3")]
    [InlineData("raum im geschaeft", "ein Raum wird dort nicht gewählt")]
    [InlineData("raum unbekannt", "den Raum")]
    [InlineData("frueher frei", "erst ab Tag 12")]
    [InlineData("ausserhalb", "außerhalb des Arbeitsorts")]
    [InlineData("anderer raum", "nicht im Raum")]
    [InlineData("auf moebel", "kann niemand stehen")]
    [InlineData("draussen wartet", "draußen steht")]
    public void Regel_Rot_Gegenprobe_Gruen(string regel, string text)
    {
        var (p, id) = MitQuest();
        if (regel == "draussen wartet")
        {
            // Petra steht draussen (wie in der Probe der Questebene), die Quest soll an ihrem Arbeitsort warten
            p.QuestgeberPlatz("petra", new QuestgeberDaten.Platz(true, "", 0, 13.0, 10.5, 180));
        }
        var ort = regel switch
        {
            "raum im geschaeft" => "elektromarkt",
            "frueher frei" => "talheim_nord",
            _ => SpielDaten.OrtBuero,
        };
        Assert.True(p.QuestArbeitsort(id, ort));
        var gut = Rot(p, id).Where(t => t.Contains("Arbeitsort", StringComparison.Ordinal) || t.Contains("Stelle", StringComparison.Ordinal)
            || t.Contains("Raum", StringComparison.Ordinal) || t.Contains("Etage", StringComparison.Ordinal)).ToList();
        if (regel != "frueher frei" && regel != "draussen wartet")
        {
            Assert.Empty(gut);
        }
        var g = p.ArbeitsortGrundriss(ort, 0)!;
        p.QuestAendern(id, "von Hand", q =>
        {
            var a = (PyObjekt)q[Questebene.FeldArbeitsort]!;
            switch (regel)
            {
                case "unlesbar":
                    Py.Setzen(a, "unbekannt", 1L);
                    break;
                case "ort unbekannt":
                    Py.Setzen(a, "ort", "gibtsnicht");
                    break;
                case "bau":
                    Py.Setzen(a, "ort", "bau:haus1");
                    break;
                case "etage":
                    Py.Setzen(a, "etage", 3L);
                    break;
                case "raum im geschaeft":
                    Py.Setzen(a, "raum", "kasse");
                    break;
                case "raum unbekannt":
                    Py.Setzen(a, "raum", "gibtsnicht");
                    break;
                case "frueher frei":
                    break;
                case "ausserhalb":
                    Py.Setzen(a, "x", -1.0);
                    break;
                case "anderer raum":
                    var anderer = Questebene.ErsteFreieStelle(g, "lager")!.Value;
                    Py.Setzen(a, "x", anderer.X);
                    Py.Setzen(a, "y", anderer.Y);
                    break;
                case "auf moebel":
                    var raum = SpielDaten.ListeOderLeer(g, "raeume").Select(SpielDaten.Objekt).Single(r => Py.Gleich(r["id"], Py.Str(a["raum"])));
                    var d = SpielDaten.ListeOderLeer(raum, "deko").Select(SpielDaten.Objekt).First(x => !Wege.Begehbar.Contains(Py.Str(x["typ"])) && Py.F(x["w"]) >= 0.6 && Py.F(x["h"]) >= 0.6);
                    Py.Setzen(a, "x", Py.F(d["x"]) + (Py.F(d["w"]) / 2));
                    Py.Setzen(a, "y", Py.F(d["y"]) + (Py.F(d["h"]) / 2));
                    break;
                case "draussen wartet":
                    break;
            }
        });
        var rot = Rot(p, id);
        Assert.Contains(rot, t => t.Contains(text, StringComparison.Ordinal));
        Assert.Throws<StadtbauFehler>(() => p.Exportieren(DateTimeOffset.Now));
        // Gegenprobe: dieselbe Quest ohne den Fehler ist gruen
        if (regel == "draussen wartet")
        {
            p.QuestArbeitsortWartet(id, false);
        }
        else if (regel == "frueher frei")
        {
            p.QuestFeld(id, "ab_tag", 12L);
        }
        else
        {
            p.QuestArbeitsort(id, ort == SpielDaten.OrtBuero ? "talheim" : SpielDaten.OrtBuero);
        }
        Assert.DoesNotContain(Rot(p, id), t => t.Contains(text, StringComparison.Ordinal));
    }

    [Fact]
    public void ZahlAmHaus_ZaehltDenArbeitsort()
    {
        var (p, id) = MitQuest();
        var vorher = Spielgeschichte.Aus(Inhalt, p.AussenStand).Alle.Single(e => e.Id == id).Stelle;
        p.QuestArbeitsort(id, "talheim_nord");
        p.QuestFeld(id, "ab_tag", 12L);
        var nachher = Spielgeschichte.Aus(Inhalt, p.AussenStand).Alle.Single(e => e.Id == id);
        Assert.Equal("talheim_nord", nachher.Stelle);
        Assert.NotEqual(vorher, nachher.Stelle);
        Assert.NotNull(nachher.Ort);
        // Gegenprobe: entfernt = wieder am Platz der Person
        p.QuestArbeitsortEntfernen(id);
        Assert.Equal(vorher, Spielgeschichte.Aus(Inhalt, p.AussenStand).Alle.Single(e => e.Id == id).Stelle);
    }

    [Fact]
    public void Projektdatei_Format4_NurMitArbeitsort()
    {
        var (p, id) = MitQuest();
        p.Speichern(vonHand: true);
        Assert.Contains("\"formatversion\": 3,", File.ReadAllText(p.Datei, Encoding.UTF8), StringComparison.Ordinal);
        p.QuestArbeitsort(id, SpielDaten.OrtBuero);
        p.Speichern(vonHand: true);
        Assert.Contains("\"formatversion\": 4,", File.ReadAllText(p.Datei, Encoding.UTF8), StringComparison.Ordinal);
        // wieder geoeffnet: derselbe Arbeitsort, bearbeitbar
        var q = Projekt.Oeffnen(p.Datei, Daten.Spiel, p.Werkzeugordner);
        Assert.False(q.NurLesen);
        Assert.Equal(Lesen(p, id), Lesen(q, id));
        // ein Titel mit dem Wortlaut des Felds zaehlt nicht (maskiert im Text)
        Assert.Equal(3, Projekt.FormatFuer("{\"stadtdaten/quests\": [[{\"titel\": \"\\\"arbeitsort\\\": x\"}]]}", "[]"));
        Assert.Equal(4, Projekt.FormatFuer("{\"stadtdaten/quests\": [[{\"arbeitsort\": {}}]]}", "[]"));
    }
}
