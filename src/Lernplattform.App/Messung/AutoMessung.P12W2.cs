using Lernplattform.Core.Spiel;
using Lernplattform.Spiel;
using Lernplattform.Spiel.Tafel;
using Lernplattform.Views;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 12 Welle 2 (Weisung P12-8): Ansichten der Spielwelt-Menues fuer die Layoutpruefung. Tafel (im Durchgang von
/// ProbeFirmaAnlegen, mit PC, Regal und zwei Bestellungen): PC zu Hause mit den Reitern Online-Shop, Kredite und
/// Gezahltes, die Uebersicht mit dem Satz unter dem Namen. Stadt (frischer Probe-Ablauf, nichts in der Datenbank): Paket
/// an der Wohnungstuer, „PC benutzen“ am Schreibtisch, draussen der Kartonstapel mit „Paket mitnehmen“.
/// </summary>
public static partial class AutoMessung
{
    private static async IAsyncEnumerable<string> P12W2Ansichten(MainView ansicht)
    {
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Tafel is not { } tafel)
        {
            P7W1AnsichtFehler.Add("spiel/pc: Spielseite nicht gebaut");
            yield break;
        }
        tafel.OhneFenster = true;
        tafel.ProbeFirmaAnlegen();
        if (!tafel.ProbePcAnlegen())
        {
            P7W1AnsichtFehler.Add("spiel/pc: PC nicht aufgestellt oder nichts bestellt");
        }
        foreach (var reiter in new[] { SpielTafel.PcShop, SpielTafel.PcKredite, SpielTafel.PcGezahlt })
        {
            tafel.PcOeffnen(reiter);
            seite.TafelZeigen();
            await ansicht.BildFertigAsync();
            yield return "spiel/pc-" + reiter;
        }
        tafel.ProbeUebersicht();
        seite.TafelZeigen();
        await ansicht.BildFertigAsync();
        yield return "spiel/uebersicht-orte";
        tafel.AllesSchliessen();

        // Stadt: PC und zwei angekommene Pakete (frischer Probe-Ablauf, nur im Speicher)
        var spiel = Frisch(seite);
        var a = spiel.Ablauf;
        a.Log(Ereignisse.Erledigt, new Core.Daten.PyObjekt { { "aufgabe", "probe-erspartes" }, { "tag", 1L }, { "richtig", false }, { "geld", 2000L } });
        const string stueck = "probe-pc";
        a.Ausfuehren(SpielAktionen.MoebelKaufen(a.Stand, Spielwelt.PcTyp, stueck));
        var gestellt = false;
        for (var y = 1.0; y < 12 && !gestellt; y += 0.5)
        {
            for (var x = 1.0; x < 16 && !gestellt; x += 0.5)
            {
                try
                {
                    a.Ausfuehren(SpielAktionen.AufstellenBei(a.Stand, stueck, x, y, 0L));
                    gestellt = true;
                }
                catch (SpielFehler)
                {
                    // naechste Stelle
                }
            }
        }
        if (gestellt && spiel.GeschaefteDaten is { } daten)
        {
            var laeden = new Geschaefte(a.Stand.Inhalt, daten);
            a.Bestellen(laeden, "moebelhaus", "pflanze");
            a.Bestellen(laeden, "moebelhaus", "bild");
            a.Log(Ereignisse.Feierabend, new Core.Daten.PyObjekt { { "tag", a.Stand.Tag }, { "gehalt", 0L } });
        }
        if (!gestellt || spiel.PaketeVorDerTuer != 2 || !spiel.Betreten() || spiel.FlaecheId != OrtZuhause)
        {
            P7W1AnsichtFehler.Add("spiel/zuhause-paket: PC nicht aufgestellt, keine zwei Pakete oder Wohnung nicht betretbar (" + spiel.Zeile.Replace('\n', ' ') + ")");
            seite.SpielSetzen(Frisch(seite));
            await ansicht.BildFertigAsync();
            yield break;
        }
        await ansicht.BildFertigAsync();
        if (!spiel.Knoepfe.Any(k => k.Aktion == Spielwelt.AktionPaket))
        {
            P7W1AnsichtFehler.Add("spiel/zuhause-paket: kein Knopf „" + Spielwelt.KnopfPaket + "“ an der Wohnungstür");
        }
        P7W1AnsichtFehler.AddRange(KnoepfeImBild(seite, "spiel/zuhause-paket"));
        yield return "spiel/zuhause-paket";
        var pc = Wohnung.AufgestellteMoebel(a.Stand).First(m => m.Stueck == stueck);
        spiel.GeheZu(pc.X + (Py.F(pc.W) / 2), pc.Y + Py.F(pc.H) + 0.4);
        spiel.SofortAnkommen();
        await ansicht.BildFertigAsync();
        if (!spiel.Knoepfe.Any(k => k.Aktion == Spielwelt.AktionComputer))
        {
            P7W1AnsichtFehler.Add("spiel/zuhause-pc: kein Knopf „" + Spielwelt.KnopfComputer + "“ am Schreibtisch");
        }
        P7W1AnsichtFehler.AddRange(KnoepfeImBild(seite, "spiel/zuhause-pc"));
        yield return "spiel/zuhause-pc";
        spiel.Verlassen();
        await ansicht.BildFertigAsync();
        if (!spiel.Knoepfe.Any(k => k.Aktion == Spielwelt.AktionPaket))
        {
            P7W1AnsichtFehler.Add("spiel/draussen-paket: kein Knopf „" + Spielwelt.KnopfPaket + "“ vor der Wohnung");
        }
        P7W1AnsichtFehler.AddRange(KnoepfeImBild(seite, "spiel/draussen-paket"));
        yield return "spiel/draussen-paket";
        seite.SpielSetzen(Frisch(seite));
        await ansicht.BildFertigAsync();
    }
}
