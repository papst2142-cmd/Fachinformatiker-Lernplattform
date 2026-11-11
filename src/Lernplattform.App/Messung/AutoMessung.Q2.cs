using System.Diagnostics;
using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Spiel;
using Lernplattform.Views;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Messung;

/// <summary>
/// Welle Q2 (Weisung P12-190): die Stadt bekommt im Start-Test die Abschnitte <c>questgeber</c> und <c>quests</c> (nur im
/// Speicher): 12 eigene Questgeber draussen (die Hoechstzahl) je vor einem Ort, Ulla im Moebelhaus, je eine neue Quest. Alle
/// bisherigen Auftraege gelten als erledigt, damit heute die neuen drankommen. Messpunkte (wiederholbar, 400 ms):
/// „Spiel: Questgeber draussen ansprechen (bis zum Bild)“ und „Spiel: Questgeber im Laden ansprechen (bis zum Bild)“.
/// Dazu die Nahansicht mit allen 12 Questgebern draussen gegen dieselbe Grenze (160 000 / 40) und die Pruefung, dass sie im
/// Paket-Netz stehen (Gegenprobe: ohne Abschnitte weniger Dreiecke im Paket-Netz). Layout: draussen und im Laden.
/// </summary>
public static partial class AutoMessung
{
    /// <summary>Die Probe-Abschnitte zur Stadt <paramref name="szene"/>: 12 draussen, Ulla im Moebelhaus.</summary>
    internal static PyObjekt Q2Abschnitte(StadtSzene szene)
    {
        var questgeber = new List<object?>
        {
            new PyObjekt
            {
                { "id", "ulla" }, { "bestand", true },
                { "platz", new PyObjekt { { "ort", "moebelhaus" }, { "etage", 0L }, { "x", 4.0 }, { "y", 3.0 }, { "blick", 0L } } },
            },
        };
        var quests = new List<object?> { Q2Quest("neu-q-laden", "ulla", "Kabel im Möbelhaus") };
        var oberteile = Aussehen.Teile.First(t => t.Teil == "oberteil").Wahl;
        var haare = Aussehen.Teile.First(t => t.Teil == "haarfarbe").Wahl;
        var orte = szene.Orte.Where(o => o.StehX > 1 && o.StehX < szene.Breite - 1 && o.StehZ > 1 && o.StehZ < szene.Tiefe - 1).ToList();
        for (var i = 0; i < Questebene.DraussenHoechstens; i++)
        {
            var o = orte[i % orte.Count];
            var seite = i < orte.Count ? 0.7 : -0.7;
            var id = "neu-qg-" + (i + 1).ToString(CultureInfo.InvariantCulture);
            questgeber.Add(new PyObjekt
            {
                { "id", id }, { "name", "Kundin " + (i + 1).ToString(CultureInfo.InvariantCulture) }, { "rolle", "Probe" },
                {
                    "aussehen", new PyObjekt
                    {
                        { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", haare[i % haare.Count].Kennung }, { "oberteil", oberteile[i % oberteile.Count].Kennung },
                    }
                },
                {
                    "platz", new PyObjekt
                    {
                        { "draussen", true }, { "x", Math.Round(o.StehX + seite, 2) }, { "y", Math.Round((double)o.StehZ, 2) },
                        { "blick", seite > 0 ? 270L : 90L },
                    }
                },
            });
            quests.Add(Q2Quest("neu-q-" + (i + 1).ToString(CultureInfo.InvariantCulture), id, "Frage " + (i + 1).ToString(CultureInfo.InvariantCulture)));
        }
        return new PyObjekt { { Questebene.AbschnittQuestgeber, questgeber }, { Questebene.AbschnittQuests, quests } };
    }

    private static PyObjekt Q2Quest(string id, string questgeber, string titel) => new()
    {
        { "id", id }, { "typ", "auswahl" }, { "questgeber", questgeber }, { "titel", titel }, { "frage", "Was prüfst du zuerst?" },
        { "ticket", new List<object?> { "Die Kasse startet nicht." } }, { "hilfe", new List<object?> { "Das Einfachste zuerst." } },
        { "erklaerung", new List<object?> { "Strom zuerst." } }, { "cat", "systeme" },
        { "belohnung", new PyObjekt { { "geld", 20L }, { "reputation", 1L } } }, { "ab_tag", 1L },
        { "optionen", new List<object?> { "Stromkabel", "Neues Betriebssystem" } }, { "antwort", "Stromkabel" },
    };

    /// <summary>Frisches Spiel in der Stadt mit Questgebern; alle bisherigen Auftraege erledigt (heute kommen die neuen).</summary>
    internal static StadtSpiel MitQuestgebern(SpielSeite seite)
    {
        var alt = seite.Spiel!;
        var daten = (PyObjekt)Py.Kopie(alt.Stadt.Daten)!;
        foreach (var (k, v) in Q2Abschnitte(alt.Szene))
        {
            Py.Setzen(daten, k, v);
        }
        var inhalt = Questebene.Anwenden(alt.Stadt.Inhalt, daten, alt.Innenraeume);
        var stadt = new Stadt(inhalt, daten) { Fassung = alt.Stadt.Fassung };
        var spiel = SpielStart.Neu(stadt, alt.Grafik, alt.Innenraeume, alt.GeschaefteDaten, alt.Feste, Festzeit.Heute, Jahreszeiten.Heute);
        foreach (var a in Liste(alt.Stadt.Inhalt["aufgaben"]).Select(Objekt))
        {
            spiel.Ablauf.Log(Ereignisse.Erledigt, new PyObjekt { { "aufgabe", a["id"] }, { "tag", 1L }, { "richtig", true }, { "geld", 0L } });
        }
        spiel.Ablauf.Log(Ereignisse.Feierabend, new PyObjekt { { "tag", 1L }, { "gehalt", 0L }, { "rang", "Praktikant" } });
        spiel.Ablauf.Neuaufbau();
        spiel.Auffrischen();
        seite.SpielSetzen(spiel);
        return spiel;
    }

    private static async Task<int> Q2QuestgeberAsync(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Spiel: Questgeber " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Spiel Questgeber " + punkt + ": " + wert);
                funde++;
            }
        }
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel?.Innenraeume is null)
        {
            Messwerte.Notiere("  FEHLER: Spiel Questgeber: Spielseite nicht gebaut");
            return 1;
        }
        var gl = await GlAbwartenAsync(seite);
        async Task Bild()
        {
            if (gl)
            {
                await seite.Flaeche.NaechstesBildAsync();
            }
            await ansicht.BildFertigAsync();
        }
        var vorher = seite.Spiel;
        static long Haeuser(StadtSzene sz) => sz.Vorbereiten().Haeuser.Values.Sum(n => (long)n.Indizes.Count / 3);
        var hausOhne = Haeuser(vorher.Szene);
        try
        {
            var spiel = MitQuestgebern(seite);
            await Bild();
            var draussen = spiel.QuestgeberDraussen;
            Pruefe("draussen", draussen.Count == Questebene.DraussenHoechstens, draussen.Count.ToString(inv));
            // im Netz des Hauses ihres Orts (Sichtkegel und Fernform wie das Haus, kein neuer Zeichenaufruf)
            var hausMit = Haeuser(spiel.Szene);
            var jeHaus = spiel.Szene.QuestgeberJeHaus.Values.Sum(l => l.Count);
            Pruefe("in den Hausnetzen (Gegenprobe ohne Abschnitte)", hausMit > hausOhne && jeHaus == draussen.Count,
                string.Create(inv, $"{hausMit} statt {hausOhne} Dreiecke, {jeHaus} von {draussen.Count} am Haus ihres Orts"));

            // 1. draussen: hin zu zwei Questgebern im Wechsel, Ankunft = Text und Knopf (wiederholbar)
            var reihe = 0;
            async Task<double?> Ansprechen()
            {
                var q = draussen[reihe++ % 2];
                var u = Stopwatch.StartNew();
                spiel.GeheZu(q.X, q.Z);
                spiel.SofortAnkommen();
                await Bild();
                return u.Elapsed.TotalMilliseconds;
            }
            var erstes = await Ansprechen();
            Pruefe("draussen Knopf", spiel.Knoepfe.Any(k => k.Aktion == "auftrag:neu-q-1"),
                string.Join(", ", spiel.Knoepfe.Select(k => k.Aktion)) + " | " + spiel.Zeile.Replace('\n', ' '));
            funde += Ergebnis("Spiel: Questgeber draussen ansprechen (bis zum Bild)",
                await Messwiederholung.MessenAsync(erstes, Geraetegrenze, Ansprechen, () => Task.CompletedTask, Task.Delay));

            // 2. ueber die Ortsliste (ohne 3D): vor dem Ort steht der Knopf des Questgebers. Heute sind nur so viele Auftraege
            // offen, wie der Tag vergibt: genommen wird ein Questgeber ab dem zweiten, dessen Auftrag heute offen ist.
            var offen = spiel.Ablauf.Stand.Auftraege(Questebene.OrtDraussen);
            var zweiter = draussen.Skip(1).FirstOrDefault(q => offen.Get(q.Id) is List<object?> { Count: > 0 });
            Pruefe("heute offen ab dem zweiten", zweiter is not null, string.Join(", ", offen.Keys.Select(k => k?.ToString())));
            if (zweiter is not null)
            {
                var ort = spiel.QuestgeberOrt(zweiter);
                var aktion = "auftrag:" + ((PyObjekt)((List<object?>)offen.Get(zweiter.Id)!)[0]!)["id"];
                spiel.GeheZu(ort!);
                spiel.SofortAnkommen();
                Pruefe("vor dem Ort " + ort, spiel.Knoepfe.Any(k => k.Aktion == aktion),
                    aktion + " in [" + string.Join(", ", spiel.Knoepfe.Select(k => k.Aktion)) + "] | " + spiel.Zeile.Replace('\n', ' '));
            }

            // 3. im Laden: Ulla steht neben den Verkaeufern und spricht an (wiederholbar: weg und wieder hin)
            spiel.GeheZu("moebelhaus");
            spiel.SofortAnkommen();
            if (!spiel.KannBetreten && spiel.KannWarten)
            {
                spiel.Warten();
            }
            if (!spiel.KannBetreten || !spiel.Betreten())
            {
                Pruefe("Moebelhaus betreten", false, "nicht hineingekommen");
                return funde;
            }
            await Bild();
            async Task<double?> Hin()
            {
                var u = Stopwatch.StartNew();
                spiel.GeheZu(4.0, 3.6);
                spiel.SofortAnkommen();
                await Bild();
                return u.Elapsed.TotalMilliseconds;
            }
            async Task Weg()
            {
                spiel.GeheZu(4.0, 6.5);
                spiel.SofortAnkommen();
                await Bild();
            }
            var laden = await Hin();
            Pruefe("im Laden Knopf", spiel.Knoepfe.Any(k => k.Aktion == "auftrag:neu-q-laden"),
                string.Join(", ", spiel.Knoepfe.Select(k => k.Aktion)) + " | " + spiel.Zeile.Replace('\n', ' '));
            funde += Ergebnis("Spiel: Questgeber im Laden ansprechen (bis zum Bild)",
                await Messwiederholung.MessenAsync(laden, Geraetegrenze, Hin, Weg, Task.Delay));
            spiel.Verlassen();
            await Bild();

            // 4. Nahansicht im teuersten Fall: alle Orte, 12 Questgeber draussen, Weihnachten, Winter, nachts
            if (gl)
            {
                Tageslicht.FesteUhr = 1260;
                Jahreszeiten.Probe(Jahreszeit.Winter);
                Festzeit.Probe(Festzeit.Weihnachten);
                try
                {
                    var alle = SpielMitAllenOrten(spiel);
                    seite.SpielSetzen(alle);
                    await seite.Flaeche.NaechstesBildAsync();
                    Pruefe("mit allen Orten draussen", alle.QuestgeberDraussen.Count == Questebene.DraussenHoechstens
                        && alle.Szene.Orte.Count == alle.Stadt.Regeln.Count && alle.Szene.Fest == Festzeit.Weihnachten,
                        string.Create(inv, $"{alle.QuestgeberDraussen.Count} Questgeber, {alle.Szene.Orte.Count} von {alle.Stadt.Regeln.Count} Orten, Fest {alle.Szene.Fest ?? "keins"}"));
                    Messwerte.Notiere("Spiel: Nahansicht mit allen Orten und 12 Questgebern draussen (Weihnachten, Winter, nachts)");
                    funde += await NahansichtAsync(seite);
                }
                finally
                {
                    Tageslicht.FesteUhr = null;
                    Jahreszeiten.ProbeEnde();
                    Festzeit.ProbeEnde();
                }
            }
            else
            {
                Messwerte.Notiere("Spiel: Nahansicht mit Questgebern nicht gemessen (ohne 3D)");
            }
        }
        finally
        {
            seite.SpielSetzen(vorher);
            await ansicht.BildFertigAsync();
        }
        return funde;
    }

    /// <summary>Layout: ein Questgeber draussen mit Text und Knopf, Ulla im Moebelhaus.</summary>
    private static async IAsyncEnumerable<string> Q2Ansichten(MainView ansicht)
    {
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel?.Innenraeume is null)
        {
            P7W1AnsichtFehler.Add("spiel/questgeber: Spielseite nicht gebaut");
            yield break;
        }
        var vorher = seite.Spiel;
        var spiel = MitQuestgebern(seite);
        var q = spiel.QuestgeberDraussen[0];
        spiel.GeheZu(q.X, q.Z);
        spiel.SofortAnkommen();
        await ansicht.BildFertigAsync();
        P7W1AnsichtFehler.AddRange(KnoepfeImBild(seite, "spiel/questgeber-draussen"));
        yield return "spiel/questgeber-draussen";
        spiel.GeheZu("moebelhaus");
        spiel.SofortAnkommen();
        if (!spiel.KannBetreten && spiel.KannWarten)
        {
            spiel.Warten();
        }
        if (spiel.KannBetreten && spiel.Betreten())
        {
            spiel.GeheZu(4.0, 3.6);
            spiel.SofortAnkommen();
            await ansicht.BildFertigAsync();
            P7W1AnsichtFehler.AddRange(KnoepfeImBild(seite, "spiel/questgeber-laden"));
            yield return "spiel/questgeber-laden";
        }
        else
        {
            P7W1AnsichtFehler.Add("spiel/questgeber-laden: Moebelhaus nicht betretbar");
        }
        seite.SpielSetzen(vorher);
        await ansicht.BildFertigAsync();
    }
}
