using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Welle A 0.88.0 (Inhaltspakete W2, Weisung P12-296, Plan P12-175 Abschnitte 3 und 5): Staedte aus Paketen und die Zugfahrt.
/// <list type="bullet">
/// <item>Eine Paket-Stadt ist eine eigene <see cref="Stadt"/> mit <see cref="Vorsatz"/> <c>kennung:</c> (W2-3). In ihren
/// Ereignissen stehen die Orte mit Vorsatz (<c>probe_testkarte:talheim</c>), Stadt 1 schreibt sie wie bisher ohne. Damit
/// ueberdeckt kein Paket Stadt 1 oder ein anderes Paket, auch wenn die Orte gleich heissen.</item>
/// <item>Alle Staedte rechnen den Stand gleich (<see cref="GlobalUndHerkunft"/>); jede Stadt zeigt ihn mit ihren eigenen
/// Kennungen (<see cref="Lokal"/>). Ist die Figur in einer anderen Stadt, ist der Ort hier <see cref="Fremd"/>: kein Schritt
/// (<see cref="StadtGrund.AndereStadt"/>).</item>
/// <item>Die Zugfahrt ist das neue Ereignis <see cref="Ereignisse.ZugGefahren"/> (Daten <c>tag</c>, <c>von</c>, <c>nach</c>,
/// <c>minuten</c>, <c>stadt</c> = Name der Zielstadt); nur von Bahnhof zu Bahnhof, kostenlos (kein Geld, kein Ruf, kein neuer
/// Wert im Spielstand), die Uhr laeuft wie bei jedem Weg (<see cref="Zugminuten"/>, ab <see cref="UhrSteht"/> gekappt).
/// Ein neuer Tag beginnt wie immer zu Hause in Stadt 1.</item>
/// </list>
/// </summary>
public sealed partial class Stadt
{
    /// <summary>Kennung des Bahnhofs in Stadt 1 und in der Probe-Stadt (das Werkzeug verknuepft den Bahnsteig mit diesem Ort).</summary>
    public const string Bahnhof = "talheim";

    /// <summary>Fahrzeit einer Zugfahrt (Minuten der Spieluhr).</summary>
    public const long Zugminuten = 20;

    /// <summary>Zeichen vor dem Ort einer anderen Stadt im Stand einer Paket-Stadt (nur Anzeige im Speicher, nie geschrieben).</summary>
    public const char FremdZeichen = '~';

    /// <summary>Vorsatz der Kennungen (<c>kennung:</c>), null = Stadt 1.</summary>
    public string? Vorsatz { get; private set; }

    /// <summary>Hat der Ort dieser Stadt einen Innenraum, den man betreten darf (der Bahnhof einer Paket-Stadt nicht)?</summary>
    public bool OhneInnenraum(string ort) => Vorsatz is not null && ort == Bahnhof;

    /// <summary>Kennung des Pakets (null = Stadt 1).</summary>
    public string? Kennung { get; private set; }

    /// <summary>Name der Stadt aus dem Paket (null = Stadt 1).</summary>
    public string? Name { get; private set; }

    /// <summary>Stadt 1 (bei einer Paket-Stadt), sonst null: dort beginnt jeder Tag.</summary>
    public Stadt? Haupt { get; private set; }

    /// <summary>Diese Stadt als Stadt aus einem Paket kennzeichnen (einmal, gleich nach dem Bau).</summary>
    public Stadt AlsPaket(string kennung, string name, Stadt haupt)
    {
        if (Vorsatz is not null || haupt.Vorsatz is not null || ReferenceEquals(haupt, this))
        {
            throw new InvalidOperationException("Paket-Stadt nur einmal und nur zu Stadt 1");
        }
        Kennung = kennung;
        Vorsatz = kennung + ":";
        // E1: Wohnung, Arbeit, Kunden und Firma gehoeren zum Spielstand in Stadt 1; eine Paket-Stadt zeigt ihre Geschaefte und
        // Schalter und ihren Bahnhof (nur draussen mit dem Fahrplan)
        Regeln = [.. Regeln.Where(r => Get(r, "id") as string == Bahnhof || NeueOrtRollen.Contains(Get(r, "rolle") as string ?? ""))];
        Name = name;
        Haupt = haupt;
        return this;
    }

    /// <summary>Kennung der Stadt eines Orts mit Vorsatz (<c>kennung:ort</c>), null = Stadt 1.</summary>
    public static string? StadtVon(string globalerOrt)
    {
        var i = globalerOrt.IndexOf(':', StringComparison.Ordinal);
        return i > 0 ? globalerOrt[..i] : null;
    }

    /// <summary>Zaehlt ein Ort (mit Vorsatz immer, ohne nur, wenn Stadt 1 ihn kennt).</summary>
    private bool Kennt(string globalerOrt) => globalerOrt.Contains(':', StringComparison.Ordinal) || (Haupt ?? this).Tuer(globalerOrt) is not null;

    /// <summary>Ort ueber alle Staedte zu einem Ort dieser Stadt (Stadt 1: gleich).</summary>
    public string Global(string ort) => Vorsatz is null ? ort : ort.Length > 0 && ort[0] == FremdZeichen ? ort[1..] : Vorsatz + ort;

    /// <summary>Ort dieser Stadt zu einem Ort ueber alle Staedte; ein Ort einer anderen Stadt bleibt <see cref="Fremd"/>.</summary>
    public string Lokal(string globalerOrt) => Vorsatz is null ? globalerOrt
        : globalerOrt.StartsWith(Vorsatz, StringComparison.Ordinal) ? globalerOrt[Vorsatz.Length..] : FremdZeichen + globalerOrt;

    /// <summary>Liegt der Ort (aus <see cref="Stand"/>) in einer anderen Stadt?</summary>
    public bool Fremd(string ort) => Vorsatz is null ? ort.Contains(':', StringComparison.Ordinal) : ort.Length > 0 && ort[0] == FremdZeichen;

    /// <summary>Ort der Figur ueber alle Staedte (mit Vorsatz, wenn sie in einer Paket-Stadt ist).</summary>
    public string OrtGlobal(Spielstand stand) => GlobalUndHerkunft(stand).Stand.Ort;

    /// <summary>Der Bahnhof einer Stadt ueber alle Staedte (null = Stadt 1).</summary>
    public static string BahnhofVon(string? kennung) => kennung is null ? Bahnhof : kennung + ":" + Bahnhof;

    /// <summary>
    /// Kann die Figur jetzt mit dem Zug zum Bahnhof <paramref name="zielBahnhof"/> (ueber alle Staedte) fahren? Nur vom Bahnhof
    /// dieser Stadt, nie zu ihm selbst. <paramref name="zielName"/> steht in den Daten (zweites Geraet ohne Paket nennt die Stadt).
    /// </summary>
    public StadtErgebnis ZugPruefen(Spielstand stand, string zielBahnhof, string zielName)
    {
        if (stand.Profil is null)
        {
            return new(StadtGrund.KeinProfil);
        }
        var herkunft = StandUndHerkunft(stand);
        var jetzt = herkunft.Stand;
        if (Fremd(jetzt.Ort))
        {
            return new(StadtGrund.AndereStadt);
        }
        if (jetzt.Ort != Bahnhof || Tuer(Bahnhof) is null)
        {
            return new(StadtGrund.NichtAmBahnhof);
        }
        if (zielBahnhof != BahnhofVon(StadtVon(zielBahnhof)))
        {
            return new(StadtGrund.Unbekannt);
        }
        if (zielBahnhof == Global(jetzt.Ort))
        {
            return new(StadtGrund.SchonDa);
        }
        return new(StadtGrund.Ok, Ereignisse.ZugGefahren, MitHerkunft(new PyObjekt
        {
            { "tag", jetzt.Tag }, { "von", Global(jetzt.Ort) }, { "nach", zielBahnhof }, { "minuten", Gekappt(jetzt.Uhr, Zugminuten) },
            { "stadt", zielName },
        }, herkunft));
    }

    /// <summary>Mit dem Zug fahren und das Ereignis schreiben (nur wenn es geht).</summary>
    public StadtErgebnis Zug(Spielablauf spiel, string zielBahnhof, string zielName) =>
        Ausfuehren(spiel, ZugPruefen(spiel.Stand, zielBahnhof, zielName));

    /// <summary>Name der Stadt, in der die Figur zuletzt mit dem Zug ankam (aus den Daten der Zugfahrt), sonst die Kennung.</summary>
    public static string StadtName(Spielstand stand, string kennung)
    {
        string? name = null;
        foreach (var e in stand.Verlauf)
        {
            if (e.Art == Ereignisse.ZugGefahren && Get(e.Daten, "nach") is string nach && StadtVon(nach) == kennung && Get(e.Daten, "stadt") is string n && n.Length > 0)
            {
                name = n;
            }
        }
        return name ?? kennung;
    }
}
