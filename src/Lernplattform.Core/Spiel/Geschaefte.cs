using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.PyFormat;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Eine Ware im Geschaeft: Moebelstueck (Art "moebel"), Teil fuer die Firma (Art "teil") oder seit Phase 8 W5
/// Kleidung im Kaufhaus (Art "kleidung", <see cref="Typ"/> ist der Slot).
/// </summary>
public sealed record Ware(string Art, string Id, string Name, long Preis, string Typ);

/// <summary>
/// Phase 7, W7 (Weisung P7-12): die Geschaefte Moebelhaus und Elektromarkt (nur C#, ohne Vorlage in
/// Python). Sortiment und Ladenpreise der Teile stehen in <c>inhalte_csharp/spiel/geschaefte.json</c>,
/// Grundriss, Auslage und Verkaeufer in <c>innenraeume.json</c>. Moebel kosten wie Python
/// (<c>wohnungen.json</c>) und werden wie <c>buy_furniture</c> als <c>moebel_gekauft</c> geschrieben
/// (das Stueck kommt in den Karton). Teile gibt es nur mit eigener Firma; der Kauf ist das neue,
/// angehaengte Ereignis <see cref="TeilGekauft"/>, die Ware liegt sofort im Lager. Eingekauft wird nur
/// im geoeffneten Geschaeft, in dem die Figur steht (Tagesuhr der Stadt).
/// </summary>
public sealed class Geschaefte(PyObjekt inhalt, PyObjekt daten)
{
    /// <summary>Neues Ereignis (nur C#): Teil im Elektromarkt gekauft {teil, menge, geld, tag, laden}.</summary>
    public const string TeilGekauft = "teil_gekauft";

    public const string ArtMoebel = "moebel";
    public const string ArtTeil = "teil";

    /// <summary>Die Geschaefte (Ids wie in stadt.json neue_orte).</summary>
    public static readonly IReadOnlyList<string> Laeden = ["moebelhaus", "elektromarkt", Kleidung.Laden];   // Kaufhaus: Phase 8 W5

    public const string NurImLaden = "Einkaufen geht nur in einem geöffneten Geschäft.";
    public const string GibtEsNicht = "Das gibt es hier nicht.";
    public const string NurMitFirma = "Teile für die Firma gibt es erst mit eigener Firma.";

    public static PyObjekt Laden(string ordner) =>
        Py.Lesen(File.ReadAllBytes(Path.Combine(ordner, "geschaefte.json"))) as PyObjekt
        ?? throw new InvalidDataException("geschaefte.json ist kein Objekt");

    public PyObjekt Inhalt { get; } = inhalt;

    public PyObjekt Daten { get; } = daten;

    /// <summary>
    /// Phase 8 W6: die Festzeit, mit der die Ansicht zuletzt gebaut wurde (null = keine); bestimmt die Festkleidung
    /// im Sortiment des Kaufhauses. Setzt die Oberflaeche beim Aufbau einer Ansicht.
    /// </summary>
    public string? Festzeit { get; set; }

    /// <summary>Das Sortiment eines Geschaefts in der Reihenfolge der Datei (erst Moebel, dann Teile).</summary>
    public List<Ware> Waren(string laden)
    {
        var waren = new List<Ware>();
        if (Get(Daten, laden) is not PyObjekt eintrag)
        {
            return waren;
        }
        foreach (var id in ListeOderLeer(eintrag, "moebel"))
        {
            if (Wohnung.MoebelStueck(id, Inhalt) is { } m)
            {
                waren.Add(new Ware(ArtMoebel, Py.Str(id), Py.Str(m["name"]), Py.Int(m["preis"]), Py.Str(m["typ"])));
            }
        }
        foreach (var t in ListeOderLeer(eintrag, "teile").Select(Objekt))
        {
            if (Teil(t["teil"], Inhalt) is { } teil)
            {
                waren.Add(new Ware(ArtTeil, Py.Str(t["teil"]), Py.Str(teil["name"]), Py.Int(t["preis"]), Py.Str(teil["typ"])));
            }
        }
        if (laden == Kleidung.Laden)
        {
            waren.AddRange(Kleidung.Sortiment(Daten, Festzeit).Select(k => new Ware(Kleidung.Art, k.Id, k.Name, k.Preis, k.Slot)));
        }
        return waren;
    }

    public Ware? Finden(string laden, string art, string id) => Waren(laden).FirstOrDefault(w => w.Art == art && w.Id == id);

    /// <summary>Name eines Teile-Typs (hardware.json typen oder ersatz_typen).</summary>
    public static string TypName(string typ, PyObjekt inhalt) => Py.Str(Get(SpielAnsicht.ErsatzTypen(inhalt), typ, typ));

    /// <summary>Steht die Figur im geoeffneten Geschaeft <paramref name="laden"/>? (leer = ja, sonst der Grund)</summary>
    public static string ImLaden(Stadt stadt, Spielstand stand, string laden) =>
        !Laeden.Contains(laden) || stadt.Stand(stand).Ort != laden || stadt.Betreten(stand) != StadtGrund.Ok ? NurImLaden : "";

    /// <summary>Moebelstueck kaufen (wie buy_furniture, stueck = "m" + uuid4().hex[:10] vom Aufrufer).</summary>
    public List<(string Art, PyObjekt Daten)> MoebelKaufen(Stadt stadt, Spielstand stand, string laden, string moebel, string stueck)
    {
        if (ImLaden(stadt, stand, laden) is { Length: > 0 } grund)
        {
            throw new SpielFehler(grund);
        }
        if (Finden(laden, ArtMoebel, moebel) is null)
        {
            throw new SpielFehler(GibtEsNicht);
        }
        return SpielAktionen.MoebelKaufen(stand, moebel, stueck);
    }

    /// <summary>Teil fuer die Firma kaufen: Ladenpreis, sofort im Lager (Ereignis <see cref="TeilGekauft"/>).</summary>
    public List<(string Art, PyObjekt Daten)> TeilKaufen(Stadt stadt, Spielstand stand, string laden, string teil)
    {
        if (ImLaden(stadt, stand, laden) is { Length: > 0 } grund)
        {
            throw new SpielFehler(grund);
        }
        var ware = Finden(laden, ArtTeil, teil) ?? throw new SpielFehler(GibtEsNicht);
        if (!Py.Wahr(stand.Firma))
        {
            throw new SpielFehler(NurMitFirma);
        }
        if (stand.Geld < ware.Preis)
        {
            throw new SpielFehler(Prozent("Dafür reicht dein Geld noch nicht (%d € fehlen).", ware.Preis - Math.Max(0, stand.Geld)));
        }
        return [(TeilGekauft, new PyObjekt
        {
            { "teil", ware.Id }, { "menge", 1L }, { "geld", -ware.Preis }, { "tag", stand.Tag }, { "laden", laden },
        })];
    }

    /// <summary>Fehler in geschaefte.json und den Laeden in innenraeume.json (leer = alles gut).</summary>
    public List<string> Pruefen(PyObjekt? innenraeume)
    {
        var fehler = new List<string>();
        foreach (var (laden, wert) in Daten)
        {
            if (laden == "_hinweis")
            {
                continue;
            }
            if (!Laeden.Contains(laden) || wert is not PyObjekt eintrag)
            {
                fehler.Add("unbekanntes Geschaeft: " + laden);
                continue;
            }
            foreach (var (k, _) in eintrag)
            {
                if (laden == Kleidung.Laden ? k is not ("slots" or "kleidung") : k is not ("moebel" or "teile"))
                {
                    fehler.Add(laden + ": unbekanntes Feld " + k);
                }
            }
            if (laden == Kleidung.Laden)
            {
                fehler.AddRange(Kleidung.Pruefen(eintrag));
            }
            var gesehen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ListeOderLeer(eintrag, "moebel"))
            {
                var m = Wohnung.MoebelStueck(id, Inhalt);
                if (m is null || !Py.Wahr(Get(m, "laden", true)))
                {
                    fehler.Add(laden + ": Moebel nicht im Laden: " + Py.Str(id));
                }
                if (!gesehen.Add("moebel:" + Py.Str(id)))
                {
                    fehler.Add(laden + ": doppelt: " + Py.Str(id));
                }
            }
            foreach (var t in ListeOderLeer(eintrag, "teile"))
            {
                var teil = t as PyObjekt;
                if (teil is null || Teil(Get(teil, "teil"), Inhalt) is null || Get(teil, "teil") is not string id || id.Contains('*'))
                {
                    fehler.Add(laden + ": unbekanntes Teil " + Py.Str(teil is null ? t : Get(teil, "teil")));
                    continue;
                }
                if (!Py.IstNurInt(Get(teil, "preis")) || Py.Int(teil["preis"]) <= 0)
                {
                    fehler.Add(laden + ": Preis fehlt oder ungueltig: " + id);
                }
                if (!gesehen.Add("teil:" + id))
                {
                    fehler.Add(laden + ": doppelt: " + id);
                }
            }
            // Auslage: jede Ware im Grundriss gibt es im Sortiment, jedes Moebel steht aus
            if ((innenraeume is null ? null : Get(innenraeume, laden)) is not PyObjekt raum)
            {
                fehler.Add(laden + ": kein Grundriss in innenraeume.json");
                continue;
            }
            var typen = Waren(laden).Where(w => w.Art == ArtTeil).Select(w => w.Typ).ToHashSet(StringComparer.Ordinal);
            var stellen = Waren(laden).Where(w => w.Art == Kleidung.Art).Select(w => w.Typ).ToHashSet(StringComparer.Ordinal);
            var ausgestellt = new HashSet<string>(StringComparer.Ordinal);
            foreach (var d in Auslage(raum))
            {
                var ware = Py.Str(d["ware"]);
                if (ware.StartsWith("teile:", StringComparison.Ordinal) ? !typen.Contains(ware["teile:".Length..])
                    : ware.StartsWith(Kleidung.Art + ":", StringComparison.Ordinal) ? !stellen.Contains(ware[(Kleidung.Art.Length + 1)..])
                    : !gesehen.Contains("moebel:" + ware))
                {
                    fehler.Add(laden + ": Auslage ohne Ware im Sortiment: " + ware);
                }
                ausgestellt.Add(ware);
            }
            foreach (var id in ListeOderLeer(eintrag, "moebel").Select(Py.Str).Where(id => !ausgestellt.Contains(id)))
            {
                fehler.Add(laden + ": Moebel nicht ausgestellt: " + id);
            }
            foreach (var typ in typen.Where(t => !ausgestellt.Contains("teile:" + t)))
            {
                fehler.Add(laden + ": Teile-Typ ohne Regal: " + typ);
            }
            foreach (var stelle in stellen.Where(t => !ausgestellt.Contains(Kleidung.Art + ":" + t)))
            {
                fehler.Add(laden + ": Kleidung ohne Auslage: " + stelle);
            }
            if (ListeOderLeer(raum, "personen").Count == 0)
            {
                fehler.Add(laden + ": keine Verkaeufer");
            }
        }
        foreach (var laden in Laeden.Where(l => !Py.Hat(Daten, l)))
        {
            fehler.Add("Geschaeft fehlt: " + laden);
        }
        return fehler;
    }

    /// <summary>Einrichtung eines Ladens mit "ware" (Auslage), aus Flur und Raeumen.</summary>
    public static IEnumerable<PyObjekt> Auslage(PyObjekt laden)
    {
        var gebaeude = Objekt(laden["gebaeude"]);
        var deko = Liste(gebaeude["raeume"]).Select(Objekt).SelectMany(r => ListeOderLeer(r, "deko"));
        if (Get(gebaeude, "flur") is PyObjekt flur)
        {
            deko = deko.Concat(ListeOderLeer(flur, "deko"));
        }
        return deko.Select(Objekt).Where(d => Py.Hat(d, "ware"));
    }
}
