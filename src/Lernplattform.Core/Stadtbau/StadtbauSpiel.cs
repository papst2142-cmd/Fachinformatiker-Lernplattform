using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Weisung P12-179 Schritt 2: die Teile des Stadtbaus, die den Spiel-Kern kennen (Stadt, Spielablauf, Spiel in der Stadt).
/// Sie gehoeren zur Spielanbindung in Lernplattform.Core, nicht zur eigenen Engine; vorher standen sie in
/// <see cref="StadtFassung"/> (StadtFuer), <see cref="StadtbauPruefung"/> (Probestand) und <see cref="StadtbauGrenzen"/>
/// (Bewegt), unveraendert.
/// </summary>
public static class StadtbauSpiel
{
    /// <summary>Die Stadt dieser Fassung zu Spielinhalten, deren Abschnitt "orte" schon die <see cref="StadtFassung.Karte"/> ist.</summary>
    public static Stadt StadtFuer(this StadtFassung fassung, PyObjekt inhalt) => new(inhalt, fassung.Stadtdaten) { Fassung = fassung };

    /// <summary>
    /// Spielstand fuer Pruefung, 3D-Ansicht und Probe: ein neuer Stand ohne Speicher, in dem alle Orte zu sehen sind
    /// (200 fertige Tage, eigene Firma auf der hoechsten Stufe, Zweigstelle, Serverfarm frei), wie die Tests des Spiels.
    /// Nur Werkzeug und Tests rufen ihn; das Spiel nutzt immer den echten Spielstand.
    /// </summary>
    /// <remarks>
    /// Sammelliste 238 (Weisungen P12-81/P12-83): bis S 0.18.0 fehlte die Serverfarm, darum fehlte das Rechenzentrum
    /// Lokschuppen in der 3D-Ansicht des Werkzeugs und die Pruefung draussen pruefte es nicht (falsches Gruen).
    /// </remarks>
    public static Spielablauf Probestand(PyObjekt inhalt)
    {
        var ablauf = new Spielablauf(inhalt);
        ablauf.Profil("Probe", new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } }, Ereignisse.Normal);
        ablauf.Stand.TageFertig = 200;
        ablauf.Stand.Firma = new PyObjekt { { "stufe", 5L }, { "name", "Probe" }, { "tag", 1L } };
        ablauf.Stand.Zweigstelle = new PyObjekt { { "stufe", 1L }, { "tag", 1L } };
        if (!ProbestandOhneFarmFuerTests)
        {
            ablauf.Stand.FarmFrei = 1L;
        }
        return ablauf;
    }

    /// <summary>Gegenprobe Sammelliste 238: Probestand wie bis S 0.18.0 (ohne Serverfarm).</summary>
    public static bool ProbestandOhneFarmFuerTests
    {
        get => OhneFarm.Value;
        set => OhneFarm.Value = value;
    }

    private static readonly AsyncLocal<bool> OhneFarm = new();

    /// <summary>
    /// Was sich draussen bewegt und jedes Bild gezeichnet wird: Autos bzw. Festgestalten (ein Aufruf, alle Netze), die
    /// Figur (zweimal: im Bild und ganz vorn), der Kreis um sie, die Zielmarke (wenn ein Ziel da ist) und die Pakete vor
    /// der Tuer (wenn welche da sind). Ohne Grafik zeichnet das Spiel keine Autos.
    /// </summary>
    public static (long Dreiecke, int Aufrufe) Bewegt(StadtSpiel spiel, bool fern, int autosHoechstens = 16, long? uhr = null, bool halb = false)
    {
        long dreiecke = 0;
        var aufrufe = 0;
        var szene = spiel.Szene;
        if (spiel.Grafik is { } grafik)
        {
            var satz = FestVerkehr.Fuer(szene.Fest, uhr ?? Tageslicht.FesteUhr ?? spiel.Jetzt.Uhr, szene.Jahreszeit);
            var gestalten = satz == FestVerkehr.Satz.Autos ? null : FestVerkehr.Gestalten(grafik, satz);
            var verkehr = FestVerkehr.VerkehrFuer(szene, satz);
            dreiecke += FestVerkehr.FahrzeugNetze(verkehr, gestalten, grafik, fern, autosHoechstens, halb).Sum(n => (long)n.Indizes.Count / 3);
            aufrufe++;
        }
        var aussehen = spiel.Ablauf.Stand.Profil is { } profil ? SpielDaten.Get(profil, "aussehen") as PyObjekt : null;
        var bild = spiel.SchrittBild;
        var figur = bild >= 0 ? Figur.Netz(spiel.Grafik, aussehen, 1f, spiel.Kleidung, spiel.Bewegung, bild)
            : spiel.Kleidung is { Count: > 0 } k ? Figur.Netz(spiel.Grafik, aussehen, 1f, k) : Figur.Netz(spiel.Grafik, aussehen, 1f);
        dreiecke += 2L * (figur.Indizes.Count / 3);
        aufrufe += 2;
        dreiecke += Figur.Ring(aussehen, 0.22f, 0.06f).Indizes.Count / 3;
        aufrufe++;
        if (spiel.Ziel is not null)
        {
            dreiecke += ProbeWelt.Zielmarke().Indizes.Count / 3;
            aufrufe++;
        }
        var pakete = spiel.PaketeVorDerTuer;
        if (pakete > 0 && Lieferpaket.Lage(szene, spiel.Stadt) is not null)
        {
            dreiecke += Lieferpaket.Bauen(spiel.Grafik, pakete).Indizes.Count / 3;
            aufrufe++;
        }
        return (dreiecke, aufrufe);
    }
}
