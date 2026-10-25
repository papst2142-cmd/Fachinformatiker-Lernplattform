using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 7, W3: Vergleichswerte G43 (<c>spiel_fingerabdruck.json</c>, Erzeuger
/// <c>werkzeuge/vergleich/erzeuge_spiel_fingerabdruck.py</c> mit <c>fingerabdruck.py</c> und
/// <c>szenario045.py</c> unveraendert aus dem Bericht 0.54): C# spielt das Szenario 045 (spaeter
/// Demo-Stand mit Firma, Filiale, Raeumen, Werbung, Zertifizierung, Kredit und 8 weiteren Tagen)
/// wie Python und ergibt jedes Ereignis, jeden Einzelwert, die Bestenliste und den Hash f2b59ce8...6656.
/// </summary>
public class SpielFingerabdruckTests
{
    internal const string SollHash = "f2b59ce8f78a533cc3ec565ab983d418959e788d8ff16d35a907de328a626656";

    private static readonly JsonElement Soll = VergleichsHilfe.Lade("spiel_fingerabdruck.json");

    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    private static object? W(JsonElement e) => Py.Lesen(e.GetRawText());

    private static string T(object? wert) => PythonJson.Text(wert);

    // szenario045.LOOK
    private static PyObjekt Aussehen() => new()
    {
        { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" },
    };

    internal static readonly Lazy<Spielablauf> Gespielt = new(Spielen);

    /// <summary>szenario045.play_day</summary>
    private static void Arbeitstag(Spielablauf spiel)
    {
        var stand = spiel.Stand;
        foreach (var bewerbung in Betrieb.Bewerbungen(stand, Inhalt))
        {
            if (stand.Personal.Count < Math.Min(7, stand.Kapazitaet) && !Py.Gleich(bewerbung["herkunft"], "bitweiche"))
            {
                spiel.Einstellen(bewerbung["id"]);
                stand = spiel.Stand;
            }
        }
        foreach (var entscheidung in stand.OffeneEntscheidungen())
        {
            spiel.Entscheiden(entscheidung["id"], ((PyObjekt)((List<object?>)entscheidung["optionen"]!)[0]!)["id"]);
        }
        stand = spiel.Stand;
        foreach (var anfrage in stand.Anfragen())
        {
            if (!Py.Wahr(anfrage["ergebnis"]))
            {
                spiel.Anbieten(anfrage["id"], 15L, Bewertung.Loesung(Betrieb.AnfrageAufgabe(anfrage, 15L, Inhalt), null, Inhalt) as PyObjekt);
            }
        }
        foreach (var ticket in spiel.Stand.KundenTickets())
        {
            if (Py.Wahr(ticket["an"]))
            {
                continue;
            }
            var optionen = Betrieb.TicketKandidaten(spiel.Stand, ticket, spiel.FirmenStufen(), Inhalt)
                .Where(o => !Py.Wahr(o["problem"]) && !Py.Gleich(o["an"], Betrieb.Selbst)).ToList();
            if (optionen.Count > 0)
            {
                var beste = optionen[0];
                foreach (var o in optionen.Skip(1))
                {
                    if (Py.Kleiner(beste["chance"], o["chance"]))
                    {
                        beste = o;
                    }
                }
                spiel.Delegieren(ticket["id"], beste["an"]);
            }
        }
        spiel.Feierabend();
    }

    /// <summary>fingerabdruck.py: szenario045.spaet(game, branch=True) und 8 weitere Tage.</summary>
    private static Spielablauf Spielen()
    {
        var zaehler = 0;
        return Spielen(new SpielSpeicher(() =>
        {
            zaehler += 7;
            return new DateTime(2026, 10, 1).AddSeconds(zaehler).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }));
    }

    /// <summary>Das Szenario mit einem beliebigen Speicher (W8: auch in der Datenbank).</summary>
    internal static Spielablauf Spielen(ISpielSpeicher speicher)
    {
        var spiel = new Spielablauf(Inhalt, speicher)
        {
            Wissen = () => (PyObjekt)W(Soll.GetProperty("wissen"))!,
            ThemenWissen = () => (PyObjekt)W(Soll.GetProperty("themen_wissen"))!,
            LernProjekte = () => new PySet(((List<object?>)W(Soll.GetProperty("lernprojekte"))!)),
        };
        spiel.Profil("Nico", Aussehen());
        var aufgaben = ((List<object?>)Inhalt["aufgaben"]!).Cast<PyObjekt>().ToList();
        for (var i = 0; i < aufgaben.Count; i++)
        {
            var reputation = new PyObjekt();
            foreach (var achse in Ereignisse.Achsen)
            {
                reputation.Add(achse, 1L);
            }
            spiel.Log(Ereignisse.Erledigt, new PyObjekt
            {
                { "aufgabe", aufgaben[i]["id"] }, { "tag", (long)Math.Min(113, 1 + i / 4) }, { "richtig", true }, { "geld", 60L }, { "reputation", reputation },
            });
        }
        for (long tag = 1; tag < 115; tag++)
        {
            spiel.Log(Ereignisse.Feierabend, new PyObjekt { { "tag", tag }, { "gehalt", tag > 60 ? 250L : 140L }, { "rang", "Senior" } });
        }
        spiel.Log(Ereignisse.Erledigt, new PyObjekt { { "aufgabe", "probe-erspartes" }, { "tag", 114L }, { "richtig", false }, { "geld", 400000L } });
        spiel.Neuaufbau();
        spiel.Gruenden("Nico IT-Service");
        for (var i = 0; i < 3; i++)
        {
            spiel.Ausbauen();
        }
        var original = spiel.PersonalEreignisse;
        spiel.PersonalEreignisse = (_, _, _) => [];
        try
        {
            while (spiel.Stand.Tag < 160)
            {
                Arbeitstag(spiel);
            }
        }
        finally
        {
            spiel.PersonalEreignisse = original;
        }
        spiel.Neuaufbau();
        if (Betrieb.FilialeMoeglich(spiel.Stand, Inhalt))
        {
            spiel.FilialeEroeffnen("Filiale Lindenau");
            var personal = spiel.Stand.PersonalListe().Where(p => Py.Gleich(p["standort"], SpielDaten.OrtBuero)).ToList();
            if (personal.Count > 0)
            {
                spiel.Versetzen(personal[^1]["id"], SpielDaten.OrtFiliale);
            }
        }
        spiel.Neuaufbau();
        foreach (var raum in new[] { "lager", "besprechung", "serverraum" })
        {
            spiel.RaumBauen(raum);
        }
        spiel.WerbungBuchen("online", Wirtschaft.WerbungLaufend);
        spiel.ZertifizierungStarten("iso9001");
        spiel.KreditAufnehmen(15000L, 100L, Wirtschaft.FreierKredit);
        for (var i = 0; i < 8; i++)
        {
            Arbeitstag(spiel);
        }
        spiel.Neuaufbau();
        return spiel;
    }

    [Fact]
    public void JedesEreignisWiePython()
    {
        var spiel = Gespielt.Value;
        var soll = Soll.GetProperty("ereignisse");
        var ist = spiel.Stand.Verlauf;
        var n = Math.Min(soll.GetArrayLength(), ist.Count);
        for (var i = 0; i < n; i++)
        {
            var s = T(W(soll[i]));
            var c = T(new List<object?> { ist[i].Art, ist[i].Daten });
            Assert.True(s == c, "Ereignis " + i + " von " + soll.GetArrayLength() + "\n  Python: " + s + "\n  C#:     " + c);
        }
        Assert.Equal(soll.GetArrayLength(), ist.Count);
        Assert.Equal(0, spiel.Abweichungen);
    }

    /// <summary>Die Werte des Fingerabdrucks (tag, geld, ansehen, mitarbeiter, stimmung).</summary>
    internal static PyObjekt Werte(Spielstand s) => new()
    {
        { "tag", s.Tag },
        { "geld", s.Geld },
        { "ansehen", s.Reputation },
        {
            "mitarbeiter", s.PersonalListe()
                .Select(m => new List<object?> { m["id"], m["gehalt"], Dumps(m["werte"], sortiert: true, ascii: true) })
                .OrderBy(m => (string)m[0]!, StringComparer.Ordinal).Cast<object?>().ToList()
        },
        {
            "stimmung", s.Stimmung.Select(p => new List<object?> { p.Key, p.Value })
                .OrderBy(p => (string)p[0]!, StringComparer.Ordinal).Cast<object?>().ToList()
        },
    };

    [Fact]
    public void JederEinzelwertWiePython()
    {
        var spiel = Gespielt.Value;
        var soll = (PyObjekt)W(Soll.GetProperty("werte"))!;
        var ist = Werte(spiel.Stand);
        foreach (var (k, v) in soll)
        {
            Assert.Equal(k + ": " + T(v), k + ": " + T(ist[k]));
        }
        Assert.Equal(soll.Select(p => p.Key), ist.Select(p => p.Key));
        var ausgabe = Soll.GetProperty("ausgabe");
        // round(s.money, 2) einer ganzen Zahl bleibt eine ganze Zahl
        Assert.Equal(ausgabe[0].GetString(), Prozent("Ereignisse %d Tag %d Geld %d", spiel.Stand.Verlauf.Count, spiel.Stand.Tag, spiel.Stand.Geld));
    }

    private static string Prozent(string fmt, params object?[] werte) => PyFormat.Prozent(fmt, werte);

    [Fact]
    public void BestenlisteWiePython()
    {
        var spiel = Gespielt.Value;
        var soll = Soll.GetProperty("bestenliste");
        var ist = spiel.Speicher.Bestenliste();
        Assert.True(soll.GetArrayLength() > 50, "zu wenige Zeilen in der Bestenliste: " + soll.GetArrayLength());
        for (var i = 0; i < Math.Min(soll.GetArrayLength(), ist.Count); i++)
        {
            var z = ist[i];
            Assert.Equal("Zeile " + i + ": " + T(W(soll[i])), "Zeile " + i + ": " + T(new List<object?> { z.Art, z.Schluessel, z.Wert, z.Daten }));
        }
        Assert.Equal(soll.GetArrayLength(), ist.Count);
    }

    [Fact]
    public void HashWieFingerabdruck()
    {
        Assert.Equal(SollHash, Soll.GetProperty("hash").GetString());
        Assert.Equal(SollHash, Hash(Gespielt.Value.Stand));
    }

    /// <summary>fingerabdruck.py: sha256 aus Ereignissen (Art, Daten) und den Werten des Spielstands.</summary>
    internal static string Hash(Spielstand s)
    {
        var blob = new PyObjekt
        {
            { "events", s.Verlauf.Select(e => (object?)new List<object?> { e.Art, e.Daten }).ToList() },
            { "werte", Werte(s) },
        };
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Dumps(blob, sortiert: true, ascii: false))));
    }

    /// <summary>json.dumps mit den Standard-Trennzeichen (", ", ": ") wie fingerabdruck.py.</summary>
    internal static string Dumps(object? wert, bool sortiert, bool ascii)
    {
        var b = new StringBuilder();
        Schreiben(b, wert, sortiert, ascii);
        return b.ToString();
    }

    private static void Schreiben(StringBuilder b, object? wert, bool sortiert, bool ascii)
    {
        switch (wert)
        {
            case null:
                b.Append("null");
                break;
            case bool x:
                b.Append(x ? "true" : "false");
                break;
            case long or int:
                b.Append(Convert.ToString(wert, CultureInfo.InvariantCulture));
                break;
            case double d:
                b.Append(PythonJson.Zahl(d));
                break;
            case string text:
                b.Append('"');
                foreach (var c in text)
                {
                    if (c == '"' || c == '\\')
                    {
                        b.Append('\\').Append(c);
                    }
                    else if (c is '\n' or '\r' or '\t' or '\b' or '\f')
                    {
                        b.Append(c switch { '\n' => "\\n", '\r' => "\\r", '\t' => "\\t", '\b' => "\\b", _ => "\\f" });
                    }
                    else if (c < 0x20 || (ascii && c > 0x7f))
                    {
                        b.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        b.Append(c);
                    }
                }
                b.Append('"');
                break;
            case PyObjekt o:
                b.Append('{');
                var paare = sortiert ? o.OrderBy(p => p.Key, StringComparer.Ordinal).ToList() : [.. o];
                for (var i = 0; i < paare.Count; i++)
                {
                    if (i > 0)
                    {
                        b.Append(", ");
                    }
                    Schreiben(b, paare[i].Key, sortiert, ascii);
                    b.Append(": ");
                    Schreiben(b, paare[i].Value, sortiert, ascii);
                }
                b.Append('}');
                break;
            case PyTupel t:
                Schreiben(b, t.Werte.ToList(), sortiert, ascii);
                break;
            case System.Collections.IEnumerable liste:
                b.Append('[');
                var erstes = true;
                foreach (var e in liste)
                {
                    if (!erstes)
                    {
                        b.Append(", ");
                    }
                    erstes = false;
                    Schreiben(b, e, sortiert, ascii);
                }
                b.Append(']');
                break;
            default:
                throw new NotSupportedException("json.dumps fuer " + wert.GetType().Name);
        }
    }
}
