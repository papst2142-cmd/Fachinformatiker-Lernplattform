namespace Lernplattform.Core.Design;

/// <summary>
/// Phase 2, L12 (Sammelliste 14, Weisung P2-14): farbige Spur und Knopf der Regler unter
/// "Eigene Farben" wie in der Python-Linie ab 0.58 (fisi_theme.track_color, track_colors,
/// track_fraction, knob_colors, knob_contrast, _safe_triple; Vergleichswerte G23).
/// Die Spur eines Reglers zeigt an jeder Stelle die Farbe, die entstuende, wenn dieser
/// Kanal dort stuende und die zwei anderen bleiben. Rechnet nur, nutzt
/// <see cref="EigeneFarben.HslZuHex"/> und <see cref="Farbwelt.Kontrast"/> lesend.
/// </summary>
public static class Reglerspur
{
    /// <summary>Python: KNOB_LIGHT.</summary>
    public const string KnopfHell = "#FFFFFF";

    /// <summary>Python: KNOB_DARK (Schriftfarbe der hellen Darstellung).</summary>
    public const string KnopfDunkel = "#1B1628";

    /// <summary>Python: KNOB_MIN_CONTRAST (Ziel aus Plan 3a der Python-Linie).</summary>
    public const double KnopfMindestkontrast = 3.0;

    private static readonly double[] Ersatz = [0, 0, 50];

    private static ReglerKanal Kanal(string id) =>
        EigeneFarben.Kanaele.FirstOrDefault(k => k.Id == id) ?? throw new KeyNotFoundException(id);

    private static int Index(string id) => id switch
    {
        "h" => 0,
        "s" => 1,
        "l" => 2,
        _ => throw new KeyNotFoundException(id),
    };

    /// <summary>Python: _safe_triple - Unbrauchbares (null, NaN, fehlt) wird (0, 0, 50), sonst auf die Grenzen gesetzt.</summary>
    public static double[] Sicher(IReadOnlyList<double?> werte)
    {
        var ergebnis = new double[3];
        for (var i = 0; i < 3; i++)
        {
            var kanal = EigeneFarben.Kanaele[i];
            var zahl = i < werte.Count && werte[i] is { } w && !double.IsNaN(w) ? w : Ersatz[i];
            ergebnis[i] = Math.Max(kanal.Min, Math.Min(kanal.Max, zahl));
        }
        return ergebnis;
    }

    /// <summary>Reglerwerte einer Gruppe als Eingabe fuer die Spur.</summary>
    public static double?[] Werte(Regler regler) => [regler.H, regler.S, regler.L];

    /// <summary>Python: track_color - Spurfarbe des Kanals an der Stelle (0 = links, 1 = rechts).</summary>
    public static string Farbe(IReadOnlyList<double?> werte, string kanal, double stelle)
    {
        var drei = Sicher(werte);
        var k = Kanal(kanal);
        stelle = Math.Max(0.0, Math.Min(1.0, stelle));
        drei[Index(kanal)] = k.Min + (k.Max - k.Min) * stelle;
        return EigeneFarben.HslZuHex(drei[0], drei[1], drei[2]);
    }

    /// <summary>Python: track_colors - anzahl gleich verteilte Spurfarben von links nach rechts.</summary>
    public static IReadOnlyList<string> Farben(IReadOnlyList<double?> werte, string kanal, int anzahl)
    {
        anzahl = Math.Max(2, anzahl);
        var farben = new string[anzahl];
        for (var i = 0; i < anzahl; i++)
        {
            farben[i] = Farbe(werte, kanal, i / (double)(anzahl - 1));
        }
        return farben;
    }

    /// <summary>
    /// Stuetzfarben fuer den Verlauf (Weisung P2-14 Frage 4 a): eine je ganzzahligem Wert
    /// (Farbton 360, Saettigung und Helligkeit je 101), an der Stelle (Wert - Min) / (Max - Min)
    /// wie Python sie fuer diesen Wert rechnet.
    /// </summary>
    public static IReadOnlyList<string> Stuetzfarben(IReadOnlyList<double?> werte, string kanal)
    {
        var k = Kanal(kanal);
        var farben = new string[k.Max - k.Min + 1];
        for (var wert = k.Min; wert <= k.Max; wert++)
        {
            farben[wert - k.Min] = Farbe(werte, kanal, (wert - k.Min) / (double)(k.Max - k.Min));
        }
        return farben;
    }

    /// <summary>Python: track_fraction - Stelle des Knopfes (0..1) beim aktuellen Wert.</summary>
    public static double Stelle(IReadOnlyList<double?> werte, string kanal)
    {
        var k = Kanal(kanal);
        return (Sicher(werte)[Index(kanal)] - k.Min) / (k.Max - k.Min);
    }

    /// <summary>
    /// Python: knob_colors - Fuellung ist von Hell und Dunkel die mit dem hoeheren Kontrast zur
    /// Spur an der Stelle des Knopfes (Gleichstand: Hell), der Rand die andere.
    /// </summary>
    public static (string Fuellung, string Rand) Knopf(IReadOnlyList<double?> werte, string kanal)
    {
        var unter = Farbe(werte, kanal, Stelle(werte, kanal));
        return Farbwelt.Kontrast(KnopfHell, unter) >= Farbwelt.Kontrast(KnopfDunkel, unter)
            ? (KnopfHell, KnopfDunkel)
            : (KnopfDunkel, KnopfHell);
    }

    /// <summary>Python: knob_contrast - Kontrast der Knopffuellung zur Spur an seiner Stelle.</summary>
    public static double KnopfKontrast(IReadOnlyList<double?> werte, string kanal) =>
        Farbwelt.Kontrast(Knopf(werte, kanal).Fuellung, Farbe(werte, kanal, Stelle(werte, kanal)));

    /// <summary>
    /// Python: _nudge (Tastatur am PC, Bedienhilfe am Handy) - Farbton laeuft im Kreis
    /// (359 + 1 = 0, 0 - 1 = 359), Saettigung und Helligkeit bleiben am Rand stehen.
    /// </summary>
    public static int Schritt(ReglerKanal kanal, int wert, int schritt)
    {
        var neu = wert + schritt;
        if (kanal.Id == "h")
        {
            neu = ((neu % 360) + 360) % 360;
        }
        return Math.Max(kanal.Min, Math.Min(kanal.Max, neu));
    }
}
