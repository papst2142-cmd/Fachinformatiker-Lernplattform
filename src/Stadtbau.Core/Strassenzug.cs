using System.Globalization;
using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.51.0 (Weisung P12-241, Kurzplan P12-131 Punkt 3, Nico P12-231 Antwort 5c): eine Strassenzug-Vorlage ist die
/// Ausstattung, die beim „Straße ziehen“ mit der Strasse in EINEM Schritt dazukommt (<see cref="AussenBearbeitung.StrassenzugPlanen"/>):
/// Gehweg ja/nein, Baeume beidseitig mit Abstand, Zebrastreifen an jeder Einmuendung, eine Haltestelle in der Mitte, wenn die
/// Strasse lang genug ist. Laternen stellt das Werkzeug seit S 0.26.0 ohnehin an jede Strasse (<see cref="LaternenAutomatik"/>).
/// <list type="bullet">
/// <item>Drei feste Vorlagen (<see cref="Feste"/>): Wohnstrasse, Hauptstrasse, Landstrasse.</item>
/// <item>Eigene Vorlagen: je Vorlage eine Datei <c>strassenzuege/&lt;Name&gt;.fisstrassenzug</c> im Werkzeugordner, JSON, je Feld
/// eine Zeile, mit Formatnummer und Pruefsumme (Muster wie <see cref="Blueprint"/>). Eine kaputte Datei oder eine aus einer
/// neueren Werkzeug-Version wird nie still uebergangen oder ueberschrieben: sie steht mit dem Grund in der Liste
/// (<see cref="Alle"/>).</item>
/// </list>
/// </summary>
public sealed class Strassenzug
{
    public const string Format = "fisstrassenzug";

    /// <summary>Format 1 seit S 0.51.0 (echte Testdatei <c>tests/Stadtbau.Tests/Testdaten/strassenzug_S0.51.0.fisstrassenzug</c>).</summary>
    public const long FormatVersion = 1;

    public const string Endung = ".fisstrassenzug";

    /// <summary>Unterordner der Sicherungen (mit Klammern: so kann kein Projekt heissen).</summary>
    public const string SicherungOrdner = "(Strassenzuege)";

    public static readonly string[] Felder = ["format", "formatversion", "werkzeug", "name", "gehweg", "baeume", "baumabstand", "zebrastreifen", "haltestelle", "pruefsumme"];

    /// <summary>Kleinster und groesster Abstand der Baeume, Schritt der Knoepfe − und +.</summary>
    public const double BaumAbstandMin = 1.0, BaumAbstandMax = 10.0, BaumAbstandSchritt = 0.5;

    /// <summary>Abstand der Baeume der festen Vorlagen (Kurzplan P12-131: „Bäume beidseitig alle 3,5“, gleich dem Laternenabstand).</summary>
    public const double BaumAbstandVorgabe = 3.5;

    public required string Name { get; init; }

    /// <summary>Strasse mit Gehweg (Breite <see cref="AussenBearbeitung.NeuerGehweg"/> je Seite); false = ohne Gehweg.</summary>
    public bool Gehweg { get; init; }

    /// <summary>Baeume beidseitig entlang der Strasse.</summary>
    public bool Baeume { get; init; }

    /// <summary>Abstand der Baeume laengs der Strasse (je Seite).</summary>
    public double BaumAbstand { get; init; } = BaumAbstandVorgabe;

    /// <summary>Zebrastreifen an jeder Einmuendung der neuen Strasse (nur mit Gehweg, Regel der Pruefung).</summary>
    public bool Zebrastreifen { get; init; }

    /// <summary>Haltestelle in der Mitte des laengsten geraden Stuecks, wenn es lang genug ist (nur mit Gehweg).</summary>
    public bool Haltestelle { get; init; }

    /// <summary>Feste Vorlage des Werkzeugs (keine Datei, nicht loeschbar).</summary>
    public bool Fest { get; init; }

    /// <summary>Datei, aus der sie gelesen wurde (null = feste Vorlage oder noch nicht gespeichert).</summary>
    public string? Datei { get; private set; }

    public static readonly Strassenzug Wohnstrasse = new() { Name = "Wohnstraße", Gehweg = true, Baeume = true, Fest = true };

    public static readonly Strassenzug Hauptstrasse = new() { Name = "Hauptstraße", Gehweg = true, Baeume = true, Zebrastreifen = true, Haltestelle = true, Fest = true };

    public static readonly Strassenzug Landstrasse = new() { Name = "Landstraße", Gehweg = false, Baeume = true, Fest = true };

    /// <summary>Die drei festen Vorlagen in dieser Reihenfolge.</summary>
    public static IReadOnlyList<Strassenzug> Feste { get; } = [Wohnstrasse, Hauptstrasse, Landstrasse];

    /// <summary>Hat die Vorlage etwas, das nur mit Gehweg geht, aber keinen Gehweg (Zebrastreifen, Haltestelle)?</summary>
    public bool OhneGehwegMitHalt => !Gehweg && (Zebrastreifen || Haltestelle);

    public static bool NameGueltig(string name) => Projekt.NameGueltig(name);

    /// <summary>Ein Name einer festen Vorlage (gleich bis auf Gross-/Kleinschreibung) ist fuer eigene Vorlagen gesperrt.</summary>
    public static bool NameFest(string name) => Feste.Any(f => string.Equals(f.Name, name, StringComparison.CurrentCultureIgnoreCase));

    public static string DateiName(string name) => Projekt.DateiName(name) + Endung;

    /// <summary>Abstand auf das Raster 0,5 und in die Grenzen.</summary>
    public static double AbstandBegrenzt(double abstand) =>
        Math.Clamp(Math.Round(abstand / BaumAbstandSchritt) * BaumAbstandSchritt, BaumAbstandMin, BaumAbstandMax);

    /// <summary>Kurzbeschreibung fuer die Liste („Gehweg, Bäume alle 3,5, Zebrastreifen, Haltestelle“).</summary>
    public string Beschreibung()
    {
        var teile = new List<string> { Gehweg ? Texte.ZugGehweg : Texte.ZugOhneGehweg };
        if (Baeume)
        {
            teile.Add(Texte.ZugBaeumeAlle(BaumAbstand));
        }
        if (Zebrastreifen)
        {
            teile.Add(Texte.Zebrastreifen);
        }
        if (Haltestelle)
        {
            teile.Add(Texte.Haltestelle);
        }
        return string.Join(", ", teile);
    }

    // ---- Datei --------------------------------------------------------------------------------------

    private PyObjekt OhnePruefsumme() => new()
    {
        { "format", Format },
        { "formatversion", FormatVersion },
        { "werkzeug", Werkzeug.Kennzeile },
        { "name", Name },
        { "gehweg", Gehweg },
        { "baeume", Baeume },
        { "baumabstand", Bearbeitung.Zahl(BaumAbstand) },
        { "zebrastreifen", Zebrastreifen },
        { "haltestelle", Haltestelle },
    };

    /// <summary>Inhalt der Datei: je Feld eine Zeile (json.dumps), Pruefsumme sha256 wie bei der Fassung.</summary>
    public byte[] Schreiben()
    {
        var objekt = OhnePruefsumme();
        objekt.Add("pruefsumme", StadtFassung.Pruefsumme(objekt));
        var text = new StringBuilder("{\n");
        for (var i = 0; i < objekt.Count; i++)
        {
            text.Append(' ').Append(PythonJson.Standard(objekt[i].Key)).Append(": ").Append(PythonJson.Standard(objekt[i].Value));
            text.Append(i + 1 < objekt.Count ? ",\n" : "\n");
        }
        return PythonJson.Utf8(text.Append("}\n").ToString());
    }

    /// <summary>
    /// Liest eine Vorlage. Kaputt (kein JSON, fehlendes oder unbekanntes Feld, falsche Pruefsumme, falsche Werte) oder aus
    /// einer neueren Werkzeug-Version: <see cref="StadtbauFehler"/> mit dem Grund in einfachen Worten.
    /// </summary>
    public static Strassenzug Lesen(ReadOnlySpan<byte> daten)
    {
        object? roh;
        try
        {
            roh = Py.Lesen(daten);
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or DecoderFallbackException)
        {
            throw new StadtbauFehler([Texte.ZugKaputt]);
        }
        if (roh is not PyObjekt d || !Py.Gleich(Get(d, "format"), Format))
        {
            throw new StadtbauFehler([Texte.ZugKeiner]);
        }
        if (!Py.IstNurInt(Get(d, "formatversion")))
        {
            throw new StadtbauFehler([Texte.ZugKaputt]);
        }
        if (Py.Int(d["formatversion"]) > FormatVersion)
        {
            throw new StadtbauFehler([Texte.ZugNeuer]);
        }
        if (Py.Int(d["formatversion"]) < 1)
        {
            throw new StadtbauFehler([Texte.ZugKaputt]);
        }
        var funde = new List<string>();
        funde.AddRange(Felder.Where(f => !Py.Hat(d, f)).Select(f => $"Feld „{f}“ fehlt."));
        funde.AddRange(d.Select(p => p.Key).Where(k => !Felder.Contains(k)).Select(k => $"Feld „{k}“ ist unbekannt."));
        if (funde.Count > 0)
        {
            throw new StadtbauFehler(funde);
        }
        var ohne = new PyObjekt();
        ohne.AddRange(d.Where(p => p.Key != "pruefsumme"));
        if (!Py.Gleich(d["pruefsumme"], StadtFassung.Pruefsumme(ohne)))
        {
            throw new StadtbauFehler([Texte.ZugPruefsumme]);
        }
        if (d["name"] is not string name || !NameGueltig(name) || NameFest(name)
            || d["gehweg"] is not bool gehweg || d["baeume"] is not bool baeume || d["zebrastreifen"] is not bool zebra || d["haltestelle"] is not bool halt
            || d["baumabstand"] is not (long or double) || Py.F(d["baumabstand"]) < BaumAbstandMin || Py.F(d["baumabstand"]) > BaumAbstandMax)
        {
            throw new StadtbauFehler([Texte.ZugKaputt]);
        }
        return new Strassenzug
        {
            Name = name,
            Gehweg = gehweg,
            Baeume = baeume,
            BaumAbstand = Py.F(d["baumabstand"]),
            Zebrastreifen = zebra,
            Haltestelle = halt,
        };
    }

    // ---- Ablage -------------------------------------------------------------------------------------

    /// <summary>Ein Eintrag der Liste: gelesene Vorlage oder der Grund, warum die Datei nicht lesbar ist.</summary>
    public sealed record Eintrag(string Datei, string Name, Strassenzug? Vorlage, string? Fehler, bool Neuer);

    /// <summary>
    /// Alle Dateien im Ordner <c>strassenzuege/</c>, nach Namen. Unlesbare stehen mit Grund dabei (nichts verschwindet still);
    /// ihr Name ist dann der Dateiname.
    /// </summary>
    public static List<Eintrag> Alle(Werkzeugordner ablage)
    {
        if (!Directory.Exists(ablage.Strassenzuege))
        {
            return [];
        }
        var liste = new List<Eintrag>();
        foreach (var f in new DirectoryInfo(ablage.Strassenzuege).GetFiles("*" + Endung))
        {
            try
            {
                var z = Lesen(LesenGeteilt(f.FullName));
                z.Datei = f.FullName;
                liste.Add(new Eintrag(f.FullName, z.Name, z, null, false));
            }
            catch (StadtbauFehler e)
            {
                liste.Add(new Eintrag(f.FullName, Path.GetFileNameWithoutExtension(f.Name), null, string.Join(" ", e.Funde), e.Funde.SequenceEqual([Texte.ZugNeuer])));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                liste.Add(new Eintrag(f.FullName, Path.GetFileNameWithoutExtension(f.Name), null, Texte.ZugNichtLesbar, false));
            }
        }
        return [.. liste.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(e => e.Datei, StringComparer.Ordinal)];
    }

    private static byte[] LesenGeteilt(string pfad)
    {
        using var s = new FileStream(pfad, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var m = new MemoryStream();
        s.CopyTo(m);
        return m.ToArray();
    }

    /// <summary>Sicherungen: <c>sicherungen/(Strassenzuege)/&lt;Name&gt;/</c>, die letzten 20 je Name.</summary>
    public static Sicherungen SicherungenFuer(Werkzeugordner ablage, string name) =>
        new(ablage, Path.Combine(SicherungOrdner, Projekt.DateiName(name)), Endung);

    /// <summary>
    /// Neu speichern. Ein Name, den es schon gibt (auch eine feste Vorlage), ist ein Fehler (<see cref="IOException"/>). Jede
    /// gespeicherte Fassung kommt zusaetzlich in die Sicherungen. Rueckgabe: Pfad.
    /// </summary>
    public string Speichern(Werkzeugordner ablage, DateTime zeit)
    {
        if (Fest || !NameGueltig(Name))
        {
            throw new ArgumentException("Name ungültig", nameof(ablage));
        }
        var pfad = Path.Combine(ablage.Strassenzuege, DateiName(Name));
        if (NameFest(Name) || File.Exists(pfad) || Alle(ablage).Any(e => string.Equals(e.Name, Name, StringComparison.CurrentCultureIgnoreCase)))
        {
            throw new IOException(Texte.ZugGibtEs);
        }
        var daten = Schreiben();
        Werkzeugordner.SicherSchreiben(pfad, daten);
        SicherungenFuer(ablage, Name).Anlegen(daten, zeit);
        Datei = pfad;
        return pfad;
    }

    /// <summary>Loeschen (nach der Rueckfrage in der Oberflaeche): die Datei kommt vorher in die Sicherungen, auch eine kaputte.</summary>
    public static void Loeschen(Werkzeugordner ablage, string datei, DateTime zeit)
    {
        var daten = File.ReadAllBytes(datei);
        string name;
        try
        {
            name = Lesen(daten).Name;
        }
        catch (StadtbauFehler)
        {
            name = Path.GetFileNameWithoutExtension(datei);
        }
        SicherungenFuer(ablage, name).Anlegen(daten, zeit);
        File.Delete(datei);
    }

    /// <summary>Gleiche Ausstattung (alles ausser Name, Datei und fest)?</summary>
    public bool GleicheAusstattung(Strassenzug z) =>
        Gehweg == z.Gehweg && Baeume == z.Baeume && Math.Abs(BaumAbstand - z.BaumAbstand) < 1e-9 && Zebrastreifen == z.Zebrastreifen && Haltestelle == z.Haltestelle;

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"Strassenzug {Name} ({Beschreibung()})");
}
