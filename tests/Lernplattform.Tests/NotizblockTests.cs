using System.Globalization;
using System.Text;
using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;
using Lernplattform.Core.Rechner;
using Lernplattform.Core.Suche;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 4, PB2: Notizblock und Listen der Szenarien/Testprojekte wie Python v0.63.2
/// (Vergleichswerte G33 notizblock.json, Grundlage testdaten.db mit den Zusatzzeilen daraus).
/// Kontrollfehler muessen rot werden: lower statt casefold, ohne NFC, gelb vor rot, instabile Sortierung.
/// </summary>
public class NotizblockTests
{
    private static readonly JsonElement G33 = VergleichsHilfe.Lade("notizblock.json");
    private static readonly Lerninhalte Inhalte = Lerninhalte.Laden(Pfade.Inhalte);
    private static readonly Lazy<StatusBuch> Buch = new(BuchLesen);

    /// <summary>Kopie von testdaten.db mit den Zusatzzeilen aus G33 (wie der Erzeuger).</summary>
    private static StatusBuch BuchLesen()
    {
        var ordner = Path.Combine(Path.GetTempPath(), "lernplattform_notizblock_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ordner);
        var pfad = Path.Combine(ordner, Lernstand.DateiName);
        File.Copy(Pfade.Vergleich("testdaten.db"), pfad);
        using (var db = VergleichsHilfe.Oeffnen(pfad))
        {
            foreach (var zeile in G33.GetProperty("zusatz").EnumerateArray())
            {
                var spalten = zeile[1].EnumerateObject().ToList();
                VergleichsHilfe.Ausfuehren(db,
                    $"INSERT INTO {zeile[0].GetString()} ({string.Join(", ", spalten.Select(s => s.Name))}) VALUES ({string.Join(", ", spalten.Select((_, i) => "$p" + i))})",
                    [.. spalten.Select(s => VergleichsHilfe.Wert(s.Value))]);
            }
        }
        var buch = new StatusBuch(Antwortbuch.Lesen(new Lernstand(pfad)));
        _ = buch.Antworten.Ergebnisse.Count;
        return buch;
    }

    private static readonly Lazy<Dictionary<(string, string), int>> Nummern = new(() =>
    {
        var nummern = new Dictionary<(string, string), int>();
        var notiz = new Notizblock(Inhalte);
        foreach (var quelle in Quellen.Alle)
        {
            var i = 0;
            foreach (var (schluessel, _) in notiz.Fragen(quelle))
            {
                nummern.TryAdd((quelle, VergleichsHilfe.Text(schluessel)), i++);
            }
        }
        return nummern;
    });

    private static string Kurz(NotizEintrag e) =>
        string.Join("|", e.Quelle, Nummern.Value[(e.Quelle, VergleichsHilfe.Text(e.Schluessel))].ToString(CultureInfo.InvariantCulture), e.Status, e.Stufe, VergleichsHilfe.Text(e.Zeit));

    private static string Kurz(JsonElement e) =>
        string.Join("|", e[0].GetString(), e[1].GetInt32().ToString(CultureInfo.InvariantCulture), e[2].GetString(), e[3].GetString(), VergleichsHilfe.Text(e[4]));

    private static IReadOnlyList<string>? Quellenliste(JsonElement e) =>
        e.ValueKind == JsonValueKind.Null ? null : [.. e.EnumerateArray().Select(q => q.GetString()!)];

    [Fact]
    public void StatusUndStatuszeileWiePython()
    {
        var a = new Abweichungen();
        foreach (var f in G33.GetProperty("status").EnumerateArray())
        {
            var quelle = f[0].GetString()!;
            var schluessel = VergleichsHilfe.Wert(f[1]);
            var (status, stufe) = Buch.Value.Status(quelle, schluessel);
            var (text, farbe) = KartenTexte.StatusZeile(Buch.Value, quelle, schluessel);
            a.Gleich(string.Join("|", f[2].GetString(), f[3].GetString(), f[4].GetString(), f[5].GetString(), VergleichsHilfe.Text(f[6])),
                string.Join("|", status, stufe, text, farbe, VergleichsHilfe.Text(Notizblock.LetzteZeit(Buch.Value, quelle, schluessel!))),
                quelle + " " + VergleichsHilfe.Text(schluessel));
        }
        a.Pruefen(30);
    }

    [Fact]
    public void EintraegeWiePython()
    {
        var notiz = new Notizblock(Inhalte);
        var a = new Abweichungen();
        foreach (var f in G33.GetProperty("eintraege").EnumerateArray())
        {
            var st = f.GetProperty("status");
            var fach = f.GetProperty("fachbereich").GetString()!;
            var thema = f.GetProperty("thema").GetString()!;
            var quellen = Quellenliste(f.GetProperty("quellen"));
            var liste = st.ValueKind == JsonValueKind.String && st.GetString() == "Standard"
                ? notiz.Eintraege(Buch.Value, fachbereich: fach, thema: thema, quellen: quellen)
                : notiz.Eintraege(Buch.Value, st.ValueKind == JsonValueKind.Null ? null : st.GetString(), fach, thema, quellen);
            a.Gleich(string.Join("\n", f.GetProperty("eintraege").EnumerateArray().Select(Kurz)), string.Join("\n", liste.Select(Kurz)),
                $"notebook_entries {st} {fach} {thema} {f.GetProperty("quellen")}");
        }
        a.Pruefen(200);
    }

    [Fact]
    public void UebersichtWiePython()
    {
        var notiz = new Notizblock(Inhalte);
        var a = new Abweichungen();
        foreach (var f in G33.GetProperty("uebersicht").EnumerateArray())
        {
            var ist = notiz.Uebersicht(Buch.Value, f.GetProperty("fachbereich").GetString()!, f.GetProperty("thema").GetString()!);
            a.Gleich(string.Join(";", f.GetProperty("werte").EnumerateObject().Select(q => q.Name + ":" + string.Join(",", q.Value.EnumerateObject().Select(s => s.Name + "=" + s.Value.GetInt32())))),
                string.Join(";", ist.Select(q => q.Key + ":" + string.Join(",", q.Value.Select(s => s.Key + "=" + s.Value)))),
                "notebook_summary " + f.GetProperty("fachbereich") + " " + f.GetProperty("thema"));
        }
        a.Pruefen(9);
    }

    [Fact]
    public void SucheWiePython()
    {
        var notiz = new Notizblock(Inhalte);
        var a = new Abweichungen();
        var basis = new Dictionary<string, List<NotizEintrag>>();
        foreach (var f in G33.GetProperty("suche").EnumerateArray())
        {
            var st = f.GetProperty("status").GetString()!;
            if (!basis.TryGetValue(st, out var eintraege))
            {
                basis[st] = eintraege = notiz.Eintraege(Buch.Value, st);
            }
            var anfrage = f.GetProperty("anfrage").GetString()!;
            a.Gleich(f.GetProperty("schluessel").GetString(), Notizblock.Suchform(anfrage), "search_key " + anfrage);
            a.Gleich(string.Join(",", f.GetProperty("treffer").EnumerateArray().Select(t => t[0].GetString() + t[1].GetInt32())),
                string.Join(",", notiz.Suchen(eintraege, anfrage).Select(e => e.Quelle + Nummern.Value[(e.Quelle, VergleichsHilfe.Text(e.Schluessel))])),
                $"notebook_search {st} \"{anfrage}\"");
        }
        a.Pruefen(100);
    }

    [Fact]
    public void TitelUndMusterantwortWiePython()
    {
        var notiz = new Notizblock(Inhalte);
        var a = new Abweichungen();
        foreach (var f in G33.GetProperty("titel_muster").EnumerateArray())
        {
            var quelle = f[0].GetString()!;
            var eintrag = notiz.Fragen(quelle).ElementAt(f[1].GetInt32()).Eintrag;
            a.Gleich(f[2].GetString(), Notizblock.Titel(quelle, eintrag), "item_title " + quelle + f[1]);
            a.Gleich(f[3].GetString(), Notizblock.Musterantwort(quelle, eintrag), "model_answer " + quelle + f[1]);
        }
        a.Pruefen(30);
    }

    private static IReadOnlyList<Lerninhalt> Daten(string quelle) => quelle switch
    {
        Quellen.Ap1 => Inhalte.Ap1Szenarien,
        Quellen.Ap2 => Inhalte.Ap2Szenarien,
        _ => Inhalte.Testprojekte,
    };

    private static string Positionen(IEnumerable<int> p) => string.Join(",", p);

    private static string Positionen(JsonElement p) => string.Join(",", p.EnumerateArray().Select(x => x.GetInt32()));

    [Fact]
    public void ListenWiePython()
    {
        var a = new Abweichungen();
        foreach (var liste in G33.GetProperty("listen").EnumerateObject())
        {
            var quelle = liste.Name;
            var daten = Daten(quelle);
            var feld = liste.Value.GetProperty("gruppenfeld").GetString()!;
            var stati = Listen.PositionsStati(Buch.Value, quelle);
            a.Gleich(string.Join(";", liste.Value.GetProperty("stati").EnumerateArray().Select(s => s[0].GetInt32() + "=" + s[1].GetString() + "/" + s[2].GetString())),
                string.Join(";", stati.OrderBy(s => s.Key).Select(s => s.Key + "=" + s.Value.Status + "/" + s.Value.Stufe)), "position_statuses " + quelle);
            a.Gleich(string.Join("¦", liste.Value.GetProperty("gruppen").EnumerateArray().Select(g => g.GetString())),
                string.Join("¦", Listen.Gruppenwerte(daten, feld)), "group_values " + quelle);
            foreach (var f in liste.Value.GetProperty("faelle").EnumerateArray())
            {
                a.Gleich(Positionen(f[4]), Positionen(Listen.Positionen(daten, f[0].GetString(), f[1].GetString()!, feld, f[2].GetString()!, f[3].GetString()!, stati)),
                    $"filter_positions {quelle} \"{f[0].GetString()}\" {f[1]} {f[2]} {f[3]}");
            }
            foreach (var f in liste.Value.GetProperty("ohne_gruppe").EnumerateArray())
            {
                a.Gleich(Positionen(f[1]), Positionen(Listen.Positionen(daten, f[0].GetString())), $"filter_positions {quelle} ohne Gruppe \"{f[0].GetString()}\"");
            }
            foreach (var f in liste.Value.GetProperty("seiten").EnumerateArray())
            {
                var (sichtbar, seite, seiten) = Listen.Seite([.. f[0].EnumerateArray().Select(x => x.GetInt32())], f[1].GetInt32(), f[2].GetInt32());
                a.Gleich(Positionen(f[3]) + "/" + f[4].GetInt32() + "/" + f[5].GetInt32(), Positionen(sichtbar) + "/" + seite + "/" + seiten,
                    $"page_slice {quelle} {f[1]} {f[2]}");
            }
            foreach (var f in liste.Value.GetProperty("naechste").EnumerateArray())
            {
                a.Gleich(f[3].GetInt32().ToString(CultureInfo.InvariantCulture),
                    Listen.NaechsteNach(f[0].GetInt32(), [.. f[1].EnumerateArray().Select(x => x.GetInt32())], daten.Count, f[2].GetString()!, stati)
                        .ToString(CultureInfo.InvariantCulture), $"next_after {quelle} {f[0]} {f[2]}");
            }
        }
        a.Pruefen(800);
    }

    /// <summary>Konstanten der Listen wie Python (LIST_PAGE_SIZE, STATUS_FILTERS, Q_STATUS_NAME, SOURCES).</summary>
    [Fact]
    public void KonstantenWiePython()
    {
        var k = G33.GetProperty("konstanten");
        Assert.Equal(15, Listen.Seitengroesse);
        Assert.Equal(15, k.GetProperty("LIST_PAGE_SIZE").GetInt32());
        Assert.Equal(k.GetProperty("STATUS_FILTERS").EnumerateArray().Select(s => s.GetString()!), Listen.StatusFilter);
        Assert.Equal(k.GetProperty("Q_STATUS_NAME").EnumerateObject().Select(p => p.Name + "=" + p.Value.GetString()),
            Listen.StatusName.Select(p => p.Key + "=" + p.Value));
        Assert.Equal(k.GetProperty("SOURCES").EnumerateArray().Select(s => s.GetString()!), Quellen.Alle);
    }

    /// <summary>
    /// Kontrollfehler sichtbar rot: lower statt casefold, ohne NFC, gelb vor rot, instabile
    /// Sortierung. Jede Variante weicht in G33 an mindestens einer Stelle ab.
    /// </summary>
    [Fact]
    public void KontrollfehlerWerdenRot()
    {
        var faelle = G33.GetProperty("suche").EnumerateArray().ToList();
        // lower statt casefold: "STRASSE" findet "straße" nicht mehr (Python casefold: ß = ss)
        Assert.Contains(faelle, f => f.GetProperty("schluessel").GetString() != PythonZeichen.Strip(PythonKlein.Klein(f.GetProperty("anfrage").GetString()!.Normalize(NormalizationForm.FormC))));
        // ohne NFC: zerlegtes "Über" hat eine andere Form als zusammengesetztes
        Assert.Contains(faelle, f => f.GetProperty("schluessel").GetString() != PythonZeichen.Strip(PythonZeichen.Falten(f.GetProperty("anfrage").GetString()!)));

        var notiz = new Notizblock(Inhalte);
        var erwartet = G33.GetProperty("eintraege").EnumerateArray()
            .First(f => f.GetProperty("status").GetString() == "Standard" && f.GetProperty("quellen").ValueKind == JsonValueKind.Null
                && f.GetProperty("fachbereich").GetString() == FragenStatus.Alle && f.GetProperty("thema").GetString() == FragenStatus.Alle)
            .GetProperty("eintraege").EnumerateArray().Select(Kurz).ToList();
        var richtig = notiz.Eintraege(Buch.Value);
        Assert.Equal(erwartet, richtig.Select(Kurz).ToList());
        Assert.Contains(richtig, e => e.Stufe == FragenStatus.Gelb);
        Assert.Contains(richtig, e => e.Stufe == FragenStatus.Rot);
        // gelb vor rot
        var gelbVorRot = richtig.OrderBy(e => e.Stufe == FragenStatus.Gelb ? 0 : 1).ToList();
        Assert.NotEqual(erwartet, gelbVorRot.Select(Kurz).ToList());
        // instabile Sortierung (List.Sort, Introsort) vertauscht gleich alte Eintraege (zwei rote Karten mit gleicher Zeit)
        var instabil = new List<NotizEintrag>(richtig);
        instabil.Reverse();
        instabil.Sort((x, y) => (x.Stufe == FragenStatus.Rot ? 0 : 1).CompareTo(y.Stufe == FragenStatus.Rot ? 0 : 1) is var r and not 0
            ? r : string.CompareOrdinal(Convert.ToString(y.Zeit, CultureInfo.InvariantCulture), Convert.ToString(x.Zeit, CultureInfo.InvariantCulture)));
        Assert.NotEqual(erwartet, instabil.Select(Kurz).ToList());
    }
}
