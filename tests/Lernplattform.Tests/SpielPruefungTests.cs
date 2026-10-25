using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 7, W2: Vergleichswerte G42 Teil "Pruefung" (<c>spiel_pruefung.json</c>, Erzeuger
/// <c>werkzeuge/vergleich/erzeuge_spiel_pruefung.py</c>): <see cref="SpielPruefung.Pruefen"/> meldet fuer die
/// echten Spielinhalte und fuer jede absichtlich kaputte Fassung dieselben Probleme in derselben
/// Reihenfolge wie <c>fisi_game.validate_game_content</c> (seit W3 mit Firma, Krediten, Geschaeft,
/// Spielwelt, Erfolgen und Serverfarm).
/// </summary>
public class SpielPruefungTests
{
    private static readonly JsonElement Soll = VergleichsHilfe.Lade("spiel_pruefung.json");
    private static readonly Lerninhalte Lernen = Lerninhalte.Laden(Pfade.Inhalte);

    private static object? W(JsonElement e) => Py.Lesen(e.GetRawText());

    private static string T(object? wert) => PythonJson.Text(wert);

    /// <summary>Aenderungen wie <c>anwenden()</c> im Erzeuger: set, del, add an einem Pfad.</summary>
    internal static PyObjekt Anwenden(PyObjekt inhalt, JsonElement aenderungen)
    {
        foreach (var schritt in aenderungen.EnumerateArray())
        {
            var pfad = schritt[1].EnumerateArray().ToList();
            object? ziel = inhalt;
            foreach (var teil in pfad.Take(pfad.Count - 1))
            {
                ziel = teil.ValueKind == JsonValueKind.Number ? ((List<object?>)ziel!)[teil.GetInt32()] : ((PyObjekt)ziel!)[teil.GetString()!];
            }
            var letzter = pfad[^1];
            switch (schritt[0].GetString())
            {
                case "set" when ziel is List<object?> liste:
                    liste[letzter.GetInt32()] = W(schritt[2]);
                    break;
                case "set":
                    Py.Setzen((PyObjekt)ziel!, letzter.GetString()!, W(schritt[2]));
                    break;
                case "del" when ziel is List<object?> liste:
                    liste.RemoveAt(letzter.GetInt32());
                    break;
                case "del":
                    var d = (PyObjekt)ziel!;
                    d.RemoveAt(d.FindIndex(e => e.Key == letzter.GetString()));
                    break;
                case "add":
                    var unter = letzter.ValueKind == JsonValueKind.Number ? ((List<object?>)ziel!)[letzter.GetInt32()] : ((PyObjekt)ziel!)[letzter.GetString()!];
                    ((List<object?>)unter!).Add(W(schritt[2]));
                    break;
                default:
                    throw new InvalidOperationException(schritt[0].GetString());
            }
        }
        return inhalt;
    }

    [Fact]
    public void EchteInhalteWiePython()
    {
        Assert.Equal(T(W(Soll.GetProperty("echt"))), T(SpielPruefung.Pruefen((PyObjekt)Py.Kopie(SpielInhaltTests.Inhalt)!, Lernen).Cast<object?>().ToList()));
    }

    [Fact]
    public void KaputteInhalteWiePython()
    {
        var anzahl = 0;
        foreach (var fall in Soll.GetProperty("faelle").EnumerateArray())
        {
            var name = fall.GetProperty("name").GetString();
            var inhalt = Anwenden((PyObjekt)Py.Kopie(SpielInhaltTests.Inhalt)!, fall.GetProperty("aenderungen"));
            if (fall.TryGetProperty("ausnahme", out var art))
            {
                // Python bricht hier mitten in der Pruefung ab (Fehler in der Python-Linie). C# darf
                // nicht still "in Ordnung" melden: entweder ebenfalls abbrechen oder das Problem nennen.
                List<string>? gemeldet = null;
                var fehler = Record.Exception(() => gemeldet = SpielPruefung.Pruefen(inhalt, Lernen));
                Assert.True(fehler is not null || gemeldet!.Count > 0, name + ": Python bricht ab (" + art + "), C# meldet nichts");
                continue;
            }
            var soll = T(W(fall.GetProperty("probleme")));
            var ist = T(SpielPruefung.Pruefen(inhalt, Lernen).Cast<object?>().ToList());
            Assert.True(soll == ist, name + "\n  Python: " + soll + "\n  C#:     " + ist);
            anzahl++;
        }
        Assert.True(anzahl > 350, anzahl + " Faelle");
    }
}
