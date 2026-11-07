using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>Art eines Katalog-Eintrags: 3D-Modell oder Effekt (nur 2D-Bild, "Nur Aussehen").</summary>
public enum KatalogArt
{
    Modell,
    Effekt,
}

/// <summary>
/// Ein Eintrag des vollen CC0-Katalogs (Weisung P12-19): Kennung "paket/name", Anzeige, Gruppe, Dreiecke (echte Zahl des
/// Modells; ein Effekt zaehlt als leuchtendes Zeichen mit 2 Dreiecken), Kasten des Modells, Vorgabegroesse innen und
/// draussen (Breite, Tiefe, Hoehe), Feld im Vorschaubild des Pakets, Hauptfarbe (Effekt) und Suchtext.
/// </summary>
public sealed record KatalogEintrag(string Id, string Name, string Anzeige, string Paket, string Gruppe, KatalogArt Art, int Feld,
    int Dreiecke, double[] Innen, double[] Aussen, string? Farbe, string Suche)
{
    /// <summary>Typ eines Stuecks im Innenraum bzw. Kennung draussen: "cc0:paket/name".</summary>
    public string Typ => Katalog.Praefix + Id;
}

/// <summary>
/// Phase 13 Welle 3b (Weisungen P12-19, P12-23 1a 2a 3a): der volle CC0-Katalog des Werkzeugs aus
/// <c>inhalte_stadtbau/grafik/</c> (Erzeuger <c>werkzeuge/stadtbau/wandle_katalog.py</c>). Beim Start wird nur die Liste
/// <c>katalog.json</c> gelesen; Modelle (<c>&lt;paket&gt;.bin</c>) und Vorschaubilder (<c>&lt;paket&gt;.png</c>) eines Pakets
/// erst, wenn sie gebraucht werden, mit Pruefsumme aus der Liste (sonst Fehler). Die Spielgrafik
/// (<c>inhalte_csharp/grafik/modelle.bin</c>) bleibt unberuehrt; ins Spiel kommt aus dem Katalog nur Verbautes (ab Welle 4).
/// </summary>
public sealed class Katalog
{
    public const string Praefix = "cc0:";

    public const string Ordner = "inhalte_stadtbau/grafik/";

    private readonly Func<string, byte[]?> _lesen;
    private readonly Dictionary<string, KatalogEintrag> _nachId;
    private readonly Dictionary<string, Dictionary<string, string>> _pruefsummen;
    private readonly ConcurrentDictionary<string, Lazy<Dictionary<string, GrafikModell>>> _modelle = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Lazy<byte[]>> _vorschau = new(StringComparer.Ordinal);

    private Katalog(Func<string, byte[]?> lesen, List<KatalogEintrag> eintraege, List<string> gruppen,
        Dictionary<string, string> titel, Dictionary<string, Dictionary<string, string>> pruefsummen, int feld, int spalten)
    {
        _lesen = lesen;
        Eintraege = eintraege;
        Gruppen = gruppen;
        PaketTitel = titel;
        _pruefsummen = pruefsummen;
        FeldGroesse = feld;
        Spalten = spalten;
        _nachId = eintraege.ToDictionary(e => e.Id, StringComparer.Ordinal);
    }

    /// <summary>Alle Eintraege in der Reihenfolge der Liste (Paket, dann Name).</summary>
    public IReadOnlyList<KatalogEintrag> Eintraege { get; }

    /// <summary>Gruppen in fester Reihenfolge (Effekte zuletzt).</summary>
    public IReadOnlyList<string> Gruppen { get; }

    /// <summary>Paket -> Titel (z. B. "Kenney Furniture Kit").</summary>
    public IReadOnlyDictionary<string, string> PaketTitel { get; }

    /// <summary>Kantenlaenge eines Vorschaufelds in Bildpunkten.</summary>
    public int FeldGroesse { get; }

    public int Spalten { get; }

    private static readonly Lazy<Katalog?> EingebettetLazy = new(() => Laden(EingebettetLesen));

    private static Katalog? _aktiv;
    private static bool _aktivGesetzt;

    /// <summary>Der Katalog, den Bearbeitung und Pruefung nutzen: die eingebetteten Dateien, in Tests die Dateien des Repos.</summary>
    public static Katalog? Aktiv
    {
        get => _aktivGesetzt ? _aktiv : Eingebettet;
        set
        {
            _aktiv = value;
            _aktivGesetzt = true;
        }
    }

    /// <summary>Der Katalog aus den eingebetteten Dateien des Werkzeugs (null, wenn keine Liste da ist).</summary>
    public static Katalog? Eingebettet => EingebettetLazy.Value;

    /// <summary>Datei aus den eingebetteten Dateien von Stadtbau.Core (Pfad wie im Repo).</summary>
    public static byte[]? EingebettetLesen(string pfad)
    {
        using var s = typeof(Katalog).Assembly.GetManifestResourceStream(pfad);
        if (s is null)
        {
            return null;
        }
        using var m = new MemoryStream();
        s.CopyTo(m);
        return m.ToArray();
    }

    /// <summary>Liest nur <c>katalog.json</c> (null ohne Liste).</summary>
    public static Katalog? Laden(Func<string, byte[]?> lesen)
    {
        if (lesen(Ordner + "katalog.json") is not { } json)
        {
            return null;
        }
        using var doc = JsonDocument.Parse(json);
        var w = doc.RootElement;
        if (w.GetProperty("format").GetInt32() != 1)
        {
            throw new InvalidDataException("katalog.json: unbekanntes Format");
        }
        var gruppen = w.GetProperty("gruppen").EnumerateArray().Select(g => g.GetString()!).ToList();
        var titel = new Dictionary<string, string>(StringComparer.Ordinal);
        var summen = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var p in w.GetProperty("pakete").EnumerateObject())
        {
            titel[p.Name] = p.Value.GetProperty("titel").GetString()!;
            summen[p.Name] = p.Value.GetProperty("dateien").EnumerateObject().ToDictionary(d => d.Name, d => d.Value.GetString()!, StringComparer.Ordinal);
        }
        var liste = new List<KatalogEintrag>();
        foreach (var m in w.GetProperty("modelle").EnumerateArray())
        {
            static double[] Zahlen(JsonElement e) => [.. e.EnumerateArray().Select(z => z.GetDouble())];
            liste.Add(new KatalogEintrag(
                m.GetProperty("id").GetString()!, m.GetProperty("name").GetString()!, m.GetProperty("anzeige").GetString()!,
                m.GetProperty("paket").GetString()!, m.GetProperty("gruppe").GetString()!,
                m.GetProperty("art").GetString() == "effekt" ? KatalogArt.Effekt : KatalogArt.Modell, m.GetProperty("feld").GetInt32(),
                m.GetProperty("dreiecke").GetInt32(), Zahlen(m.GetProperty("innen")), Zahlen(m.GetProperty("aussen")),
                m.TryGetProperty("farbe", out var f) ? f.GetString() : null, Normal(m.GetProperty("suche").GetString()!)));
        }
        return new Katalog(lesen, liste, gruppen, titel, summen, w.GetProperty("feld").GetInt32(), w.GetProperty("spalten").GetInt32());
    }

    /// <summary>Ist <paramref name="typ"/> ein Katalog-Typ ("cc0:...")?</summary>
    public static bool IstKatalogTyp(string? typ) => typ is not null && typ.StartsWith(Praefix, StringComparison.Ordinal);

    public KatalogEintrag? Eintrag(string idOderTyp) =>
        _nachId.GetValueOrDefault(IstKatalogTyp(idOderTyp) ? idOderTyp[Praefix.Length..] : idOderTyp);

    /// <summary>Eintraege einer Gruppe (nach Paket wie in der Liste, darin nach Anzeige).</summary>
    public IEnumerable<KatalogEintrag> InGruppe(string gruppe) => Eintraege.Where(e => e.Gruppe == gruppe);

    /// <summary>Suchwort in Kleinbuchstaben wie Python (lower) ohne Umlaute (ä -> ae, ß -> ss), damit "Kühlschrank" und "kuehlschrank" gleich finden; gilt fuer Eingabe und Suchtext.</summary>
    public static string Normal(string text)
    {
        var sb = new StringBuilder(text.Length + 4);
        foreach (var c in Lernplattform.Core.Suche.PythonKlein.Klein(text))
        {
            sb.Append(c switch { 'ä' => "ae", 'ö' => "oe", 'ü' => "ue", 'ß' => "ss", _ => c.ToString() });
        }
        return sb.ToString();
    }

    /// <summary>Suche nach Namen: jedes Wort der Eingabe muss im Suchtext stehen (deutsch oder englisch); leer = nichts.</summary>
    public List<KatalogEintrag> Suchen(string eingabe, string? gruppe = null)
    {
        var worte = Normal(eingabe).Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries);
        if (worte.Length == 0)
        {
            return [];
        }
        return [.. Eintraege.Where(e => (gruppe is null || e.Gruppe == gruppe) && worte.All(w => e.Suche.Contains(w, StringComparison.Ordinal)))];
    }

    private byte[] DateiGeprueft(string paket, string datei)
    {
        var daten = _lesen(Ordner + datei) ?? throw new FileNotFoundException("Katalog: " + datei + " fehlt");
        var soll = _pruefsummen.TryGetValue(paket, out var s) ? s.GetValueOrDefault(datei) : null;
        var ist = Convert.ToHexStringLower(SHA256.HashData(daten));
        if (ist != soll)
        {
            throw new InvalidDataException("Katalog: Pruefsumme von " + datei + " ist " + ist + " statt " + soll);
        }
        return daten;
    }

    /// <summary>Das Vorschaubild eines Pakets als PNG (geprueft, einmal gelesen).</summary>
    public byte[] Vorschaubild(string paket) =>
        _vorschau.GetOrAdd(paket, p => new Lazy<byte[]>(() => DateiGeprueft(p, p + ".png"))).Value;

    /// <summary>Alle Modelle eines Pakets (geprueft, einmal gelesen; ein Effekt-Paket hat keine).</summary>
    public IReadOnlyDictionary<string, GrafikModell> Modelle(string paket) =>
        _modelle.GetOrAdd(paket, p => new Lazy<Dictionary<string, GrafikModell>>(() =>
            _pruefsummen.TryGetValue(p, out var s) && s.ContainsKey(p + ".bin") ? Lesen(DateiGeprueft(p, p + ".bin")) : [])).Value;

    /// <summary>Das Modell eines Eintrags (null bei einem Effekt).</summary>
    public GrafikModell? Modell(KatalogEintrag e) => e.Art == KatalogArt.Modell ? Modelle(e.Paket).GetValueOrDefault(e.Name) : null;

    /// <summary>
    /// Phase 13 Welle 4a (Weisung P12-28): das Modell eines Eintrags fuer die 3D-Zeichnung des Spiels, mit dem Namen
    /// "cc0:paket/name" wie in <c>katalog_verbaut.bin</c>; ein Effekt ist der leuchtende Stern des Spiels
    /// (<see cref="Katalogstueck.Stern"/>). Gleiche Dreiecke wie die mitgelieferte Datei (Test bytegleich).
    /// </summary>
    public GrafikModell? SpielModell(KatalogEintrag e) =>
        e.Art == KatalogArt.Effekt ? Katalogstueck.Stern(e.Typ, e.Farbe ?? "#FFFFFF")
        : Modell(e) is { } m ? new GrafikModell(e.Typ, m.Ecken, m.Farben) : null;

    /// <summary>
    /// Phase 13 Welle 4a: <paramref name="basis"/> (Spielgrafik) mit allen Stuecken aus dem Katalog, auf die
    /// <paramref name="stand"/> verweist (innen und draussen) und die <paramref name="basis"/> noch nicht hat. So zeichnet die
    /// 3D-Ansicht des Werkzeugs dieselben Modelle an derselben Stelle wie das Spiel nach dem Einbau.
    /// </summary>
    public GrafikBibliothek? MitStuecken(GrafikBibliothek? basis, StadtFassung stand)
    {
        if (basis is null)
        {
            return null;
        }
        var typen = new SortedSet<string>(StringComparer.Ordinal);
        StadtbauPruefung.KatalogVerweise(stand.Innenraeume, typen);
        try
        {
            foreach (var k in Katalogstueck.AusStadtdaten(stand.Stadtdaten))
            {
                typen.Add(k.Typ);
            }
        }
        catch (InvalidDataException)
        {
            // falscher Aufbau ist ein Fund der Pruefung; gezeichnet wird, was sich lesen laesst
        }
        var neu = typen.Where(t => basis.Modell(t) is null).Select(t => Eintrag(t) is { } e ? SpielModell(e) : null).OfType<GrafikModell>().ToList();
        return neu.Count == 0 ? basis : basis.Mit(neu);
    }

    /// <summary>
    /// S 0.9.0: ein Teil fuer den Gebaeude-Editor ("cc0:modular/&lt;name&gt;") aus dem aktiven Katalog, sonst null (gleiche
    /// Dreiecke wie das Paket).
    /// </summary>
    public static GrafikModell? Bauteil(string id) =>
        id.StartsWith(Bauteile.Praefix, StringComparison.Ordinal) ? Aktiv?.Modelle(Bauteile.Paket).GetValueOrDefault(id[Bauteile.Praefix.Length..]) : null;

    /// <summary>Pakete, deren Modelle schon gelesen sind (Test: der Katalog laedt nur, was gezeigt wird).</summary>
    public IReadOnlyCollection<string> GeleseneModellPakete => [.. _modelle.Where(p => p.Value.IsValueCreated).Select(p => p.Key)];

    public IReadOnlyCollection<string> GeleseneVorschauPakete => [.. _vorschau.Where(p => p.Value.IsValueCreated).Select(p => p.Key)];

    /// <summary>Aufbau wie modelle.bin der Spielgrafik ("FLGM", Version 1).</summary>
    internal static Dictionary<string, GrafikModell> Lesen(byte[] bin)
    {
        using var leser = new BinaryReader(new MemoryStream(bin));
        if (Encoding.ASCII.GetString(leser.ReadBytes(4)) != "FLGM" || leser.ReadUInt32() != 1)
        {
            throw new InvalidDataException("Katalog: unbekannter Aufbau einer Modell-Datei");
        }
        var anzahl = leser.ReadUInt32();
        var modelle = new Dictionary<string, GrafikModell>(StringComparer.Ordinal);
        for (var m = 0; m < anzahl; m++)
        {
            var name = Encoding.UTF8.GetString(leser.ReadBytes(leser.ReadUInt16()));
            var dreiecke = (int)leser.ReadUInt32();
            var ecken = new float[dreiecke * 9];
            var farben = new byte[dreiecke * 3];
            for (var d = 0; d < dreiecke; d++)
            {
                for (var i = 0; i < 9; i++)
                {
                    ecken[(d * 9) + i] = leser.ReadSingle();
                }
                var rgba = leser.ReadBytes(4);
                farben[d * 3] = rgba[0];
                farben[(d * 3) + 1] = rgba[1];
                farben[(d * 3) + 2] = rgba[2];
            }
            modelle[name] = new GrafikModell(name, ecken, farben);
        }
        return modelle;
    }

    /// <summary>Anzahl Eintraege je Gruppe (fuer die Anzeige im Katalog).</summary>
    public int AnzahlIn(string gruppe) => Eintraege.Count(e => e.Gruppe == gruppe);

    internal static Assembly Baugruppe => typeof(Katalog).Assembly;
}
