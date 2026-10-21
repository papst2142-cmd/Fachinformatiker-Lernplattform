using System.Reflection;
using System.Text.Json;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 4, W1: Texte der Lernseiten zeichengleich aus Python (G33 "texte", PC-Wortlaut) und
/// "nur Einfachleerzeichen im Zeilenkern" mit der festen Ausnahmeliste der Wortliste W1
/// (Abschnitt 4, Weisung P4-3): Testprojekt-Kopfzeile, Kopf eines Notizblock-Eintrags, Status im
/// Listeneintrag. Der Zeilenkern beginnt nach den fuehrenden Leerzeichen.
/// </summary>
public class LernseitenTexteTests
{
    private static readonly JsonElement Texte = VergleichsHilfe.Lade("notizblock.json").GetProperty("texte");
    private static readonly Lerninhalte Inhalte = Lerninhalte.Laden(Pfade.Inhalte);

    /// <summary>Feste Ausnahmeliste (Wortliste W1 Abschnitt 4): genau diese Muster duerfen "  " enthalten.</summary>
    private static readonly string[] Ausnahmen =
    [
        "  ·  Schwierigkeit: ", "  ·  Fachbereich: ",   // Nr. 1 app_gui 4540
        "  ·  ",                                         // Nr. 2 app_gui 5448 f. und Nr. 3 app_gui 706
    ];

    private static readonly Type[] Klassen = [typeof(SzenarienTexte), typeof(TestprojektTexte), typeof(NotizblockTexte)];

    [Fact]
    public void TexteWiePython()
    {
        var a = new Abweichungen();
        foreach (var eintrag in Texte.EnumerateObject())
        {
            var teile = eintrag.Name.Split('.');
            var feld = Klassen.Single(k => k.Name == teile[0]).GetField(teile[1], BindingFlags.Public | BindingFlags.Static);
            a.Gleich(eintrag.Value.GetString(), feld?.GetRawConstantValue() as string, eintrag.Name);
        }
        a.Pruefen(55);
        // jeder feste Text der drei Klassen ist mit Python verglichen (ausser den Pillenwerten "aus"/"an"/"Alle")
        var verglichen = Texte.EnumerateObject().Select(e => e.Name).ToHashSet();
        foreach (var klasse in Klassen)
        {
            foreach (var feld in klasse.GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral && f.FieldType == typeof(string)))
            {
                if (klasse == typeof(NotizblockTexte) && feld.Name is "Aus" or "An" or "Alle")
                {
                    continue;
                }
                Assert.Contains(klasse.Name + "." + feld.Name, verglichen);
            }
        }
    }

    private static string Kern(string text, bool mitAusnahme)
    {
        if (mitAusnahme)
        {
            foreach (var muster in Ausnahmen)
            {
                text = text.Replace(muster, "·", StringComparison.Ordinal);
            }
        }
        return string.Join("\n", text.Split('\n').Select(z => z.TrimStart(' ')));
    }

    [Fact]
    public void NurEinfachleerzeichen()
    {
        var texte = new List<(string Text, bool Ausnahme, string Name)>();
        foreach (var klasse in Klassen)
        {
            foreach (var feld in klasse.GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral && f.FieldType == typeof(string)))
            {
                texte.Add(((string)feld.GetRawConstantValue()!, feld.Name == nameof(SzenarienTexte.StatusTrenner), klasse.Name + "." + feld.Name));
            }
        }
        foreach (var (wert, text) in NotizblockTexte.StatusPillen)
        {
            texte.Add((text, false, "StatusPillen " + wert));
        }
        foreach (var status in new[] { FragenStatus.Offen, FragenStatus.Ueben, FragenStatus.Fertig })
        {
            texte.Add((SzenarienTexte.Unterzeile("Thema", status), true, "Unterzeile " + status));
            texte.Add((NotizblockTexte.ListeTitel(status), false, "ListeTitel " + status));
            texte.Add((NotizblockTexte.KartenKnopf(3, status), false, "KartenKnopf " + status));
            texte.Add((NotizblockTexte.FragenKnopf(1, status), false, "FragenKnopf " + status));
            texte.Add((NotizblockTexte.Leer(status, "  Wort ", true), false, "Leer " + status));
        }
        texte.Add((SzenarienTexte.ZaehlerText(1234, 5678, SzenarienTexte.Aufgaben), false, "Zaehler"));
        texte.Add((SzenarienTexte.SeiteText(0, 3), false, "Seite"));
        texte.Add((SzenarienTexte.Eintrag(4, "Titel", true), false, "Eintrag"));
        texte.Add((NotizblockTexte.ZuUeben(2), false, "ZuUeben"));
        texte.Add((NotizblockTexte.Abgeschlossen(1, 2), false, "Abgeschlossen"));
        texte.Add((NotizblockTexte.OffenText(1), false, "Offen"));
        texte.Add((NotizblockTexte.ListeUnter(1), false, "ListeUnter"));
        for (var i = 0; i < Inhalte.Testprojekte.Count; i++)
        {
            var projekt = Inhalte.Testprojekte[i];
            texte.Add((TestprojektTexte.Kopfzeile(projekt), true, "Kopfzeile " + i));
            texte.Add((TestprojektTexte.Unterzeile(projekt), false, "Unterzeile Testprojekt " + i));
            texte.Add((TestprojektTexte.Aufgabe(projekt), false, "Aufgabe " + i));
            texte.Add((TestprojektTexte.Hinweise(projekt), false, "Hinweise " + i));
        }
        foreach (var quelle in Quellen.Alle)
        {
            var eintrag = new Notizblock(Inhalte).Fragen(quelle).First().Eintrag;
            texte.Add((NotizblockTexte.Kopf(quelle, eintrag, "Kurz"), true, "Kopf " + quelle));
            texte.Add((NotizblockTexte.Kopf(quelle, eintrag, ""), true, "Kopf ohne Thema " + quelle));
        }
        foreach (var (text, ausnahme, name) in texte)
        {
            Assert.False(Kern(text, ausnahme).Contains("  ", StringComparison.Ordinal), name + ": Doppelleerzeichen in \"" + text + "\"");
        }
        // Kontrollfehler: ohne die Ausnahmeliste wird die Testprojekt-Kopfzeile rot
        Assert.Contains("  ", Kern(TestprojektTexte.Kopfzeile(Inhalte.Testprojekte[0]), false), StringComparison.Ordinal);
    }

    [Fact]
    public void GekuerztWiePython()
    {
        Assert.Equal("kurz", NotizblockTexte.Gekuerzt("kurz"));
        var genau = new string('a', 1200);
        Assert.Equal(genau, NotizblockTexte.Gekuerzt(genau));
        // answer[:1200].rsplit(" ", 1)[0] + " …"
        Assert.Equal("wort " + new string('b', 1190) + " …", NotizblockTexte.Gekuerzt("wort " + new string('b', 1190) + " cccc" + new string('d', 20)));
        Assert.Equal(new string('x', 1200) + " …", NotizblockTexte.Gekuerzt(new string('x', 1300)));
        // Codepunkte wie Python len(): 1 200 Emoji bleiben ungekuerzt
        var emoji = string.Concat(Enumerable.Repeat("😀", 1200));
        Assert.Equal(emoji, NotizblockTexte.Gekuerzt(emoji));
    }
}
