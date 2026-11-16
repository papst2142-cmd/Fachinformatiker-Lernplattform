using System.Numerics;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Welle A 0.83.0 (Weisung P12-282: Pruefung der Testkarte im Werkzeug deutlich schneller): Regeln, die jede Strasse gegen
/// jede andere Linie pruefen (Strassen aufeinander, Strasse im Teich oder laengs im Fluss, Kreuzung im Bogen einer Kurve),
/// ueberspringen ein Paar, dessen Rahmen weiter auseinander liegen als der Abstand, ab dem die Regel ueberhaupt etwas findet.
/// Jeder Punkt einer Linie liegt in ihrem Rahmen, also ist sein Abstand zur anderen Linie mindestens der Abstand der Rahmen:
/// uebersprungen wird nur, was ohnehin nichts findet (Zugabe <see cref="RahmenZugabe"/> gegen Rundung). Gleiche Funde in
/// gleicher Reihenfolge; Gegenprobe <see cref="RahmenAusFuerTests"/>.
/// </summary>
public static partial class StadtbauPruefung
{
    /// <summary>Zugabe auf jede Grenze beim Vergleich der Rahmen.</summary>
    public const float RahmenZugabe = 0.05f;

    /// <summary>Rahmen (achsparallel) eines Linienzugs oder Vielecks.</summary>
    public readonly record struct Rahmen(Vector2 Min, Vector2 Max)
    {
        public static Rahmen Von(IReadOnlyList<Vector2> punkte)
        {
            if (punkte.Count == 0)
            {
                return new Rahmen(new Vector2(float.MaxValue), new Vector2(float.MinValue));
            }
            Vector2 min = punkte[0], max = punkte[0];
            for (var i = 1; i < punkte.Count; i++)
            {
                min = Vector2.Min(min, punkte[i]);
                max = Vector2.Max(max, punkte[i]);
            }
            return new Rahmen(min, max);
        }

        /// <summary>Kleinster Abstand zweier Rahmen (0, wenn sie sich beruehren oder ueberschneiden).</summary>
        public float Abstand(Rahmen o)
        {
            var dx = MathF.Max(MathF.Max(o.Min.X - Max.X, Min.X - o.Max.X), 0f);
            var dz = MathF.Max(MathF.Max(o.Min.Y - Max.Y, Min.Y - o.Max.Y), 0f);
            return MathF.Sqrt((dx * dx) + (dz * dz));
        }

        /// <summary>Liegen die Rahmen weiter als <paramref name="grenze"/> (plus Zugabe) auseinander? Dann findet die Regel nichts.</summary>
        public bool Fern(Rahmen o, float grenze) => !RahmenAus.Value && Abstand(o) > grenze + RahmenZugabe;
    }

    /// <summary>Gegenprobe Welle A 0.83.0: jedes Paar pruefen wie bis A 0.82.0 (je Ablauf).</summary>
    public static bool RahmenAusFuerTests { get => RahmenAus.Value; set => RahmenAus.Value = value; }

    private static readonly AsyncLocal<bool> RahmenAus = new();
}
