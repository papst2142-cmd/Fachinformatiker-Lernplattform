using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.PyFormat;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 12 Welle 2 (Plan Phase 12 Abschnitte 2 und 3, Weisungen P12-3/P12-4/P12-5, Nico 05.11.2026 1a 4a 5a): die Menues
/// des Spiels liegen jetzt in der Spielwelt. Der „Schreibtisch mit PC“ in der Wohnung ist der PC zu Hause (Online-Shop,
/// Kredite ansehen, Gezahltes), das Buecherregal zeigt „Meine Reise“, die Firma fuehrt man am Schreibtisch im eigenen Buero,
/// Feierabend an der Eingangstuer des Bueros oder im Bett. Alles, was den Spielstand veraendert (Kredit, Gruendung,
/// Feierabend), geht ohne Moebel; nur Ansehen und Online-Shop brauchen den PC, Meine Reise das Regal.
/// <para>
/// Online-Shop: gleiche Moebel und Preise wie Moebelhaus und Elektromarkt (keine Kleidung, keine Firmen-Teile). Bestellt wird
/// mit dem vorhandenen Ereignis <see cref="Ereignisse.MoebelGekauft"/> und den Feldern „weg“ = „online“ und „liefertag“; das
/// Geld ist sofort weg, das Stueck liegt bis zum Abholen im Paket vor der Haustuer (<see cref="Spielstand.Pakete"/>).
/// Abholen ist die neue Ereignisart <see cref="PaketAbgeholt"/> (nur C#); ein schon abgeholtes Stueck zaehlt kein zweites Mal.
/// Ein Geraet mit altem Stand (A 0.40.0 und aelter) kennt die Felder nicht: es legt das Stueck sofort in den Karton und
/// uebergeht die neue Zeile (Grenze von CC angenommen, E1; Chat liest mit, E2).
/// </para>
/// </summary>
public static class Spielwelt
{
    /// <summary>Ereignisart: Pakete vor der Haustuer mitgenommen (Daten: „stuecke“ = Liste der Stuecke, „tag“).</summary>
    public const string PaketAbgeholt = "paket_abgeholt";

    /// <summary>Wert von „weg“ im Ereignis moebel_gekauft bei einer Bestellung im Online-Shop.</summary>
    public const string Online = "online";

    /// <summary>Typ des PCs zu Hause (Schreibtisch mit PC aus wohnungen.json, aktion lernen).</summary>
    public const string PcTyp = "schreibtisch";

    /// <summary>Typ des Buecherregals (wohnungen.json), dort liest man „Meine Reise“.</summary>
    public const string RegalTyp = "regal";

    /// <summary>Typ des Schreibtischs im eigenen Buero (Grundriss der Firma), dort fuehrt man die Firma.</summary>
    public const string BueroTischTyp = "schreibtisch";

    // Aktionen der Knoepfe (StadtSpiel → SpielTafel.AktionAusfuehren)
    public const string AktionComputer = "pc";
    public const string AktionReise = "reise";
    public const string AktionFirma = "firma";
    public const string AktionPaket = "paket";

    public const string KnopfComputer = "PC benutzen";
    public const string KnopfReise = "Meine Reise";
    public const string KnopfFirma = "Firma öffnen";
    public const string KnopfPaket = "Paket mitnehmen";
    public const string KnopfFeierabend = "Feierabend machen";

    /// <summary>Laeden, deren Moebel der Online-Shop fuehrt (gleiche Preise, keine Kleidung, keine Teile).</summary>
    public static readonly IReadOnlyList<string> ShopLaeden = ["moebelhaus", "elektromarkt"];

    // Texte (Oberflaeche)
    public const string PcTitel = "PC zu Hause";
    public const string ReiterShop = "Online-Shop";
    public const string ReiterKredite = "Kredite";
    public const string ReiterGezahlt = "Gezahltes";
    public const string ShopUnter = "Gleiche Preise wie im Laden. Das Paket steht dann vor deiner Haustür.";
    public const string ShopMoebel = "Möbel";
    public const string ShopElektro = "Elektrogeräte für die Wohnung";
    public const string KnopfBestellen = "Bestellen";
    public const string Bestellt = "Bestellt";
    public const string KrediteHinweis = "Neue Kredite und das Ablösen gibt es nur am Schalter der Bank.";
    public const string KeineKredite = "Du hast gerade keinen Kredit.";
    public const string GezahltHinweis = "Hier siehst du, was du bezahlt und eingenommen hast.";
    public const string Unterwegs = "Unterwegs zu dir";
    public const string AmPcText = "Hier kannst du online einkaufen und deine Kredite und Zahlungen ansehen.";
    public const string AmRegalText = "Hier steht dein Tagebuch mit deiner Reise: Rückblick und Erfolge.";
    public const string AmBueroTischText = "Am PC führst du deine Firma: Aufträge, Projekte, Mitarbeiter, Gebäude und Finanzen.";
    public const string BueroTischTitel = "Dein Schreibtisch";

    /// <summary>Satz unter dem Namen in der Uebersicht (Nico 5a), dazu der Satz zum Kleiderschrank aus Welle 1.</summary>
    public const string HinweisUebersicht = "Kredite und Gezahltes siehst du am PC zu Hause, „Meine Reise“ im Bücherregal. Deine Firma führst du am Schreibtisch in deinem Büro, gegründet wird sie auf dem Amt.";

    public const string OhneComputer = "Einen eigenen PC hast du noch nicht. Den „Schreibtisch mit PC“ gibt es im Möbelhaus; damit kannst du zu Hause online einkaufen und deine Kredite und Zahlungen ansehen.";
    public const string OhneRegal = "„Meine Reise“ liest du im Bücherregal deiner Wohnung (zu kaufen im Möbelhaus).";
    public const string FeierabendWo = "Feierabend machst du an der Eingangstür im Büro oder zu Hause im Bett.";
    public const string GruendenWo = "Gegründet wird die Firma auf dem Amt.";
    public const string FirmaWo = "Deine Firma führst du am Schreibtisch in deinem Büro.";
    public const string NurAmPc = "Online bestellen kannst du nur an deinem PC zu Hause.";
    public const string GibtEsNichtOnline = "Das gibt es im Online-Shop nicht.";
    public const string KeinPaket = "Vor der Haustür steht gerade kein Paket.";

    /// <summary>Zeile vor der Haustuer (draussen und an der Wohnungstuer), wenn Pakete angekommen sind.</summary>
    public static string PaketText(int anzahl) => anzahl == 1
        ? "Paket vor der Haustür: Ein Paket ist für dich angekommen."
        : Prozent("Paket vor der Haustür: %d Pakete sind für dich angekommen.", anzahl);

    /// <summary>Hinweis nach dem Abholen.</summary>
    public static string Mitgenommen(int anzahl) => anzahl == 1
        ? "Du hast das Paket mitgenommen. Es liegt jetzt im Karton zum Einrichten."
        : Prozent("Du hast %d Pakete mitgenommen. Sie liegen jetzt im Karton zum Einrichten.", anzahl);

    /// <summary>
    /// Lieferzeit in Arbeitstagen nach der Grundflaeche w × h (Nico 1a): bis 1 m² am naechsten Arbeitstag, bis 3 m² nach
    /// 2 Arbeitstagen, darueber nach 3. Einzige Stelle der Regel; neue Moebel bekommen ihre Stufe von selbst.
    /// </summary>
    public static long Liefertage(PyObjekt moebel)
    {
        var flaeche = Py.F(moebel["w"]) * Py.F(moebel["h"]);
        return flaeche <= 1.0 + 1e-9 ? 1 : flaeche <= 3.0 + 1e-9 ? 2 : 3;
    }

    /// <summary>„Lieferung am nächsten Arbeitstag“ bzw. „Lieferung in 2 Arbeitstagen“.</summary>
    public static string LieferText(long tage) => tage == 1 ? "Lieferung am nächsten Arbeitstag" : Prozent("Lieferung in %d Arbeitstagen", tage);

    /// <summary>„kommt an Arbeitstag 5“ bzw. „liegt vor der Haustür“.</summary>
    public static string Ankunft(long liefertag, long heute) => liefertag <= heute ? "liegt vor der Haustür" : Prozent("kommt an Arbeitstag %d", liefertag);

    /// <summary>Reiter der Firmen-Tafel, der in der Oberflaeche wegfaellt (Kredite: Bank-Schalter und PC zu Hause, Nico 1).</summary>
    public const string OhneReiter = "kredite";

    /// <summary>
    /// Reiter der Firmen-Tafel ohne „kredite“: feste, benannte Ausnahme von Python (G47 nennt die Reiter mit „kredite“,
    /// <see cref="Betrieb.Reiter"/> bleibt wie Python, Test Firma_ReiterOhneKredite_AusnahmeVonPython).
    /// </summary>
    public static List<(string Id, string Name)> FirmaReiter(Spielstand stand) =>
        [.. Betrieb.Reiter(stand).Where(r => r.Id != OhneReiter)];

    /// <summary>Ist ein Moebel dieses Typs in der Wohnung aufgestellt?</summary>
    public static bool Aufgestellt(Spielstand stand, string typ) => Wohnung.AufgestellteMoebel(stand).Any(m => m.Typ == typ);

    /// <summary>Aufgestelltes Moebel des Typs in Reichweite der Figur (wie furniture_near, nur dieser Typ), sonst null.</summary>
    public static Wohnung.Aufgestellt? Nahe(Spielstand stand, string typ, double x, double y)
    {
        Wohnung.Aufgestellt? beste = null;
        var besteWeite = double.MaxValue;
        foreach (var m in Wohnung.AufgestellteMoebel(stand).Where(m => m.Typ == typ))
        {
            var weite = Abstand(m.X, m.Y, Py.F(m.W), Py.F(m.H), x, y);
            if (weite <= Wohnung.MoebelReichweite && weite < besteWeite)
            {
                (beste, besteWeite) = (m, weite);
            }
        }
        return beste;
    }

    /// <summary>Abstand eines Punkts zum Rechteck (x, y, w, h), 0 innen.</summary>
    public static double Abstand(double rx, double ry, double rw, double rh, double x, double y) => Probe.SpielKern.Abstand(rx, ry, rw, rh, x, y);

    /// <summary>Das Sortiment des Online-Shops: (Laden, Ware) aller Moebel aus <see cref="ShopLaeden"/>.</summary>
    public static List<(string Laden, Ware Ware)> Sortiment(Geschaefte geschaefte) =>
        [.. ShopLaeden.SelectMany(l => geschaefte.Waren(l).Where(w => w.Art == Geschaefte.ArtMoebel).Select(w => (l, w)))];

    /// <summary>
    /// Im Online-Shop bestellen: nur mit aufgestelltem PC, nur Moebel aus dem Sortiment, Geld wie beim Kauf im Laden
    /// (<see cref="SpielAktionen.MoebelKaufen"/>). Das Ereignis ist moebel_gekauft mit „weg“ und „liefertag“.
    /// </summary>
    public static List<(string Art, PyObjekt Daten)> Bestellen(Spielstand stand, Geschaefte geschaefte, string laden, string moebel, string stueck)
    {
        if (!Aufgestellt(stand, PcTyp))
        {
            throw new SpielFehler(NurAmPc);
        }
        if (!ShopLaeden.Contains(laden) || geschaefte.Finden(laden, Geschaefte.ArtMoebel, moebel) is null)
        {
            throw new SpielFehler(GibtEsNichtOnline);
        }
        var ergebnis = SpielAktionen.MoebelKaufen(stand, moebel, stueck);
        var ware = Wohnung.MoebelStueck(moebel, stand.Inhalt)!;
        var daten = ergebnis[0].Daten;
        Py.Setzen(daten, "weg", Online);
        Py.Setzen(daten, "liefertag", checked(stand.Tag + Liefertage(ware)));
        return ergebnis;
    }

    /// <summary>Ist dieses moebel_gekauft eine Online-Bestellung (mit Liefertag)?</summary>
    public static bool IstBestellung(PyObjekt daten) => Py.Gleich(Get(daten, "weg"), Online) && Py.IstNurInt(Get(daten, "liefertag"));

    /// <summary>Bestellte, noch nicht abgeholte Stuecke: (Stueck, Moebel, Liefertag), nach Liefertag und Stueck.</summary>
    public static List<(string Stueck, object? Moebel, long Liefertag)> Bestellungen(Spielstand stand) =>
        [.. stand.Pakete.Select(p => (Py.Str(p.Key), Get(Objekt(p.Value), "moebel"), Py.Int(Get(Objekt(p.Value), "liefertag", 0L))))
            .OrderBy(p => p.Item3).ThenBy(p => p.Item1, StringComparer.Ordinal)];

    /// <summary>Angekommene Pakete vor der Haustuer (Liefertag erreicht), nach Stueck.</summary>
    public static List<string> Angekommen(Spielstand stand) =>
        [.. Bestellungen(stand).Where(b => b.Liefertag <= stand.Tag).Select(b => b.Stueck).OrderBy(s => s, StringComparer.Ordinal)];

    /// <summary>
    /// Ohne 3D (Ortsliste) geht die Figur im Haus nicht zu den Moebeln: diese Knoepfe stehen dann immer am Ort, sofern sie
    /// nicht schon da sind (<paramref name="vorhanden"/>). Zu Hause: Kleiderschrank, PC, Meine Reise, Schlafen (je mit
    /// aufgestelltem Moebel) und Paket mitnehmen; im Buero bzw. in der Filiale mit Firma „Firma öffnen“, im Buero
    /// „Feierabend machen“, sobald er moeglich ist. So erreicht die Ortsliste jeden neuen Platz (P12-3, Wege am Handy).
    /// </summary>
    public static List<(string Aktion, string Name)> ErsatzKnoepfe(Spielstand stand, string? ort, IReadOnlyCollection<string> vorhanden)
    {
        var neu = new List<(string, string)>();
        void Dazu(string aktion, string name)
        {
            if (!vorhanden.Contains(aktion) && neu.All(k => k.Item1 != aktion))
            {
                neu.Add((aktion, name));
            }
        }
        if (ort == OrtZuhause)
        {
            if (Charakter.SchrankAufgestellt(stand))
            {
                Dazu(Charakter.Aktion, Charakter.KnopfOeffnen);
            }
            if (Aufgestellt(stand, PcTyp))
            {
                Dazu(AktionComputer, KnopfComputer);
            }
            if (Aufgestellt(stand, RegalTyp))
            {
                Dazu(AktionReise, KnopfReise);
            }
            if (Wohnung.AufgestellteMoebel(stand).Any(m => Py.Gleich(Get(Wohnung.MoebelStueck(m.Moebel, stand.Inhalt) ?? [], "aktion"), "schlafen")))
            {
                Dazu("schlafen", "Schlafen");
            }
            if (Angekommen(stand).Count > 0)
            {
                Dazu(AktionPaket, KnopfPaket);
            }
        }
        else if ((ort == OrtBuero || ort == OrtFiliale) && Py.Wahr(stand.Firma))
        {
            Dazu(AktionFirma, KnopfFirma);
        }
        if (ort == OrtBuero && stand.FeierabendMoeglich())
        {
            Dazu("feierabend", KnopfFeierabend);
        }
        return neu;
    }

    /// <summary>Alle angekommenen Pakete mitnehmen (Nico 4a: ein Knopf fuer den ganzen Stapel).</summary>
    public static List<(string Art, PyObjekt Daten)> Abholen(Spielstand stand)
    {
        var stuecke = Angekommen(stand);
        if (stuecke.Count == 0)
        {
            throw new SpielFehler(KeinPaket);
        }
        return [(PaketAbgeholt, new PyObjekt { { "stuecke", stuecke.Cast<object?>().ToList() }, { "tag", stand.Tag } })];
    }
}
