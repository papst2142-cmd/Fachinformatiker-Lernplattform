using System.Numerics;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Welle S 0.55.0 (Weisung P12-282 Punkt 3): Dreiecke und Aufrufe eines Bilds MIT Nachladen ohne Grafikkarte, mit denselben
/// Regeln wie <c>StadtFlaeche.NachladenZeichnen</c>: Boden (ein Aufruf), je Kachel im Sichtkegel (<see cref="Nachlader.Kugel"/>)
/// geladen ihr Netz (nur Zellen im Bild, <see cref="Netz.Laeufe"/>) oder sonst ihre Fernkaesten (aufeinanderfolgende in einem
/// Aufruf), die Lampen der Ampeln (ein Aufruf), je Satz des Instancing die Stuecke im Bild (wie <see cref="Draussen"/>) und
/// je Haus einer geladenen Kachel im Sichtkegel ein Aufruf (Netz nah: die Kachel bringt keine Fernform). Was sich bewegt,
/// kommt wie bei <see cref="Draussen"/> dazu. Der Start-Test des Werkzeugs vergleicht die Zaehlung mit dem gezeichneten Bild.
/// </summary>
public static partial class StadtbauGrenzen
{
    public sealed class Nachladen
    {
        private readonly long _boden;
        private readonly long? _ampelLichter;
        private readonly IReadOnlyList<InstanzSatz> _saetze;
        private readonly StadtSzene.Grundlage _grundlage;
        private readonly List<(Stadtteile.Kachel Kachel, Vector3 Mitte, float Halb)> _kacheln = [];
        private readonly Dictionary<Stadtteile.Kachel, (Netz Fest, List<(Vector3 Mitte, float Halb, long Dreiecke)> Haeuser)> _netze = [];

        /// <param name="szene">Die Szene (Breite, Tiefe).</param>
        /// <param name="grundlage">Was immer geladen ist (<see cref="StadtSzene.GrundlageVorbereiten"/>).</param>
        /// <param name="pakete">Die gepackten Kacheln (mit oder ohne Kleingruen wie die Grafikstufe beim Laden).</param>
        public Nachladen(StadtSzene szene, StadtSzene.Grundlage grundlage, IEnumerable<Nachlader.Paket> pakete)
        {
            _grundlage = grundlage;
            _boden = grundlage.Boden.Indizes.Count / 3;
            _ampelLichter = grundlage.AmpelLichter.Count > 0 ? grundlage.AmpelLichter.Sum(n => (long)n.Indizes.Count / 3) : null;
            _saetze = grundlage.Instanzen?.Saetze ?? [];
            foreach (var k in Stadtteile.Alle(szene.Breite, szene.Tiefe))
            {
                var (mitte, halb) = Nachlader.Kugel(grundlage, k);
                _kacheln.Add((k, mitte, halb));
            }
            foreach (var p in pakete)
            {
                _netze[p.Kachel] = (p.Fest, [.. p.Haeuser.Select(h =>
                {
                    var (m, r) = h.Netz.Kugel();
                    return (m, r, (long)h.Netz.Indizes.Count / 3);
                })]);
            }
        }

        /// <summary>
        /// Fester Teil eines Bilds: Kamera <paramref name="sichtProj"/> im Abstand <paramref name="abstand"/>, geladen sind
        /// die Kacheln <paramref name="geladen"/>; <paramref name="werte"/> = Grafikstufe (Fernform der Stuecke, Kleingruen
        /// des Instancing), <paramref name="ohneSparen"/> = kein Sichtkegel (wie <c>StadtFlaeche.OhneSparenFuerTests</c>).
        /// </summary>
        public (long Dreiecke, int Aufrufe) Bild(Matrix4x4 sichtProj, float abstand, IReadOnlySet<Stadtteile.Kachel> geladen, Grafikwerte werte, bool ohneSparen = false)
        {
            long dreiecke = _boden;
            var aufrufe = 1;
            var fernBild = werte.Fern(abstand) && !ohneSparen;
            // eigene Listen je Aufruf: das Werkzeug zaehlt die Kacheln auf allen Kernen
            var _laeufe = new List<(int Start, int Anzahl)>();
            var _fern = new List<(int Start, int Anzahl)>();
            foreach (var (k, mitte, halb) in _kacheln)
            {
                if (!ohneSparen && !FestUebergang.ImSichtkegel(sichtProj, mitte, halb))
                {
                    continue;
                }
                if (geladen.Contains(k) && _netze.TryGetValue(k, out var e))
                {
                    Netz.Laeufe(e.Fest.Zellen, e.Fest.Indizes.Count, sichtProj, ohneSparen, _laeufe);
                    foreach (var (_, anzahl) in _laeufe)
                    {
                        dreiecke += anzahl / 3;
                        aufrufe++;
                    }
                    continue;
                }
                if (_grundlage.FernBereiche.TryGetValue(k, out var b) && b.Anzahl > 0)
                {
                    _fern.Add(b);
                }
            }
            if (_fern.Count > 0)
            {
                _fern.Sort((a, b) => a.Start.CompareTo(b.Start));
                var (start, anzahl) = _fern[0];
                for (var i = 1; i <= _fern.Count; i++)
                {
                    if (i < _fern.Count && _fern[i].Start == start + anzahl)
                    {
                        anzahl += _fern[i].Anzahl;
                        continue;
                    }
                    dreiecke += anzahl / 3;
                    aufrufe++;
                    if (i < _fern.Count)
                    {
                        (start, anzahl) = _fern[i];
                    }
                }
            }
            if (_ampelLichter is { } al)
            {
                dreiecke += al;
                aufrufe++;
            }
            var gruen = werte.Gruen(abstand);
            foreach (var s in _saetze)
            {
                if (s.Ziel == Instanzen.Gruen && !gruen)
                {
                    continue;
                }
                var zahl = s.ImBild(sichtProj, null, alle: ohneSparen);
                if (zahl <= 0)
                {
                    continue;
                }
                dreiecke += (long)zahl * (fernBild && s.Fern is not null ? s.DreieckeFern : s.DreieckeNah);
                aufrufe++;
            }
            foreach (var k in geladen)
            {
                if (!_netze.TryGetValue(k, out var e))
                {
                    continue;
                }
                foreach (var (mitte, halb, d) in e.Haeuser)
                {
                    if (!ohneSparen && (halb < 0 || !FestUebergang.ImSichtkegel(sichtProj, mitte, halb)))
                    {
                        continue;
                    }
                    dreiecke += d;
                    aufrufe++;
                }
            }
            return (dreiecke, aufrufe);
        }
    }
}
