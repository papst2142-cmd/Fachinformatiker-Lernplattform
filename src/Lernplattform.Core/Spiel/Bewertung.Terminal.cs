using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.PyFormat;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W2: Terminal, Diagnose mit Austausch und Wartung wie <c>fisi_game.py</c> (Python
/// 0.63.2, Z. 2264-2630: terminal_review, diagnosis_review, spare_part, spare_fits,
/// exchange_review, exchange_solution, maintenance_review).
/// </summary>
public static partial class Bewertung
{
    /// <summary>Ergebnis von terminal_review.</summary>
    public sealed record TerminalErgebnis(bool Richtig, long Fehlgriffe, long Gefahr, long Offen, List<string> Probleme);

    /// <summary>Ergebnis von diagnosis_review.</summary>
    public sealed record DiagnoseErgebnis(bool Richtig, long Pruefungen, bool Systematisch, long Gefahr, List<string> Probleme);

    /// <summary>Ergebnis von maintenance_review.</summary>
    public sealed record WartungErgebnis(bool Richtig, List<string> Probleme, long Uebersehen);

    public const string BewertungOk = "ok";                   // RATING_OK
    public const string BewertungAuffaellig = "auffaellig";   // RATING_ISSUE

    private static List<PyObjekt> Schritte(PyObjekt auftrag) => Liste(auftrag["schritte"]).Select(Objekt).ToList();

    private static List<PyObjekt> Befehle(PyObjekt schritt) => Liste(schritt["befehle"]).Select(Objekt).ToList();

    /// <summary>terminal_right_index</summary>
    public static int? RichtigerBefehl(PyObjekt schritt)
    {
        var befehle = Befehle(schritt);
        for (var i = 0; i < befehle.Count; i++)
        {
            if (Py.Wahr(Get(befehle[i], "richtig")))
            {
                return i;
            }
        }
        return null;
    }

    /// <summary>terminal_attempts: Versuche je Schritt bis zum richtigen.</summary>
    public static List<List<int>> TerminalVersuche(PyObjekt auftrag, PyObjekt? antwort)
    {
        var gegeben = antwort is not null && Py.Wahr(Get(antwort, "schritte")) ? Liste(antwort["schritte"]) : [];
        var ergebnis = new List<List<int>>();
        var schritte = Schritte(auftrag);
        for (var i = 0; i < schritte.Count; i++)
        {
            var richtig = RichtigerBefehl(schritte[i]);
            var anzahl = Befehle(schritte[i]).Count;
            var versuche = new List<int>();
            var eintrag = i < gegeben.Count ? gegeben[i] : null;
            foreach (var wert in Py.Wahr(eintrag) ? Liste(eintrag) : [])
            {
                if (IntOderNichts(wert) is not long w)
                {
                    continue;
                }
                if (w >= 0 && w < anzahl)
                {
                    versuche.Add((int)w);
                    if (w == richtig)
                    {
                        break;
                    }
                }
            }
            ergebnis.Add(versuche);
        }
        return ergebnis;
    }

    /// <summary>terminal_review</summary>
    public static TerminalErgebnis TerminalPruefen(PyObjekt auftrag, PyObjekt? antwort, PyObjekt inhalt)
    {
        long fehler = 0, gefahr = 0, offen = 0;
        var probleme = new List<string>();
        var schritte = Schritte(auftrag);
        var versuche = TerminalVersuche(auftrag, antwort);
        for (var n = 0; n < schritte.Count; n++)
        {
            var wo = Prozent("Schritt %d (%s)", n + 1, schritte[n]["ziel"]);
            var befehle = Befehle(schritte[n]);
            foreach (var index in versuche[n])
            {
                var befehl = befehle[index];
                var hinweis = Get(befehl, "hinweis", "");
                if (Py.Wahr(Get(befehl, "richtig")))
                {
                    continue;
                }
                if (Py.Wahr(Get(befehl, "gefaehrlich")))
                {
                    gefahr++;
                    probleme.Add(Prozent("%s: „%s“ ist gefährlich. %s", wo, befehl["befehl"], hinweis));
                }
                else
                {
                    fehler++;
                    probleme.Add(Prozent("%s: „%s“ war falsch. %s", wo, befehl["befehl"], hinweis));
                }
            }
            if (RichtigerBefehl(schritte[n]) is not int r || !versuche[n].Contains(r))
            {
                offen++;
                probleme.Add(wo + " wurde nicht abgeschlossen.");
            }
        }
        probleme = probleme.Select(Rechner.PythonZeichen.Strip).ToList();
        var erlaubt = Get(auftrag, "fehlgriffe_erlaubt", Objekt(inhalt["balancing"])["terminal_fehlgriffe_erlaubt"]);
        var richtig = offen == 0 && gefahr == 0 && !Py.Kleiner(erlaubt, fehler);
        return new TerminalErgebnis(richtig, fehler, gefahr, offen, probleme);
    }

    internal static PyObjekt TerminalLoesung(PyObjekt auftrag) => new()
    {
        { "schritte", Schritte(auftrag).Select(s => (object?)new List<object?> { RichtigerBefehl(s) is int i ? (long)i : null }).ToList() },
    };

    private static PyObjekt? DiagnosePruefung(PyObjekt auftrag, object? kennung) =>
        Liste(auftrag["pruefungen"]).Select(Objekt).FirstOrDefault(p => Py.Gleich(p["id"], kennung));

    /// <summary>diagnosis_done: durchgefuehrte Pruefungen ohne doppelte.</summary>
    private static List<object?> DiagnoseErledigt(PyObjekt auftrag, PyObjekt antwort)
    {
        var erledigt = new List<object?>();
        foreach (var kennung in Py.Wahr(Get(antwort, "pruefungen")) ? Liste(antwort["pruefungen"]) : [])
        {
            if (DiagnosePruefung(auftrag, kennung) is not null && !Py.Enthaelt(erledigt, kennung))
            {
                erledigt.Add(kennung);
            }
        }
        return erledigt;
    }

    private static List<object?> Entscheidend(PyObjekt auftrag) =>
        Liste(auftrag["pruefungen"]).Select(Objekt).Where(p => Py.Wahr(Get(p, "entscheidend"))).Select(p => p["id"]).ToList();

    private static string Wahl(PyObjekt antwort, string k) => Get(antwort, k) is var w && Py.Wahr(w) ? Py.Str(w) : "";

    /// <summary>diagnosis_review; verfuegbar = Teile im Lager (null = nicht pruefen).</summary>
    public static DiagnoseErgebnis DiagnosePruefen(PyObjekt auftrag, PyObjekt? antwort, PyObjekt inhalt, IReadOnlyList<object?>? verfuegbar)
    {
        antwort ??= [];
        var erledigt = DiagnoseErledigt(auftrag, antwort);
        var ursache = Wahl(antwort, "ursache");
        var massnahme = Wahl(antwort, "massnahme");
        var schluessel = Entscheidend(auftrag);
        var hinweis = Prozent("Den entscheidenden Hinweis gibt: %s.",
            string.Join(", ", schluessel.Select(k => Prozent("„%s“", DiagnosePruefung(auftrag, k)!["text"]))));
        var probleme = new List<string>();
        if (ursache.Length == 0)
        {
            probleme.Add("Es wurde keine Ursache gewählt.");
        }
        else if (!Py.Gleich(ursache, auftrag["ursache"]))
        {
            probleme.Add(Prozent("Die Ursache „%s“ stimmt nicht. %s", ursache, hinweis));
        }
        var unsicher = Py.Enthaelt(ListeOderLeer(auftrag, "unsicher"), massnahme);
        if (massnahme.Length == 0)
        {
            probleme.Add("Es wurde keine Maßnahme gewählt.");
        }
        else if (unsicher)
        {
            probleme.Add(Prozent("Die Maßnahme „%s“ ist unsicher und kostet Sicherheitsbewusstsein.", massnahme));
        }
        else if (!Py.Gleich(massnahme, auftrag["massnahme"]))
        {
            probleme.Add(Prozent("Die Maßnahme „%s“ behebt den Fehler nicht.", massnahme));
        }
        probleme.AddRange(AustauschPruefen(auftrag, antwort, verfuegbar, inhalt));
        var richtig = probleme.Count == 0;
        var systematisch = richtig && schluessel.All(k => Py.Enthaelt(erledigt, k)) && !Py.Kleiner(auftrag["ziel_pruefungen"], (long)erledigt.Count);
        return new DiagnoseErgebnis(richtig, erledigt.Count, systematisch, unsicher ? 1 : 0, probleme);
    }

    internal static PyObjekt DiagnoseLoesung(PyObjekt auftrag, PyObjekt inhalt, IReadOnlyList<object?>? verfuegbar)
    {
        var ergebnis = new PyObjekt
        {
            { "pruefungen", Entscheidend(auftrag) }, { "ursache", auftrag["ursache"] }, { "massnahme", auftrag["massnahme"] },
        };
        foreach (var (k, v) in AustauschLoesung(auftrag, inhalt, verfuegbar))
        {
            Py.Setzen(ergebnis, k, v);
        }
        return ergebnis;
    }

    /// <summary>spare_part: Ersatzteil aus dem Lager fuer eine Diagnose (oder null).</summary>
    public static object? ErsatzAusLager(PyObjekt auftrag, PyDict? lager, PyObjekt inhalt)
    {
        var erlaubt = ListeOderLeer(auftrag, "aus_lager");
        var ordnung = Liste(Hardware(inhalt)["teile"]).Select(Objekt).Select(t => t["id"]).ToList();
        int Platz(object? k)
        {
            var i = ordnung.FindIndex(o => Py.Gleich(o, k));
            return i >= 0 ? i : ordnung.Count;
        }
        foreach (var kennung in (lager?.Keys ?? []).OrderBy(Platz))
        {
            var teil = Teil(kennung, inhalt);
            if (teil is not null && Py.Enthaelt(erlaubt, teil["typ"]) && Py.Kleiner(0L, lager!.Get(kennung, 0L)))
            {
                return kennung;
            }
        }
        return null;
    }

    private static PyObjekt? Austausch(PyObjekt auftrag) => Py.Wahr(Get(auftrag, "austausch")) ? Objekt(auftrag["austausch"]) : null;

    /// <summary>spare_kinds: {typ: Name} aus typen und ersatz_typen.</summary>
    private static PyObjekt ErsatzTypen(PyObjekt inhalt)
    {
        var arten = Py.Flach(Get(Hardware(inhalt), "typen") as PyObjekt ?? []);
        foreach (var (k, v) in Get(Hardware(inhalt), "ersatz_typen") as PyObjekt ?? [])
        {
            Py.Setzen(arten, k, v);
        }
        return arten;
    }

    /// <summary>spare_fits: Abweichungen eines Ersatzteils als Saetze ([] = passt).</summary>
    public static List<string> ErsatzPasst(PyObjekt auftrag, object? kennung, PyObjekt inhalt)
    {
        var teil = Teil(kennung, inhalt);
        var austausch = Austausch(auftrag) ?? [];
        if (teil is null || !Py.Gleich(teil["typ"], Get(austausch, "typ")))
        {
            var art = Get(ErsatzTypen(inhalt), Py.Str(Get(austausch, "typ")));
            return [Py.Wahr(art) ? Prozent("Das ist kein Teil vom Typ „%s“.", art) : "Das ist kein passendes Teil."];
        }
        var probleme = new List<string>();
        foreach (var (k, soll) in Get(austausch, "passt") is PyObjekt p && p.Count > 0 ? p : [])
        {
            if (!Py.Gleich(Get(teil, k), soll))
            {
                var (name, einheit) = Marke(austausch["typ"], k, inhalt);
                probleme.Add(Prozent("%s %s statt %s", name, MerkmalWert(Get(teil, k), einheit), MerkmalWert(soll, einheit)));
            }
        }
        return probleme;
    }

    /// <summary>spare_catalog: alle Teile vom Typ des Austauschs.</summary>
    private static List<PyObjekt> ErsatzKatalog(PyObjekt auftrag, PyObjekt inhalt)
    {
        var typ = Get(Austausch(auftrag) ?? [], "typ");
        var hardware = Hardware(inhalt);
        return Liste(hardware["teile"]).Concat(Py.Hat(hardware, "ersatzteile") ? Liste(hardware["ersatzteile"]) : [])
            .Select(Objekt).Where(t => Py.Gleich(t["typ"], typ)).ToList();
    }

    /// <summary>exchange_review: Probleme im Austausch-Schritt.</summary>
    public static List<string> AustauschPruefen(PyObjekt auftrag, PyObjekt? antwort, IReadOnlyList<object?>? verfuegbar, PyObjekt inhalt)
    {
        if (Austausch(auftrag) is not { } austausch)
        {
            return [];
        }
        antwort ??= [];
        var probleme = new List<string>();
        var gewaehlt = Get(antwort, "teil");
        if (!Py.Wahr(gewaehlt))
        {
            probleme.Add("Es wurde kein Ersatzteil gewählt.");
        }
        else
        {
            var teil = Teil(gewaehlt, inhalt);
            var passt = ErsatzPasst(auftrag, gewaehlt, inhalt);
            if (passt.Count > 0)
            {
                probleme.Add(Prozent("„%s“ passt nicht: %s.", teil is not null ? teil["name"] : gewaehlt, string.Join(", ", passt)));
            }
            else if (verfuegbar is not null && !Py.Enthaelt(verfuegbar.ToList(), gewaehlt))
            {
                probleme.Add(Prozent("„%s“ liegt nicht im Lager.", teil!["name"]));
            }
        }
        var folge = Py.Wahr(Get(antwort, "reihenfolge")) ? Liste(antwort["reihenfolge"]) : [];
        var falschListe = ListeOderLeer(austausch, "falsch");
        var falsch = folge.Where(t => Py.Enthaelt(falschListe, t)).ToList();
        foreach (var text in falsch)
        {
            probleme.Add(Prozent("„%s“ gehört nicht zum Austausch.", text));
        }
        var richtig = folge.Where(t => !Py.Enthaelt(falsch, t)).ToList();
        var soll = Liste(austausch["schritte"]);
        if (!Py.Gleich(richtig, soll))
        {
            probleme.Add(Prozent("Der Ablauf stimmt nicht. Richtig: %s.", string.Join(" → ",
                soll.Select((t, i) => Prozent("%d. %s", i + 1, Py.Str(t).TrimEnd('.'))))));
        }
        return probleme;
    }

    /// <summary>exchange_solution: zuerst ein passendes Teil aus dem Lager, sonst das guenstigste.</summary>
    private static PyObjekt AustauschLoesung(PyObjekt auftrag, PyObjekt inhalt, IReadOnlyList<object?>? verfuegbar)
    {
        if (Austausch(auftrag) is not { } austausch)
        {
            return [];
        }
        var da = verfuegbar?.ToList() ?? [];
        var passend = ErsatzKatalog(auftrag, inhalt).Where(t => ErsatzPasst(auftrag, t["id"], inhalt).Count == 0)
            .OrderBy(t => Py.Enthaelt(da, t["id"]) ? 0 : 1).ThenBy(t => Py.F(Get(t, "preis", 0L))).ToList();
        return new PyObjekt
        {
            { "teil", passend.Count > 0 ? passend[0]["id"] : null }, { "reihenfolge", Liste(austausch["schritte"]).ToList() },
        };
    }

    /// <summary>maintenance_review</summary>
    public static WartungErgebnis WartungPruefen(PyObjekt auftrag, PyObjekt? antwort)
    {
        antwort ??= [];
        var bewertung = Py.Wahr(Get(antwort, "bewertung")) ? Objekt(antwort["bewertung"]) : [];
        var probleme = new List<string>();
        long uebersehen = 0;
        foreach (var punkt in Liste(auftrag["pruefpunkte"]).Select(Objekt))
        {
            var wert = Get(bewertung, Py.Str(punkt["id"]));
            var soll = Py.Wahr(Get(punkt, "auffaellig")) ? BewertungAuffaellig : BewertungOk;
            var text = Py.Str(punkt["text"]).TrimEnd('.');
            if (wert is null)
            {
                probleme.Add(Prozent("„%s“ wurde nicht geprüft.", text));
            }
            else if (!Py.Gleich(wert, soll))
            {
                if (soll == BewertungAuffaellig)
                {
                    uebersehen++;
                    probleme.Add(Prozent("„%s“ ist auffällig: %s", text, punkt["ergebnis"]));
                }
                else
                {
                    probleme.Add(Prozent("„%s“ ist in Ordnung: %s", text, punkt["ergebnis"]));
                }
            }
        }
        var abschluss = Wahl(antwort, "abschluss");
        if (abschluss.Length == 0)
        {
            probleme.Add("Es wurde kein Abschluss gewählt.");
        }
        else if (!Py.Gleich(abschluss, auftrag["abschluss_antwort"]))
        {
            probleme.Add(Prozent("Der Abschluss „%s“ passt nicht zum Ergebnis.", abschluss));
        }
        return new WartungErgebnis(probleme.Count == 0, probleme, uebersehen);
    }

    internal static PyObjekt WartungLoesung(PyObjekt auftrag)
    {
        var bewertung = new PyObjekt();
        foreach (var punkt in Liste(auftrag["pruefpunkte"]).Select(Objekt))
        {
            Py.Setzen(bewertung, Py.Str(punkt["id"]), Py.Wahr(Get(punkt, "auffaellig")) ? BewertungAuffaellig : BewertungOk);
        }
        return new PyObjekt { { "bewertung", bewertung }, { "abschluss", auftrag["abschluss_antwort"] } };
    }
}
