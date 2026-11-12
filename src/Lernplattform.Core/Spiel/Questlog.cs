using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Liegt im Spiel-Kern (nicht in der Engine), weil es die laufende Stadt <see cref="StadtSpiel"/> liest.
/// Welle A 0.67.0 (Weisung P12-204, Sammelliste 301, Kurzplan P12-118 Abschnitt 3): das Questlog im Handy. Nur Anzeige aus
/// vorhandenen Daten (offene Tickets des Tages, geloeste Auftraege aus dem Verlauf, Ketten ueber das Feld „nach“, Questgeber und
/// Ort), schreibt nichts. Reiter „Offen“ und „Erledigt“; je Eintrag Titel, Questgeber, Ort (mit Etage und Oeffnungszeit), der erste
/// Satz des Tickets, „neu heute“ und die Kette „2 von 3“ mit ✓ (erledigt), ● (jetzt) und 🔒 (erst nach dem vorigen).
/// </summary>
public static class Questlog
{
    /// <summary>Stufe eines Schritts einer Kette.</summary>
    public enum Stufe { Erledigt, Jetzt, Gesperrt }

    /// <summary>Ein Auftrag im Questlog; <see cref="Ziel"/> = Ort, zu dem „Hingehen“ fuehrt (null = keiner).</summary>
    public sealed record Eintrag(string Id, string Titel, string Questgeber, string OrtText, string Ticket, bool NeuHeute,
        IReadOnlyList<(string Titel, Stufe Stufe)> Kette, bool Erledigt, string? Ziel);

    /// <summary>Offene Auftraege des Tages in der Reihenfolge des Spiels.</summary>
    public static List<Eintrag> Offen(StadtSpiel spiel)
    {
        var stand = spiel.Ablauf.Stand;
        return [.. stand.OffeneTickets().Select(a => Bauen(spiel, a, erledigt: false))];
    }

    /// <summary>Geloeste Auftraege, zuletzt geloeste zuerst (jeder einmal).</summary>
    public static List<Eintrag> Erledigte(StadtSpiel spiel)
    {
        var stand = spiel.Ablauf.Stand;
        var gesehen = new HashSet<string>(StringComparer.Ordinal);
        var liste = new List<Eintrag>();
        for (var i = stand.Verlauf.Count - 1; i >= 0; i--)
        {
            var e = stand.Verlauf[i];
            if (e.Art != Ereignisse.Erledigt || SpielDaten.Get(e.Daten, "aufgabe", null) is not string id || !stand.Geloest.Contains(id) || !gesehen.Add(id))
            {
                continue;
            }
            if (SpielDaten.Auftrag(id, stand.Inhalt) is { } auftrag)
            {
                liste.Add(Bauen(spiel, auftrag, erledigt: true));
            }
        }
        return liste;
    }

    /// <summary>Zahl der offenen Auftraege (gruene Zahl am Handy-Knopf).</summary>
    public static int ZahlOffen(StadtSpiel spiel) => spiel.Ablauf.Stand.OffeneTickets().Count;

    /// <summary>Text eines leeren Reiters (mit eigener Firma: die Auftraege laufen dort ueber die Firma).</summary>
    public static string LeerText(StadtSpiel spiel, bool erledigt) => erledigt ? QuestlogTexte.KeineErledigten
        : Py.Wahr(spiel.Ablauf.Stand.Firma) ? QuestlogTexte.MitFirma : QuestlogTexte.KeineOffenen;

    private static Eintrag Bauen(StadtSpiel spiel, PyObjekt auftrag, bool erledigt)
    {
        var stand = spiel.Ablauf.Stand;
        var inhalt = stand.Inhalt;
        var id = Py.Str(auftrag["id"]);
        var titel = Py.Str(SpielDaten.Get(auftrag, "titel", id));
        var person = SpielDaten.Person(auftrag["auftraggeber"], inhalt);
        var questgeber = person is not null ? Py.Str(SpielDaten.Get(person, "name", auftrag["auftraggeber"])) : Py.Str(auftrag["auftraggeber"] ?? "");
        var (ziel, ortText) = OrtVon(spiel, auftrag);
        var ticket = ErsterSatz(auftrag["ticket"]);
        var neu = !erledigt && (Py.F(SpielDaten.Get(auftrag, "ab_tag", 1L)) == stand.Tag || id.StartsWith(SpielDaten.Falschlieferung, StringComparison.Ordinal));
        return new Eintrag(id, titel, questgeber, ortText, ticket, neu, Kette(stand, auftrag, erledigt), erledigt, ziel);
    }

    /// <summary>Ort eines Auftrags (Ort-Kennung und Text mit Etage und Oeffnungszeit); Questgeber draussen: der Ort ihres Platzes.</summary>
    private static (string? Ziel, string Text) OrtVon(StadtSpiel spiel, PyObjekt auftrag)
    {
        var inhalt = spiel.Ablauf.Stand.Inhalt;
        var ort = Py.Str(SpielDaten.AuftragOrt(auftrag, inhalt) ?? "");
        if (ort == Questebene.OrtDraussen)
        {
            var q = spiel.QuestgeberDraussen.FirstOrDefault(x => Py.Gleich(x.Id, auftrag["auftraggeber"]));
            return q is not null && spiel.QuestgeberOrt(q) is { } bei
                ? (bei, string.Format(CultureInfo.InvariantCulture, QuestlogTexte.Draussen, spiel.Name(bei)))
                : (null, QuestlogTexte.DraussenOhneOrt);
        }
        var (grund, etage) = Etagen.Teilen(ort);
        // Ort des Spiels ("buero", Kunde, Laden) zum Ort der Stadt: gleiche Kennung oder Innenraum dieses Orts
        var o = spiel.Szene.Ort(grund) ?? spiel.Szene.Orte.FirstOrDefault(x => Innenraum.OrtFuer(x.Ansicht) == grund);
        var name = o is not null ? o.Name : SpielAnsicht.OrtName(grund, inhalt, spiel.Ablauf.Stand);
        var text = etage > 0 ? string.Format(CultureInfo.InvariantCulture, QuestlogTexte.MitEtage, name, etage) : name;
        if (spiel.Stadt.Oeffnungszeit(o?.Id ?? grund) is { } zeit)
        {
            text += string.Format(CultureInfo.InvariantCulture, QuestlogTexte.Geoeffnet, Stadt.Uhrzeit(zeit.Auf), Stadt.Uhrzeit(zeit.Zu));
        }
        return (o?.Id, text);
    }

    /// <summary>Erster Satz des Tickets (beim Laden sind die Zeilen schon zu einem Text verbunden).</summary>
    public static string ErsterSatz(object? ticket)
    {
        var zeilen = ticket switch
        {
            List<object?> liste => liste.Select(Py.Str).ToList(),
            string t => t.Split('\n').ToList(),
            _ => [],
        };
        var satz = SatzAus(zeilen.Count > 0 ? zeilen[0] : "");
        // Sammelliste 310 (Weisung P12-205): endet die erste Zeile mit einem Doppelpunkt („… der Talbahn an:“), gehoert
        // der erste Satz der naechsten Zeile dazu, sonst fehlt, worum es geht
        if (satz.EndsWith(':') && !DoppelpunktAltFuerTests && zeilen.Skip(1).Select(SatzAus).FirstOrDefault(z => z.Length > 0) is { } weiter)
        {
            satz += " " + weiter;
        }
        return satz;
    }

    /// <summary>Sammelliste 310: nur fuer die Gegenprobe; an = erster Satz endet beim Doppelpunkt wie in A 0.67.0.</summary>
    internal static bool DoppelpunktAltFuerTests
    {
        get => DoppelpunktAlt.Value;
        set => DoppelpunktAlt.Value = value;
    }

    private static readonly AsyncLocal<bool> DoppelpunktAlt = new();

    /// <summary>Erster Satz einer Zeile: bis zum ersten „.“, „!“ oder „?“, auf das ein Leerzeichen und ein Grossbuchstabe folgen.</summary>
    private static string SatzAus(string zeile)
    {
        var text = zeile.Split('\n', 2)[0].Trim();
        for (var i = 0; i < text.Length - 1; i++)
        {
            if (text[i] is '.' or '!' or '?' && text[i + 1] == ' ' && i + 2 < text.Length && char.IsUpper(text[i + 2]))
            {
                return text[..(i + 1)];
            }
        }
        return text;
    }

    /// <summary>Kette ueber „nach“: zuerst zum Anfang zurueck, dann vorwaerts (je Schritt der erste Auftrag mit nach = diesem).</summary>
    private static List<(string Titel, Stufe Stufe)> Kette(Spielstand stand, PyObjekt auftrag, bool erledigt)
    {
        var inhalt = stand.Inhalt;
        var alle = SpielDaten.Liste(inhalt["aufgaben"]).Select(SpielDaten.Objekt).ToList();
        var anfang = auftrag;
        var gesehen = new HashSet<string>(StringComparer.Ordinal) { Py.Str(auftrag["id"]) };
        while (Py.Wahr(anfang["nach"]) && SpielDaten.Auftrag(anfang["nach"], inhalt) is { } vorher && gesehen.Add(Py.Str(vorher["id"])))
        {
            anfang = vorher;
        }
        var kette = new List<PyObjekt> { anfang };
        gesehen = new HashSet<string>(StringComparer.Ordinal) { Py.Str(anfang["id"]) };
        while (alle.FirstOrDefault(a => Py.Gleich(a["nach"], kette[^1]["id"])) is { } weiter && gesehen.Add(Py.Str(weiter["id"])))
        {
            kette.Add(weiter);
        }
        if (kette.Count < 2)
        {
            return [];
        }
        var offen = stand.OffeneTickets().Select(a => Py.Str(a["id"])).ToHashSet(StringComparer.Ordinal);
        return [.. kette.Select(a =>
        {
            var aid = Py.Str(a["id"]);
            var stufe = stand.Geloest.Contains(aid) ? Stufe.Erledigt
                : offen.Contains(aid) || aid == Py.Str(auftrag["id"]) && !erledigt ? Stufe.Jetzt
                : Stufe.Gesperrt;
            return (Py.Str(SpielDaten.Get(a, "titel", aid)), stufe);
        })];
    }

    /// <summary>Zeile der Kette, z. B. „Kette 2 von 3: ✓ Erster · ● Zweiter · 🔒 Dritter“.</summary>
    public static string KetteText(Eintrag e)
    {
        if (e.Kette.Count < 2)
        {
            return "";
        }
        var nr = e.Kette.Select((k, i) => (k, i)).FirstOrDefault(p => p.k.Titel == e.Titel).i + 1;
        return string.Format(CultureInfo.InvariantCulture, QuestlogTexte.Kette, nr, e.Kette.Count,
            string.Join(" · ", e.Kette.Select(k => Zeichen(k.Stufe) + " " + k.Titel)));
    }

    /// <summary>Zeichen einer Stufe (✓ ● 🔒).</summary>
    public static string Zeichen(Stufe s) => s switch
    {
        Stufe.Erledigt => "✓",
        Stufe.Jetzt => "●",
        _ => "🔒",
    };
}
