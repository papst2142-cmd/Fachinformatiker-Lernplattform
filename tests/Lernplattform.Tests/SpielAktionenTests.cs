using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 7, W2: Vergleichswerte G44 Teil "Aktionen" (<c>spiel_aktionen.json</c>, Erzeuger
/// <c>werkzeuge/vergleich/erzeuge_spiel_aktionen.py</c>): Jeder mit der Python-Spiel-API gespielte
/// Aufruf (Profil, Tickets, Ersatzteil, Feierabend, Moebel, Umzug) schreibt in C# dieselben Ereignisse
/// oder bricht mit demselben Text ab, auf dem Spielstand aus den ersten n Ereignissen.
/// </summary>
public class SpielAktionenTests
{
    private static readonly JsonElement Soll = VergleichsHilfe.Lade("spiel_aktionen.json");

    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    private static object? W(JsonElement e) => Py.Lesen(e.GetRawText());

    private static string T(object? wert) => PythonJson.Text(wert);

    private static string S(JsonElement e) => e.GetString()!;

    private static List<(string Art, PyObjekt Daten)> Rufen(Spielstand stand, JsonElement fall)
    {
        var a = fall.GetProperty("a");
        return S(fall.GetProperty("m")) switch
        {
            "set_profile" => SpielAktionen.Profil(stand, a[0].GetString(), W(a[1]), W(a[2])),
            "solve" => SpielAktionen.Loesen(stand, W(a[0]), W(a[1]), a[2].GetBoolean(), (PyObjekt)W(fall.GetProperty("wissen"))!),
            "order_spare" => SpielAktionen.Ersatzteil(stand, W(a[0]), W(a[1])),
            "defer" => SpielAktionen.Verschieben(stand, W(a[0])),
            "end_day" => SpielAktionen.Feierabend(stand),
            "buy_furniture" => SpielAktionen.MoebelKaufen(stand, W(a[0]), fall.TryGetProperty("e", out var e) ? S(e[0][1].GetProperty("stueck")) : "m_fehler"),
            "sell_furniture" => SpielAktionen.MoebelVerkaufen(stand, S(a[0])),
            "place_furniture" => SpielAktionen.Aufstellen(stand, S(a[0]), W(a[1]), W(a[2]), W(a[3])),
            "place_furniture_at" => SpielAktionen.AufstellenBei(stand, S(a[0]), W(a[1]), W(a[2]), W(a[3])),
            "rotate_furniture" => SpielAktionen.Drehen(stand, S(a[0])),
            "nudge_furniture" => SpielAktionen.Schieben(stand, S(a[0]), a[1].GetInt64(), a[2].GetInt64()),
            "restore_layout" => SpielAktionen.EinrichtungZurueck(stand, (PyObjekt)W(a[0])!),
            "box_furniture" => SpielAktionen.Einpacken(stand, S(a[0])),
            "set_floor" => SpielAktionen.Boden(stand, S(a[0]), W(a[1]), W(a[2])),
            "move_home" => SpielAktionen.Umziehen(stand, W(a[0]), a[1].GetBoolean()),
            var m => throw new InvalidOperationException("unbekannte Methode " + m),
        };
    }

    [Fact]
    public void JederAufrufWiePython()
    {
        var anzahl = 0;
        var fehler = 0;
        foreach (var lauf in Soll.GetProperty("laeufe").EnumerateArray())
        {
            var ereignisse = SpielNeuaufbauTests.Ereignisse(lauf.GetProperty("ereignisse"));
            var staende = new Dictionary<int, Spielstand>();
            foreach (var fall in lauf.GetProperty("faelle").EnumerateArray())
            {
                var n = fall.GetProperty("n").GetInt32();
                var wo = lauf.GetProperty("name").GetString() + " n=" + n + " " + S(fall.GetProperty("m")) + " " + fall.GetProperty("a").GetRawText();
                if (!staende.TryGetValue(n, out var stand))
                {
                    stand = staende[n] = new Spielstand(ereignisse.Take(n), Inhalt);
                }
                if (fall.TryGetProperty("f", out var text))
                {
                    var ausnahme = Record.Exception(() => Rufen(stand, fall));
                    Assert.True(ausnahme is SpielFehler, wo + ": Python bricht ab mit \"" + S(text) + "\", C# " + (ausnahme?.ToString() ?? "nicht"));
                    Assert.Equal(wo + ": " + S(text), wo + ": " + ausnahme!.Message);
                    fehler++;
                }
                else
                {
                    var ist = Rufen(stand, fall).Select(z => (object?)new List<object?> { z.Art, z.Daten }).ToList();
                    var soll = T(W(fall.GetProperty("e")));
                    Assert.True(soll == T(ist), wo + "\n  Python: " + soll + "\n  C#:     " + T(ist));
                    // Die mitgeschriebenen Ereignisse stehen genau so im Protokoll (dahinter hoechstens Erfolge)
                    var i = n;
                    foreach (var z in fall.GetProperty("e").EnumerateArray())
                    {
                        while (ereignisse[i].Art == Ereignisse.Erfolg)
                        {
                            i++;
                        }
                        Assert.Equal(wo + ": " + T(W(z)), wo + ": " + T(new List<object?> { ereignisse[i].Art, ereignisse[i].Daten }));
                        i++;
                    }
                }
                anzahl++;
            }
        }
        Assert.True(anzahl > 2000 && fehler > 800, anzahl + " Aufrufe, " + fehler + " mit Fehlertext");
    }
}
