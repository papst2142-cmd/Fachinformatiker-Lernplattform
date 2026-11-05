using Lernplattform.Core.Spiel;
using Lernplattform.Spiel;
using Lernplattform.Views;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 12 Welle 1 (Weisung P12-5): Ansichten des Kleiderschranks fuer die Layoutpruefung. Tafel: neue Figur mit dem
/// Hinweis „Einmalig“, Kaufhaus ohne Schrank, danach (im Durchgang von ProbeFirmaAnlegen) ein kleiner Kleiderschrank
/// mit 8 Stuecken: Kleiderschrank und Kaufhaus „voll“. Stadt (frischer Probe-Ablauf, nichts in der Datenbank): Wohnung
/// mit aufgestelltem Schrank, Figur davor (Knopf „Kleiderschrank öffnen“), Einrichten mit „Zurückverkaufen“.
/// </summary>
public static partial class AutoMessung
{
    private static async IAsyncEnumerable<string> P12W1Ansichten(MainView ansicht)
    {
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Tafel is not { } tafel)
        {
            P7W1AnsichtFehler.Add("spiel/kleiderschrank: Spielseite nicht gebaut");
            yield break;
        }
        tafel.OhneFenster = true;
        tafel.ProbeAnlegen();
        seite.TafelZeigen();
        await ansicht.BildFertigAsync();
        yield return "spiel/anlegen-hinweis";
        tafel.ProbeFirmaAnlegen();
        if (tafel.ProbeGeschaeft(Kleidung.Laden))
        {
            seite.TafelZeigen();
            await ansicht.BildFertigAsync();
            yield return "spiel/kaufhaus-ohne-schrank";
        }
        if (!tafel.ProbeSchrankAnlegen())
        {
            P7W1AnsichtFehler.Add("spiel/kleiderschrank: Schrank nicht aufgestellt oder nicht voll");
        }
        tafel.ProbeFigur();
        seite.TafelZeigen();
        await ansicht.BildFertigAsync();
        yield return "spiel/kleiderschrank";
        if (tafel.ProbeGeschaeft(Kleidung.Laden))
        {
            seite.TafelZeigen();
            await ansicht.BildFertigAsync();
            yield return "spiel/kaufhaus-voll";
        }
        else
        {
            P7W1AnsichtFehler.Add("spiel/kaufhaus-voll: Kaufhaus nicht offen");
        }
        tafel.AllesSchliessen();

        // Stadt: Schrank in der Wohnung (frischer Probe-Ablauf, nur im Speicher)
        var spiel = Frisch(seite);
        var a = spiel.Ablauf;
        a.Log(Ereignisse.Erledigt, new Core.Daten.PyObjekt { { "aufgabe", "probe-erspartes" }, { "tag", 1L }, { "richtig", false }, { "geld", 2000L } });
        const string stueck = "probe-schrank";
        a.Ausfuehren(SpielAktionen.MoebelKaufen(a.Stand, "kleiderschrank_mittel", stueck));
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
        if (!gestellt || !spiel.Betreten() || spiel.FlaecheId != OrtZuhause)
        {
            P7W1AnsichtFehler.Add("spiel/zuhause-kleiderschrank: Schrank nicht aufgestellt oder Wohnung nicht betretbar (" + spiel.Zeile.Replace('\n', ' ') + ")");
            seite.SpielSetzen(Frisch(seite));
            await ansicht.BildFertigAsync();
            yield break;
        }
        var schrank = Wohnung.AufgestellteMoebel(a.Stand).First(m => m.Stueck == stueck);
        spiel.GeheZu(schrank.X + (Py.F(schrank.W) / 2), schrank.Y + Py.F(schrank.H) + 0.4);
        spiel.SofortAnkommen();
        await ansicht.BildFertigAsync();
        if (!spiel.Knoepfe.Any(k => k.Aktion == Charakter.Aktion))
        {
            P7W1AnsichtFehler.Add("spiel/zuhause-kleiderschrank: kein Knopf „" + Charakter.KnopfOeffnen + "“ vor dem Schrank");
        }
        P7W1AnsichtFehler.AddRange(KnoepfeImBild(seite, "spiel/zuhause-kleiderschrank"));
        yield return "spiel/zuhause-kleiderschrank";
        spiel.EinrichtenUmschalten();
        spiel.Waehlen(stueck);
        await ansicht.BildFertigAsync();
        if (spiel.AusgewaehltRueckkauf != 200)
        {
            P7W1AnsichtFehler.Add("spiel/einrichten-verkaufen: Rueckkauf " + spiel.AusgewaehltRueckkauf + " statt 200");
        }
        P7W1AnsichtFehler.AddRange(KnoepfeImBild(seite, "spiel/einrichten-verkaufen"));
        yield return "spiel/einrichten-verkaufen";
        spiel.EinrichtenUmschalten();
        seite.SpielSetzen(Frisch(seite));
        await ansicht.BildFertigAsync();
    }
}
