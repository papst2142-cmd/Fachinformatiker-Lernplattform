using System.Runtime.CompilerServices;
using System.Text.Json;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Pakete;

/// <summary>
/// Inhalt eines Fachrichtungs-Pakets: je Liste (karteikarten, quizfragen, szenarien_ap2, projektarbeiten, fachgespraech) die
/// Eintraege mit ihrer festen Stelle in der ganzen Datei, die Pruefungssaetze im echten Format mit ihrer Stelle und die
/// Ausschluesse, die Eintraege des Pakets betreffen. Stand und Kennung stehen im Kopf des Pakets.
/// </summary>
public sealed record FachrichtungsInhalt(string Fachrichtung, long Stand,
    IReadOnlyDictionary<string, IReadOnlyList<(int Stelle, JsonElement Eintrag)>> Listen,
    IReadOnlyList<(int Stelle, JsonElement Satz)> Saetze,
    IReadOnlyList<(string Datei, int Stelle, string Frage)> Ausschluss)
{
    /// <summary>Groesse der zwei Inhaltsdateien (Anzeige in der Zeile).</summary>
    public long Groesse { get; init; }

    /// <summary>Hoechste Stelle je Liste plus 1 (0 = leer); fuer den Hinweis nach einem Abgleich.</summary>
    public int Ende(string liste) => Listen.TryGetValue(liste, out var e) && e.Count > 0 ? e[^1].Stelle + 1 : 0;
}

/// <summary>
/// Welle A 0.91.0 (Weisung P12-300, Kurzplan P12-180, Nico 1b 2c 3a 4a 5a, Zusatz a): Fachrichtungs-Pakete (Art
/// <c>lernen</c>, Kennung <c>lernen_ae</c>, <c>lernen_dpa</c>, <c>lernen_dv</c>). Ein Paket bringt alle eigenen Inhalte einer
/// Fachrichtung aus inhalte_csharp/lernen mit ihrer festen Stelle in der ganzen Datei (der Lernstand der Szenarien und
/// Testprojekte merkt sich genau diese Stelle). Regel „nur angehaengt“: jeder Eintrag, den die App selbst fuer diese
/// Fachrichtung an einer Stelle hat, steht im Paket an derselben Stelle gleich; an einer Stelle, an der die App einen
/// Eintrag einer anderen Fachrichtung hat, darf das Paket nichts haben. Sonst ist das Paket rot und wird nicht genommen.
/// <para>
/// Vorbereitung 386 (AE, DPA, DV spaeter nur noch als Paket): die App darf statt eines eigenen Eintrags einen Platzhalter
/// <c>{"fachrichtung": "ae", "im_paket": true}</c> tragen. Er haelt die Stelle frei (die Nummern aller anderen bleiben),
/// wird nie gezeigt und muss im Paket an derselben Stelle stehen.
/// </para>
/// </summary>
public static class FachrichtungsPaket
{
    public const string Praefix = "lernen_";
    public const string LernenDatei = "lernen.json";
    public const string SaetzeDatei = "pruefungssaetze.json";
    public const string ImPaket = "im_paket";
    public const long Format = 1;

    /// <summary>Fachrichtungen, die es als Paket gibt (SI bleibt immer ganz in der App).</summary>
    public static readonly IReadOnlyList<string> Fachrichtungen = ["ae", "dpa", "dv"];

    /// <summary>Listen eines Pakets: die vier Lernlisten und die neuen Fragen fuers Fachgespraech.</summary>
    public static readonly IReadOnlyList<string> Listen =
        [.. Lerninhalte.LernenListen.Select(l => l.Name), Lerninhalte.FachgespraechDatei];

    /// <summary>Kennung des Pakets einer Fachrichtung.</summary>
    public static string Kennung(string fachrichtung) => Praefix + fachrichtung;

    /// <summary>Fachrichtung aus einer Kennung <c>lernen_&lt;fr&gt;</c>, sonst null.</summary>
    public static string? FachrichtungAus(string? kennung) =>
        kennung is not null && kennung.StartsWith(Praefix, StringComparison.Ordinal) && Fachrichtungen.Contains(kennung[Praefix.Length..])
            ? kennung[Praefix.Length..] : null;

    /// <summary>Ist dieses Element ein Platzhalter (Eintrag liegt nur im Paket)?</summary>
    public static bool IstPlatzhalter(JsonElement e) => e.ValueKind == JsonValueKind.Object
        && e.TryGetProperty(ImPaket, out var p) && p.ValueKind == JsonValueKind.True;

    /// <summary>Fachrichtung eines Elements (Feld "fachrichtung"), sonst null.</summary>
    public static string? FachrichtungVon(JsonElement e) => e.ValueKind == JsonValueKind.Object
        && e.TryGetProperty(Inhalte.Fachrichtungen.Feld, out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : null;

    /// <summary>
    /// Liest und prueft den Aufbau der zwei Inhaltsdateien: Format, Fachrichtung, Stellen ab 0 streng steigend, jeder
    /// Eintrag ein Objekt dieser Fachrichtung (kein Platzhalter), Saetze mit Kennung dieser Fachrichtung, Ausschluesse nur
    /// auf Eintraege des Pakets. Gibt den ersten Grund zurueck.
    /// </summary>
    public static (FachrichtungsInhalt? Inhalt, string? Grund) Lesen(IReadOnlyDictionary<string, byte[]> dateien, string fachrichtung, long stand)
    {
        var (inhalt, grund, ladepruefung) = LesenOffen(dateien, fachrichtung, stand);
        return inhalt is null ? (null, grund) : ladepruefung.GetAwaiter().GetResult() is { } satzGrund ? (null, satzGrund) : (inhalt, null);
    }

    /// <summary>Gelungene Ladepruefung der Saetze (nichts offen).</summary>
    private static readonly Task<string?> OhneGrund = Task.FromResult<string?>(null);

    /// <summary>
    /// Welle A 1.5.0 (Weisung P12-358): wie <see cref="Lesen"/>, aber die Ladepruefung der Saetze (dieselbe wie fuer die Saetze
    /// der App, EchtSaetze) ist eine eigene Aufgabe, die neben dem Rest laeuft: <c>Ladepruefung</c> ergibt null (gut) oder den
    /// Grund. Ein Paket ist erst gut, wenn auch sie gut ist (<see cref="Lesen"/> und <see cref="Paket.Pruefen"/> warten auf sie;
    /// nur der Start der App bildet die Inhalte schon vorher und verwirft sie, wenn die Saetze durchfallen).
    /// </summary>
    public static (FachrichtungsInhalt? Inhalt, string? Grund, Task<string?> Ladepruefung) LesenOffen(IReadOnlyDictionary<string, byte[]> dateien,
        string fachrichtung, long stand) => LesenOffen(dateien, fachrichtung, stand, true);

    /// <summary>
    /// Welle A 1.5.0 (Lauf 1193): wie <see cref="LesenOffen(IReadOnlyDictionary{string, byte[]}, string, long)"/>; ohne
    /// <paramref name="mitLadepruefung"/> entfaellt nur die Ladepruefung der Saetze (Ergebnis gut). Nur fuer ein Paket, dessen
    /// Dateien (Pruefsummen) mit genau dieser App schon ganz geprueft und gut waren (<see cref="IPaketPruefMerker"/>).
    /// </summary>
    public static (FachrichtungsInhalt? Inhalt, string? Grund, Task<string?> Ladepruefung) LesenOffen(IReadOnlyDictionary<string, byte[]> dateien,
        string fachrichtung, long stand, bool mitLadepruefung)
    {
        if (!dateien.TryGetValue(LernenDatei, out var lernen) || !dateien.TryGetValue(SaetzeDatei, out var saetze))
        {
            return (null, PaketTexte.Format(PaketTexte.DateiFehlt, dateien.ContainsKey(LernenDatei) ? SaetzeDatei : LernenDatei), OhneGrund);
        }
        // Welle A 1.5.0 (Weisung P12-358): AE, DPA und DV kommen beim Start nur noch aus den Paketen; die Saetze (Aufbau, danach
        // die Ladepruefung) werden darum neben der grossen Datei lernen.json gelesen, nicht danach. Die Reihenfolge der Gruende
        // bleibt: erst lernen.json, dann der Aufbau der Saetze, dann ihre Ausschluesse, zuletzt die Ladepruefung.
        var satzAufgabe = Task.Run(() => SaetzeLesen(saetze, fachrichtung, mitLadepruefung));
        try
        {
            var l = JsonDocument.Parse(lernen).RootElement;
            string Falsch(string was) => PaketTexte.Format(PaketTexte.LernenAufbau, LernenDatei, was);
            if (l.ValueKind != JsonValueKind.Object || !l.TryGetProperty("format", out var f) || f.ValueKind != JsonValueKind.Number
                || !f.TryGetInt64(out var format) || format < 1)
            {
                return (null, Falsch("format"), OhneGrund);
            }
            if (format > Format)
            {
                return (null, PaketTexte.Format(PaketTexte.FormatNeu, format, Format), OhneGrund);
            }
            if (FachrichtungVon(l) != fachrichtung)
            {
                return (null, Falsch("fachrichtung"), OhneGrund);
            }
            if (!l.TryGetProperty("listen", out var listen) || listen.ValueKind != JsonValueKind.Object)
            {
                return (null, Falsch("listen"), OhneGrund);
            }
            var ergebnis = new Dictionary<string, IReadOnlyList<(int, JsonElement)>>(StringComparer.Ordinal);
            foreach (var liste in listen.EnumerateObject())
            {
                if (!Listen.Contains(liste.Name) || ergebnis.ContainsKey(liste.Name))
                {
                    return (null, Falsch("listen." + liste.Name), OhneGrund);
                }
                var (eintraege, grund) = Stellen(liste.Value, "eintrag", fachrichtung);
                if (eintraege is null)
                {
                    return (null, Falsch(liste.Name + ": " + grund), OhneGrund);
                }
                ergebnis[liste.Name] = eintraege;
            }
            var ausschluss = new List<(string, int, string)>();
            var satzAusschluss = new List<(int Stelle, string Kennung)>();
            if (l.TryGetProperty("ausschluss", out var aus))
            {
                if (aus.ValueKind != JsonValueKind.Array)
                {
                    return (null, Falsch("ausschluss"), OhneGrund);
                }
                foreach (var a in aus.EnumerateArray())
                {
                    // A 0.94.0 (Weisung P12-304): auch Pruefungssaetze des Pakets (Stelle und Kennung, geprueft nach dem Lesen der Saetze)
                    if (a.ValueKind == JsonValueKind.Object && a.TryGetProperty("datei", out var sd) && sd.ValueKind == JsonValueKind.String
                        && sd.GetString() == Pruefung.Echt.EchtSaetze.DateiName)
                    {
                        if (!a.TryGetProperty("stelle", out var ss) || !ss.TryGetInt32(out var satzStelle)
                            || !a.TryGetProperty("kennung", out var sk) || sk.ValueKind != JsonValueKind.String)
                        {
                            return (null, Falsch("ausschluss"), OhneGrund);
                        }
                        satzAusschluss.Add((satzStelle, sk.GetString()!));
                        continue;
                    }
                    if (a.ValueKind != JsonValueKind.Object || !a.TryGetProperty("datei", out var d) || d.ValueKind != JsonValueKind.String
                        || !a.TryGetProperty("stelle", out var s) || !s.TryGetInt32(out var stelle)
                        || !a.TryGetProperty("q", out var q) || q.ValueKind != JsonValueKind.String
                        || !ergebnis.TryGetValue(d.GetString()!, out var ziel)
                        || ziel.FirstOrDefault(e => e.Item1 == stelle) is not { Item2.ValueKind: JsonValueKind.Object } treffer
                        || !treffer.Item2.TryGetProperty("q", out var tq) || tq.ValueKind != JsonValueKind.String || tq.GetString() != q.GetString())
                    {
                        return (null, Falsch("ausschluss"), OhneGrund);
                    }
                    ausschluss.Add((d.GetString()!, stelle, q.GetString()!));
                }
            }
            var (satzListe, satzGrund, ladepruefung) = satzAufgabe.GetAwaiter().GetResult();
            if (satzListe is null)
            {
                return (null, satzGrund, OhneGrund);
            }
            foreach (var (stelle, kennung) in satzAusschluss)
            {
                if (satzListe.FirstOrDefault(e => e.Item1 == stelle) is not { Item2.ValueKind: JsonValueKind.Object } treffer
                    || !treffer.Item2.TryGetProperty("kennung", out var tk) || tk.GetString() != kennung)
                {
                    return (null, Falsch("ausschluss"), OhneGrund);
                }
                ausschluss.Add((Pruefung.Echt.EchtSaetze.DateiName, stelle, kennung));
            }
            return (new FachrichtungsInhalt(fachrichtung, stand, ergebnis, satzListe, ausschluss)
            {
                Groesse = lernen.LongLength + saetze.LongLength,
            }, null, ladepruefung!);
        }
        catch (Exception x) when (x is JsonException or InvalidOperationException or FormatException)
        {
            return (null, PaketTexte.Format(PaketTexte.LernenAufbau, LernenDatei, "JSON"), OhneGrund);
        }
    }

    /// <summary>
    /// Saetze eines Fachrichtungs-Pakets: Format, Stellen, Kennung dieser Fachrichtung und die Ladepruefung wie die Saetze der
    /// App (EchtSaetze, nie still weglassen). Liste = null: Grund ist der fertige Text (wie bisher in <see cref="Lesen"/>).
    /// </summary>
    private static (List<(int, JsonElement)>? Liste, string? Grund, Task<string?>? Ladepruefung) SaetzeLesen(byte[] saetze, string fachrichtung,
        bool mitLadepruefung)
    {
        try
        {
            var s2 = JsonDocument.Parse(saetze).RootElement;
            if (s2.ValueKind != JsonValueKind.Object || !s2.TryGetProperty("format", out var sf) || !sf.TryGetInt32(out var sformat)
                || sformat < 1 || sformat > Pruefung.Echt.EchtSaetze.Format || !s2.TryGetProperty("saetze", out var sl))
            {
                return (null, PaketTexte.Format(PaketTexte.LernenAufbau, SaetzeDatei, "format"), null);
            }
            var (satzListe, satzGrund) = Stellen(sl, "satz", fachrichtung);
            if (satzListe is null)
            {
                return (null, PaketTexte.Format(PaketTexte.LernenAufbau, SaetzeDatei, satzGrund!), null);
            }
            foreach (var (_, satz) in satzListe)
            {
                if (!satz.TryGetProperty("kennung", out var k) || k.ValueKind != JsonValueKind.String
                    || Pruefung.Echt.EchtSaetze.BereichAusKennung(k.GetString()!) is not { } b || b.Fachrichtung != fachrichtung)
                {
                    return (null, PaketTexte.Format(PaketTexte.LernenAufbau, SaetzeDatei, "kennung"), null);
                }
            }
            // jeder Satz besteht dieselbe Ladepruefung wie die Saetze der App (EchtSaetze), nie still weglassen; sie beginnt sofort
            return (satzListe, null, mitLadepruefung ? Task.Run(() => Ladepruefung(sformat, satzListe)) : OhneGrund);
        }
        catch (Exception x) when (x is JsonException or InvalidOperationException or FormatException)
        {
            return (null, PaketTexte.Format(PaketTexte.LernenAufbau, LernenDatei, "JSON"), null);
        }
    }

    /// <summary>Ladepruefung der Saetze eines Pakets wie die der App (EchtSaetze): null = gut, sonst der Grund.</summary>
    private static string? Ladepruefung(int sformat, List<(int, JsonElement)> satzListe)
    {
        try
        {
            var probe = Pruefung.Echt.EchtSaetze.Laden(SaetzeBytes(sformat, satzListe.Select(s => s.Item2)));
            return probe.Fehler.FirstOrDefault() is { } fehler
                ? PaketTexte.Format(PaketTexte.LernenAufbau, SaetzeDatei, fehler.Kennung + ": " + fehler.Meldung) : null;
        }
        catch (Exception x) when (x is JsonException or InvalidOperationException or FormatException)
        {
            return PaketTexte.Format(PaketTexte.LernenAufbau, LernenDatei, "JSON");
        }
    }

    private static (List<(int, JsonElement)>? Liste, string? Grund) Stellen(JsonElement liste, string feld, string fachrichtung)
    {
        if (liste.ValueKind != JsonValueKind.Array)
        {
            return (null, "keine Liste");
        }
        var ergebnis = new List<(int, JsonElement)>();
        var vorher = -1;
        foreach (var e in liste.EnumerateArray())
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("stelle", out var s) || !s.TryGetInt32(out var stelle)
                || !e.TryGetProperty(feld, out var eintrag))
            {
                return (null, "stelle/" + feld);
            }
            if (stelle <= vorher)
            {
                return (null, PaketTexte.Format(PaketTexte.StelleDoppelt, stelle));
            }
            if (eintrag.ValueKind != JsonValueKind.Object || IstPlatzhalter(eintrag) || FachrichtungVon(eintrag) != fachrichtung)
            {
                return (null, PaketTexte.Format(PaketTexte.StelleFremd, stelle));
            }
            ergebnis.Add((stelle, eintrag));
            vorher = stelle;
        }
        return (ergebnis, null);
    }

    private static readonly ConditionalWeakTable<LernenOrdner, Dictionary<string, string?>> Geprueft = new();
    private static readonly Dictionary<string, (FachrichtungsInhalt?, string?, Task<string?>)> Gelesen = new(StringComparer.Ordinal);

    /// <summary>
    /// Wie <see cref="LesenOffen"/>, je <paramref name="schluessel"/> (Fachrichtung, Stand und die schon gepruefte Pruefsumme
    /// beider Dateien) nur einmal (auch die Ladepruefung der Saetze); hoechstens 8 Inhalte gemerkt.
    /// </summary>
    public static (FachrichtungsInhalt? Inhalt, string? Grund, Task<string?> Ladepruefung) LesenGemerkt(string schluessel,
        IReadOnlyDictionary<string, byte[]> dateien, string fachrichtung, long stand) => LesenGemerkt(schluessel, dateien, fachrichtung, stand, true);

    /// <summary>Wie oben; ohne <paramref name="mitLadepruefung"/> siehe <see cref="LesenOffen(IReadOnlyDictionary{string, byte[]}, string, long, bool)"/>.</summary>
    public static (FachrichtungsInhalt? Inhalt, string? Grund, Task<string?> Ladepruefung) LesenGemerkt(string schluessel,
        IReadOnlyDictionary<string, byte[]> dateien, string fachrichtung, long stand, bool mitLadepruefung)
    {
        // ein Eintrag mit Ladepruefung taugt auch ohne; einer ohne nur ohne
        if (!mitLadepruefung)
        {
            lock (Gelesen)
            {
                if (Gelesen.TryGetValue(schluessel, out var mit))
                {
                    return mit;
                }
            }
            schluessel += "|ohne";
        }
        lock (Gelesen)
        {
            if (Gelesen.TryGetValue(schluessel, out var gemerkt))
            {
                return gemerkt;
            }
        }
        var ergebnis = LesenOffen(dateien, fachrichtung, stand, mitLadepruefung);
        lock (Gelesen)
        {
            if (Gelesen.Count >= 8)
            {
                Gelesen.Clear();
            }
            Gelesen[schluessel] = ergebnis;
        }
        return ergebnis;
    }

    /// <summary>
    /// Regel „nur angehaengt“ gegenueber den Inhalten der App (<paramref name="eingebaut"/>): je Liste und bei den Saetzen
    /// steht jeder Eintrag der App fuer diese Fachrichtung (auch ein Platzhalter) im Paket an derselben Stelle (gleich), an
    /// keiner Stelle eines fremden Eintrags steht etwas. null = gut. Das Ergebnis wird je Inhalt (SHA-256) gemerkt.
    /// </summary>
    public static string? NurAngehaengt(FachrichtungsInhalt inhalt, string schluessel, LernenOrdner eingebaut)
    {
        var merker = Geprueft.GetOrCreateValue(eingebaut);
        lock (merker)
        {
            if (merker.TryGetValue(schluessel, out var gemerkt))
            {
                return gemerkt;
            }
        }
        var grund = NurAngehaengtRechnen(inhalt, eingebaut);
        lock (merker)
        {
            merker[schluessel] = grund;
        }
        return grund;
    }

    private static string? NurAngehaengtRechnen(FachrichtungsInhalt inhalt, LernenOrdner eingebaut)
    {
        var fr = inhalt.Fachrichtung;
        foreach (var name in Listen)
        {
            var paket = inhalt.Listen.TryGetValue(name, out var p) ? p : [];
            if (Vergleichen(name, eingebaut.Eingebaut(name), paket, fr) is { } grund)
            {
                return grund;
            }
        }
        return Vergleichen(SaetzeDatei, eingebaut.Eingebaut(Pruefung.Echt.EchtSaetze.DateiName), inhalt.Saetze, fr);
    }

    private static string? Vergleichen(string name, IReadOnlyList<JsonElement> app, IReadOnlyList<(int Stelle, JsonElement Eintrag)> paket, string fr)
    {
        var imPaket = new Dictionary<int, JsonElement>(paket.Count);
        foreach (var (stelle, eintrag) in paket)
        {
            imPaket[stelle] = eintrag;
        }
        for (var i = 0; i < app.Count; i++)
        {
            var eigene = FachrichtungVon(app[i]) == fr;
            var vorhanden = imPaket.TryGetValue(i, out var gegen);
            if (!eigene && vorhanden)
            {
                return PaketTexte.Format(PaketTexte.StelleBelegt, name, i);
            }
            if (eigene && !vorhanden)
            {
                return PaketTexte.Format(PaketTexte.EintragFehlt, name, i);
            }
            if (eigene && !IstPlatzhalter(app[i]) && !JsonElement.DeepEquals(app[i], gegen))
            {
                return PaketTexte.Format(PaketTexte.EintragAnders, name, i);
            }
        }
        return null;
    }

    /// <summary>Fachrichtung (ae, dpa, dv) eines Pruefungssatzes nach seiner Kennung (ae_b1_01), sonst null.</summary>
    public static string? FachrichtungVonSatz(string? satz) =>
        satz is not null && Pruefung.Echt.EchtSaetze.BereichAusKennung(satz) is { } b && Fachrichtungen.Contains(b.Fachrichtung) ? b.Fachrichtung : null;

    /// <summary>
    /// Entfernen gesperrt (Kurzplan P12-180 Abschnitt 4): solange ein Pruefungssatz im echten Format dieser Fachrichtung offen ist
    /// (<paramref name="offenerSatz"/> = Kennung des gespeicherten Satzes, null = keiner). null = Entfernen geht.
    /// </summary>
    public static string? EntfernenSperre(string kennung, string? offenerSatz) =>
        FachrichtungAus(kennung) is { } fr && FachrichtungVonSatz(offenerSatz) == fr
            ? PaketTexte.Format(PaketTexte.FrEntfernenGesperrt, PaketTexte.Fr(fr)) : null;

    /// <summary>Hinweis, wenn der gespeicherte Satz fehlt und zu einem Fachrichtungs-Paket gehoert (sonst null).</summary>
    public static string? SatzFehltText(string? satz) =>
        FachrichtungVonSatz(satz) is { } fr ? PaketTexte.Format(PaketTexte.FrSatzFehlt, PaketTexte.Fr(fr)) : null;

    /// <summary>
    /// Hinweis nach einem Abgleich (Kurzplan P12-180 Abschnitt 4): Fachrichtungen, deren Inhalte der Lernstand kennt, die auf
    /// diesem Geraet aber fehlen. Szenarien und Testprojekte: eine Stelle im Lernstand hinter der bekannten Laenge der Liste
    /// (App oder Paket) oder auf einem Platzhalter ohne Paket; dazu die Sicht, wenn ihr alle Inhalte fehlen. Nur lesen; der
    /// Lernstand bleibt, wie er ist (fehlende Eintraege ruhen).
    /// </summary>
    public static IReadOnlyList<string> FehlenNachAbgleich(Daten.Lernstand lernstand, Lerninhalte alle, LernenOrdner ordner, string sicht)
    {
        var fehlen = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (tabelle, spalte, liste, bisher) in new[]
        {
            ("scenario_events", "scenario_index", "szenarien_ap2", alle.Bisher.Ap2Szenarien.Count),
            ("project_events", "project_index", "projektarbeiten", alle.Bisher.Testprojekte.Count),
        })
        {
            var app = ordner.Eingebaut(liste);
            var laenge = ordner.Laenge(liste);
            foreach (var zeile in lernstand.Abfragen($"SELECT DISTINCT {spalte} FROM {tabelle} WHERE {spalte} >= $n", ("$n", bisher)))
            {
                if (zeile[0] is not long index)
                {
                    continue;
                }
                var stelle = index - bisher;
                if (stelle >= laenge)
                {
                    fehlen.Add(Fachrichtungen.Contains(sicht) ? sicht : Fachrichtungen[0]);
                }
                else if (stelle < app.Count && IstPlatzhalter(app[(int)stelle]) && FachrichtungVon(app[(int)stelle]) is { } fr
                    && !ordner.Pakete.ContainsKey(fr))
                {
                    fehlen.Add(fr);
                }
            }
        }
        if (Fachrichtungen.Contains(sicht) && ordner.Fehlt(sicht))
        {
            fehlen.Add(sicht);
        }
        return [.. fehlen];
    }

    /// <summary>Saetze als Datei im Format von pruefungssaetze.json (fuer <see cref="Pruefung.Echt.EchtSaetze.Laden"/>).</summary>
    public static byte[] SaetzeBytes(int format, IEnumerable<JsonElement> saetze)
    {
        using var puffer = new MemoryStream();
        using (var schreiber = new Utf8JsonWriter(puffer))
        {
            schreiber.WriteStartObject();
            schreiber.WriteNumber("format", format);
            schreiber.WriteStartArray("saetze");
            foreach (var satz in saetze)
            {
                satz.WriteTo(schreiber);
            }
            schreiber.WriteEndArray();
            schreiber.WriteEndObject();
        }
        return puffer.ToArray();
    }
}
