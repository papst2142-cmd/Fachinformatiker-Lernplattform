using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Fortschritt;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Mathe;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Rechner;
using Lernplattform.Core.Suche;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Projekt;

public static partial class Abschlussprojekt
{
    // -- Zahlen und Texte ----------------------------------------------------------------------

    /// <summary>"%.{stellen}f" % x: exakter Binaerwert, halb gerade gerundet; das Vorzeichen bleibt auch bei -0.</summary>
    public static string Fest(double x, int stellen)
    {
        if (double.IsNaN(x))
        {
            return "nan";
        }
        if (double.IsInfinity(x))
        {
            return x > 0 ? "inf" : "-inf";
        }
        var ziffern = BigInteger.Abs(PythonRunden.Gerundet(x, stellen)).ToString(CultureInfo.InvariantCulture)
            .PadLeft(stellen + 1, '0');
        var text = stellen == 0 ? ziffern : ziffern[..^stellen] + "." + ziffern[^stellen..];
        return (double.IsNegative(x) ? "-" : "") + text;
    }

    /// <summary>"{:,.2f}".format(x): Tausender mit Komma, Dezimalpunkt.</summary>
    private static string MitGruppen(double x)
    {
        var text = Fest(x, 2);
        var minus = text.StartsWith('-');
        var ganz = text[(minus ? 1 : 0)..^3];
        var gruppen = new StringBuilder();
        for (var i = 0; i < ganz.Length; i++)
        {
            if (i > 0 && (ganz.Length - i) % 3 == 0)
            {
                gruppen.Append(',');
            }
            gruppen.Append(ganz[i]);
        }
        return (minus ? "-" : "") + gruppen + text[^3..];
    }

    /// <summary>
    /// to_number: strip, jedes "€" und jedes Leerzeichen (U+0020) weg, leer ergibt die Vorgabe; sonst die
    /// Leseregel der Rechner (Zahlen.Wert, _number_value). Was die Regel ablehnt, ergibt die Vorgabe.
    /// </summary>
    public static double Zahl(string? text, double vorgabe = 0.0)
    {
        var wert = PythonZeichen.Strip(text ?? "").Replace("€", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
        if (wert.Length == 0)
        {
            return vorgabe;
        }
        return Zahlen.Wert(wert, out var zahl, out _) ? zahl : vorgabe;
    }

    /// <summary>euro: "1.234,50⍽€" (geschuetztes Leerzeichen vor dem Zeichen).</summary>
    public static string Euro(double wert) =>
        MitGruppen(wert).Replace(',', 'X').Replace('.', ',').Replace('X', '.') + " €";

    /// <summary>hours_text: "%.1f" mit Komma, jedes ",0" entfaellt, dann " Std.".</summary>
    public static string StundenText(double wert) =>
        Fest(wert, 1).Replace('.', ',').Replace(",0", "", StringComparison.Ordinal) + ProjektTexte.Stunden;

    private static string Feld(IReadOnlyDictionary<string, string> felder, string schluessel) =>
        felder.TryGetValue(schluessel, out var wert) ? wert : "";

    // -- Rechnungen ----------------------------------------------------------------------------

    /// <summary>schedule: Zeilen (Vorgabe, solange das Feld fehlt) und Summe der Zeilen mit Name oder Stunden.</summary>
    public static (List<Zeitplanzeile> Zeilen, double Summe) Zeitplan(IReadOnlyDictionary<string, string> felder)
    {
        var zeilen = new List<Zeitplanzeile>();
        foreach (var phase in Phasen)
        {
            var name = phase.Schluessel.StartsWith("frei", StringComparison.Ordinal)
                ? (felder.TryGetValue("phase_name_" + phase.Schluessel, out var frei) ? frei : phase.Name) : phase.Name;
            var stunden = felder.TryGetValue("phase_" + phase.Schluessel, out var roh) ? Zahl(roh, 0.0) : phase.Vorgabe;
            zeilen.Add(new(phase.Schluessel, name, stunden));
        }
        return (zeilen, PythonSumme.Summe(zeilen.Where(z => z.Name.Length > 0 || z.Stunden != 0).Select(z => z.Stunden)));
    }

    /// <summary>schedule_warnings mit den Projektstunden der Ausbildung.</summary>
    public static List<string> Warnungen(IReadOnlyDictionary<string, string> felder, int maxStunden)
    {
        var (zeilen, summe) = Zeitplan(felder);
        var hinweise = new List<string>();
        if (summe > maxStunden)
        {
            hinweise.Add(Beschriftungen.Einsetzen(ProjektTexte.WarnungSumme, StundenText(summe), maxStunden));
        }
        var doku = zeilen.First(z => z.Schluessel == "dokumentation").Stunden;
        if (summe != 0 && doku / summe * 100 > DokuAnteilMax)
        {
            hinweise.Add(Beschriftungen.Einsetzen(ProjektTexte.WarnungDoku, PythonRunden.Runde(doku / summe * 100)));
        }
        return hinweise;
    }

    /// <summary>costs.</summary>
    public static Kosten Kostenrechnung(IReadOnlyDictionary<string, string> felder)
    {
        double Wert(string schluessel) => Zahl(felder.TryGetValue("kosten_" + schluessel, out var roh)
            ? roh : Kostenfelder.First(k => k.Schluessel == schluessel).Zusatz);
        var stunden = Zeitplan(felder).Summe;
        var personal = stunden * Wert("satz_azubi") + Wert("stunden_ma") * Wert("satz_ma");
        var material = new List<(string Name, double Betrag)>();
        for (var n = 0; n < Sachkostenzeilen; n++)
        {
            var nummer = n.ToString(CultureInfo.InvariantCulture);
            var name = PythonZeichen.Strip(Feld(felder, "material_name_" + nummer));
            var betrag = Zahl(Feld(felder, "material_betrag_" + nummer));
            if (name.Length > 0 || betrag != 0)
            {
                material.Add((name.Length > 0 ? name : Beschriftungen.Einsetzen(ProjektTexte.Position, n + 1), betrag));
            }
        }
        var summe = PythonSumme.Summe(material.Select(m => m.Betrag));
        var gesamt = personal + summe;
        var netto = Wert("einsparung") - Wert("laufend");
        return new(stunden, personal, material, summe, gesamt, netto, netto > 0 ? gesamt / netto : null);
    }

    /// <summary>cost_lines.</summary>
    public static List<string> Kostenzeilen(IReadOnlyDictionary<string, string> felder)
    {
        var k = Kostenrechnung(felder);
        var zeilen = new List<string>
        {
            Beschriftungen.Einsetzen(ProjektTexte.KostenStunden, StundenText(k.Stunden)),
            Beschriftungen.Einsetzen(ProjektTexte.KostenPersonal, Euro(k.Personal)),
            Beschriftungen.Einsetzen(ProjektTexte.KostenSach, Euro(k.MaterialSumme)),
            Beschriftungen.Einsetzen(ProjektTexte.KostenGesamt, Euro(k.Gesamt)),
        };
        zeilen.Add(k.Amortisation is { } monate
            ? Beschriftungen.Einsetzen(ProjektTexte.Amortisation, Fest(monate, 1).Replace('.', ','), Euro(k.NettoMonat))
            : ProjektTexte.KeineAmortisation);
        return zeilen;
    }

    /// <summary>page_estimate: (Zeichen der Kapitel in Codepunkten / 2800, Seitenziel); das Ziel ist int(...) or 15.</summary>
    public static (double Seiten, BigInteger Ziel) Seitenschaetzung(IReadOnlyDictionary<string, string> felder)
    {
        var zeichen = Kapitel.Sum(k => (long)PythonZeichen.Laenge(Feld(felder, "kapitel_" + k.Schluessel)));
        var ziel = new BigInteger(Math.Truncate(felder.TryGetValue(SeitenFeld, out var roh) ? Zahl(roh, SeitenVorgabe) : SeitenVorgabe));
        return (zeichen / (double)ZeichenJeSeite, ziel.IsZero ? SeitenVorgabe : ziel);
    }

    /// <summary>"Etwa %s von %d Seiten" und ob die Schaetzung ueber dem Ziel liegt (rot).</summary>
    public static (string Text, bool Rot) SeitenText(IReadOnlyDictionary<string, string> felder)
    {
        var (seiten, ziel) = Seitenschaetzung(felder);
        return (Beschriftungen.Einsetzen(ProjektTexte.Seiten, Fest(seiten, 1).Replace('.', ','), ziel), seiten > (double)ziel);
    }

    /// <summary>checked.</summary>
    public static bool Abgehakt(IReadOnlyDictionary<string, string> felder, string schluessel) =>
        felder.TryGetValue("check_" + schluessel, out var wert) && wert == "1";

    /// <summary>overview_progress.</summary>
    public static (int Erledigt, int Gesamt) Fortschritt(IReadOnlyDictionary<string, string> felder) =>
        (ChecklisteUebersicht.Count(c => Abgehakt(felder, c.Schluessel)), ChecklisteUebersicht.Count);

    /// <summary>proposal_missing: Namen der leeren Antragsfelder.</summary>
    public static List<string> AntragFehlt(IReadOnlyDictionary<string, string> felder) =>
        Antrag.Where(a => PythonZeichen.Strip(Feld(felder, a.Schluessel)).Length == 0).Select(a => a.Name).ToList();

    /// <summary>estimate: geschaetzte Projektpunkte 0 bis 100 oder null (leer).</summary>
    public static double? Schaetzung(IReadOnlyDictionary<string, string> felder)
    {
        var roh = PythonZeichen.Strip(Feld(felder, SchaetzungFeld));
        return roh.Length == 0 ? null : Math.Max(0.0, Math.Min(100.0, Zahl(roh)));
    }

    /// <summary>project_title.</summary>
    public static string Titel(IReadOnlyDictionary<string, string> felder)
    {
        var titel = PythonZeichen.Strip(Feld(felder, TitelFeld));
        return titel.Length > 0 ? titel : ProjektTexte.OhneTitel;
    }

    /// <summary>question_rating: true (gewusst), false (nicht gewusst) oder null.</summary>
    public static bool? Bewertung(IReadOnlyDictionary<string, string> felder, string frage) =>
        felder.TryGetValue("fg_" + frage, out var wert) && wert.Length > 0 ? wert == "1" : null;

    /// <summary>questions_for: allgemeine Fragen plus die des Fachbereichs, in Dateireihenfolge.</summary>
    public static List<ProjektFrage> FragenFuer(IReadOnlyDictionary<string, string> felder, IReadOnlyList<ProjektFrage> fragen)
    {
        var fachbereich = Feld(felder, FachbereichFeld);
        string? kategorie = fachbereich.Length > 0 ? fachbereich : null;
        return fragen.Where(f => f.Kategorie is null || f.Kategorie == kategorie).ToList();
    }

    /// <summary>delete_question: Satz mit oder ohne Abgleich (eingerichtet = Repository und Schluessel eingetragen).</summary>
    public static string Rueckfrage(IReadOnlyDictionary<string, string> felder, bool abgleich) =>
        Beschriftungen.Einsetzen(abgleich ? ProjektTexte.RueckfrageAbgleich : ProjektTexte.RueckfrageLokal, Titel(felder));

    // -- Export --------------------------------------------------------------------------------

    /// <summary>export_blocks mit den Projektstunden der Ausbildung.</summary>
    public static List<ExportBlock> ExportBloecke(IReadOnlyDictionary<string, string> felder, int maxStunden)
    {
        var bloecke = new List<ExportBlock> { new("h1", Titel(felder)) };
        foreach (var (schluessel, name) in Uebersicht.Skip(1))
        {
            var wert = PythonZeichen.Strip(Feld(felder, schluessel));
            if (wert.Length > 0)
            {
                bloecke.Add(new("p", Beschriftungen.Einsetzen(ProjektTexte.ExportFeld, name, wert)));
            }
        }
        bloecke.Add(new("h2", ProjektTexte.Antrag));
        foreach (var feld in Antrag)
        {
            bloecke.Add(new("h3", feld.Name));
            bloecke.Add(new("p", OderOffen(Feld(felder, feld.Schluessel))));
        }
        bloecke.Add(new("h2", ProjektTexte.Zeitplanung));
        var (zeilen, summe) = Zeitplan(felder);
        foreach (var zeile in zeilen.Where(z => z.Name.Length > 0 || z.Stunden != 0))
        {
            bloecke.Add(new("li", Beschriftungen.Einsetzen(ProjektTexte.ExportFeld,
                zeile.Name.Length > 0 ? zeile.Name : ProjektTexte.Weitere, StundenText(zeile.Stunden))));
        }
        bloecke.Add(new("p", Beschriftungen.Einsetzen(ProjektTexte.ExportSumme, StundenText(summe), maxStunden)));
        bloecke.Add(new("h2", ProjektTexte.Kostenrechnung));
        foreach (var (name, betrag) in Kostenrechnung(felder).Material)
        {
            bloecke.Add(new("li", Beschriftungen.Einsetzen(ProjektTexte.ExportFeld, name, Euro(betrag))));
        }
        bloecke.AddRange(Kostenzeilen(felder).Select(z => new ExportBlock("p", z)));
        bloecke.Add(new("h2", ProjektTexte.Dokumentation));
        foreach (var kapitel in Kapitel)
        {
            bloecke.Add(new("h3", kapitel.Name));
            bloecke.Add(new("p", OderOffen(Feld(felder, "kapitel_" + kapitel.Schluessel))));
        }
        bloecke.Add(new("h2", ProjektTexte.Checkliste));
        foreach (var (schluessel, text) in ChecklisteUebersicht.Concat(ChecklisteAntrag(maxStunden)).Concat(ChecklistePraesentation))
        {
            bloecke.Add(new("li", Beschriftungen.Einsetzen(ProjektTexte.Haken, Abgehakt(felder, schluessel) ? "x" : " ", text)));
        }
        return bloecke;
    }

    private static string OderOffen(string wert)
    {
        var text = PythonZeichen.Strip(wert);
        return text.Length > 0 ? text : ProjektTexte.NochOffen;
    }

    /// <summary>export_text: Zeilen aus den Bloecken, Unterstreichung in Codepunkten, strip, LF am Ende.</summary>
    public static string ExportText(IReadOnlyDictionary<string, string> felder, int maxStunden)
    {
        var zeilen = new List<string>();
        foreach (var block in ExportBloecke(felder, maxStunden))
        {
            switch (block.Art)
            {
                case "h1":
                    zeilen.AddRange([block.Text, new string('=', PythonZeichen.Laenge(block.Text)), ""]);
                    break;
                case "h2":
                    zeilen.AddRange(["", block.Text, new string('-', PythonZeichen.Laenge(block.Text))]);
                    break;
                case "h3":
                    zeilen.AddRange(["", block.Text]);
                    break;
                case "li":
                    zeilen.Add("  - " + block.Text);
                    break;
                default:
                    zeilen.Add(block.Text);
                    break;
            }
        }
        return PythonZeichen.Strip(string.Join("\n", zeilen)) + "\n";
    }

    /// <summary>Dateibytes des Textexports: PC mit CRLF und UTF-8 mit BOM (app_gui 4736), Handy LF ohne BOM (main.py 2388).</summary>
    public static byte[] TextDatei(string text, bool handy) => handy
        ? new UTF8Encoding(false).GetBytes(text)
        : [.. Encoding.UTF8.GetPreamble(), .. new UTF8Encoding(false).GetBytes(text.Replace("\n", "\r\n", StringComparison.Ordinal))];

    [GeneratedRegex("[^A-Za-z0-9ÄÖÜäöüß_-]+", RegexOptions.CultureInvariant)]
    private static partial Regex NichtImDateinamen();

    /// <summary>export_name: unerlaubte Zeichenfolgen werden "_", "_" am Rand weg, hoechstens 40 Zeichen.</summary>
    public static string ExportName(IReadOnlyDictionary<string, string> felder, string endung)
    {
        var basis = PythonZeichen.Strip(NichtImDateinamen().Replace(Titel(felder), "_"), "_");
        var teil = PythonZeichen.Teil(basis, 0, 40);
        return Beschriftungen.Einsetzen(ProjektTexte.Dateiname, teil.Length > 0 ? teil : ProjektTexte.DateinameVorgabe, endung);
    }

    /// <summary>export_pdf: die Bloecke als PDF; das Datum im Fuss kommt von aussen ("%d.%m.%Y").</summary>
    public static byte[] ExportPdf(IReadOnlyDictionary<string, string> felder, int maxStunden, DateTime tag) =>
        Pdf.Bauen(ExportBloecke(felder, maxStunden), Titel(felder), "", tag);

    // -- Projekte ------------------------------------------------------------------------------

    /// <summary>new_project_id: 12 Hex-Zeichen.</summary>
    public static string NeueKennung() => Guid.NewGuid().ToString("N")[..12];

    /// <summary>projects: ohne geloeschte Projekte und ohne die Zeile der Ausbildung (W2, nie ein Projekt).</summary>
    public static OrderedDictionary<string, OrderedDictionary<string, string>> Projekte(
        OrderedDictionary<string, OrderedDictionary<string, string>> alle)
    {
        var projekte = new OrderedDictionary<string, OrderedDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var (projekt, felder) in alle)
        {
            if (projekt != Ausbildung.Zeile && !(felder.TryGetValue(GeloeschtFeld, out var weg) && weg == "1"))
            {
                projekte.Add(projekt, felder);
            }
        }
        return projekte;
    }

    /// <summary>Auswahlliste (_fill_project_menu): Kennungen stabil nach project_title(...).lower() in Codepunkten.</summary>
    public static List<string> Menue(IEnumerable<KeyValuePair<string, OrderedDictionary<string, string>>> projekte) =>
        projekte.OrderBy(p => PythonKlein.Klein(Titel(p.Value)), PyStringVergleich.Instanz).Select(p => p.Key).ToList();

    /// <summary>active_project: gespeichertes aktives Projekt, sonst das kleinste nach Codepunkten, sonst eine neue Kennung.</summary>
    public static string AktivesProjekt(OrderedDictionary<string, OrderedDictionary<string, string>> projekte, string? aktiv,
        Func<string>? neu = null)
    {
        if (aktiv is not null && projekte.ContainsKey(aktiv))
        {
            return aktiv;
        }
        return projekte.Count > 0 ? projekte.Keys.Min(PyStringVergleich.Instanz)! : (neu ?? NeueKennung)();
    }

    /// <summary>create_project: neue Kennung, Titel gespeichert (auch "").</summary>
    public static string Anlegen(LernstandSchreiber schreiber, string titel = "", Func<string>? neu = null)
    {
        var projekt = (neu ?? NeueKennung)();
        schreiber.ProjektFeldSpeichern(projekt, TitelFeld, titel);
        return projekt;
    }

    /// <summary>delete_project in einer Buchung (Diff A). Die Zeile der Ausbildung wird nie geloescht.</summary>
    public static bool Loeschen(LernstandSchreiber schreiber, string projekt) =>
        projekt != Ausbildung.Zeile && schreiber.ProjektLoeschen(projekt);

    /// <summary>
    /// apply_template: Testprojekt als Anregung, nur leere Felder (strip) werden gefuellt; Rueckgabe die
    /// Anzahl (wie Python auch dann gezaehlt, wenn das Speichern misslingt).
    /// </summary>
    public static int VorlageUebernehmen(LernstandSchreiber schreiber, string projekt, Lerninhalt vorlage)
    {
        if (projekt == Ausbildung.Zeile)
        {
            return 0;
        }
        var felder = Projekte(schreiber.ProjektFelder()).TryGetValue(projekt, out var vorhanden) ? vorhanden : [];
        var titel = vorlage.Titel ?? "";
        (string Feld, string Wert)[] werte =
        [
            (TitelFeld, titel),
            ("bezeichnung", titel),
            ("betrieb", vorlage.Text("branche") ?? ""),
            ("ausgangssituation", vorlage.Text("ausgangssituation") ?? ""),
            ("ziel", vorlage.Text("auftrag") ?? ""),
            ("umfeld", string.Join("\n", vorlage.Liste("rahmenbedingungen").Select(r => "- " + r))),
            (FachbereichFeld, vorlage.Fachbereich ?? ""),
        ];
        var anzahl = 0;
        foreach (var (feld, wert) in werte)
        {
            if (wert.Length > 0 && PythonZeichen.Strip(Feld(felder, feld)).Length == 0)
            {
                schreiber.ProjektFeldSpeichern(projekt, feld, wert);
                anzahl++;
            }
        }
        return anzahl;
    }

    /// <summary>Kurzhinweis nach der Vorlage: "%s aus dem Testprojekt übernommen" bzw. alles schon ausgefuellt.</summary>
    public static string VorlageHinweis(int anzahl) => anzahl > 0
        ? Beschriftungen.Einsetzen(ProjektTexte.VorlageUebernommen, Zahlen.Mehrzahl(anzahl, ProjektTexte.Feld, ProjektTexte.Felder))
        : ProjektTexte.VorlageVoll;
}
