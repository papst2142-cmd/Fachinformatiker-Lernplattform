using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>Welle Q1 (S 0.40.0, Weisung P12-186): Quests und Questgeber im Projekt (Pruefung, Schritte im Verlauf).</summary>
public sealed partial class Projekt
{
    /// <summary>
    /// Umgebung der Quest-Pruefung zum Stand jetzt. Alles, was der Hintergrund liest, ist kopiert (Stand draussen, Grundrisse
    /// und Personen der Innenraeume, in denen ein Questgeber steht, Namen der Orte); auf dem Oberflaechen-Thread aufrufen.
    /// </summary>
    public QuestPruefung.Umgebung QuestUmgebung()
    {
        var aussen = (PyObjekt)Py.Kopie(AussenStand)!;
        var daten = Daten;
        var basisInnen = Basis.Innenraeume;
        var innen = new Dictionary<string, (PyObjekt Grundriss, List<PyObjekt> Personen)>(StringComparer.Ordinal);
        var namen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var q in QuestgeberDaten.Alle(daten.Inhalt, aussen))
        {
            if (q.Platz is not { Draussen: false } p)
            {
                continue;
            }
            var k = p.InnenSchluessel;
            namen.TryAdd(p.Ort, OrtName(p.Ort));
            if (!innen.ContainsKey(k) && GibtInnenort(k))
            {
                innen[k] = ((PyObjekt)Py.Kopie(Grundriss(k))!, [.. Personen(k).Select(x => (PyObjekt)Py.Kopie(x)!)]);
            }
        }
        foreach (var o in AussenBearbeitung.Orte(aussen).Select(o => Py.Str(o["id"])))
        {
            namen.TryAdd(o, OrtName(o));
        }
        var innenraeume = Innenraeume();
        var basis = Basis;
        return new QuestPruefung.Umgebung
        {
            Inhalt = daten.Inhalt,
            Aussen = aussen,
            Innen = k => innen.TryGetValue(k, out var v) ? v : null,
            InnenPruefen = (k, personen) =>
            {
                var (b, e) = Etagen.Teilen(k);
                return KatalogPruefung.Innen(Katalog.Aktiv, b, innen[k].Grundriss, personen, daten.Grafik, daten.Geschaefte, basisInnen, e).Funde;
            },
            Szene = () => QuestSzene(aussen, basis, innenraeume, daten),
            GeschichteBauen = () => Spielgeschichte.Aus(daten.Inhalt, aussen),
            OrtName = o => namen.TryGetValue(o, out var n) ? n : o,
        };
    }

    /// <summary>Szene der Stadt draussen (Probestand wie die 3D-Ansicht) mit dem Ort, an dem die Figur startet.</summary>
    internal static (StadtSzene Szene, string StartOrt)? QuestSzene(PyObjekt aussen, StadtFassung basis, PyObjekt innenraeume, Spieldaten daten)
    {
        try
        {
            var fassung = Aussen.Fassung(aussen, basis, innenraeume, 1, null, Werkzeug.Kennzeile, "", "");
            var stadt = fassung.StadtFuer(fassung.MitKarte(daten.Inhalt));
            var stand = StadtbauSpiel.Probestand(stadt.Inhalt).Stand;
            var szene = StadtSzene.Bauen(stadt, stand, Ansicht3D.MitGebaeuden(daten.Grafik, fassung), null, daten.Feste);
            return (szene, ((IStadtSicht)stadt).StartOrt);
        }
        catch (Exception fehler) when (fehler is InvalidDataException or IOException or KeyNotFoundException or InvalidCastException or FormatException
            or OverflowException or Lernplattform.Core.Spiel.SpielFehler or ArgumentException or InvalidOperationException or IndexOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>Teile im Verlauf; Quests und Questgeber aendern keine Laterne (keine Nachpruefung der automatischen).</summary>
    public const string QuestTeil = "stadtdaten/" + QuestDaten.Abschnitt;

    public const string QuestgeberTeil = "stadtdaten/" + QuestgeberDaten.Abschnitt;

    // ---- Schritte (je Aufruf ein Schritt im Verlauf) ----

    public string QuestNeu(string typ, string? questgeber) => AussenAendern(Texte.QuestNeu, a => QuestDaten.Neu(Daten.Inhalt, a, typ, questgeber));

    public string? QuestKopieren(string id) => AussenAendern(Texte.QuestKopieren, a => QuestDaten.Kopieren(Daten.Inhalt, a, id));

    /// <summary>Feld einer neuen Quest; gleicher Wert = kein Schritt.</summary>
    public bool QuestFeld(string id, string feld, object? wert) => AussenAendern(Texte.QuestFeldName(feld), a => QuestDaten.FeldSetzen(a, id, feld, wert));

    public void QuestUmhaengen(string id, string? questgeber) =>
        AussenAendern(Texte.QuestFeldQuestgeber, a => QuestDaten.Umhaengen(Daten.Inhalt, a, id, questgeber));

    /// <summary>„Erst nach“ einer neuen Quest (auch Pfeil ziehen in den Zusammenhängen). false = vorhandene Quest.</summary>
    /// <summary>Mehrere Felder einer neuen Quest in einem Schritt aendern (Antwort und Optionen zusammen). false = keine neue Quest.</summary>
    public bool QuestAendern(string id, string was, Action<PyObjekt> aenderung) => AussenAendern(was, a =>
    {
        if (QuestDaten.NeueQuest(a, id) is not { } q)
        {
            return false;
        }
        aenderung(q);
        return true;
    });

    public bool QuestNach(string id, string? nach) => AussenAendern(Texte.QuestFeldNach, a => QuestDaten.NachSetzen(a, id, nach));

    /// <summary>Neue Quest loeschen; Quests, die nach ihr kamen, verlieren die Bedingung (im selben Schritt).</summary>
    public bool QuestLoeschen(string id) => AussenAendern(Texte.QuestLoeschen, a =>
    {
        foreach (var e in QuestDaten.Liste(a).OfType<PyObjekt>().Where(e => Py.Gleich(SpielDaten.Get(e, "nach"), id)).ToList())
        {
            QuestDaten.NachSetzen(a, Py.Str(e["id"]), null);
        }
        return QuestDaten.Loeschen(a, id);
    });

    public string QuestgeberNeu() => AussenAendern(Texte.QgNeu, a => QuestgeberDaten.Neu(Daten.Inhalt, a, Texte.QgNeuName));

    public bool QuestgeberFeld(string id, string feld, string wert) =>
        AussenAendern(feld == "name" ? Texte.QgName : Texte.QgRolle, a => QuestgeberDaten.FeldSetzen(a, id, feld, wert));

    public bool QuestgeberAussehen(string id, string teil, string wert) => AussenAendern(Texte.QgAussehen, a => QuestgeberDaten.AussehenSetzen(a, id, teil, wert));

    public void QuestgeberPlatz(string id, QuestgeberDaten.Platz platz) => AussenAendern(Texte.QgPlatz, a => QuestgeberDaten.PlatzSetzen(Daten.Inhalt, a, id, platz));

    public bool QuestgeberPlatzZurueck(string id) => AussenAendern(Texte.QgPlatzZurueck, a => QuestgeberDaten.PlatzZurueck(a, id));

    public bool QuestgeberLoeschen(string id) => AussenAendern(Texte.QgLoeschen, a => QuestgeberDaten.Loeschen(Daten.Inhalt, a, id));

    /// <summary>Alle Quests und Questgeber zum Stand jetzt.</summary>
    public List<QuestDaten.Quest> Quests() => QuestDaten.Alle(Daten.Inhalt, AussenStand);

    public List<QuestgeberDaten.Questgeber> Questgeber() => QuestgeberDaten.Alle(Daten.Inhalt, AussenStand);

    /// <summary>Funde der Quests und Questgeber (leer ohne Abschnitte).</summary>
    public List<QuestPruefung.Fund> QuestFunde() => QuestPruefung.Pruefen(QuestUmgebung());
}
