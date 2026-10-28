namespace Lernplattform.Core.Spiel.Ton;

/// <summary>Die Lautstaerke-Regler der Karte "Musik und Geräusche" (Phase 8 W9).</summary>
public enum Tonregler { Gesamt, Musik, Umgebung }

/// <summary>Ein Ton eines Beispiels: Schluessel im Tonplan (oder <see cref="Hoerprobe.EigenesLied"/>), Gruppe, Grundlautstaerke.</summary>
public sealed record Probenton(string Schluessel, Tongruppe Gruppe, float Laut, bool Schleife);

/// <summary>
/// Phase 8 W9 (Weisungen P8-29 und P8-32, Nico 4b): Hoerprobe an jedem Ton-Regler. Beim Verstellen klingt ein
/// passendes Beispiel in der eingestellten Lautstaerke; hoechstens eins zugleich, nach der letzten Aenderung
/// <see cref="Nachlauf"/> und <see cref="Ausklang"/>, auf 0 % still (das regelt der Mischer). Nur vorhandene Toene.
/// </summary>
public static class Hoerprobe
{
    /// <summary>Einblenden beim Start und Ausblenden beim Wechsel zu einem anderen Regler (Sekunden).</summary>
    public const double Einblenden = 0.15;

    /// <summary>Nach der letzten Aenderung klingt das Beispiel so lange weiter (Sekunden).</summary>
    public const double Nachlauf = 1.5;

    /// <summary>Danach blendet es so lange aus (Sekunden).</summary>
    public const double Ausklang = 0.5;

    /// <summary>Ausserhalb des Spiels bleibt das Geraet nach dem Ausklang so lange offen (Sekunden).</summary>
    public const double GeraetNach = 3.0;

    /// <summary>Pause zwischen den beiden Melodien des Musik-Beispiels (Sekunden).</summary>
    public const double MelodiePause = 0.3;

    /// <summary>Schluessel des Musik-Beispiels aus den Melodien STEEL01 (Erfolg) und STEEL07 (Feierabend) im Wechsel.</summary>
    public const string Melodien = "hoerprobe_melodien";

    /// <summary>Platzhalter: das erste lesbare eigene Lied (Nico 4b), sonst <see cref="Melodien"/>.</summary>
    public const string EigenesLied = "eigenes_lied";

    /// <summary>Gezaehlter Anlass je Regler (auch im stillen Dienst).</summary>
    public static string Schluessel(Tonregler regler) => "hoerprobe:" + regler switch
    {
        Tonregler.Gesamt => "gesamt",
        Tonregler.Musik => "musik",
        _ => "umgebung",
    };

    /// <summary>Regler zum Schluessel in einstellungen.json (ton_gesamt, ton_musik, ton_umgebung); sonst null.</summary>
    public static Tonregler? Fuer(string schluessel) => schluessel switch
    {
        TonEinstellungen.Gesamt => Tonregler.Gesamt,
        TonEinstellungen.Musik => Tonregler.Musik,
        TonEinstellungen.Umgebung => Tonregler.Umgebung,
        _ => null,
    };

    /// <summary>
    /// Das Beispiel je Regler: Gesamt = Stadtklang der Jahreszeit und Melodien (beide Gruppen), Musik = eigenes Lied
    /// oder Melodien, Umgebung = Stadtklang der Jahreszeit, im Fruehling und Sommer mit einem Vogelruf zu Beginn.
    /// </summary>
    public static IReadOnlyList<Probenton> BeispielFuer(Tonregler regler, Jahreszeit jahreszeit) => regler switch
    {
        Tonregler.Gesamt =>
        [
            new(Tonplan.StadtGrundFuer(jahreszeit), Tongruppe.Umgebung, Tonplan.StadtGrund, true),
            new(Melodien, Tongruppe.Musik, Tonplan.Melodie, true),
        ],
        Tonregler.Musik => [new(EigenesLied, Tongruppe.Musik, Tonplan.LiedInnen, false)],
        _ => Tonplan.RufeFuer(jahreszeit) is { } rufe && jahreszeit is Jahreszeit.Fruehling or Jahreszeit.Sommer
            ?
            [
                new(Tonplan.StadtGrundFuer(jahreszeit), Tongruppe.Umgebung, Tonplan.StadtGrund, true),
                new("vogelruf", Tongruppe.Umgebung, rufe.Laut, false),
            ]
            : [new(Tonplan.StadtGrundFuer(jahreszeit), Tongruppe.Umgebung, Tonplan.StadtGrund, true)],
    };

    /// <summary>Musik-Beispiel: erste Melodie, Pause, zweite Melodie, Pause (als Schleife spielbar).</summary>
    public static TonDaten Kette(TonDaten erste, TonDaten zweite)
    {
        var pause = (int)Math.Round(MelodiePause * Mischer.Rate);
        var a = Angepasst(erste);
        var b = Angepasst(zweite);
        var proben = new float[a.Length + pause + b.Length + pause];
        a.CopyTo(proben, 0);
        b.CopyTo(proben, a.Length + pause);
        return new TonDaten(proben, Mischer.Rate);
    }

    /// <summary>Auf die Rate des Mischers umgerechnet (linear), damit beide Melodien gleich klingen.</summary>
    private static float[] Angepasst(TonDaten daten)
    {
        if (daten.Rate == Mischer.Rate)
        {
            return daten.Proben;
        }
        var laenge = (int)Math.Round(daten.Proben.Length * (double)Mischer.Rate / daten.Rate);
        var neu = new float[laenge];
        for (var i = 0; i < laenge; i++)
        {
            var lage = i * (double)daten.Rate / Mischer.Rate;
            var j = Math.Min((int)lage, daten.Proben.Length - 1);
            var k = Math.Min(j + 1, daten.Proben.Length - 1);
            neu[i] = daten.Proben[j] + (float)((daten.Proben[k] - daten.Proben[j]) * (lage - j));
        }
        return neu;
    }
}

/// <summary>Was nach einer Aenderung am Regler zu tun ist.</summary>
public enum Probenwechsel
{
    /// <summary>Das laufende Beispiel klingt weiter (gleicher Regler).</summary>
    Weiter,

    /// <summary>Kein Beispiel lief: eins starten.</summary>
    Neu,

    /// <summary>Ein anderer Regler: das laufende ausblenden, das neue starten (nie zwei zugleich).</summary>
    Wechsel,
}

/// <summary>
/// Zustand der Hoerprobe mit eigener Uhr (Sekunden): welcher Regler gerade klingt, wie laut das Beispiel gerade ist
/// (Einblenden, Nachlauf, Ausklang) und wann das Geraet ausserhalb des Spiels wieder schliessen darf.
/// </summary>
public sealed class Probenlauf
{
    private double _beginn;
    private double _letzte;

    /// <summary>Der Regler des laufenden Beispiels (null = keins).</summary>
    public Tonregler? Regler { get; private set; }

    /// <summary>Wie viele Beispiele gestartet wurden.</summary>
    public int Starts { get; private set; }

    /// <summary>Der Regler wurde verstellt.</summary>
    public Probenwechsel Aenderung(Tonregler regler, double jetzt)
    {
        var alt = Regler;
        var weiter = alt == regler && !Ausgeklungen(jetzt);
        _letzte = jetzt;
        if (weiter)
        {
            return Probenwechsel.Weiter;
        }
        Regler = regler;
        _beginn = jetzt;
        Starts++;
        return alt is null || alt == regler ? Probenwechsel.Neu : Probenwechsel.Wechsel;
    }

    /// <summary>Lautstaerke des Beispiels 0..1: Einblenden, voll waehrend der Aenderungen und im Nachlauf, dann Ausklang.</summary>
    public double Huelle(double jetzt)
    {
        if (Regler is null)
        {
            return 0;
        }
        var ein = Math.Clamp((jetzt - _beginn) / Hoerprobe.Einblenden, 0, 1);
        var nach = jetzt - _letzte - Hoerprobe.Nachlauf;
        var aus = nach <= 0 ? 1 : Math.Clamp(1 - (nach / Hoerprobe.Ausklang), 0, 1);
        return ein * aus;
    }

    /// <summary>Ist das Beispiel ganz ausgeklungen?</summary>
    public bool Ausgeklungen(double jetzt) => Regler is null || jetzt - _letzte >= Hoerprobe.Nachlauf + Hoerprobe.Ausklang;

    /// <summary>Darf das Geraet (ausserhalb des Spiels) wieder schliessen?</summary>
    public bool GeraetFrei(double jetzt) => Regler is null || jetzt - _letzte >= Hoerprobe.Nachlauf + Hoerprobe.Ausklang + Hoerprobe.GeraetNach;

    /// <summary>Beispiel ganz beendet (nach dem Ausklang, Hintergrund, Spiel betreten).</summary>
    public void Ende() => Regler = null;
}
