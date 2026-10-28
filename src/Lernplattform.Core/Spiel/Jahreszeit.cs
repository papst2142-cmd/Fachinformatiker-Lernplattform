namespace Lernplattform.Core.Spiel;

/// <summary>Jahreszeit nach dem Kalenderdatum (meteorologisch), nur Darstellung.</summary>
public enum Jahreszeit { Fruehling, Sommer, Herbst, Winter }

/// <summary>
/// Phase 8 W8 (Weisung P8-24, Nico): Jahreszeit fuer den Stadt-Grundklang, spaeter auch fuer die Faerbung (W9).
/// Grenzen meteorologisch: Fruehling 1.3.-31.5., Sommer 1.6.-31.8., Herbst 1.9.-30.11., Winter 1.12.-28./29.2.
/// Keine Spielregel und kein Ereignis; das Datum kommt nur ueber <see cref="Festzeit.Datum"/> (Schalter
/// <c>LERNPLATTFORM_DATUM</c> wirkt).
/// </summary>
public static class Jahreszeiten
{
    public static Jahreszeit Fuer(DateOnly tag) => tag.Month switch
    {
        3 or 4 or 5 => Jahreszeit.Fruehling,
        6 or 7 or 8 => Jahreszeit.Sommer,
        9 or 10 or 11 => Jahreszeit.Herbst,
        _ => Jahreszeit.Winter,
    };

    private static Jahreszeit? _probe;

    /// <summary>Start-Test: die Jahreszeit fest setzen bis <see cref="ProbeEnde"/>. Nur der Pruefstand ruft das.</summary>
    public static void Probe(Jahreszeit jahreszeit) => _probe = jahreszeit;

    /// <summary>Start-Test: wieder nach dem Datum.</summary>
    public static void ProbeEnde() => _probe = null;

    /// <summary>Jahreszeit jetzt: Probe (Start-Test), sonst nach <see cref="Festzeit.Datum"/>.</summary>
    public static Jahreszeit Heute() =>
        _probe ?? Fuer(Festzeit.Datum());
}
