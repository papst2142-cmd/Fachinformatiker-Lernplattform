using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.49.0 (Weisung P12-231 Antwort 2c, Sammelliste 328): die Funde aller Innenraeume, die Speichern und Senden
/// sperren koennen (jeder geaenderte Innenraum, jede seiner Etagen, jeder Innenraum eines eigenen Gebaeudes), ohne dass der
/// Raum offen sein muss. <see cref="Vorbereiten"/> kopiert auf dem Oberflaechen-Faden alles, was die Pruefung liest;
/// <see cref="Rechnen"/> laeuft danach im Hintergrund und liest nie den Stand, der gerade bearbeitet wird.
/// <see cref="Projekt.RoteFunde"/> nutzt denselben Weg, darum zeigt die Liste genau die Funde, die das Senden sperren.
/// </summary>
public static class InnenFunde
{
    /// <summary>Ein Innenraum (oder eine Etage „ort@k“) mit seinem Namen fuer die Anzeige und seinen Funden (rot und gelb).</summary>
    public sealed record Ort(string Schluessel, string Name, List<StadtbauFund> Funde)
    {
        public int Rot => Funde.Count(f => f.Rot);
    }

    /// <summary>Was die Pruefung eines Raums liest (Kopien).</summary>
    public sealed record Raum(string Schluessel, string Name, PyObjekt Grundriss, List<PyObjekt> Personen, PyObjekt? Erdgeschoss,
        List<StadtbauFund> EntwurfFunde);

    /// <summary>Pruefauftrag: Kopien aller Raeume plus die festen Daten des Projekts.</summary>
    public sealed record Auftrag(List<Raum> Raeume, Spieldaten Daten, PyObjekt BasisInnenraeume);

    /// <summary>Schluessel aller Raeume, die das Senden sperren koennen (wie bisher in <see cref="Projekt.RoteFunde"/>).</summary>
    public static List<string> Schluessel(Projekt p) => [.. p.GeaenderteOrte.Concat(p.Bauten).SelectMany(p.EtagenVon)];

    /// <summary>Auf dem Oberflaechen-Faden: Kopien der Grundrisse aller Raeume (<paramref name="nur"/>: nur diese Schluessel).</summary>
    public static Auftrag Vorbereiten(Projekt p, IEnumerable<string>? nur = null)
    {
        var raeume = new List<Raum>();
        foreach (var o in nur ?? Schluessel(p))
        {
            var basis = Etagen.Teilen(o).Ort;
            var etage = Etagen.Teilen(o).Etage;
            var eg = p.Grundriss(basis);
            raeume.Add(new Raum(o, p.OrtName(o), (PyObjekt)Py.Kopie(p.Grundriss(o))!, [.. p.Personen(o).Select(x => (PyObjekt)Py.Kopie(x)!)],
                Etagen.Hat(eg) ? (PyObjekt)Py.Kopie(eg)! : null,
                [.. p.EtagenEntwurfFunde(basis).Where(f => f.Etage == etage).Select(f => f.Fund)]));
        }
        return new Auftrag(raeume, p.Daten, p.Basis.Innenraeume);
    }

    /// <summary>Im Hintergrund: Funde je Raum, in der Reihenfolge des Auftrags (gleiche Rechnung wie <see cref="Projekt.PruefenMitGrenzen"/>).</summary>
    public static List<Ort> Rechnen(Auftrag a) => [.. a.Raeume.Select(r => new Ort(r.Schluessel, r.Name, PruefenMitGrenzen(a, r).Funde))];

    /// <summary>Funde und Grenzen eines Raums.</summary>
    public static (List<StadtbauFund> Funde, (long Dreiecke, int Aufrufe) Grenzen) PruefenMitGrenzen(Auftrag a, Raum r)
    {
        var (basis, etage) = Etagen.Teilen(r.Schluessel);
        var (funde, grenzen) = KatalogPruefung.Innen(Katalog.Aktiv, basis, r.Grundriss, r.Personen, a.Daten.Grafik, a.Daten.Geschaefte, a.BasisInnenraeume, etage);
        if (r.Erdgeschoss is { } eg)
        {
            // der Grundriss jeder Etage ist oben schon geprueft (mit Katalog-Stuecken); hier nur die Regeln zwischen den Etagen
            funde.AddRange(StadtbauPruefung.Etagen(basis, eg, a.Daten.Grafik, (_, _) => []).Where(f => f.Etage == etage).Select(f => f.Fund));
        }
        // S 0.19.0 (Nico 3a, 5a): Etagen eines selbst gebauten Gebaeudes gegen die Ebenen seines Entwurfs
        funde.AddRange(r.EntwurfFunde);
        return (funde, grenzen);
    }

    /// <summary>Nur die roten Funde, je Raum gruppiert; Raeume ohne Rot fallen weg.</summary>
    public static List<Ort> NurRot(IEnumerable<Ort> orte) =>
        [.. orte.Select(o => o with { Funde = [.. o.Funde.Where(f => f.Rot)] }).Where(o => o.Funde.Count > 0)];
}
