using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Pruefung.Echt;

/// <summary>
/// Arten der Pruefung im echten Format in der Tabelle "pruefungen": "echt_ap1", "echt_&lt;Bereich&gt;" (z. B.
/// "echt_netzwerke"). Sie zaehlen nie ins Gesamtergebnis (Nico P12-262, Antwort 2a) und stehen in einer eigenen Liste.
/// </summary>
public static class EchtArten
{
    public const string Praefix = "echt_";

    public static bool IstEcht(string? art) => art is not null && art.StartsWith(Praefix, StringComparison.Ordinal);

    /// <summary>Art fuer einen Bereich einer Fachrichtung (Rolle ap1, b1, b2).</summary>
    public static string Art(Fachrichtung fachrichtung, string rolle) => Praefix + fachrichtung.Bereich(rolle).Art;

    /// <summary>Bereich (Rolle, Name, Kurzname) einer Art; null, wenn es keine Art im echten Format ist.</summary>
    public static Pruefungsbereich? Bereich(string? art) => IstEcht(art)
        ? Fachrichtung.Alle.SelectMany(f => f.Bereiche).FirstOrDefault(b => b.Rolle is "ap1" or "b1" or "b2" && Praefix + b.Art == art)
        : null;

    /// <summary>Verlauf ohne die Pruefungen im echten Format (heutige Karten "Letzte Prüfungen" und Gesamtergebnis).</summary>
    public static List<KlausurEintrag> Ohne(IEnumerable<KlausurEintrag> verlauf) => verlauf.Where(e => !IstEcht(e.Art)).ToList();

    /// <summary>Nur die Pruefungen im echten Format (eigene Liste auf ihrer Karte), neueste zuerst.</summary>
    public static List<KlausurEintrag> Nur(IEnumerable<KlausurEintrag> verlauf) => verlauf.Where(e => IstEcht(e.Art)).ToList();

    /// <summary>Name und Kurzname fuer Listen (Fortschritt, eigene Liste).</summary>
    public static (string Name, string Kurz)? Namen(string? art) => Bereich(art) is { } b
        ? (b.Name + EchtTexte.NameZusatz, b.Kurz + EchtTexte.KurzZusatz) : null;
}

/// <summary>Was die Oberflaeche zeigt.</summary>
public enum EchtAnsicht
{
    Auswahl,
    Lauf,
    Bewertung,
    Ergebnis,
}

/// <summary>Ergebnis eines Schritts: neue Ansicht und ggf. ein Hinweis.</summary>
public sealed record EchtSchritt(EchtAnsicht Ansicht, string? HinweisTitel = null, string? Hinweis = null);

/// <summary>
/// Die laufende Pruefung im echten Format dieses Geraets in einstellungen.json unter einem eigenen Schluessel
/// "laufende_pruefung_echt" (die heutige Klausur liest "laufende_pruefung" und wuerde eine fremde Art dort als
/// "keine Pruefung" sehen und beim Start ueberschreiben). Schreiben nur ueber den uebergebenen Weg
/// (Einstellungen.Aendern).
/// </summary>
public sealed class EchtAblage(Func<JsonObject> laden, Func<Action<JsonObject>, bool> aendern)
{
    public const string Schluessel = "laufende_pruefung_echt";
    public const string KennungSchluessel = "kennung";

    public JsonObject? Laden() => laden()[Schluessel] is JsonObject gespeichert && EchtArten.IstEcht(Json.Text(gespeichert["art"]))
        ? gespeichert.DeepClone().AsObject() : null;

    public bool Speichern(JsonObject zustand)
    {
        var kopie = zustand.DeepClone();
        return aendern(werte => werte[Schluessel] = kopie);
    }

    public bool Loeschen() => aendern(werte => werte.Remove(Schluessel));
}

/// <summary>
/// Welle 1 "Pruefung im echten Format" (Weisung P12-262): Ablauf ohne Oberflaeche wie die heutige Klausur
/// (<see cref="KlausurAblauf"/>): Start nach Rueckfrage, Zeit ueber start_utc ohne Pause, jede Eingabe gespeichert,
/// Abgabe in Phase "bewertung", Selbstbewertung, Abschluss in einer Buchung (uid gegen Doppelbuchung), danach die
/// laufende Pruefung entfernt. Misslingt der Abschluss, bleibt die Bewertung stehen. Mit <see cref="Probe"/> wird
/// nichts gespeichert (Start ohne Bedienung).
/// </summary>
public sealed class EchtAblauf(Func<EchtBestand> bestand, EchtAblage ablage, Func<LernstandSchreiber> schreiber,
    Func<DateTimeOffset> uhr, TimeZoneInfo zone)
{
    private static readonly JsonSerializerOptions Ausgabe = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public EchtBestand Bestand => bestand();

    public JsonObject? Zustand { get; private set; }

    public EchtErgebnis? Ergebnis { get; private set; }

    public string? ErgebnisArt { get; private set; }

    public bool Probe { get; set; }

    private double Jetzt => Klausur.Sekunden(uhr());

    public bool Laeuft => ablage.Laden() is not null;

    public string? Art => Zustand is { } z ? Json.Text(z["art"]) : null;

    public EchtSatz? Satz => Zustand is { } z ? Bestand.Satz(Json.Text(z["satz"])) : null;

    public bool ImLauf => Zustand is { } z && Json.Text(z["phase"]) == Klausur.Laeuft;

    public long Rest => Zustand is { } z ? Klausur.Rest(z, Jetzt, zone) : 0;

    private bool Speichern() => Probe || ablage.Speichern(Zustand!);

    public void ProbeSetzen(JsonObject? zustand, EchtErgebnis? ergebnis = null)
    {
        Probe = true;
        Zustand = zustand;
        Ergebnis = ergebnis;
        ErgebnisArt = zustand is null ? null : Json.Text(zustand["art"]);
    }

    /// <summary>
    /// Naechster Satz eines Bereichs: der erste noch nie bearbeitete (Reihenfolge der Datei), sonst der am
    /// laengsten nicht bearbeitete. Ohne gueltigen Satz null.
    /// </summary>
    public static EchtSatz? NaechsterSatz(IReadOnlyList<EchtSatz> saetze, string art, IEnumerable<KlausurEintrag> verlauf)
    {
        if (saetze.Count == 0)
        {
            return null;
        }
        // Verlauf: neueste zuerst; je Satz die juengste Stelle (kleiner = juenger)
        var zuletzt = new Dictionary<string, int>(StringComparer.Ordinal);
        var stelle = 0;
        foreach (var eintrag in verlauf)
        {
            if (eintrag.Art == art && eintrag.Daten is JsonObject daten && Json.Text(daten["satz"]) is { } kennung)
            {
                zuletzt.TryAdd(kennung, stelle);
            }
            stelle++;
        }
        return saetze.FirstOrDefault(s => !zuletzt.ContainsKey(s.Kennung)) ?? saetze.MaxBy(s => zuletzt[s.Kennung]);
    }

    /// <summary>Neuer Zustand einer Pruefung (Ortszeit und start_utc in ganzen Sekunden).</summary>
    public static JsonObject Neu(EchtSatz satz, string art, DateTimeOffset jetzt, TimeZoneInfo zone)
    {
        var sekunden = (long)Math.Floor(jetzt.ToUnixTimeMilliseconds() / 1000.0);
        var ort = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UnixEpoch.AddSeconds(sekunden), zone);
        var zustand = new JsonObject
        {
            ["art"] = art,
            ["satz"] = satz.Kennung,
            ["start"] = ort.ToString(Klausur.ZeitFormat, CultureInfo.InvariantCulture),
            ["start_utc"] = sekunden,
            ["minuten"] = satz.Minuten,
            ["phase"] = Klausur.Laeuft,
            ["antworten"] = new JsonObject(),
            ["haken"] = new JsonObject(),
            ["zeitfrage"] = null,
        };
        // A 1.8.0 (Weisung P12-372): Fassung des Satzes beim Start (ohne Feld = 1, so bleiben alte Zustaende gleich)
        if (satz.Fassung > 1)
        {
            zustand[FassungSchluessel] = satz.Fassung;
        }
        return zustand;
    }

    public const string FassungSchluessel = "fassung";

    /// <summary>Beiseite gelegte Antworten und Haekchen geaenderter Teilaufgaben, je alter Fassung (nie geloescht).</summary>
    public const string AlteFassungen = "alte_fassungen";

    /// <summary>
    /// A 1.8.0 (Weisung P12-372, E1): ein offener Lauf einer aelteren Fassung geht mit der neuen weiter. Antworten und
    /// Haekchen der Teilaufgaben, die die neue Fassung geaendert hat oder die es in ihr nicht mehr gibt, kommen unter "alte_fassungen"/    /// Haekchen der Teilaufgaben, die die neue Fassung geaendert hat, kommen unter "alte_fassungen"/&lt;alte Fassung&gt;lt;alte Fassung    /// Haekchen der Teilaufgaben, die die neue Fassung geaendert hat, kommen unter "alte_fassungen"/&lt;alte Fassung&gt;gt;
    /// (gespeichert, nicht mehr gezeigt, nichts still verworfen); alle anderen bleiben. true: der Zustand wurde angeglichen.
    /// </summary>
    public static bool Angleichen(JsonObject zustand, EchtSatz satz)
    {
        var stand = Json.Zahl(zustand[FassungSchluessel]) is { } f ? (int)f : 1;
        if (stand >= satz.Fassung)
        {
            return false;
        }
        var antworten = zustand["antworten"] as JsonObject;
        var haken = zustand["haken"] as JsonObject;
        var archiv = zustand[AlteFassungen] as JsonObject ?? [];
        zustand[AlteFassungen] = archiv;
        var name = stand.ToString(CultureInfo.InvariantCulture);
        var beiseite = archiv[name] as JsonObject ?? new JsonObject { ["antworten"] = new JsonObject(), ["haken"] = new JsonObject() };
        archiv[name] = beiseite;
        var alteAntworten = beiseite["antworten"] as JsonObject ?? [];
        beiseite["antworten"] = alteAntworten;
        var alteHaken = beiseite["haken"] as JsonObject ?? [];
        beiseite["haken"] = alteHaken;
        // geaenderte oder neue Teile der neuen Fassung und Teile, die es in ihr nicht mehr gibt (z. B. c geteilt in ca und cb)
        var alle = satz.Aufgaben.SelectMany((a, n) => a.Teile.Select(t => EchtBewertung.Schluessel(n + 1, t.Nr))).ToHashSet(StringComparer.Ordinal);
        var weg = (antworten?.Select(p => p.Key.Split('/')[0]) ?? []).Concat(haken?.Select(p => p.Key) ?? [])
            .Where(k => k.Contains('.', StringComparison.Ordinal) && !alle.Contains(k)).Distinct(StringComparer.Ordinal).ToList();
        var schluesselListe = satz.Aufgaben.SelectMany((a, n) => a.Teile.Where(t => t.Fassung > stand).Select(t => EchtBewertung.Schluessel(n + 1, t.Nr)))
            .Concat(weg).ToList();
        foreach (var schluessel in schluesselListe)
        {
            foreach (var feld in antworten?.Select(p => p.Key).Where(k => k.StartsWith(schluessel + "/", StringComparison.Ordinal)).ToList() ?? [])
            {
                var wert = antworten![feld];
                antworten.Remove(feld);
                alteAntworten[feld] = wert;
            }
            if (haken?[schluessel] is { } h)
            {
                haken.Remove(schluessel);
                alteHaken[schluessel] = h;
            }
        }
        zustand[FassungSchluessel] = satz.Fassung;
        return true;
    }

    /// <summary>Gespeicherte Pruefung fortsetzen; abgelaufen: abgeben. Fehlt ihr Satz (Inhalte geaendert), bleibt sie stehen.</summary>
    public EchtSchritt Zeigen()
    {
        Probe = false;
        Zustand = ablage.Laden();
        if (Zustand is null)
        {
            return new(EchtAnsicht.Auswahl);
        }
        // A 1.18.0 (L0, Nico 5a, P12-437 4a): offener Lauf eines geloeschten Satzes oder einer geloeschten aelteren Fassung:
        // beenden, nicht werten, Hinweis (fertige Pruefungen bleiben im Verlauf)
        var stand = Json.Zahl(Zustand[FassungSchluessel]) is { } gespeichert ? (int)gespeichert : 1;
        if (Bestand.IstGeloescht(Json.Text(Zustand["satz"]), stand))
        {
            ablage.Loeschen();
            Zustand = null;
            return new(EchtAnsicht.Auswahl, KartenTexte.HinweisTitel, EchtTexte.SatzEntfernt);
        }
        if (Satz is null)
        {
            // Welle A 0.91.0: gehoert der Satz zu einem Fachrichtungs-Paket, nennt der Hinweis das Paket; der Stand bleibt gespeichert
            return new(EchtAnsicht.Auswahl, KartenTexte.HinweisTitel,
                Pakete.FachrichtungsPaket.SatzFehltText(Json.Text(Zustand["satz"])) ?? EchtTexte.SatzFehlt);
        }
        // A 0.94.0 (Weisung P12-304): ein ersetzter Satz (ausschluss.json) bleibt im Bestand; der offene Lauf geht mit Hinweis weiter
        var ersetzt = Bestand.IstAusgeschlossen(Satz.Kennung);
        // A 1.8.0 (Weisung P12-372): neue Fassung des Satzes, Antworten der geaenderten Teile beiseite (gespeichert), mit Hinweis
        var neuGefasst = Angleichen(Zustand, Satz);
        if (neuGefasst)
        {
            Speichern();
        }
        var hinweis = ersetzt ? EchtTexte.SatzErsetzt : neuGefasst ? EchtTexte.TeilNeuGefasst : null;
        if (ImLauf)
        {
            return Rest <= 0 ? Abgeben(auto: true)
                : hinweis is not null ? new(EchtAnsicht.Lauf, KartenTexte.HinweisTitel, hinweis) : new(EchtAnsicht.Lauf);
        }
        return hinweis is not null ? new(EchtAnsicht.Bewertung, KartenTexte.HinweisTitel, hinweis) : new(EchtAnsicht.Bewertung);
    }

    /// <summary>Start (nach "Ja"): eine laufende Pruefung wird gezeigt und nie ueberschrieben.</summary>
    public EchtSchritt Starten(EchtSatz satz, string art)
    {
        if (Laeuft)
        {
            return Zeigen();
        }
        var zustand = Neu(satz, art, uhr(), zone);
        zustand[EchtAblage.KennungSchluessel] = LernstandSchreiber.NeueKennung();
        if (!ablage.Speichern(zustand))
        {
            return new(EchtAnsicht.Auswahl, Texte.Optionen.SpeichernFehlgeschlagenTitel, Texte.Optionen.SpeichernFehlgeschlagen);
        }
        Probe = false;
        Zustand = zustand;
        return new(EchtAnsicht.Lauf);
    }

    /// <summary>Geaenderte Felder speichern (nur im Lauf); true: etwas geaendert.</summary>
    public bool Festhalten(IEnumerable<(string Schluessel, string? Text)> felder)
    {
        if (!ImLauf)
        {
            return false;
        }
        var antworten = (JsonObject)Zustand!["antworten"]!;
        var geaendert = false;
        foreach (var (schluessel, text) in felder)
        {
            var wert = Klausur.Feldtext(text);
            var gleich = antworten.ContainsKey(schluessel) ? Json.Text(antworten[schluessel]) == wert : wert.Length == 0;
            if (!gleich)
            {
                antworten[schluessel] = wert;
                geaendert = true;
            }
        }
        if (geaendert)
        {
            Speichern();
        }
        return geaendert;
    }

    /// <summary>Antwort auf die Zeitfrage (unbewertet), auch noch in der Bewertung.</summary>
    public void Zeitfrage(int wahl)
    {
        if (Zustand is null)
        {
            return;
        }
        Zustand["zeitfrage"] = wahl;
        Speichern();
    }

    public int? ZeitfrageWahl => Zustand is { } z && Json.Zahl(z["zeitfrage"]) is { } w ? (int)w : null;

    /// <summary>Abbrechen nach "Ja": verwerfen, kein Ergebnis.</summary>
    public EchtSchritt Abbrechen()
    {
        if (!Probe)
        {
            ablage.Loeschen();
        }
        Zustand = null;
        return new(EchtAnsicht.Auswahl);
    }

    /// <summary>Abgeben: Dauer fest, Phase "bewertung", gespeichert (bei abgelaufener Zeit mit Hinweis).</summary>
    public EchtSchritt Abgeben(bool auto)
    {
        if (!ImLauf)
        {
            return new(Zustand is null ? EchtAnsicht.Auswahl : EchtAnsicht.Bewertung);
        }
        Klausur.Abgeben(Zustand!, Jetzt, zone);
        Speichern();
        return auto ? new(EchtAnsicht.Bewertung, PruefungTexte.ZeitTitel, PruefungTexte.ZeitOffen) : new(EchtAnsicht.Bewertung);
    }

    /// <summary>Haekchen setzen (nur in der Bewertung) und speichern.</summary>
    public void Haken(int aufgabe, EchtTeil teil, int index, bool an)
    {
        if (Zustand is null || ImLauf)
        {
            return;
        }
        var alle = Zustand["haken"] as JsonObject ?? [];
        Zustand["haken"] = alle;
        var schluessel = EchtBewertung.Schluessel(aufgabe, teil.Nr);
        var liste = EchtBewertung.Haken(Zustand, schluessel);
        while (liste.Count < EchtBewertung.HakenAnzahl(teil))
        {
            liste.Add(false);
        }
        liste[index] = an;
        alle[schluessel] = new JsonArray([.. liste.Select(h => (JsonNode?)h)]);
        Speichern();
    }

    /// <summary>Abschluss in einer Buchung; erst danach wird die laufende Pruefung entfernt.</summary>
    public EchtSchritt Abschliessen()
    {
        var art = Art!;
        if (Probe || Satz is not { } satz)
        {
            return new(EchtAnsicht.Bewertung, KartenTexte.HinweisTitel, TrainerTexte.NichtGespeichert);
        }
        var ergebnis = EchtBewertung.Auswerten(satz, Zustand!);
        if (Json.Text(Zustand![EchtAblage.KennungSchluessel]) is not { Length: > 0 } kennung)
        {
            kennung = LernstandSchreiber.NeueKennung();
            Zustand[EchtAblage.KennungSchluessel] = kennung;
            if (!ablage.Speichern(Zustand))
            {
                return new(EchtAnsicht.Bewertung, KartenTexte.HinweisTitel, TrainerTexte.NichtGespeichert);
            }
        }
        if (!schreiber().PruefungAbschliessen(kennung, art, ergebnis.Punkte, ergebnis.Note, (int)ergebnis.Dauer,
                ergebnis.Daten.ToJsonString(Ausgabe), []))
        {
            return new(EchtAnsicht.Bewertung, KartenTexte.HinweisTitel, TrainerTexte.NichtGespeichert);
        }
        ablage.Loeschen();      // misslingt es, bleibt die Pruefung stehen; ein neuer Abschluss bucht nichts doppelt
        Zustand = null;
        Ergebnis = ergebnis;
        ErgebnisArt = art;
        return new(EchtAnsicht.Ergebnis);
    }

    /// <summary>Bearbeitete Teilaufgaben "x von y".</summary>
    public string Fortschritt()
    {
        if (Zustand is not { } z || Satz is not { } satz)
        {
            return "";
        }
        var alle = satz.Aufgaben.Select((a, n) => a.Teile.Select(t => EchtBewertung.Bearbeitet(z, n + 1, t.Nr))).SelectMany(x => x).ToList();
        return Beschriftungen.Einsetzen(PruefungTexte.TeileBearbeitet, alle.Count(b => b), alle.Count);
    }

    /// <summary>Zwischenstand der Bewertung "x von 100 Punkten".</summary>
    public string Zwischenstand()
    {
        if (Zustand is not { } z || Satz is not { } satz)
        {
            return "";
        }
        var punkte = satz.Aufgaben.Select((a, n) => a.Teile.Sum(t => EchtBewertung.Teil(z, n + 1, t).Erreicht)).Sum();
        return Beschriftungen.Einsetzen(EchtTexte.Zwischenstand, punkte);
    }
}
