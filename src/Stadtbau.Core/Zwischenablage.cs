using System.Numerics;
using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.47.0 W1 (Weisung P12-226, Liste P12-119 Punkt W1): Zwischenablage ueber Projekte hinweg. Sie haelt Stuecke der
/// Stadt draussen (ein Stueck oder eine Mehrfachauswahl) als Daten mit den Lagen des Quellprojekts; Haeuser (Orte und
/// Gebaeude ohne Ort) liegen darin als Blueprint mit Innenraum und Etagen. Sie liegt als Datei im Werkzeugordner
/// (<see cref="Dateiname"/>), damit sie nach dem Wechsel in ein anderes Projekt (auch nach einem Neustart) noch da ist.
/// Das Einfuegen macht <see cref="AussenBearbeitung.Einfuegen"/> (ein Schritt).
/// </summary>
public sealed class Zwischenablage
{
    public const string Format = "fiszwischenablage";
    public const int FormatVersion = 1;
    public const string Dateiname = "zwischenablage.json";

    /// <summary>Name des Projekts, aus dem die Stuecke kommen.</summary>
    public required string Projekt { get; init; }

    /// <summary>Mitte aller Stuecke im Quellprojekt (um sie liegt die Vorschau beim Einfuegen).</summary>
    public required Vector2 Mitte { get; init; }

    /// <summary>
    /// Die Stuecke: je {art, daten} (Rohdaten wie in der Stadt) bzw. fuer Haeuser {art: "haus", blueprint (Text der Datei),
    /// ort (Kennung oder ""), winkel (Grad im Uhrzeigersinn), mitte [x, y]}.
    /// </summary>
    public required List<object?> Stuecke { get; init; }

    /// <summary>Umrisse der Stuecke (Vielecke, relativ zur <see cref="Mitte"/>) fuer die Vorschau.</summary>
    public required List<List<Vector2>> Umrisse { get; init; }

    public int Anzahl => Stuecke.Count;

    public byte[] Schreiben()
    {
        var o = new PyObjekt
        {
            { "format", Format },
            { "formatversion", (long)FormatVersion },
            { "werkzeug", Werkzeug.Kennzeile },
            { "projekt", Projekt },
            { "mitte", new List<object?> { (double)Mitte.X, (double)Mitte.Y } },
            { "stuecke", Stuecke },
            { "umrisse", Umrisse.Select(u => (object?)u.Select(p => (object?)new List<object?> { Math.Round(p.X, 4), Math.Round(p.Y, 4) }).ToList()).ToList() },
        };
        return PythonJson.Utf8(PythonJson.Standard(o) + "\n");
    }

    /// <summary>Liest die Zwischenablage; kaputt, fremd oder neuer: null (dann ist sie fuer dieses Werkzeug leer).</summary>
    public static Zwischenablage? Lesen(byte[] daten)
    {
        try
        {
            if (Py.Lesen(daten) is not PyObjekt o || !Py.Gleich(Get(o, "format"), Format) || Get(o, "formatversion") is not long v || v != FormatVersion
                || Get(o, "projekt") is not string projekt || Get(o, "mitte") is not List<object?> { Count: 2 } m
                || Get(o, "stuecke") is not List<object?> stuecke || Get(o, "umrisse") is not List<object?> umrisse)
            {
                return null;
            }
            return new Zwischenablage
            {
                Projekt = projekt,
                Mitte = new Vector2((float)Py.F(m[0]), (float)Py.F(m[1])),
                Stuecke = stuecke,
                Umrisse = [.. umrisse.OfType<List<object?>>().Select(u => u.OfType<List<object?>>().Select(p => new Vector2((float)Py.F(p[0]), (float)Py.F(p[1]))).ToList())],
            };
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>In den Werkzeugordner legen (ersetzt die vorige Zwischenablage).</summary>
    public void Ablegen(Werkzeugordner ablage) => Werkzeugordner.SicherSchreiben(Path.Combine(ablage.Anlegen().Wurzel, Dateiname), Schreiben());

    /// <summary>Die Zwischenablage aus dem Werkzeugordner (null = leer, kaputt oder fremd).</summary>
    public static Zwischenablage? Laden(Werkzeugordner ablage)
    {
        var datei = Path.Combine(ablage.Wurzel, Dateiname);
        try
        {
            return File.Exists(datei) ? Lesen(File.ReadAllBytes(datei)) : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Text eines Blueprints fuer die Zwischenablage.</summary>
    internal static string BlueprintText(Blueprint b) => Encoding.UTF8.GetString(b.Schreiben());
}
