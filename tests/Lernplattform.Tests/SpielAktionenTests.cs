using System.Security.Cryptography;
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

    private static string Sha(object? wert) => Convert.ToHexStringLower(SHA256.HashData(PythonJson.Utf8(PythonJson.Text(wert))));

    private static List<object?> Paar((string A, string B) p) => [p.A, p.B];

    /// <summary>Die Abfragen der Wohnung wie im Erzeuger (Reihenfolge der Schluessel wie Python).</summary>
    private static PyObjekt WohnungWerte(Spielstand stand)
    {
        var gebaeude = (PyObjekt)Wohnung.Bleibe(stand.Wohnung, stand.Inhalt)!["gebaeude"]!;
        var raster = new List<object?>();
        for (var x = -1; x < (int)(Py.F(gebaeude["breite"]) * 4) + 2; x += 2)
        {
            for (var y = -1; y < (int)(Py.F(gebaeude["hoehe"]) * 4) + 2; y += 2)
            {
                var (px, py) = (x / 4.0, y / 4.0);
                var (titel, text, knoepfe) = Wohnung.Nachricht(px, py, stand);
                raster.Add(new List<object?> { px, py, Wohnung.MoebelBei(stand, px, py)?.Stueck,
                    new List<object?> { titel, text, knoepfe.Select(k => (object?)new List<object?> { k.Aktion, k.Name }).ToList() } });
            }
        }
        var aufgestellt = Wohnung.AufgestellteMoebel(stand).Select(m => (object?)new PyObjekt
        {
            { "typ", m.Typ }, { "x", m.X }, { "y", m.Y }, { "w", m.W }, { "h", m.H }, { "dreh", m.Dreh },
            { "stueck", m.Stueck }, { "moebel", m.Moebel }, { "blick", m.Blick },
        }).ToList();
        var umzuege = Wohnung.Umzuege(stand).Select(w => (object?)new List<object?>
        {
            w["id"], Wohnung.UmzugAngebot(stand, w, false), Wohnung.UmzugAngebot(stand, w, true),
            Paar(Wohnung.UmzugTexte(stand, w, false)), Paar(Wohnung.UmzugTexte(stand, w, true)),
        }).ToList();
        return new PyObjekt
        {
            { "aufgestellt", aufgestellt },
            { "karton", Wohnung.ImKarton(stand).Select(k => (object?)new List<object?> { k.Stueck, k.Moebel }).ToList() },
            { "raster", raster }, { "schlafen", Wohnung.SchlafText(stand) }, { "miete", Wohnung.MietText(stand) }, { "umzuege", umzuege },
        };
    }

    [Fact]
    public void WohnungWiePython()
    {
        var anzahl = 0;
        foreach (var lauf in Soll.GetProperty("laeufe").EnumerateArray())
        {
            var ereignisse = SpielNeuaufbauTests.Ereignisse(lauf.GetProperty("ereignisse"));
            foreach (var probe in lauf.GetProperty("wohnung").EnumerateArray())
            {
                var n = probe.GetProperty("n").GetInt32();
                var wo = lauf.GetProperty("name").GetString() + " n=" + n;
                var ist = WohnungWerte(new Spielstand(ereignisse.Take(n), Inhalt));
                if (probe.TryGetProperty("werte", out var werte))
                {
                    foreach (var (k, v) in (PyObjekt)W(werte)!)
                    {
                        Assert.True(T(v) == T(ist[k]), wo + " " + k + "\n  Python: " + T(v) + "\n  C#:     " + T(ist[k]));
                    }
                }
                Assert.True(S(probe.GetProperty("sha256")) == Sha(ist), wo + " weicht ab: " + T(ist));
                anzahl++;
            }
            foreach (var f in lauf.GetProperty("feierabend_geld").EnumerateArray())
            {
                Assert.Equal(S(f[1]), Wohnung.FeierabendGeldText((PyObjekt)W(f[0])!));
            }
        }
        Assert.True(anzahl > 50, anzahl + " Stichproben");
    }
}
