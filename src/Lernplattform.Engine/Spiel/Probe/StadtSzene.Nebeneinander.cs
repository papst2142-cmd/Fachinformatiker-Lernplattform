namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Engine-Welle A 1.4.0 (Weisung P12-351, Sammelliste 436): die Stadt wird nebeneinander gebaut. Die Mittellinien entstehen
/// neben dem Bau der Landschaft und werden an der alten Stelle angehaengt, das Laufraster entsteht neben der Grundlage, der
/// Plan des Instancing in einem Durchgang auf allen Kernen. Das Ergebnis gleicht Byte fuer Byte dem Bau der Reihe nach (Test
/// <c>StadtNebeneinanderTests</c>).
/// </summary>
public sealed partial class StadtSzene
{
    /// <summary>Nur Tests (Sammelliste 307, je Ablauf): alles der Reihe nach auf einem Faden wie bis A 1.3.2.</summary>
    public static bool NacheinanderFuerTests { get => Nacheinander.Value; set => Nacheinander.Value = value; }

    private static readonly AsyncLocal<bool> Nacheinander = new();

    /// <summary>Nur Tests (Gegenprobe): haengt die nebenher gebauten Mittellinien in umgekehrter Reihenfolge an.</summary>
    public static bool VertauschtFuerTests { get => Vertauscht.Value; set => Vertauscht.Value = value; }

    private static readonly AsyncLocal<bool> Vertauscht = new();

    /// <summary>
    /// Engine-Welle A 1.4.0: wartet, bis alles fertig ist, was nach <see cref="Bauen"/> noch nebenher entsteht (Laufraster).
    /// Die Messpunkte „Stadt bauen“ rufen es, damit keine Arbeit nach der Messung weiterlaeuft.
    /// </summary>
    public void FertigWarten() => _ = _laufraster.Wert;

    /// <summary>
    /// Ein Ergebnis, das nebenher entsteht (eigene Aufgabe auf einem freien Kern). Mit <see cref="NacheinanderFuerTests"/>
    /// entsteht es der Reihe nach: <c>sofort</c> an der Stelle des Anlegens, sonst beim ersten Lesen (= alte Stelle im Bau).
    /// Ein Fehler kommt beim Lesen unveraendert heraus.
    /// </summary>
    internal sealed class Spaeter<T>
    {
        private readonly Task<T>? _aufgabe;
        private Func<T>? _rechnen;
        private T? _wert;

        public Spaeter(Func<T> rechnen, bool sofort = false)
        {
            if (!Nacheinander.Value)
            {
                _aufgabe = Task.Run(rechnen);
            }
            else if (sofort)
            {
                _wert = rechnen();
            }
            else
            {
                _rechnen = rechnen;
            }
        }

        public T Wert
        {
            get
            {
                if (_aufgabe is not null)
                {
                    return _aufgabe.GetAwaiter().GetResult();
                }
                if (_rechnen is { } r)
                {
                    _wert = r();
                    _rechnen = null;
                }
                return _wert!;
            }
        }
    }
}
