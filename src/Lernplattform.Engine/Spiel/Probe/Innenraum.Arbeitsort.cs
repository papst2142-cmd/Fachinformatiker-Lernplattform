using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle E4 (A 0.75.0 / S 0.53.0, Weisung P12-247): Grundriss eines Arbeitsorts so, wie das Spiel ihn beim Betreten zeigt
/// (mit allen angehaengten Zusaetzen), damit Werkzeug und Ladepruefung der Quest-Ebene die Stelle gegen dieselbe Einrichtung
/// pruefen wie das Spiel. Moebel, die erst nach einer Aufgabe verschwinden ("bis_aufgabe"), bleiben hier stehen (strenger).
/// </summary>
public sealed partial class Innenraum
{
    /// <summary>
    /// Grundriss des Orts <paramref name="ort"/> (Spiel-Ort: buero, Kunden-Ort, Geschaeft, Schalter) auf der Etage
    /// <paramref name="etage"/>; null = gibt es nicht.
    /// </summary>
    public static PyObjekt? ArbeitsGrundriss(string ort, int etage, PyObjekt inhalt, PyObjekt? innenraeume)
    {
        if (SpielKern.Laeden.Contains(ort) || SpielKern.SchalterOrte.Contains(ort))
        {
            if (innenraeume is null || Get(innenraeume, ort) is not PyObjekt laden || laden["gebaeude"] is not PyObjekt eg)
            {
                return null;
            }
            if (etage > 0)
            {
                return Stadtbau.Etagen.Gibt(eg, etage) ? Stadtbau.Etagen.FuerDarstellung(eg, ort, etage) : null;
            }
            return etage == 0 ? LadenGebaeude(innenraeume, ort) : null;
        }
        if (etage != 0)
        {
            return null;
        }
        PyObjekt basis;
        if (ort == OrtBuero)
        {
            if (inhalt["gebaeude"] is not PyObjekt b)
            {
                return null;
            }
            basis = b;
        }
        else if (Kundenorte(inhalt).FirstOrDefault(k => Py.Gleich(k["id"], ort)) is { } kunde && kunde["gebaeude"] is PyObjekt kg)
        {
            basis = kg;
            if (innenraeume is not null && Get(innenraeume, "zusatz_kunden") is PyObjekt zk && Get(zk, ort) is PyObjekt zo)
            {
                basis = MitZusatz(basis, zo);
            }
        }
        else
        {
            return null;
        }
        var (gebaeude, _) = MoebelZusatz(innenraeume, ort, basis);
        return MitZusatz(gebaeude, SchrankZusatz(innenraeume, ort));
    }

    /// <summary>
    /// Welle E4 (Antwort 3c): Text am Arbeitsort eines Auftrags, dessen Questgeber nicht dort wartet. Gilt im Raum des
    /// Arbeitsorts (Buero, Kunde) bzw. bis <see cref="Stadtbau.Questebene.ArbeitsortReichweite"/> an der Stelle (Geschaeft,
    /// Schalter); sonst null.
    /// </summary>
    public (string Titel, string Text, List<(string Aktion, string Name)> Knoepfe)? ArbeitsortNachricht(double x, double z, ISpielSicht stand)
    {
        var etage = Stadtbau.Etagen.Teilen(Ansicht).Etage;
        if (Stadtbau.Questebene.AuftragOhneQuestgeber(Ort, etage, x, z, Gebaeude, stand) is not { } auftrag)
        {
            return null;
        }
        var name = Get(auftrag, "auftraggeber") is { } id && stand.Inhalt["kollegen"] is List<object?> kollegen
            && kollegen.OfType<PyObjekt>().FirstOrDefault(k => Py.Gleich(k["id"], id)) is { } person ? Py.Str(person["name"]) : "";
        return (ArbeitsortTitel,
            PyFormat.Prozent(ArbeitsortText, auftrag["titel"], auftrag["prioritaet"], name),
            [("auftrag:" + Py.Str(auftrag["id"]), "Auftrag annehmen")]);
    }

    /// <summary>Ueberschrift am Arbeitsort ohne wartenden Questgeber.</summary>
    public const string ArbeitsortTitel = "Hier wartet ein Auftrag";

    /// <summary>Text dazu (Titel, Prioritaet, Name des Questgebers).</summary>
    public const string ArbeitsortText = "„%s“ · Priorität %s. Den Auftrag hat dir %s gegeben, die Arbeit wartet hier.";
}
