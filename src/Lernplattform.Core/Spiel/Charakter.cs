using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 12 Welle 1 (Plan Phase 12, Weisung P12-5; Nico 30.10.2026 1a 2a 3a, 05.11.2026 1c 2a 3b 4a 5a): das Aussehen
/// der Figur waehlt man nur beim Anlegen frei. Danach aendert man Name und Aussehen nur am aufgestellten Kleiderschrank
/// in der eigenen Wohnung; Hautton, Frisur und Haarfarbe sind dort gesperrt (spaeter Friseur, Beta 1). Kein neues
/// Ereignis: der Kleiderschrank schreibt <see cref="Ereignisse.Profil"/> (profil_gesetzt) mit den gesperrten Teilen
/// unveraendert. Kleiderschraenke haben Plaetze fuer gekaufte Kleidung; es zaehlt nur der groesste, den man besitzt
/// (auch im Karton). Die Grenze gilt nur beim Kaufen (<see cref="KaufGrund"/>): was man hat, bleibt und ist anziehbar.
/// Die Python-Nachbauten (<see cref="SpielAktionen.Profil"/>, <see cref="Kleidung.Kaufen"/>) bleiben unveraendert.
/// </summary>
public static class Charakter
{
    /// <summary>Hinweis beim Anlegen, woertlich (Nico 30.10.2026, Weisung P10-2 Abschnitt 3; nur mit Nico aendern).</summary>
    public const string HinweisAnlegen = "Hier kannst du einmalig deinen Charakter designen und diesen später im Spiel über einen gekauften Kleiderschrank ändern. Sowie in einer anderen Stadt die Frisur beim Friseur.";

    /// <summary>Ueberschrift der Karte mit dem Hinweis beim Anlegen.</summary>
    public const string HinweisTitel = "Einmalig";

    /// <summary>Typ des Moebels (wohnungen.json und geschaefte.json moebel_neu).</summary>
    public const string SchrankTyp = Probe.SpielKern.SchrankTyp;

    /// <summary>Der alte Kleiderschrank aus den Python-Inhalten (340 EUR): nicht mehr im Laden, zaehlt wie der mittlere (Nico 4a).</summary>
    public const string AlterSchrank = "kleiderschrank";

    public const long AlterSchrankPlaetze = 16;

    /// <summary>Wert von "plaetze" fuer den begehbaren Kleiderschrank.</summary>
    public const string Unbegrenzt = "unbegrenzt";

    /// <summary>Aktion des Knopfs am Kleiderschrank in der Wohnung.</summary>
    public const string Aktion = "kleiderschrank";

    public const string KnopfOeffnen = "Kleiderschrank öffnen";

    public const string Ueberschrift = "Kleiderschrank";

    public const string TextAmSchrank = "Hier änderst du deinen Namen, dein Oberteil, ein Accessoire, deine Kleidung und die Farbe deines Kreises.";

    public const string Gesperrt = "Ändern kannst du das später beim Friseur in einer anderen Stadt.";

    public const string GesperrtZusatz = " · gesperrt";

    /// <summary>Phase 12 Welle 2: Beschriftung des Barts beim Anlegen und im Kleiderschrank.</summary>
    public const string BartName = "Bart";

    /// <summary>Satz unter dem Namen in der Uebersicht (Nico 3b, Bild 8 der Entwurfsbilder).</summary>
    public const string HinweisUebersicht = "Name und Aussehen änderst du am Kleiderschrank in deiner Wohnung (zu kaufen im Möbelhaus).";

    /// <summary>Einmaliges Fenster fuer Spielstaende, die schon eine Figur haben (Nico 5a).</summary>
    public const string UpdateTitel = "Neu: Kleiderschrank";

    public const string UpdateText = "Name und Aussehen deiner Figur änderst du jetzt am Kleiderschrank in deiner Wohnung. Kleiderschränke gibt es im Möbelhaus, vom kleinen für 100 € bis zum begehbaren für 1.000 €. Dein Aussehen bleibt, wie es ist.";

    public const string UpdateKnopf = "OK";

    /// <summary>Merker in einstellungen.json: Liste der Spielstaende (lauf), denen das Fenster gezeigt wurde.</summary>
    public const string UpdateMerker = "kleiderschrank_hinweis";

    /// <summary>Wurde das Fenster „Neu: Kleiderschrank“ dem Spielstand <paramref name="lauf"/> schon gezeigt?</summary>
    public static bool UpdateGezeigt(JsonObject werte, string lauf) =>
        werte[UpdateMerker] is JsonArray liste && liste.Any(e => e is JsonValue v && v.TryGetValue<string>(out var s) && s == lauf);

    /// <summary>Den Spielstand im Merker eintragen (Liste von Texten; ein kaputter Wert wird eine neue Liste).</summary>
    public static void UpdateMerken(JsonObject werte, string lauf)
    {
        if (werte[UpdateMerker] is not JsonArray liste)
        {
            liste = [];
            werte[UpdateMerker] = liste;
        }
        if (!UpdateGezeigt(werte, lauf))
        {
            liste.Add(lauf);
        }
    }

    public const string Voll = "Dein Kleiderschrank ist voll. Im Möbelhaus gibt es größere Schränke. Was du schon hast, bleibt dir.";

    public const string OhneSchrankKaufen = "Für neue Kleidung brauchst du zuerst einen Kleiderschrank aus dem Möbelhaus.";

    public const string ImSchrank = "Im Schrank";

    /// <summary>Kleidung, die man schon hat (statt Kleidung.SchonGekauft, das auf die alte Tafel „Figur“ zeigt).</summary>
    public const string SchonGekauft = "Das hast du schon. Anziehen kannst du es an deinem Kleiderschrank.";

    public const string ImMoebelhaus = Probe.SpielKern.ImMoebelhaus;

    public const string KeinSchrankAufgestellt = "Dafür musst du einen Kleiderschrank in deiner Wohnung aufstellen.";

    public const string NurBeimFriseur = "Hautton, Frisur, Haarfarbe und Bart lassen sich hier nicht ändern.";

    public const string KeinProfil = "Bitte gib deiner Spielfigur einen Namen.";

    /// <summary>
    /// Teile von <see cref="Aussehen.Teile"/>, die am Kleiderschrank gesperrt sind (Nico 3a); Phase 12 Welle 2 (Weisung P12-206):
    /// dazu der Bart (<see cref="Aussehen.ZusatzTeile"/>), neue Baerte nur beim Anlegen, fuer alte Staende der Friseur in Beta 1.
    /// </summary>
    public static readonly IReadOnlyList<string> GesperrteTeile = ["haut", "frisur", "haarfarbe", "bart"];

    /// <summary>Ist <paramref name="wert"/> ein gueltiger Wert fuer "plaetze" (ganze Zahl &gt; 0 oder "unbegrenzt")?</summary>
    public static bool PlaetzeGueltig(object? wert) => wert is Unbegrenzt || (Py.IstNurInt(wert) && Py.Int(wert) > 0);

    /// <summary>Plaetze eines Kleiderschranks (null = unbegrenzt); der alte Schrank ohne Feld zaehlt 16, andere Moebel 0.</summary>
    public static long? Plaetze(PyObjekt moebel)
    {
        if (!Py.Gleich(Get(moebel, "typ"), SchrankTyp))
        {
            return 0;
        }
        var wert = Get(moebel, "plaetze");
        return wert is Unbegrenzt ? null : PlaetzeGueltig(wert) ? Py.Int(wert) : AlterSchrankPlaetze;
    }

    /// <summary>Plaetze als Text („8 Plätze für Kleidung“, „Unbegrenzt Platz für Kleidung“).</summary>
    public static string PlaetzeText(long? plaetze) => plaetze is { } n ? n + " Plätze für Kleidung" : "Unbegrenzt Platz für Kleidung";

    /// <summary>Wie viel Platz die Figur hat: der groesste Kleiderschrank, den sie besitzt (aufgestellt oder im Karton).</summary>
    public sealed record Platz(string? Schrank, string SchrankName, long? Plaetze, long Belegt)
    {
        public bool HatSchrank => Schrank is not null;

        /// <summary>Noch ein Stueck kaufen: mit Schrank und freiem Platz (unbegrenzt = immer).</summary>
        public bool Frei => HatSchrank && (Plaetze is not { } n || Belegt < n);
    }

    /// <summary>Plaetze der Figur: groesster eigener Kleiderschrank, belegt = Zahl der gekauften Kleidungsstuecke.</summary>
    public static Platz Plaetze(Spielstand stand)
    {
        string? beste = null;
        var name = "";
        long? meiste = 0;
        foreach (var (_, kennung) in stand.Moebel)
        {
            if (Wohnung.MoebelStueck(kennung, stand.Inhalt) is not { } m || !Py.Gleich(Get(m, "typ"), SchrankTyp))
            {
                continue;
            }
            var plaetze = Plaetze(m);
            if (beste is null || Mehr(plaetze, meiste))
            {
                (beste, name, meiste) = (Py.Str(m["id"]), Py.Str(m["name"]), plaetze);
            }
        }
        return new Platz(beste, name, beste is null ? 0 : meiste, stand.Kleider.Count);
    }

    /// <summary>a hat mehr Plaetze als b (null = unbegrenzt).</summary>
    private static bool Mehr(long? a, long? b) => b is { } nb && (a is not { } na || na > nb);

    /// <summary>Zeile im Kleiderschrank („Plätze: 6 von 8 belegt (Kleiderschrank klein)“).</summary>
    public static string Belegt(Platz platz) => !platz.HatSchrank ? OhneSchrankKaufen : platz.Plaetze is { } n
        ? "Plätze: " + platz.Belegt + " von " + n + " belegt (" + platz.SchrankName + ")"
        : "Plätze: " + platz.Belegt + " belegt, unbegrenzt (" + platz.SchrankName + ")";

    /// <summary>Warum die Figur keine neue Kleidung kaufen darf ("" = sie darf).</summary>
    public static string KaufGrund(Spielstand stand)
    {
        var platz = Plaetze(stand);
        return !platz.HatSchrank ? OhneSchrankKaufen : platz.Frei ? "" : Voll;
    }

    /// <summary>Steht ein Kleiderschrank in der Wohnung (aufgestellt, nicht im Karton)?</summary>
    public static bool SchrankAufgestellt(Spielstand stand) => Wohnung.AufgestellteMoebel(stand).Any(m => m.Typ == SchrankTyp);

    /// <summary>Aufgestellter Kleiderschrank in Reichweite der Figur (wie furniture_near, nur dieser Typ).</summary>
    public static Wohnung.Aufgestellt? SchrankNahe(Spielstand stand, double x, double y)
    {
        Wohnung.Aufgestellt? beste = null;
        var besteWeite = double.MaxValue;
        foreach (var m in Wohnung.AufgestellteMoebel(stand).Where(m => m.Typ == SchrankTyp))
        {
            var dx = Math.Max(Math.Max(m.X - x, 0), x - m.X - Py.F(m.W));
            var dy = Math.Max(Math.Max(m.Y - y, 0), y - m.Y - Py.F(m.H));
            var weite = double.Hypot(dx, dy);
            if (weite <= Wohnung.MoebelReichweite && weite < besteWeite)
            {
                (beste, besteWeite) = (m, weite);
            }
        }
        return beste;
    }

    /// <summary>
    /// Name und Aussehen am Kleiderschrank aendern: nur mit Figur und aufgestelltem Kleiderschrank; Hautton, Frisur und
    /// Haarfarbe muessen bleiben. Schreibt wie <see cref="SpielAktionen.Profil"/> eine Zeile profil_gesetzt (Name gekuerzt).
    /// </summary>
    public static List<(string Art, PyObjekt Daten)> AmSchrank(Spielstand stand, string? name, object? aussehen)
    {
        if (stand.Profil is not { } profil)
        {
            throw new SpielFehler(KeinProfil);
        }
        if (!SchrankAufgestellt(stand))
        {
            throw new SpielFehler(KeinSchrankAufgestellt);
        }
        var alt = Aussehen.Normalisieren(profil["aussehen"]);
        var neu = Aussehen.Normalisieren(aussehen);
        if (GesperrteTeile.Any(t => !Py.Gleich(alt[t], neu[t])))
        {
            throw new SpielFehler(NurBeimFriseur);
        }
        return SpielAktionen.Profil(stand, name, neu, null);
    }
}
