using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 7, W2: Vergleichswerte G49 (<c>spiel_texte.json</c>, Erzeuger
/// <c>werkzeuge/vergleich/erzeuge_spiel_texte.py</c>): Anzeigetexte der Ticket-Bretter genau wie
/// <c>fisi_game.py</c> (Python 0.63.2). Die Antworten kommen aus G46 (<c>spiel_bewertung.json</c>).
/// </summary>
public class SpielTexteTests
{
    private static readonly JsonElement Soll = VergleichsHilfe.Lade("spiel_texte.json");
    private static readonly JsonElement Bewertung46 = VergleichsHilfe.Lade("spiel_bewertung.json");
    private static readonly Lerninhalte Lernen = Lerninhalte.Laden(Pfade.Inhalte);

    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    private static object? W(JsonElement e) => Py.Lesen(e.GetRawText());

    private static string T(object? wert) => PythonJson.Text(wert);

    private static void Gleich(JsonElement soll, object? ist, string wo) => Assert.Equal(wo + ": " + T(W(soll)), wo + ": " + T(ist));

    private static List<object?> Antworten(string id) => Bewertung46.GetProperty("aufgaben").EnumerateArray()
        .First(a => a.GetProperty("id").GetString() == id && a.GetProperty("extra").ValueKind == JsonValueKind.Null)
        .GetProperty("antworten").EnumerateArray().Select(z => W(z.GetProperty("antwort"))).ToList();

    private static List<object?> L(IEnumerable<string> texte) => [.. texte];

    private static List<object?> Paar((string, bool) p) => [p.Item1, p.Item2];

    [Fact]
    public void TexteJeAuftragWiePython()
    {
        var hw = (PyObjekt)Inhalt["hardware"]!;
        var ids = ((List<object?>)hw["teile"]!).Concat((List<object?>)hw["ersatzteile"]!).Select(t => ((PyObjekt)t!)["id"]).ToList();
        var lagerIds = ids.Where((_, i) => i % 3 != 0).ToList();
        var anzahl = 0;
        foreach (var e in Soll.GetProperty("aufgaben").EnumerateArray())
        {
            var id = e.GetProperty("id").GetString()!;
            var auftrag = Py.Flach(SpielDaten.Auftrag(id, Inhalt)!);
            if (e.TryGetProperty("extra", out var extra))
            {
                foreach (var (k, v) in (PyObjekt)W(extra)!)
                {
                    Py.Setzen(auftrag, k, v);
                }
            }
            var wo = id + (e.TryGetProperty("extra", out _) ? " (extra)" : "");
            var antworten = Antworten(id);
            var anforderungen = auftrag["anforderungen"] as PyObjekt ?? [];
            var wissen = new List<PyObjekt> { new(), new(), new() };
            var i = 0;
            foreach (var (k, v) in anforderungen)
            {
                Py.Setzen(wissen[1], k, 100.0);
                Py.Setzen(wissen[2], k, i++ % 2 == 0 ? Py.F(v) - 0.5 : Py.F(v) + 1.7);
            }
            Gleich(e.GetProperty("luecken"), L(wissen.Select(w => SpielTexte.LueckenWarnung(Bewertung.Luecken(auftrag, w), Lernen))), wo + " gap_warning");
            Gleich(e.GetProperty("loesung"), SpielTexte.LoesungText(auftrag, null, Inhalt), wo + " solution_text");
            Gleich(e.GetProperty("loesung_b"), SpielTexte.LoesungText(auftrag, lagerIds, Inhalt), wo + " solution_text B");
            switch (Py.Str(auftrag["typ"]))
            {
                case "rack":
                    Gleich(e.GetProperty("kopf"), L(SpielTexte.RackKopf(auftrag, Inhalt)), wo + " rack_header");
                    Gleich(e.GetProperty("herkunft"), L(Enumerable.Range(0, ((List<object?>)auftrag["geraete"]!).Count).Select(n => SpielTexte.RackHerkunft(auftrag, n))), wo + " rack_source_text");
                    Gleich(e.GetProperty("zeilen"), antworten.Select(a => (object?)L(SpielTexte.RackZeilen(auftrag, a as PyObjekt, Inhalt))).ToList(), wo + " rack_lines");
                    Gleich(e.GetProperty("zusammen"), antworten.Select(a => (object?)Paar(SpielTexte.RackZusammenfassung(auftrag, a as PyObjekt, Inhalt))).ToList(), wo + " rack_summary");
                    foreach (var s in e.GetProperty("setzen").EnumerateArray())
                    {
                        var antwort = (PyObjekt)W(s[0])!;
                        if (s[1].ValueKind == JsonValueKind.String)
                        {
                            var groesse = Py.Int(((PyObjekt)auftrag["schrank"]!)["he"]);
                            Gleich(s[2], Enumerable.Range(0, (int)groesse + 2).Select(u => (object?)SpielTexte.RackBelegtVon(auftrag, antwort, u, Inhalt)).ToList(), wo + " rack_occupant");
                        }
                        else
                        {
                            Gleich(s[3], SpielTexte.RackSetzen(auftrag, antwort, s[1].GetInt64(), s[2].GetInt64(), Inhalt), wo + " rack_place " + s[1] + " " + s[2]);
                        }
                    }
                    break;
                case "formular":
                    Gleich(e.GetProperty("ausgang"), L(SpielTexte.FormAusgang(auftrag)), wo + " form_given");
                    break;
                case "terminal":
                    Gleich(e.GetProperty("schritte"), L(Enumerable.Range(0, ((List<object?>)auftrag["schritte"]!).Count).Select(n => SpielTexte.TerminalSchritt(auftrag, n))), wo + " terminal_step_label");
                    var versuche = antworten.Select(a => Bewertung.TerminalVersuche(auftrag, a as PyObjekt)).ToList();
                    Gleich(e.GetProperty("versuche"), versuche.Select(v => (object?)v.Select(x => (object?)x.Select(y => (object?)(long)y).ToList()).ToList()).ToList(), wo + " terminal_attempts");
                    Gleich(e.GetProperty("protokoll"), versuche.Select(v => (object?)SpielTexte.TerminalProtokoll(auftrag, v).Select(z => (object?)new List<object?> { z.Rolle, z.Text }).ToList()).ToList(), wo + " terminal_log");
                    Gleich(e.GetProperty("aktuell"), versuche.SelectMany(v => Enumerable.Range(0, v.Count + 1).Select(k => (object?)(SpielTexte.TerminalAktuell(auftrag, v.Take(k).ToList()) is int n ? (long)n : null))).ToList(), wo + " terminal_current_step");
                    Gleich(e.GetProperty("befehle"), L(SpielTexte.TerminalBefehle(auftrag)), wo + " terminal_commands");
                    break;
                case "diagnose":
                    Gleich(e.GetProperty("zaehler"), L(new[] { 0, 1, 2, 5 }.Select(n => SpielTexte.DiagnoseZaehler(auftrag, n))), wo + " diagnosis_counter");
                    Gleich(e.GetProperty("ersatz"), L(new List<object?>?[] { null, [], lagerIds, Enumerable.Reverse(lagerIds).ToList() }.Select(v => SpielTexte.ErsatzImLager(auftrag, v, Inhalt))), wo + " spare_parts_text");
                    Gleich(e.GetProperty("schritte_wahl"), L(SpielTexte.AustauschSchritte(auftrag)), wo + " exchange_steps");
                    break;
                case "wartung":
                    var zaehler = antworten.Select(a => SpielTexte.WartungZaehler(auftrag, a is PyObjekt o ? o["bewertung"] as PyObjekt : null)).ToList();
                    zaehler.Add(SpielTexte.WartungZaehler(auftrag, new PyObjekt { { "gibtsnicht", "auffaellig" } }));
                    Gleich(e.GetProperty("zaehler"), L(zaehler), wo + " maintenance_counter");
                    break;
                case "bestellung":
                    Gleich(e.GetProperty("kopf"), L(SpielTexte.BestellKopf(auftrag)), wo + " order_header");
                    var koerbe = antworten.Where(a => ((PyObjekt?)a ?? []).All(p => Py.Str(p.Value) is var t && t.Length > 0 && t.All(char.IsAsciiDigit))).Cast<PyObjekt?>().ToList();
                    Gleich(e.GetProperty("korb"), koerbe.Select(a => (object?)L(SpielTexte.KorbZeilen(auftrag, a, Inhalt))).ToList(), wo + " cart_lines");
                    Gleich(e.GetProperty("zusammen"), koerbe.Select(a =>
                    {
                        var (text, teuer, spaet) = SpielTexte.KorbZusammenfassung(auftrag, a);
                        return (object?)new List<object?> { text, teuer, spaet };
                    }).ToList(), wo + " cart_summary");
                    break;
                case "bauteile":
                    Gleich(e.GetProperty("vorgaben"), SpielTexte.BauVorgaben(auftrag), wo + " build_rules_text");
                    break;
            }
            anzahl++;
        }
        Assert.True(anzahl > 450);
    }

    [Fact]
    public void TeileUndRaengeWiePython()
    {
        foreach (var f in Soll.GetProperty("teile").EnumerateArray())
        {
            var teil = SpielDaten.Teil(f[0].GetString(), Inhalt)!;
            Assert.Equal(f[0].GetString() + " " + f[1].GetString(), f[0].GetString() + " " + SpielTexte.TeilDaten(teil, Inhalt));
        }
        var r = Soll.GetProperty("raenge");
        var ereignisse = SpielNeuaufbauTests.Ereignisse(r.GetProperty("ereignisse"));
        foreach (var h in r.GetProperty("hinweise").EnumerateArray())
        {
            Assert.Equal(h[1].GetString(), SpielTexte.RangHinweis(new Spielstand(ereignisse.Take(h[0].GetInt32()), Inhalt)));
        }
    }
}
