using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 7, W3: Vergleichswerte G47 (<c>spiel_firma.json</c>, Erzeuger
/// <c>werkzeuge/vergleich/erzeuge_spiel_firma.py</c>): die eigene Firma ueber viele Tage, mit der
/// Python-Spiel-API gespielt (Normal und Einfach, Personal-Ereignisse an). Jeder Aufruf schreibt in
/// C# dieselben Ereignisse oder bricht mit demselben Text ab, und nach jedem Feierabend der Firma
/// ergeben ueber 80 Funktionen des Kerns (Finanzen, Personal, Kredite, Steuer, Werbung, Zertifikate,
/// Markt, Projekte, Serverfarm, Erfolge, Bestenliste, Rueckblick, Texte) dieselben Werte.
/// </summary>
public class SpielFirmaTests
{
    private static readonly JsonElement Soll = VergleichsHilfe.Lade("spiel_firma.json");

    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    private static object? W(JsonElement e) => Py.Lesen(e.GetRawText());

    private static string T(object? wert) => PythonJson.Text(wert);

    private static string S(JsonElement e) => e.GetString()!;

    private static FirmenWissen Wissen(JsonElement lauf)
    {
        var stufen = (PyObjekt)W(lauf.GetProperty("wissen"))!;
        var themen = (PyObjekt)W(lauf.GetProperty("themen_wissen"))!;
        foreach (var (k, v) in themen)
        {
            Py.Setzen(stufen, k, v);
        }
        return new FirmenWissen(stufen, themen, new PySet((List<object?>)W(lauf.GetProperty("lernprojekte"))!));
    }

    private static List<object?>? Team(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : (List<object?>)W(e)!;

    private static List<(string Art, PyObjekt Daten)> Rufen(Spielstand stand, JsonElement fall, FirmenWissen wissen)
    {
        var a = fall.GetProperty("a");
        return S(fall.GetProperty("m")) switch
        {
            "set_profile" => SpielAktionen.Profil(stand, a[0].GetString(), W(a[1]), W(a[2])),
            "found_firm" => SpielAktionen.Gruenden(stand, a[0].GetString()),
            "hire" => SpielAktionen.Einstellen(stand, W(a[0])),
            "fire" => SpielAktionen.Entlassen(stand, W(a[0])),
            "train" => SpielAktionen.Weiterbilden(stand, W(a[0]), S(a[1])),
            "coach" => SpielAktionen.Coachen(stand, W(a[0])),
            "decide" => SpielAktionen.Entscheiden(stand, W(a[0]), W(a[1])),
            "expand" => SpielAktionen.Ausbauen(stand),
            "build_room" => SpielAktionen.RaumBauen(stand, W(a[0])),
            "open_branch" => SpielAktionen.FilialeEroeffnen(stand, a[0].GetString()),
            "expand_branch" => SpielAktionen.FilialeAusbauen(stand),
            "transfer" => SpielAktionen.Versetzen(stand, W(a[0]), W(a[1])),
            "send_offer" => SpielAktionen.Anbieten(stand, W(a[0]), W(a[1]), W(a[2]) as PyObjekt),
            "delegate" => SpielAktionen.Delegieren(stand, W(a[0]), W(a[1]), wissen.Stufen),
            "take_loan" => SpielAktionen.KreditAufnehmen(stand, W(a[0]), W(a[1]), W(a[2])),
            "repay_loan" => SpielAktionen.KreditAbloesen(stand, W(a[0])),
            "book_ad" => SpielAktionen.WerbungBuchen(stand, W(a[0]), W(a[1])),
            "stop_ad" => SpielAktionen.WerbungBeenden(stand, W(a[0])),
            "start_cert" => SpielAktionen.ZertifizierungStarten(stand, W(a[0])),
            "farm_start" => SpielAktionen.FarmStarten(stand),
            "farm_solve" => SpielAktionen.FarmLoesen(stand, W(a[0]), W(a[1]), a[2].GetBoolean(), wissen.Themen, W(a[3])),
            "farm_set_team" => SpielAktionen.FarmTeamSetzen(stand, Team(a[0]), wissen.Themen),
            "farm_toggle_member" => SpielAktionen.FarmMitglied(stand, W(a[0]), wissen.Themen),
            "send_project_offer" => SpielAktionen.ProjektAnbieten(stand, W(a[0]), W(a[1]), W(a[2]) as PyObjekt),
            "set_project_team" => SpielAktionen.ProjektTeamSetzen(stand, W(a[0]), Team(a[1]), wissen.Stufen),
            "toggle_project_member" => SpielAktionen.ProjektMitglied(stand, W(a[0]), W(a[1]), wissen.Stufen),
            "end_day" => SpielAktionen.Feierabend(stand, Py.Wahr(stand.Firma) ? wissen : null),
            var m => throw new InvalidOperationException("unbekannte Methode " + m),
        };
    }

    [Fact]
    public void JederAufrufWiePython()
    {
        var methoden = new HashSet<string>();
        foreach (var lauf in Soll.GetProperty("laeufe").EnumerateArray())
        {
            var ereignisse = SpielNeuaufbauTests.Ereignisse(lauf.GetProperty("ereignisse"));
            var wissen = Wissen(lauf);
            Spielstand? stand = null;
            var bei = -1;
            foreach (var fall in VergleichsHilfe.Mindestens(lauf.GetProperty("faelle"), 900, "faelle"))
            {
                var n = fall.GetProperty("n").GetInt32();
                var wo = lauf.GetProperty("name").GetString() + " n=" + n + " " + S(fall.GetProperty("m")) + " " + fall.GetProperty("a").GetRawText();
                if (stand is null || bei != n)
                {
                    stand = new Spielstand(ereignisse.Take(n), Inhalt);
                    bei = n;
                }
                methoden.Add(S(fall.GetProperty("m")));
                if (fall.TryGetProperty("f", out var text))
                {
                    var ausnahme = Record.Exception(() => Rufen(stand, fall, wissen));
                    Assert.True(ausnahme is SpielFehler, wo + ": Python bricht ab mit \"" + S(text) + "\", C# " + (ausnahme?.ToString() ?? "nicht"));
                    Assert.Equal(wo + ": " + S(text), wo + ": " + ausnahme!.Message);
                }
                else
                {
                    var ist = Rufen(stand, fall, wissen).Select(z => (object?)new List<object?> { z.Art, z.Daten }).ToList();
                    var soll = T(W(fall.GetProperty("e")));
                    Assert.True(soll == T(ist), wo + "\n  Python: " + soll + "\n  C#:     " + T(ist));
                    // Die Ereignisse stehen genau so im Protokoll (dahinter hoechstens Erfolge)
                    for (var i = 0; i < ist.Count; i++)
                    {
                        Assert.Equal(T(ist[i]), T(new List<object?> { ereignisse[n + i].Art, ereignisse[n + i].Daten }));
                    }
                    stand = null;
                }
            }
        }
        // Jede Aktion der Firma kommt mindestens einmal vor
        foreach (var m in new[] { "found_firm", "hire", "fire", "train", "coach", "decide", "expand", "build_room", "open_branch", "expand_branch",
            "transfer", "send_offer", "delegate", "take_loan", "repay_loan", "book_ad", "stop_ad", "start_cert", "farm_start", "farm_solve",
            "farm_set_team", "farm_toggle_member", "send_project_offer", "set_project_team", "toggle_project_member", "end_day" })
        {
            Assert.Contains(m, methoden);
        }
    }

    /// <summary>C#-Tupel (ValueTuple) als Liste wie Python-Tupel, dann wie <c>wert()</c> der Erzeuger.</summary>
    private static object? Wert(object? wert) => PyAusgabe.Wert(Tupel(wert));

    private static object? Tupel(object? wert) => wert switch
    {
        null or string or bool or long or int or double or PyObjekt or PyDict or PySet or PyTupel => wert,
        ITuple t => Enumerable.Range(0, t.Length).Select(i => Tupel(t[i])).ToList(),
        System.Collections.IEnumerable l => l.Cast<object?>().Select(Tupel).ToList(),
        _ => wert,
    };

    private static string Sha(object? wert) => Convert.ToHexStringLower(SHA256.HashData(PythonJson.Utf8(PythonJson.Text(Wert(wert)))));

    private static Dictionary<string, Func<object?>> Funktionen(Spielstand s, FirmenWissen wissen, List<BestenZeile> zeilen)
    {
        var i = Inhalt;
        var t = SpielThemen.Jetzt;
        var stufen = wissen.Stufen;
        var themen = wissen.Themen;
        var faecher = new List<string?> { null };
        faecher.AddRange(t.Fachbereiche);
        return new()
        {
            ["firm_day"] = () => s.FirmenTag(),
            ["staff_list"] = () => s.PersonalListe(),
            ["capacity"] = () => s.Kapazitaet,
            ["firm_costs"] = () => s.FirmenKosten(),
            ["firm_summary"] = () => Betrieb.FirmenZeile(s),
            ["firm_orders_summary"] = () => Betrieb.AuftraegeZeile(s),
            ["projects_summary"] = () => Betrieb.ProjekteZeile(s),
            ["finance_days"] = () => Betrieb.FinanzTage(s),
            ["finance_totals"] = () => Betrieb.FinanzSummen(s),
            ["balance_series"] = () => Betrieb.KontoVerlauf(s),
            ["fixed_costs_text"] = () => Wirtschaft.FesteKostenText(s),
            ["dunning_text"] = () => Wirtschaft.MahnText(s, i),
            ["tax_debt_text"] = () => Wirtschaft.SteuerSchuldText(s, i),
            ["tax_next_due"] = () => Wirtschaft.NaechsteVoranmeldung(s, i),
            ["tax_status"] = () => Wirtschaft.SteuerStand(s, i),
            ["rate_limit"] = () => Wirtschaft.RatenGrenze(s, i),
            ["credit_check"] = () => Wirtschaft.Bonitaet(s, i),
            ["loan_packages"] = () => Wirtschaft.KreditPakete(s, i),
            ["loan_status"] = () => s.Kredite.Values.Select(k => Wirtschaft.KreditStand(s, (PyObjekt)k!, i)).ToList(),
            ["marketing_effects"] = () => Wirtschaft.WerbeWirkung(s),
            ["marketing_summary_text"] = () => Wirtschaft.MarketingText(s),
            ["ad_status"] = () => Wirtschaft.WerbeStand(s),
            ["cert_status"] = () => Wirtschaft.ZertStand(s),
            ["certs_summary_text"] = () => Wirtschaft.ZertText(s),
            ["cert_advantage"] = () => faecher.Select(f => (object?)Wirtschaft.ZertVorteil(s, f)).ToList(),
            ["offer_advantage"] = () => faecher.Select(f => (object?)Betrieb.AngebotVorteil(s, i, f)).ToList(),
            ["offer_advantage_parts"] = () => Wirtschaft.VorteilTeile(s),
            ["market_state"] = () => Betrieb.Marktlage(s, s.Tag, i),
            ["market_shift"] = () => new List<object?> { Betrieb.MarktVerschiebung(s, s.Tag, i), Betrieb.MarktVerschiebung(s, s.Tag, i, trend: true) },
            ["lost_to"] = () => Betrieb.VerlorenAn(s, i),
            ["rivalry_pressure"] = () => Betrieb.Gegenwind(s, s.Tag, i),
            ["pressure_text"] = () => Betrieb.GegenwindText(Betrieb.Gegenwind(s, s.Tag, i)),
            ["market_status"] = () => Betrieb.MarktStand(s, i),
            ["market_news"] = () => Betrieb.MarktNachricht(s, i),
            ["own_ticket_limit"] = () => Betrieb.EigeneTicketGrenze(s, i),
            ["inquiries"] = () => s.Anfragen(),
            ["customer_tickets"] = () => s.KundenTickets(),
            ["ticket_candidates"] = () => s.KundenTickets().Select(k => Betrieb.TicketKandidaten(s, k, stufen, i)).ToList(),
            ["project_limit"] = () => s.ProjektGrenze(),
            ["project_offer_problem"] = () => Betrieb.ProjektAngebotProblem(s, i),
            ["tenders"] = () => s.Ausschreibungen(),
            ["running_projects"] = () => s.LaufendeProjekte(),
            ["project_candidates"] = () => s.LaufendeProjekte().Select(p => Betrieb.ProjektKandidaten(s, p, stufen, i)).ToList(),
            ["project_status_text"] = () => s.LaufendeProjekte().Select(p => Betrieb.ProjektStandText(s, p, stufen, i)).ToList(),
            ["project_team_text"] = () => s.LaufendeProjekte().Select(p => Betrieb.ProjektTeamText(s, p)).ToList(),
            ["applicants"] = () => Betrieb.Bewerbungen(s, i),
            ["switchers"] = () => Betrieb.Wechsler(s, i),
            ["switch_news"] = () => Betrieb.WechselNachricht(s, i),
            ["personal_news"] = () => Betrieb.PersonalNachricht(s, i),
            ["open_decisions"] = () => s.OffeneEntscheidungen(),
            ["firm_tabs"] = () => Betrieb.Reiter(s),
            ["room_status"] = () => Betrieb.RaumStand(s, i),
            ["firm_building"] = () => Betrieb.FirmenGebaeude(s, i),
            ["branch_possible"] = () => Betrieb.FilialeMoeglich(s, i),
            ["branch_status"] = () => Betrieb.FilialStand(s, i),
            ["branch_inquiries"] = () => Betrieb.FilialAnfragen(s, i),
            ["farm_unlock_missing"] = () => Betrieb.FarmFehlt(s, i),
            ["farm_unlock_text"] = () => Betrieb.FarmFreiHinweis(s, i),
            ["farm_status"] = () => Betrieb.FarmStand(s, themen, i),
            ["farm_badge_count"] = () => Betrieb.FarmMarke(s, i),
            ["farm_place_text"] = () => Betrieb.FarmOrtText(s, i),
            ["farm_phase_list"] = () => Py.Wahr(s.Farm) ? Betrieb.FarmPhasenListe(s, i) : null,
            ["farm_open_tasks"] = () => Py.Wahr(s.Farm) ? Betrieb.FarmOffen(s, i) : null,
            ["farm_candidates"] = () => Py.Wahr(s.Farm) ? Betrieb.FarmKandidaten(s, themen, i) : null,
            ["staff_money_text"] = () => s.PersonalListe().Select(e => Betrieb.GeldText(e, i)).ToList(),
            ["training_text"] = () => s.PersonalListe().Select(e => Betrieb.WeiterbildungText(s, e)).ToList(),
            ["strengths_text"] = () => s.PersonalListe().Select(Betrieb.StaerkenText).ToList(),
            ["training_offer"] = () => s.PersonalListe().Take(2)
                .SelectMany(e => new[] { t.Reihenfolge[3], t.Fachbereiche[1] }.Select(z => Betrieb.WeiterbildungAngebot(s, e["id"], z, i))).ToList(),
            ["coaching_offer"] = () => s.PersonalListe().Take(3).Select(e => Betrieb.CoachingAngebot(s, e["id"], i)).ToList(),
            ["run_metrics"] = () => Betrieb.LaufWerte(s, themen, i),
            ["new_achievements"] = () => Betrieb.NeueErfolge(s, themen, i).Select(e => (object?)new List<object?> { e.Regel["id"], e.Stufe }).ToList(),
            ["achievement_overview"] = () => Betrieb.ErfolgUebersicht(s, themen, zeilen, i),
            ["record_board"] = () => Betrieb.BestenTafel(zeilen, s, i),
            ["record_rows"] = () => Betrieb.BestenNeu(s, zeilen, i),
            ["journey"] = () => Betrieb.Reise(s, i),
            ["journey_stats"] = () => Betrieb.ReiseStatistik(s, i),
            ["morning_text"] = () => Betrieb.MorgenText(s.Tag, i, s),
            ["day_end_text"] = () => Betrieb.FeierabendSzene(s.Tag, i, Py.Wahr(s.Firma)),
        };
    }

    [Fact]
    public void JederStandWiePython()
    {
        foreach (var lauf in Soll.GetProperty("laeufe").EnumerateArray())
        {
            var name = lauf.GetProperty("name").GetString();
            var ereignisse = SpielNeuaufbauTests.Ereignisse(lauf.GetProperty("ereignisse"));
            var wissen = Wissen(lauf);
            var alle = lauf.GetProperty("bestenliste").EnumerateArray()
                .Select(z => new BestenZeile(S(z[0]), S(z[1]), S(z[2]), S(z[3]), z[4].ValueKind == JsonValueKind.Null ? null : z[4].GetDouble(), (PyObjekt)W(z[5])!))
                .ToList();
            foreach (var stand in VergleichsHilfe.Mindestens(lauf.GetProperty("staende"), 40, "staende"))
            {
                var n = stand.GetProperty("n").GetInt32();
                var zeit = S(stand.GetProperty("zeit"));
                var s = new Spielstand(ereignisse.Take(n), Inhalt);
                var zeilen = alle.Where(z => string.CompareOrdinal(z.Zeit, zeit) <= 0).ToList();
                var funktionen = Funktionen(s, wissen, zeilen);
                var sha = stand.GetProperty("sha256");
                Assert.Equal(sha.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal), funktionen.Keys.Order(StringComparer.Ordinal));
                foreach (var p in sha.EnumerateObject())
                {
                    var ist = funktionen[p.Name]();
                    if (Sha(ist) == p.Value.GetString())
                    {
                        continue;
                    }
                    var soll = stand.TryGetProperty("werte", out var w) ? w.GetProperty(p.Name).GetRawText() : "(nur SHA-256)";
                    Assert.Fail(name + " n=" + n + " Tag " + s.Tag + " " + p.Name + "\n  Python: " + soll + "\n  C#:     " + T(Wert(ist)));
                }
            }
        }
    }
}
