using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>
/// S 0.49.0 (Weisung P12-231 Antwort 1a): einen Innenraum (oder eine Etage „ort@k“) in 3D ansehen, genau so, wie das Spiel ihn
/// beim Betreten baut (<see cref="Innenraum.Bauen"/> mit allen Zusaetzen, Moebeln, Personen und Questgebern), bei Tag oder
/// Nacht. Das Spiel um den Innenraum herum ist dasselbe wie in der 3D-Ansicht der Stadt (<see cref="Ansicht3D.Bauen"/>, Probe-
/// Spielstand, nur im Speicher). Gebaut wird im Hintergrund (<see cref="Bauen"/>); nach einer Aenderung nur im Innenraum bleibt
/// das Spiel, und nur der Innenraum wird neu gebaut. Innenraeume neuer Gebaeude („bau:…“) kennt das Spiel noch nicht.
/// </summary>
public static class Innen3D
{
    /// <summary>Was ein Bau braucht (Kopien vom Oberflaechen-Faden).</summary>
    public sealed record Auftrag(string Schluessel, PyObjekt Aussen, StadtFassung Basis, PyObjekt Innenraeume, Spieldaten Daten);

    /// <summary>Ergebnis: das Spiel (neu oder das vorige), der fertige Innenraum und die Stelle der Figur.</summary>
    public sealed record Ergebnis(StadtSpiel Spiel, bool SpielNeu, string AussenText, Innenraum Innen, Vector3 Figur);

    /// <summary>Grund, warum der Innenraum nicht in 3D geht (null = geht).</summary>
    public static string? Nicht(string schluessel) => Projekt.IstBau(Etagen.Teilen(schluessel).Ort) ? Texte.Innen3DBauNicht : null;

    /// <summary>Auf dem Oberflaechen-Faden: Kopien fuer den Bau.</summary>
    public static Auftrag Vorbereiten(Projekt p, string schluessel) =>
        new(schluessel, (PyObjekt)Py.Kopie(p.AussenStand)!, p.Basis, (PyObjekt)Py.Kopie(p.Innenraeume())!, p.Daten);

    /// <summary>
    /// Im Hintergrund: Spiel (nur neu, wenn sich draussen etwas geaendert hat: <paramref name="vorher"/> mit gleichem
    /// <see cref="Ergebnis.AussenText"/> wird weiter benutzt) und Innenraum. Wirft <see cref="InvalidDataException"/>, wenn es
    /// den Innenraum nicht gibt.
    /// </summary>
    public static Ergebnis Bauen(Auftrag a, Ergebnis? vorher, StadtSzene.Vorrat? vorrat = null)
    {
        if (Nicht(a.Schluessel) is { } grund)
        {
            throw new InvalidDataException(grund);
        }
        var text = PythonJson.Text(a.Aussen);
        var spielNeu = vorher is null || vorher.AussenText != text;
        var spiel = spielNeu
            ? Ansicht3D.Bauen(Aussen.Fassung(a.Aussen, a.Basis, a.Innenraeume, 1, 0, Werkzeug.Kennzeile, "", ""), a.Daten, Ansicht3D.Einstellung.Standard, vorrat)
            : vorher!.Spiel;
        var (ort, etage) = Etagen.Teilen(a.Schluessel);
        var innen = Innenraum.Bauen(StadtbauPruefung.Ansicht(ort, etage), spiel.Ablauf.Stand, a.Innenraeume, spiel.Grafik, spiel.GeschaefteDaten, null,
            spiel.Feste, Jahreszeit.Sommer) ?? throw new InvalidDataException(Texte.Innen3DFehlt);
        innen.Vorbereiten();
        return new Ergebnis(spiel, spielNeu, text, innen, Start(a.Innenraeume, ort, etage, innen));
    }

    /// <summary>
    /// Wo die Figur steht: im Erdgeschoss an der Tuer (wie beim Betreten), auf einer Etage an der Ankunft des ersten
    /// Uebergangs, der dort haelt (wie beim Etagenwechsel), sonst in der Mitte. Z = Blick in Grad.
    /// </summary>
    public static Vector3 Start(PyObjekt innenraeume, string ort, int etage, Innenraum innen)
    {
        if (etage == 0)
        {
            return new Vector3(innen.StartX, innen.StartZ, 90);
        }
        var eg = innenraeume[ort] is PyObjekt o && o["gebaeude"] is PyObjekt g ? g : null;
        var halt = eg is null ? null : Etagen.Uebergaenge(eg, ort).Select(u => u.Auf(etage)).FirstOrDefault(h => h is not null);
        return halt is null ? new Vector3(innen.Breite / 2, innen.Tiefe / 2, 90) : new Vector3((float)halt.X, (float)halt.Y, halt.Blick);
    }
}
