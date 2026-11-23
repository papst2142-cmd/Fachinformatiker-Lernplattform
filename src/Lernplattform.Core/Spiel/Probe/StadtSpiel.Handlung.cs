using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle A 1.6.0 (Sammelliste 444, Weisungen P12-359/P12-360, Nico 3a): was die Figur an ihrer Stelle tun kann, statt der
/// Knoepfe im Dreieck-Menue. Taste F (PC) bzw. der Knopf ueber dem linken Stick (Handy, Tablet) loest die Haupthandlung aus (die
/// erste der Liste); gehalten zeigen sie eine kleine Auswahl aller Handlungen. Die Liste fasst nur zusammen, was das Menue bis
/// A 1.5.0 an dieser Stelle zeigte (Knoepfe des Orts, Hineingehen, Warten, Einrichten) und fuehrt dieselben Wege aus; neu ist nur
/// „Zur Stadt“ allein an der Ausgangstuer und die Raumtuer (Sammelliste 443). Ohne 3D bleibt das Menue wie bisher.
/// </summary>
public sealed partial class StadtSpiel
{
    public const string HandlungBetreten = "handlung:betreten";

    public const string HandlungZurStadt = "handlung:zur_stadt";

    public const string HandlungWarten = "handlung:warten";

    public const string HandlungEinrichten = "handlung:einrichten";

    /// <summary>So weit (Einheiten) von der Mitte der Ausgangstuer reicht die Handlung „Zur Stadt“.</summary>
    public const float AusgangReichweite = 1.8f;

    /// <summary>Alle Handlungen an der Stelle der Figur, die Haupthandlung zuerst (leer = keine).</summary>
    public IReadOnlyList<Handlung> Handlungen
    {
        get
        {
            var liste = new List<Handlung>();
            if (Einrichten)
            {
                return liste;
            }
            if (KannBetreten)
            {
                liste.Add(new Handlung(HandlungBetreten, SpielProbeTexte.Betreten, HandlungArt.Tuer));
            }
            if (Innen is { } innen && Etage == 0 && !Geht && MeinPlatz is null && innen.AmAusgang(FigurX, FigurZ, AusgangReichweite))
            {
                liste.Add(new Handlung(HandlungZurStadt, SpielProbeTexte.ZurStadt, HandlungArt.Tuer));
            }
            foreach (var (aktion, name) in Knoepfe.Where(k => k.Aktion is AktionTuerAuf or AktionTuerZu))
            {
                liste.Add(new Handlung(aktion, name, HandlungArt.Tuer));
            }
            if (KannWarten)
            {
                liste.Add(new Handlung(HandlungWarten, StadtTexte.Warten, HandlungArt.Tuer));
            }
            foreach (var (aktion, name) in Knoepfe.Where(k => k.Aktion is not (AktionTuerAuf or AktionTuerZu)))
            {
                var art = aktion is AktionEinsteigen or AktionAussteigen or AktionHaeltGleich || aktion.StartsWith(AktionZug, StringComparison.Ordinal)
                    ? HandlungArt.Bus : HandlungArt.Hand;
                liste.Add(new Handlung(aktion, name, art));
            }
            if (KannEinrichten)
            {
                liste.Add(new Handlung(HandlungEinrichten, SpielProbeTexte.Einrichten, HandlungArt.Hand));
            }
            return liste;
        }
    }

    /// <summary>Die Haupthandlung (erste der Liste) oder null.</summary>
    public Handlung? Haupthandlung => Handlungen is { Count: > 0 } l ? l[0] : null;

    /// <summary>Eine Handlung ausfuehren (nur, wenn sie gerade in der Liste steht); true = ausgefuehrt.</summary>
    public bool Handeln(string aktion)
    {
        if (!Handlungen.Any(h => h.Aktion == aktion))
        {
            return false;
        }
        switch (aktion)
        {
            case HandlungBetreten:
                return Betreten();
            case HandlungZurStadt:
                Verlassen();
                return true;
            case HandlungWarten:
                Warten();
                return true;
            case HandlungEinrichten:
                EinrichtenUmschalten();
                return true;
            default:
                return Knopf(aktion);
        }
    }
}
