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

    /// <summary>
    /// Phase 9 W1 (Sammelliste 85): die einzigen Faelle, in denen C# bewusst strenger ist als Python. Python liest dort
    /// Zahlen aus Texten (float("19"), int("١٢"), "٣٠".isdigit()) und laesst den Inhalt durch; das C#-Spiel rechnet
    /// nur mit echten Zahlen und lehnt solche Inhalte deshalb schon beim Laden ab. Neue Eintraege nur mit Begruendung.
    /// </summary>
    private static readonly HashSet<string> CSharpStrenger =
    [
        "Kredite Laufzeit arabisch", "Kredite Zins Text", "Steuersatz Text", "Steuersatz Text Leerzeichen",
        "Steuersatz Text Strich", "Steuersatz Text unendlich", "Steuersatz Text arabisch", "Steuer faellig arabisch",
    ];

    [Fact]
    public void KaputteInhalteWiePython()
    {
        var anzahl = 0;
        var abbrueche = 0;
        var abweichungen = new List<string>();
        var strenger = 0;
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
                if (fehler is null && gemeldet!.Count == 0)
                    abweichungen.Add(name + ": Python bricht ab (" + art + "), C# meldet nichts");
                abbrueche++;
                continue;
            }
            var soll = T(W(fall.GetProperty("probleme")));
            List<string>? liste = null;
            var ausnahme = Record.Exception(() => liste = SpielPruefung.Pruefen(inhalt, Lernen));
            var ist = ausnahme is null ? T(liste!.Cast<object?>().ToList()) : "Abbruch: " + ausnahme.Message;
            if (CSharpStrenger.Contains(name!))
            {
                // C# lehnt ab, was Python als Zahl umdeutet; abgelehnt werden muss es (sonst stuerzt das Spiel spaeter)
                if (ausnahme is null && liste!.Count == 0)
                    abweichungen.Add(name + ": C# muss strenger sein als Python, meldet aber nichts");
                strenger++;
            }
            else if (soll != ist)
            {
                abweichungen.Add(name + "\n  Python: " + soll + "\n  C#:     " + ist);
            }
            anzahl++;
        }
        Assert.True(abweichungen.Count == 0, abweichungen.Count + " Abweichungen:\n" + string.Join("\n", abweichungen));
        Assert.Equal(CSharpStrenger.Count, strenger);
        Assert.True(anzahl > 400, anzahl + " Faelle");
        Assert.True(abbrueche >= 15, abbrueche + " Abbrueche");
    }

    /// <summary>
    /// Phase 9 W1 (Sammelliste 84): text.format(**werte) wie Python 3.12 fuer alle Proben aus G42 "format"
    /// (von Hand und mit festem Startwert zufaellig zusammengesetzt, je mit den Werten der Pruefung).
    /// "Nicht nachgebildet" (Ketten ueber Methoden und __doc__ hinaus) bricht ab und ist damit strenger
    /// als Python; das darf nur selten vorkommen.
    /// </summary>
    [Fact]
    public void PlatzhalterWiePython()
    {
        var format = Soll.GetProperty("format");
        var werte = format.GetProperty("werte").EnumerateObject().ToDictionary(g => g.Name, g => (IReadOnlyDictionary<string, object>)g.Value
            .EnumerateObject().ToDictionary(w => w.Name, w => w.Value.ValueKind == JsonValueKind.String ? (object)w.Value.GetString()! : w.Value.GetInt64()));
        var fehler = new List<string>();
        int proben = 0, strenger = 0;
        var zahl = new Dictionary<string, int>();
        foreach (var probe in format.GetProperty("proben").EnumerateArray())
        {
            var gruppe = probe[0].GetString()!;
            var text = probe[1].GetString()!;
            var soll = probe[2].GetString()!;
            var ist = SpielPruefung.FormatPruefen(text, werte[gruppe]);
            proben++;
            zahl[soll] = zahl.GetValueOrDefault(soll) + 1;
            if (ist == FormatUrteil.NichtNachgebildet)
            {
                strenger++;
                continue;
            }
            var name = ist switch { FormatUrteil.Geht => "ok", FormatUrteil.Fehler => "fehler", _ => "abbruch" };
            if (name != soll)
            {
                fehler.Add(gruppe + " " + text + ": Python " + soll + ", C# " + name);
            }
        }
        Assert.True(fehler.Count == 0, string.Join("\n", fehler.Take(30)) + "\n(" + fehler.Count + " von " + proben + ")");
        Assert.True(proben > 6000 && zahl.GetValueOrDefault("ok") > 1000 && zahl.GetValueOrDefault("fehler") > 1000
            && zahl.GetValueOrDefault("abbruch") > 50, proben + " Proben");
        Assert.True(strenger * 100 < proben, strenger + " von " + proben + " nicht nachgebildet");
    }

    /// <summary>Die eingebauten Listen dir(str) und dir(int) sind die von Python 3.12.</summary>
    [Fact]
    public void AttributeWiePython()
    {
        var attribute = Soll.GetProperty("format").GetProperty("attribute");
        Assert.Equal(attribute.GetProperty("str").EnumerateArray().Select(a => a.GetString()!).Order(StringComparer.Ordinal), SpielPruefung.StrAttribute.Order(StringComparer.Ordinal));
        Assert.Equal(attribute.GetProperty("int").EnumerateArray().Select(a => a.GetString()!).Order(StringComparer.Ordinal), SpielPruefung.IntAttribute.Order(StringComparer.Ordinal));
    }
}
