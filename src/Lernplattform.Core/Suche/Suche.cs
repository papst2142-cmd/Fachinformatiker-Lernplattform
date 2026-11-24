using System.Runtime.CompilerServices;
using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Design;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Suche;

/// <summary>Ein Treffer der Suche (Art, Fachbereich, Titel, Detail) wie in search_content.</summary>
public sealed record SuchTreffer(string Art, string? Fachbereich, string Titel, string Detail)
{
    /// <summary>Kopfzeile: "Art · Kurzname" bzw. nur die Art (Optionen).</summary>
    public string Kopf => Fachbereich is { Length: > 0 } f
        ? Beschriftungen.Einsetzen(SucheTexte.Kopf, Art, Fachbereiche.Kurzname.GetValueOrDefault(f, ""))
        : Art;

    /// <summary>Hoechstens 140 Zeichen, sonst die ersten 138 und "…" (gezaehlt wie Pythons len).</summary>
    public string Ausschnitt => PyText.Laenge(Detail) <= 140 ? Detail : PyText.Teil(Detail, 0, 138) + SucheTexte.Auslassung;
}

/// <summary>Ergebnis einer Suche: alle Treffer, gezeigt werden die ersten <see cref="GlobaleSuche.Zeilen"/>.</summary>
public sealed record SuchErgebnis(string Anfrage, IReadOnlyList<SuchTreffer> Treffer)
{
    public string Info => Beschriftungen.Einsetzen(SucheTexte.Info, Treffer.Count, Anfrage);

    public IReadOnlyList<SuchTreffer> Gezeigt => Treffer.Count <= GlobaleSuche.Zeilen ? Treffer : Treffer.Take(GlobaleSuche.Zeilen).ToList();

    /// <summary>"… weitere %d Treffer nicht angezeigt." oder null.</summary>
    public string? Weitere => Treffer.Count > GlobaleSuche.Zeilen ? Beschriftungen.Einsetzen(SucheTexte.Weitere, Treffer.Count - GlobaleSuche.Zeilen) : null;
}

/// <summary>
/// Phase 2, L8: Suchtext der Lerninhalte, einmal je Programmlauf vorbereitet wie
/// app_gui.content_search_index (gleiche Felder, gleiche Reihenfolge wie
/// fisi_core.search_content): Karteikarten (Frage, Antwort), Pruefungsfragen (Frage,
/// Erklaerung), AP2- und AP1-Szenarien (Titel + Text + Loesung). Kleinschreibung mit
/// <see cref="PythonKlein"/>.
/// </summary>
public sealed class Suchtext
{
    private readonly List<(SuchTreffer Treffer, string[] Texte)> _eintraege = [];

    /// <summary>Wie oft ein Suchtext gebaut wurde (einmal je Programmlauf, Messwert und Test).</summary>
    public static int Aufbauten => _aufbauten;

    private static int _aufbauten;

    public Suchtext(Lerninhalte inhalte) : this(inhalte, PythonKlein.Klein, Vorrat)
    {
    }

    /// <param name="klein">Kleinschreibung (nur Tests setzen eine andere, Kontrollfehler).</param>
    internal Suchtext(Lerninhalte inhalte, Func<string, string> klein) : this(inhalte, klein, null)
    {
    }

    /// <summary>
    /// Sammelliste 451 (Weisung P12-370): fertige Eintraege je Lerninhalt (Treffer und kleingeschriebene Texte). Nach einem
    /// Wechsel der Fachrichtung baut der neue Suchtext nur die Eintraege neu, die noch keiner gebaut hat; die gemeinsamen
    /// Inhalte (si und schon geladene Fachrichtungen) sind dieselben Objekte und werden uebernommen. Vorher legte jeder
    /// Aufbau rund 25 MB neu an (alle Texte klein geschrieben), die Speicherbereinigung hielt dabei auch die Oberflaeche an.
    /// Ein Lerninhalt aendert sich nach dem Laden nie; der Eintrag ist derselbe wie ohne Vorrat (SucheTests).
    /// </summary>
    private static readonly ConditionalWeakTable<Lerninhalt, Eintrag> Vorrat = new();

    private sealed record Eintrag(SuchTreffer Treffer, string[] Texte);

    private Suchtext(Lerninhalte inhalte, Func<string, string> klein, ConditionalWeakTable<Lerninhalt, Eintrag>? vorrat)
    {
        Klein = klein;
        void Neu(Lerninhalt inhalt, Func<Lerninhalt, Eintrag> bauen)
        {
            var eintrag = vorrat is null ? bauen(inhalt) : vorrat.GetValue(inhalt, i => bauen(i));
            _eintraege.Add((eintrag.Treffer, eintrag.Texte));
        }
        foreach (var karte in inhalte.Karteikarten)
        {
            Neu(karte, k =>
            {
                var (frage, antwort) = (k.Frage ?? "", k.Text("a_full") ?? "");
                return new(new SuchTreffer(SucheTexte.ArtKarte, k.Fachbereich, frage, antwort), [klein(frage), klein(antwort)]);
            });
        }
        foreach (var quiz in inhalte.Quizfragen)
        {
            Neu(quiz, q =>
            {
                var (frage, erklaerung) = (q.Frage ?? "", q.Text("exp") ?? "");
                return new(new SuchTreffer(SucheTexte.ArtQuiz, q.Fachbereich, frage, erklaerung), [klein(frage), klein(erklaerung)]);
            });
        }
        foreach (var (art, liste) in new[] { (SucheTexte.ArtAp2, inhalte.Ap2Szenarien), (SucheTexte.ArtAp1, inhalte.Ap1Szenarien) })
        {
            foreach (var szenario in liste)
            {
                Neu(szenario, sz =>
                {
                    var (titel, text) = (sz.Titel ?? "", sz.Text("text") ?? "");
                    var heuhaufen = titel + text + (sz.Text("solution") ?? "");
                    return new(new SuchTreffer(art, sz.Fachbereich, titel, text.Split('\n')[0]), [klein(heuhaufen)]);
                });
            }
        }
        Interlocked.Increment(ref _aufbauten);
    }

    /// <summary>Fuer Tests: die Eintraege in Suchreihenfolge (Treffer und kleingeschriebene Texte).</summary>
    internal IReadOnlyList<(SuchTreffer Treffer, string[] Texte)> Eintraege => _eintraege;

    internal Func<string, string> Klein { get; }

    public int Anzahl => _eintraege.Count;

    /// <summary>search_content_prepared: Treffer in fester Reihenfolge, Teilstring, kein Ranking.</summary>
    public List<SuchTreffer> Suchen(string anfrage, CancellationToken abbruch = default)
    {
        var nadel = Klein(anfrage);
        var treffer = new List<SuchTreffer>();
        for (var i = 0; i < _eintraege.Count; i++)
        {
            if ((i & 255) == 0)
            {
                abbruch.ThrowIfCancellationRequested();
            }
            var (eintrag, texte) = _eintraege[i];
            foreach (var text in texte)
            {
                if (text.Contains(nadel, StringComparison.Ordinal))
                {
                    treffer.Add(eintrag);
                    break;
                }
            }
        }
        return treffer;
    }
}

/// <summary>
/// Phase 2, L8: globale Suche wie app_gui.SearchView.search / SearchScreen.search:
/// erst die Hilfe-Abschnitte (fisi_hilfe.search_help, ab Phase 4 W4), dann Bereiche der
/// Optionen (fisi_optionen.search_options), dann die Lerninhalte. Hoechstens 60 Zeilen.
/// </summary>
public static class GlobaleSuche
{
    /// <summary>SEARCH_MAX_ROWS (app_gui 8253), Handy hits[:60].</summary>
    public const int Zeilen = 60;

    /// <summary>Woerter, bei denen ein Optionen-Treffer "Vorlagen" mit aufklappt (TEMPLATE_WORDS).</summary>
    public static readonly IReadOnlyList<string> VorlagenWoerter =
    [
        FarbenTexte.VorlagenTitel, "Grundfarbe", "Hintergrund",
        .. Farbwelt.Grundfarben.Select(g => g.Name),
        .. Farbwelt.Hintergruende.Select(h => h.NameDunkel),
        .. Farbwelt.Hintergruende.Select(h => h.NameHell),
    ];

    /// <summary>
    /// Suchbegriff wie die Kopfleiste (TopBar._search) und das Handy: str.strip();
    /// leer = keine Suche (null).
    /// </summary>
    public static string? Anfrage(string? eingabe) => Lernregeln.Strip(eingabe ?? "") is { Length: > 0 } anfrage ? anfrage : null;

    /// <summary>Alle Treffer zu einer (schon bereinigten) Anfrage, Hilfe und Optionen zuerst.</summary>
    public static SuchErgebnis Suchen(Suchtext text, string anfrage, bool pc, CancellationToken abbruch = default)
    {
        var treffer = SchnelleTreffer(anfrage, pc, text.Klein);
        abbruch.ThrowIfCancellationRequested();
        treffer.AddRange(text.Suchen(anfrage, abbruch));
        return new SuchErgebnis(anfrage, treffer);
    }

    /// <summary>Hilfe- und Optionen-Treffer ohne Lerninhalte (solange der Suchtext noch nicht bereit ist).</summary>
    public static List<SuchTreffer> SchnelleTreffer(string anfrage, bool pc) => SchnelleTreffer(anfrage, pc, PythonKlein.Klein);

    private static List<SuchTreffer> SchnelleTreffer(string anfrage, bool pc, Func<string, string> klein)
    {
        var treffer = Hilfe.HilfeTexte.Treffer(anfrage, klein);
        treffer.AddRange(OptionenTreffer(anfrage, pc, klein));
        return treffer;
    }

    /// <summary>search_options: Bereiche, deren Titel oder Stichwoerter die Anfrage enthalten.</summary>
    public static List<SuchTreffer> OptionenTreffer(string anfrage, bool pc) => OptionenTreffer(anfrage, pc, PythonKlein.Klein);

    private static List<SuchTreffer> OptionenTreffer(string anfrage, bool pc, Func<string, string> klein)
    {
        var nadel = klein(Lernregeln.Strip(anfrage));
        var treffer = new List<SuchTreffer>();
        if (nadel.Length == 0)
        {
            return treffer;
        }
        foreach (var bereich in Texte.Optionen.AlleFuer(pc))
        {
            var alle = bereich.StichwoerterFuer(pc);
            var woerter = alle.Where(w => klein(w).Contains(nadel, StringComparison.Ordinal)).ToList();
            if (!klein(bereich.Titel).Contains(nadel, StringComparison.Ordinal) && woerter.Count == 0)
            {
                continue;
            }
            var gezeigt = woerter.Count > 0 ? woerter : alle;
            treffer.Add(new SuchTreffer(Texte.Optionen.SucheArt, null, bereich.Titel,
                SucheTexte.OptionenDetail + string.Join(", ", gezeigt.Take(6))));
        }
        return treffer;
    }

    /// <summary>hit_target: Bereich zu einem Optionen-Treffer und ob "Vorlagen" mit aufklappt.</summary>
    public static (string? Bereich, bool Vorlagen) Ziel(string titel, string anfrage)
    {
        // Patch A 0.37.0: auch Bereiche, die es nur am Handy gibt ("Programm beenden")
        var bereich = Texte.Optionen.AlleFuer(true).Concat(Texte.Optionen.AlleFuer(false)).FirstOrDefault(b => b.Titel == titel);
        if (bereich is null)
        {
            return (null, false);
        }
        var nadel = PythonKlein.Klein(Lernregeln.Strip(anfrage));
        var vorlagen = bereich.Id == Texte.Optionen.Optik && nadel.Length > 0
            && VorlagenWoerter.Any(w => PythonKlein.Klein(w).Contains(nadel, StringComparison.Ordinal));
        return (bereich.Id, vorlagen);
    }
}
