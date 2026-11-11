using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Welle D2 (Weisung P12-157, Kurzplan P12-97 Punkt c): Dreiecke und Aufrufe eines Bilds draussen OHNE Grafikkarte, mit
/// denselben Regeln wie das Zeichnen in <c>StadtFlaeche</c>: Netz der Stadt (fern ab <see cref="StadtSzene.FernAb"/> die
/// Fernform), Kleingruen bis <see cref="StadtSzene.GruenBis"/>, Paket-Netz nah/fern, je Satz des Instancing die Stuecke im
/// Sichtkegel (ein Aufruf je Satz mit Stuecken im Bild), die Lampen der Ampeln (ein Aufruf, seit A 0.60.0), je Haus im Sichtkegel ein Aufruf (fern die Fernform). Das ist
/// der feste Teil (<see cref="Bild"/>); dazu kommt, was sich bewegt (<see cref="Bewegt"/>: Autos oder Festgestalten,
/// Figur zweimal, Kreis, Zielmarke, Pakete vor der Tuer). Der Start-Test vergleicht beides mit dem gezeichneten Bild.
/// </summary>
public static partial class StadtbauGrenzen
{
    /// <summary>Die Groessen der Netze einer Szene (einmal je Bau gemerkt), rechnet je Kamera nur noch Sichtkegel.</summary>
    public sealed class Draussen
    {
        private readonly long _stadt;
        private readonly long _stadtFern;
        private readonly long? _gruen;
        private readonly long? _paketNah;
        private readonly long? _paketFern;
        private readonly List<(Vector3 Mitte, float Halb, long Nah, long Fern)> _haeuser = [];
        private readonly IReadOnlyList<InstanzSatz> _saetze;
        private readonly long? _ampelLichter;

        public Draussen(StadtSzene szene, StadtSzene.Fertig f)
        {
            _stadt = f.Stadt.Indizes.Count / 3;
            _stadtFern = (f.StadtFern ?? f.Stadt).Indizes.Count / 3;
            _gruen = f.Gruen is { } g ? g.Indizes.Count / 3 : null;
            if (f.PaketNah is { } pn && f.PaketFern is { } pf)
            {
                (_paketNah, _paketFern) = (pn.Indizes.Count / 3, pf.Indizes.Count / 3);
            }
            foreach (var haus in szene.Haeuser)
            {
                var nah = f.Haeuser[haus];
                var (mitte, halb) = nah.Kugel();
                var fern = f.HaeuserFern?.GetValueOrDefault(haus) ?? nah;
                _haeuser.Add((mitte, halb, nah.Indizes.Count / 3, fern.Indizes.Count / 3));
            }
            _saetze = f.Instanzen?.Saetze ?? [];
            // Welle A 0.60.0: die Lampen der Ampeln (ein Aufruf, alle Gruppen, auch die gerade dunklen)
            _ampelLichter = f.AmpelLichter?.Sum(n => (long)n.Indizes.Count / 3);
        }

        /// <summary>
        /// Fester Teil eines Bilds mit Kamera <paramref name="sichtProj"/> im Abstand <paramref name="abstand"/>;
        /// <paramref name="ohneSparen"/> = wie <c>StadtFlaeche.OhneSparenFuerTests</c> (kein Sichtkegel, keine Fernform).
        /// </summary>
        public (long Dreiecke, int Aufrufe) Bild(Matrix4x4 sichtProj, float abstand, bool ohneSparen = false)
        {
            var fern = abstand > StadtSzene.FernAb && !ohneSparen;
            var dreiecke = fern ? _stadtFern : _stadt;
            var aufrufe = 1;
            if (_gruen is { } g && abstand <= StadtSzene.GruenBis)
            {
                dreiecke += g;
                aufrufe++;
            }
            if (_paketNah is { } pn && _paketFern is { } pf)
            {
                dreiecke += abstand > StadtSzene.FernAb ? pf : pn;
                aufrufe++;
            }
            if (_ampelLichter is { } al)
            {
                dreiecke += al;
                aufrufe++;
            }
            foreach (var s in _saetze)
            {
                if (s.Ziel == Instanzen.Gruen && abstand > StadtSzene.GruenBis)
                {
                    continue;
                }
                var zahl = s.ImBild(sichtProj, null, alle: ohneSparen);
                if (zahl > 0)
                {
                    dreiecke += (long)zahl * (fern && s.Fern is not null ? s.DreieckeFern : s.DreieckeNah);
                    aufrufe++;
                }
            }
            foreach (var (mitte, halb, nah, hausFern) in _haeuser)
            {
                if (!ohneSparen && (halb < 0 || !FestUebergang.ImSichtkegel(sichtProj, mitte, halb)))
                {
                    continue;
                }
                dreiecke += fern ? hausFern : nah;
                aufrufe++;
            }
            return (dreiecke, aufrufe);
        }
    }
}
