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

    /// <summary>
    /// Phase 12 Welle 1 (Weisung P12-5): Abschnitt "moebel_neu" in geschaefte.json, nur angehaengt. "moebel" sind Moebel
    /// nur der C#-Linie (wie die Eintraege in wohnungen.json, dazu "geschaeft" und bei Kleiderschraenken "plaetze"), die
    /// <see cref="NeueMoebelEinfuegen"/> beim Laden an die Moebel der Python-Inhalte haengt; "ohne" nimmt Moebel aus dem
    /// Sortiment eines Geschaefts (wer sie hat, behaelt sie).
    /// </summary>
    public const string NeueMoebelAbschnitt = "moebel_neu";

    /// <summary>
    /// Phase 12 Welle 1: die neuen Moebel aus <paramref name="daten"/> (geschaefte.json) an <c>inhalt["wohnungen"]["moebel"]</c>
    /// haengen (Kopien, in der Reihenfolge der Datei). Schon vorhandene Kennungen bleiben unveraendert (zweimal laden aendert
    /// nichts). Danach kennen Kauf, Karton, Einrichtung und Rueckkauf die neuen Moebel wie die aus Python.
    /// </summary>
    public static PyObjekt NeueMoebelEinfuegen(PyObjekt inhalt, PyObjekt? daten)
    {
        if (daten is null || Get(daten, NeueMoebelAbschnitt) is not PyObjekt neu)
        {
            return inhalt;
        }
        var liste = (List<object?>)Objekt(inhalt["wohnungen"])["moebel"]!;
        foreach (var m in ListeOderLeer(neu, "moebel").OfType<PyObjekt>())
        {
            if (Wohnung.MoebelStueck(m["id"], inhalt) is null)
            {
                liste.Add(Py.Kopie(m));
            }
        }
        return inhalt;
    }

    /// <summary>Phase 12 Welle 1: Moebel, die <paramref name="laden"/> laut "moebel_neu" > "ohne" nicht mehr verkauft.</summary>
    public IReadOnlySet<string> Ohne(string laden) =>
        Get(Daten, NeueMoebelAbschnitt) is PyObjekt neu && Get(neu, "ohne") is PyObjekt ohne && Get(ohne, laden) is List<object?> weg
            ? weg.Select(Py.Str).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Phase 12 Welle 1: Kennungen der neuen Moebel fuer <paramref name="laden"/> (Feld "geschaeft").</summary>
    public List<string> NeueMoebel(string laden) =>
        Get(Daten, NeueMoebelAbschnitt) is PyObjekt neu
            ? [.. ListeOderLeer(neu, "moebel").OfType<PyObjekt>().Where(m => Py.Gleich(Get(m, "geschaeft"), laden)).Select(m => Py.Str(m["id"]))]
            : [];

    /// <summary>Moebel im Sortiment von <paramref name="laden"/>: die Liste des Geschaefts ohne "ohne", dann die neuen.</summary>
    private List<object?> MoebelIds(PyObjekt eintrag, string laden)
    {
        var ohne = Ohne(laden);
        return [.. ListeOderLeer(eintrag, "moebel").Where(id => !ohne.Contains(Py.Str(id))), .. NeueMoebel(laden)];
    }

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
        foreach (var id in MoebelIds(eintrag, laden))
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
            throw new SpielFehler(Prozent("Dafür reicht dein Geld noch nicht (%d € fehlen).", checked(ware.Preis - Math.Max(0, stand.Geld))));
        }
        return [(TeilGekauft, new PyObjekt
        {
            { "teil", ware.Id }, { "menge", 1L }, { "geld", checked(-ware.Preis) }, { "tag", stand.Tag }, { "laden", laden },
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
            if (laden == NeueMoebelAbschnitt)
            {
                fehler.AddRange(NeueMoebelPruefen(wert));
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
            foreach (var id in MoebelIds(eintrag, laden))
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
            // Phase 12 Welle 1: die Auslage so, wie sie im Laden steht (mit allen angehaengten Zusaetzen)
            var gebaeude = Probe.Innenraum.LadenGebaeude(innenraeume!, laden);
            // Phase 13 Welle 5 (Weisung P12-73): Waren auf den Etagen zaehlen mit (ohne Etagen wie bisher)
            foreach (var d in AuslageMitEtagen(gebaeude))
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
            foreach (var id in MoebelIds(eintrag, laden).Select(Py.Str).Where(id => !ausgestellt.Contains(id)))
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

    /// <summary>
    /// Phase 12 Welle 1: Fehler im Abschnitt "moebel_neu" (leer = alles gut). Jedes Moebel: neue Kennung (nicht in
    /// wohnungen.json und nicht doppelt), Name, bekannter Einrichtungs-Typ, Groesse &gt; 0, ganzer Preis &gt; 0, ein
    /// bekanntes Geschaeft mit Moebeln; ein Kleiderschrank braucht "plaetze" (ganze Zahl &gt; 0 oder "unbegrenzt"), andere
    /// Moebel haben keine. "ohne": nur bekannte Geschaefte und Moebel aus deren Liste.
    /// </summary>
    public List<string> NeueMoebelPruefen(object? wert)
    {
        var fehler = new List<string>();
        if (wert is not PyObjekt neu)
        {
            return ["moebel_neu ist kein Objekt"];
        }
        foreach (var (k, _) in neu)
        {
            if (k is not ("_hinweis" or "moebel" or "ohne"))
            {
                fehler.Add("moebel_neu: unbekanntes Feld " + k);
            }
        }
        var gesehen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var roh in ListeOderLeer(neu, "moebel"))
        {
            if (roh is not PyObjekt m || Get(m, "id") is not string id || id.Length == 0)
            {
                fehler.Add("moebel_neu: Moebel ohne Kennung");
                continue;
            }
            var wo = "moebel_neu " + id + ": ";
            // ein Python-Moebel hat kein Feld "geschaeft" (die eingefuegten Kopien schon)
            if (!gesehen.Add(id) || (Wohnung.MoebelStueck(id, Inhalt) is { } da && !Py.Hat(da, "geschaeft")))
            {
                fehler.Add(wo + "Kennung doppelt oder schon in wohnungen.json");
            }
            foreach (var (k, _) in m)
            {
                if (k is not ("id" or "name" or "typ" or "w" or "h" or "preis" or "geschaeft" or "plaetze"))
                {
                    fehler.Add(wo + "unbekanntes Feld " + k);
                }
            }
            if (Get(m, "name") is not string { Length: > 0 })
            {
                fehler.Add(wo + "Name fehlt");
            }
            if (Get(m, "typ") is not string typ || !SpielPruefung.Einrichtung.Contains(typ))
            {
                fehler.Add(wo + "unbekannter Typ " + Py.Str(Get(m, "typ")));
            }
            if (!(Get(m, "w") is long or double && Py.F(m["w"]) > 0 && Get(m, "h") is long or double && Py.F(m["h"]) > 0))
            {
                fehler.Add(wo + "Groesse fehlt oder ungueltig");
            }
            if (!Py.IstNurInt(Get(m, "preis")) || Py.Int(m["preis"]) <= 0)
            {
                fehler.Add(wo + "Preis fehlt oder ungueltig");
            }
            if (Get(m, "geschaeft") is not string g || !Laeden.Contains(g) || g == Kleidung.Laden)
            {
                fehler.Add(wo + "unbekanntes Geschaeft " + Py.Str(Get(m, "geschaeft")));
            }
            var schrank = Py.Gleich(Get(m, "typ"), Charakter.SchrankTyp);
            if (schrank && !Charakter.PlaetzeGueltig(Get(m, "plaetze")))
            {
                fehler.Add(wo + "Plaetze fehlen oder ungueltig");
            }
            if (!schrank && Py.Hat(m, "plaetze"))
            {
                fehler.Add(wo + "Plaetze nur bei Kleiderschraenken");
            }
        }
        if (Get(neu, "ohne") is { } ohneWert)
        {
            if (ohneWert is not PyObjekt ohne)
            {
                fehler.Add("moebel_neu: ohne ist kein Objekt");
            }
            else
            {
                foreach (var (laden, ids) in ohne)
                {
                    var liste = Get(Daten, laden) is PyObjekt e ? ListeOderLeer(e, "moebel").Select(Py.Str).ToHashSet(StringComparer.Ordinal) : null;
                    if (liste is null || !Laeden.Contains(laden))
                    {
                        fehler.Add("moebel_neu ohne: unbekanntes Geschaeft " + laden);
                        continue;
                    }
                    foreach (var id in ids as List<object?> ?? [ids])
                    {
                        if (!liste.Contains(Py.Str(id)))
                        {
                            fehler.Add("moebel_neu ohne " + laden + ": nicht im Sortiment " + Py.Str(id));
                        }
                    }
                }
            }
        }
        return fehler;
    }

    /// <summary>Einrichtung eines Ladens mit "ware" (Auslage), aus Flur und Raeumen.</summary>
    /// <summary>Phase 13 Welle 5: Auslage des Erdgeschosses und aller Etagen (in dieser Reihenfolge).</summary>
    public static IEnumerable<PyObjekt> AuslageMitEtagen(PyObjekt gebaeude) =>
        Auslage(new PyObjekt { { "gebaeude", gebaeude } }).Concat(Enumerable.Range(1, Stadtbau.Etagen.Anzahl(gebaeude))
            .Where(k => Stadtbau.Etagen.Gibt(gebaeude, k)).SelectMany(k => Auslage(new PyObjekt { { "gebaeude", Stadtbau.Etagen.Gebaeude(gebaeude, k) } })));

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
