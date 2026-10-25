using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Zufall;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 7, W2: Vergleichswerte G45 (<c>spiel_tickets.json</c>, Erzeuger
/// <c>werkzeuge/vergleich/erzeuge_spiel_tickets.py</c>): Tickets je Tag, Zwischenfall des Tages
/// und Raenge genau wie <c>fisi_game.py</c> (Python 0.63.2).
/// </summary>
public class SpielTicketsTests
{
    private static readonly JsonElement Soll = VergleichsHilfe.Lade("spiel_tickets.json");

    private static PyObjekt Balancing => (PyObjekt)SpielInhaltTests.Inhalt["balancing"]!;

    private static object? W(JsonElement e) => Py.Lesen(e.GetRawText());

    [Fact]
    public void RaengeWiePython()
    {
        var r = Soll.GetProperty("raenge");
        foreach (var f in VergleichsHilfe.Mindestens(r.GetProperty("tickets"), 6, "tickets"))
        {
            Assert.Equal(f[1].GetInt64(), Spielstand.TicketsJeTag(W(f[0]), Balancing));
        }
        foreach (var f in VergleichsHilfe.Mindestens(r.GetProperty("rang"), 80, "rang"))
        {
            long? tag = f[1].ValueKind == JsonValueKind.Null ? null : f[1].GetInt64();
            Assert.Equal(f[2].GetString(), Spielstand.RangFuer(f[0].GetDouble(), Balancing, tag));
        }
        foreach (var f in VergleichsHilfe.Mindestens(r.GetProperty("naechster"), 40, "naechster"))
        {
            Assert.Equal(PythonJson.Text(W(f[2])), PythonJson.Text(PyAusgabe.Wert(Spielstand.NaechsterRangFuer(f[0].GetDouble(), f[1].GetInt64(), Balancing))));
        }
    }

    [Fact]
    public void WuerfelWiePython()
    {
        foreach (var f in VergleichsHilfe.Mindestens(Soll.GetProperty("wuerfel"), 100, "wuerfel"))
        {
            Assert.Equal(f[3].GetDouble(), Wuerfel.Werfen(f[0].GetString()!, f[1].GetInt64(), f[2].GetString()!));
        }
    }

    [Fact]
    public void TageWiePython()
    {
        var inhalt = SpielInhaltTests.Inhalt;
        foreach (var lauf in VergleichsHilfe.Mindestens(Soll.GetProperty("laeufe"), 10, "laeufe"))
        {
            var start = lauf.GetProperty("start").GetString()!;
            var ereignisse = new List<SpielEreignis>
            {
                new(start, Ereignisse.Schwierigkeit, new PyObjekt { { "stufe", "normal" } }),
                new(start, Ereignisse.Profil, new PyObjekt { { "name", "Nico" }, { "aussehen", new PyObjekt() } }),
            };
            var nummer = 0;
            foreach (var soll in VergleichsHilfe.Mindestens(lauf.GetProperty("tage"), 75, "tage"))
            {
                var s = new Spielstand(ereignisse, inhalt);
                var tag = s.Tag;
                var offen = s.OffeneTickets();
                var fall = s.ZwischenfallHeute();
                var ist = new PyObjekt
                {
                    { "tag", tag }, { "offen", offen.Select(a => a["id"]).ToList() }, { "zwischenfall", fall?["id"] },
                    { "tickets_heute", s.TicketsHeute }, { "rang", s.TagesRang }, { "ansehen", s.MittlereReputation },
                    { "pool", (long)s.Vorrat().Count }, { "warten", s.WartenAufLieferung().Select(a => a["id"]).ToList() },
                };
                Assert.Equal(start + " " + PythonJson.Text(W(soll)), start + " " + PythonJson.Text(ist));
                string Stempel() => start + "~" + ereignisse.Count.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
                foreach (var auftrag in offen)
                {
                    nummer++;
                    if (nummer % 11 == 0)
                    {
                        ereignisse.Add(new(Stempel(), Ereignisse.Verschoben, Bewertung.Verschieben(auftrag, tag, Balancing)));
                        continue;
                    }
                    var richtig = nummer % 7 != 0;
                    var reputation = new PyObjekt();
                    foreach (var achse in Ereignisse.Achsen)
                    {
                        reputation.Add(achse, richtig ? 2L : -3L);
                    }
                    var daten = new PyObjekt
                    {
                        { "aufgabe", auftrag["id"] }, { "tag", tag }, { "richtig", richtig }, { "geld", richtig ? 40L : -10L }, { "reputation", reputation },
                    };
                    if (Py.Wahr(auftrag["zwischenfall"]))
                    {
                        daten.Add("zwischenfall", true);
                    }
                    ereignisse.Add(new(Stempel(), Ereignisse.Erledigt, daten));
                }
                ereignisse.Add(new(Stempel(), Ereignisse.Feierabend, new PyObjekt { { "tag", tag }, { "gehalt", s.Gehalt }, { "rang", s.Rang } }));
            }
        }
    }
}
