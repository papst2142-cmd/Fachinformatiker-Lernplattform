using System.Security.Cryptography;
using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 7, W2: Vergleichswerte G44 (<c>spiel_neuaufbau.json</c>, Erzeuger
/// <c>werkzeuge/vergleich/erzeuge_spiel_neuaufbau.py</c>): der Spielstand aus dem Ereignisprotokoll
/// (von Hand und mit der echten Python-Spiel-API gespielt) hat jedes Feld und jeden abgeleiteten
/// Wert genau wie <c>fisi_game.GameState</c> (Python 0.63.2).
/// </summary>
public class SpielNeuaufbauTests
{
    private static readonly JsonElement Soll = VergleichsHilfe.Lade("spiel_neuaufbau.json");

    internal static List<SpielEreignis> Ereignisse(JsonElement liste) =>
        liste.EnumerateArray().Select(e => new SpielEreignis(Py.Lesen(e[0].GetRawText()), e[1].GetString()!, (PyObjekt)Py.Lesen(e[2].GetRawText())!)).ToList();

    private static string Sha(object? wert) => Convert.ToHexStringLower(SHA256.HashData(PythonJson.Utf8(PythonJson.Text(wert))));

    private static List<object?> Kennungen(IEnumerable<PyObjekt> auftraege) => auftraege.Select(a => a["id"]).ToList();

    private static PyObjekt NachPerson(PyDict d)
    {
        var o = new PyObjekt();
        foreach (var (k, v) in d)
        {
            o.Add((string)k!, Kennungen(((List<object?>)v!).Cast<PyObjekt>()));
        }
        return o;
    }

    internal static PyObjekt Abgeleitet(Spielstand s)
    {
        var offen = s.OffeneTickets();
        var fall = s.ZwischenfallHeute();
        return new PyObjekt
        {
            { "day", s.Tag }, { "mean_reputation", s.MittlereReputation }, { "rank", s.Rang }, { "day_rank", s.TagesRang },
            { "level", s.Stufe }, { "easy", s.Leicht }, { "tickets_today", s.TicketsHeute },
            { "next_rank", PyAusgabe.Wert(s.NaechsterRang()) }, { "salary", s.Gehalt },
            { "counted_once", PyAusgabe.Wert(s.EinmalGezaehlt()) }, { "stock", PyAusgabe.Wert(s.Lager()) },
            { "warehouse", PyAusgabe.Wert(s.Lagerhaus()) }, { "pool", Kennungen(s.Vorrat()).Take(40).ToList() },
            { "open_tickets", Kennungen(offen) },
            { "todays_tickets", s.TicketsDesTages().Select(t => (object?)new List<object?> { t.Auftrag["id"], t.Status }).ToList() },
            { "incident_today", fall?["id"] }, { "wrong_deliveries", Kennungen(s.FalscheLieferungen()) },
            { "waiting_for_delivery", Kennungen(s.WartenAufLieferung()) },
            { "quests", NachPerson(s.Auftraege()) }, { "quests_buero", NachPerson(s.Auftraege(SpielDaten.OrtBuero)) },
            { "open_count_by_site", PyAusgabe.Wert(s.OffenJeOrt()) }, { "open_count_by_room", PyAusgabe.Wert(s.OffenJeRaum()) },
            { "can_end_day", s.FeierabendMoeglich() }, { "all_done", s.AllesErledigt() },
            { "prepared", offen.Select(a => (object?)Sha(PyAusgabe.Wert(s.Vorbereitet(a)))).ToList() },
            { "available_parts", offen.Select(a => (object?)s.VerfuegbareTeile(s.Vorbereitet(a)!)).ToList() },
        };
    }

    internal static PyObjekt Felder(Spielstand s)
    {
        var felder = (PyObjekt)PyAusgabe.Wert(s.Felder())!;
        Py.Setzen(felder, "history", new PyObjekt { { "anzahl", (long)s.Verlauf.Count }, { "sha256", Sha(felder["history"]) } });
        return felder;
    }

    /// <summary>Vergleicht Schluessel fuer Schluessel und nennt den ersten abweichenden.</summary>
    internal static void Gleich(JsonElement soll, PyObjekt ist, string wo)
    {
        Assert.Equal(soll.EnumerateObject().Select(p => p.Name), ist.Select(p => p.Key));
        foreach (var p in soll.EnumerateObject())
        {
            Assert.Equal(wo + " " + p.Name + ": " + PythonJson.Text(Py.Lesen(p.Value.GetRawText())), wo + " " + p.Name + ": " + PythonJson.Text(ist[p.Name]));
        }
    }

    [Fact]
    public void JederStandWiePython()
    {
        var anzahl = 0;
        foreach (var protokoll in Soll.GetProperty("protokolle").EnumerateArray())
        {
            var name = protokoll.GetProperty("name").GetString()!;
            var ereignisse = Ereignisse(protokoll.GetProperty("ereignisse"));
            foreach (var stand in protokoll.GetProperty("staende").EnumerateArray())
            {
                var n = stand.GetProperty("n").GetInt32();
                var s = new Spielstand(ereignisse.Take(n), SpielInhaltTests.Inhalt);
                Gleich(stand.GetProperty("stand").GetProperty("felder"), Felder(s), name + " n=" + n + " Feld");
                Gleich(stand.GetProperty("stand").GetProperty("abgeleitet"), Abgeleitet(s), name + " n=" + n);
                anzahl++;
            }
        }
        Assert.True(anzahl > 200);
    }

    [Fact]
    public void SchrittweiseWieNeuaufbau()
    {
        // Game._log: apply_event + note_handled auf dem vorhandenen Stand ergibt denselben Stand
        foreach (var protokoll in VergleichsHilfe.Mindestens(Soll.GetProperty("protokolle"), 18, "protokolle"))
        {
            var ereignisse = Ereignisse(protokoll.GetProperty("ereignisse"));
            var s = new Spielstand([], SpielInhaltTests.Inhalt);
            var einmal = s.EinmalGezaehlt();
            foreach (var e in ereignisse)
            {
                var vorher = s.Tag;
                if (s.Anwenden(e, einmal))
                {
                    s.BearbeitetNotieren(e.Art, e.Daten, vorher);
                }
            }
            var neu = new Spielstand(ereignisse, SpielInhaltTests.Inhalt);
            Assert.Equal(PythonJson.Text(Felder(neu)), PythonJson.Text(Felder(s)));
        }
    }
}
