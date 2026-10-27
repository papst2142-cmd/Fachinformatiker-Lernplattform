using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.PyFormat;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 8 W5 (Weisung P8-8c, Teilplan P8-7a, Nico 2b: nur Kleidung fuer die Figur): das Kaufhaus.
/// Abteilungen (Slots) und Sortiment stehen in <c>inhalte_csharp/spiel/geschaefte.json</c> (Abschnitt
/// <c>kaufhaus</c>, nur angehaengt), Grundriss, Auslage und Verkaeuferin in <c>innenraeume.json</c>.
/// Zwei neue Ereignisse, nur angehaengt (Auswertung in <see cref="Spielstand"/>): <see cref="Gekauft"/>
/// {stueck, geld, tag, laden} und <see cref="Getragen"/> {getragen: Slot -> Stueck oder null}, der juengste
/// gilt. Der Kauf zieht das Stueck gleich an (beide Zeilen in einem Schritt); ablegen und wieder anziehen
/// geht ueber die Tafel "Figur". Keine Wirkung auf Geld oder Ruf ausser dem Kaufpreis (Buchung "Kleidung").
/// Aeltere Versionen und Python 0.63.2 uebergehen beide Ereignisarten (P8-5, G51 unbekannte_arten).
/// </summary>
public static class Kleidung
{
    /// <summary>Neues Ereignis (nur C#): Kleidung im Kaufhaus gekauft {stueck, geld, tag, laden}.</summary>
    public const string Gekauft = "kleidung_gekauft";

    /// <summary>Neues Ereignis (nur C#): getragene Kleidung {getragen: {Slot: Stueck oder null}}.</summary>
    public const string Getragen = "kleidung_getragen";

    /// <summary>Buchung im Kassenbuch (Rueckblick, Monatsrechnung).</summary>
    public const string BuchKleidung = "Kleidung";

    /// <summary>Art der Ware im Geschaeft (<see cref="Ware.Typ"/> ist der Slot).</summary>
    public const string Art = "kleidung";

    /// <summary>Das Geschaeft mit Kleidung (Id wie in stadt.json neue_orte).</summary>
    public const string Laden = "kaufhaus";

    public const string Ueberschrift = "Kleidung";
    public const string FuerDieFigur = "Kleidung für deine Figur.";
    public const string KnopfAnziehen = "Anziehen";
    public const string KnopfAblegen = "Ablegen";
    public const string Nichts = "nichts";
    public const string SchonGekauft = "Das hast du schon. Anziehen kannst du es unter „Figur“.";
    public const string NichtGekauft = "Das hast du noch nicht gekauft.";

    /// <summary>Ein Kleidungsstueck: Slot (Stelle an der Figur), Form (Schnitt) und Farbe.</summary>
    public sealed record Stueck(string Id, string Name, string Slot, long Preis, string Form, string Farbe);

    /// <summary>Eine Stelle an der Figur mit Namen und Abteilung im Kaufhaus.</summary>
    public sealed record Stelle(string Id, string Name, string Abteilung);

    /// <summary>Die Slots in der Reihenfolge der Datei (leer ohne Kaufhaus).</summary>
    public static List<Stelle> Stellen(PyObjekt daten) =>
        Get(daten, Laden) is PyObjekt laden
            ? [.. ListeOderLeer(laden, "slots").OfType<PyObjekt>().Select(s => new Stelle(Py.Str(s["id"]), Py.Str(s["name"]), Py.Str(s["abteilung"])))]
            : [];

    /// <summary>Das Sortiment in der Reihenfolge der Datei.</summary>
    public static List<Stueck> Sortiment(PyObjekt daten) =>
        Get(daten, Laden) is PyObjekt laden
            ? [.. ListeOderLeer(laden, "kleidung").OfType<PyObjekt>().Select(k => new Stueck(Py.Str(k["id"]), Py.Str(k["name"]), Py.Str(k["slot"]),
                Py.Int(k["preis"]), Py.Str(k["form"]), Py.Str(k["farbe"])))]
            : [];

    public static Stueck? Finden(PyObjekt daten, string id) => Sortiment(daten).FirstOrDefault(k => k.Id == id);

    /// <summary>Was die Figur traegt (Slot -> Stueck), nur gekaufte Stuecke aus dem Sortiment am richtigen Slot.</summary>
    public static Dictionary<string, Stueck> Angezogen(Spielstand stand, PyObjekt daten)
    {
        var ergebnis = new Dictionary<string, Stueck>(StringComparer.Ordinal);
        if (stand.Getragen is not { } getragen)
        {
            return ergebnis;
        }
        foreach (var stelle in Stellen(daten))
        {
            if (Get(getragen, stelle.Id) is string id && stand.Kleider.ContainsKey(id) && Finden(daten, id) is { } stueck && stueck.Slot == stelle.Id)
            {
                ergebnis[stelle.Id] = stueck;
            }
        }
        return ergebnis;
    }

    /// <summary>Die Zeile kleidung_getragen, wenn an <paramref name="slot"/> <paramref name="stueck"/> (null = nichts) getragen wird.</summary>
    private static (string, PyObjekt) Tragen(Spielstand stand, PyObjekt daten, string slot, string? stueck)
    {
        var angezogen = Angezogen(stand, daten);
        var getragen = new PyObjekt();
        foreach (var stelle in Stellen(daten))
        {
            getragen.Add(stelle.Id, stelle.Id == slot ? stueck : angezogen.TryGetValue(stelle.Id, out var s) ? s.Id : null);
        }
        return (Getragen, new PyObjekt { { "getragen", getragen } });
    }

    /// <summary>Ein gekauftes Stueck anziehen oder einen Slot ablegen (<paramref name="stueck"/> null), ueberall (Tafel "Figur").</summary>
    public static List<(string Art, PyObjekt Daten)> Anziehen(Spielstand stand, PyObjekt daten, string slot, string? stueck)
    {
        if (!Stellen(daten).Any(s => s.Id == slot))
        {
            throw new SpielFehler(Geschaefte.GibtEsNicht);
        }
        if (stueck is not null && (Finden(daten, stueck) is not { } k || k.Slot != slot || !stand.Kleider.ContainsKey(stueck)))
        {
            throw new SpielFehler(NichtGekauft);
        }
        return [Tragen(stand, daten, slot, stueck)];
    }

    /// <summary>Kaufen (nur im geoeffneten Kaufhaus, in dem die Figur steht): Kaufpreis ab und gleich anziehen.</summary>
    public static List<(string Art, PyObjekt Daten)> Kaufen(Stadt stadt, Spielstand stand, PyObjekt daten, string laden, string stueck)
    {
        if (Geschaefte.ImLaden(stadt, stand, laden) is { Length: > 0 } grund)
        {
            throw new SpielFehler(grund);
        }
        if (laden != Laden || Finden(daten, stueck) is not { } ware)
        {
            throw new SpielFehler(Geschaefte.GibtEsNicht);
        }
        if (stand.Kleider.ContainsKey(stueck))
        {
            throw new SpielFehler(SchonGekauft);
        }
        if (stand.Geld < ware.Preis)
        {
            throw new SpielFehler(Prozent("Dafür reicht dein Geld noch nicht (%d € fehlen).", ware.Preis - Math.Max(0, stand.Geld)));
        }
        var gekauft = (Gekauft, new PyObjekt { { "stueck", ware.Id }, { "geld", -ware.Preis }, { "tag", stand.Tag }, { "laden", laden } });
        return [gekauft, Tragen(stand, daten, ware.Slot, ware.Id)];
    }

    /// <summary>Name der Abteilung eines Slots (Auslage im Kaufhaus), ohne Daten der Slot selbst.</summary>
    public static string Abteilung(PyObjekt? daten, string slot) =>
        (daten is null ? null : Stellen(daten).FirstOrDefault(s => s.Id == slot)?.Abteilung) ?? slot;

    /// <summary>Farben des Sortiments fuer eine Auslage "kleidung:&lt;slot&gt;" (Kleiderstange, Regal, Tisch); ohne Daten grau.</summary>
    public static List<System.Numerics.Vector3> Farben(PyObjekt? daten, string? ware)
    {
        var slot = ware is not null && ware.StartsWith(Art + ":", StringComparison.Ordinal) ? ware[(Art.Length + 1)..] : "";
        var farben = daten is null ? [] : Sortiment(daten).Where(k => k.Slot == slot).Select(k => Probe.Netz.Farbe(k.Farbe)).ToList();
        return farben.Count > 0 ? farben : [Probe.Netz.Farbe("#9CA3AF")];
    }

    /// <summary>Ein Stueck fuer die Darstellung an der Figur (Schaufensterpuppe, Spielfigur).</summary>
    public static Probe.Figur.Stueck? FigurStueck(PyObjekt? daten, string id) =>
        daten is not null && Finden(daten, id) is { } k ? new Probe.Figur.Stueck(k.Slot, k.Form, k.Farbe) : null;

    /// <summary>Fehler im Abschnitt kaufhaus von geschaefte.json (leer = alles gut).</summary>
    public static List<string> Pruefen(PyObjekt eintrag)
    {
        var fehler = new List<string>();
        var stellen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in ListeOderLeer(eintrag, "slots"))
        {
            if (s is not PyObjekt stelle || Get(stelle, "id") is not string { Length: > 0 } id || Get(stelle, "name") is not string { Length: > 0 }
                || Get(stelle, "abteilung") is not string { Length: > 0 } || stelle.Count != 3)
            {
                fehler.Add("kaufhaus: Slot ungueltig: " + Py.Str(s));
                continue;
            }
            if (!stellen.Add(id))
            {
                fehler.Add("kaufhaus: Slot doppelt: " + id);
            }
        }
        if (stellen.Count == 0)
        {
            fehler.Add("kaufhaus: keine Slots");
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var k in ListeOderLeer(eintrag, "kleidung"))
        {
            if (k is not PyObjekt stueck || Get(stueck, "id") is not string { Length: > 0 } id)
            {
                fehler.Add("kaufhaus: Kleidung ohne id: " + Py.Str(k));
                continue;
            }
            foreach (var (feld, _) in stueck)
            {
                if (feld is not ("id" or "name" or "slot" or "preis" or "form" or "farbe"))
                {
                    fehler.Add("kaufhaus: " + id + ": unbekanntes Feld " + feld);
                }
            }
            if (!ids.Add(id))
            {
                fehler.Add("kaufhaus: doppelt: " + id);
            }
            if (Get(stueck, "name") is not string { Length: > 0 } || Get(stueck, "form") is not string { Length: > 0 })
            {
                fehler.Add("kaufhaus: " + id + ": Name oder Form fehlt");
            }
            if (Get(stueck, "slot") is not string slot || !stellen.Contains(slot))
            {
                fehler.Add("kaufhaus: " + id + ": unbekannter Slot");
            }
            if (!Py.IstNurInt(Get(stueck, "preis")) || Py.Int(stueck["preis"]) <= 0)
            {
                fehler.Add("kaufhaus: Preis fehlt oder ungueltig: " + id);
            }
            if (Get(stueck, "farbe") is not string { Length: 7 } farbe || farbe[0] != '#' || !farbe[1..].All(Uri.IsHexDigit))
            {
                fehler.Add("kaufhaus: " + id + ": Farbe ungueltig");
            }
        }
        foreach (var stelle in stellen.Where(s => !ListeOderLeer(eintrag, "kleidung").OfType<PyObjekt>().Any(k => Py.Gleich(Get(k, "slot"), s))))
        {
            fehler.Add("kaufhaus: Slot ohne Kleidung: " + stelle);
        }
        return fehler;
    }
}
