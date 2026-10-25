using Avalonia.Controls;
using Avalonia.Layout;
using Lernplattform.Design;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Views;
using static Lernplattform.Core.Spiel.SpielDaten;
using static Lernplattform.Spiel.Tafel.Bau;

namespace Lernplattform.Spiel.Tafel;

/// <summary>Platzwahl und Spielfigur wie GameScreen._slot_picker und _profile_editor (mobile/src/spiel.py).</summary>
public sealed partial class SpielTafel
{
    // -- Spielstand-Plaetze --------------------------------------------------

    private IEnumerable<Control?> Platzwahl()
    {
        yield return T(Spielstaende.Titel, 24, "text", true);
        yield return T(Spielstaende.Hilfe, 13, "text_dim");
        foreach (var platz in _s.Verwaltung.Plaetze())
        {
            yield return PlatzKarte(platz);
        }
    }

    private Control PlatzKarte(PyObjekt item)
    {
        var aktiv = Py.Wahr(item["aktiv"]);
        var extra = Py.Wahr(item["extra"]);
        var leer = Py.Wahr(Get(item, "leer"));
        var lauf = item["lauf"] as string;
        var inhalt = new List<Control?>();
        if (extra)
        {
            inhalt.Add(T(Spielstaende.ExtraHilfe, 12, "text_dim"));
        }
        if (leer)
        {
            inhalt.Add(Stapel(4, [T(Spielstaende.Leer, 18, "text_dim", true),
                T("Spielfigur und Schwierigkeitsgrad wählst du beim Start.", 12, "muted")]));
        }
        else
        {
            inhalt.Add(Zeile(Stapel(2, [
                T(Py.Str(item["spieler"]), 18, "text", true),
                T(PyFormat.Prozent("%s · Tag %d", item["schwierigkeit"], item["tag"]), 13, "accent", true),
                T(Py.Wahr(item["firma"]) ? "Firma: " + Py.Str(item["firma"]) : "noch angestellt", 12, "text_dim"),
            ]), null, 12, new Avatar(Get(item, "aussehen") as PyObjekt, 64)));
        }
        var knoepfe = new List<Control?>();
        if (leer && lauf is null)
        {
            var platz = Py.Int(item["platz"]);
            knoepfe.Add(Knopf("Starten", () => NeuerPlatz(platz), hoehe: 38));
        }
        else
        {
            knoepfe.Add(Knopf(leer ? "Starten" : "Spielen", () => PlatzSpielen(lauf!), hoehe: 38));
            if (!leer || extra || lauf != Spielstaende.AlterLauf)
            {
                knoepfe.Add(Knopf("Löschen", () => _ = PlatzLoeschenAsync(item), "ghost", 38));
            }
        }
        if (!extra)
        {
            knoepfe.Add(Knopf("Umbenennen", () => _ = PlatzUmbenennenAsync(item), "ghost", 38));
        }
        inhalt.Add(Reihe(knoepfe));
        var karte = Karte(Py.Str(item["name"]), inhalt, aktiv ? "accent" : "accent2", aktiv ? "zuletzt gespielt" : null);
        if (aktiv)
        {
            karte.BorderThickness = new Avalonia.Thickness(2);
            karte.Farbe(Border.BorderBrushProperty, "Pinsel.accent");
        }
        return karte;
    }

    private void PlatzSpielen(string lauf)
    {
        if (Verwalten(() => _s.Verwaltung.LaufWaehlen(lauf)))
        {
            PlatzBetreten();
        }
        else
        {
            Zeichnen();
        }
    }

    private void NeuerPlatz(long platz)
    {
        if (Verwalten(() => _s.Verwaltung.NeuerLauf(platz)))
        {
            PlatzBetreten();
        }
        else
        {
            Zeichnen();
        }
    }

    /// <summary>_enter_slot</summary>
    private void PlatzBetreten()
    {
        _s.PlatzGewaehlt = true;
        _waehlen = false;
        _stapel.Clear();
        _entwurf.Clear();
        Raum = null;
        Verwalten(() => _s.Verwaltung.Ablauf?.WissenPruefen());
        _s.Abgleichen();
        Geaendert?.Invoke();
        Zeichnen();
        Freischaltungen();
    }

    private async Task PlatzLoeschenAsync(PyObjekt item)
    {
        if (await Dialog.Zeigen("Spielstand löschen", SpielAnsicht.LoeschFrage(item), "Abbrechen", "Ja") != 1)
        {
            return;
        }
        Verwalten(() => _s.Verwaltung.LaufLoeschen(Py.Str(item["lauf"])));
        _s.Verwaltung.Ablauf?.Freigeschaltet.Clear();
        Fortschritt();
    }

    private async Task PlatzUmbenennenAsync(PyObjekt item)
    {
        var platz = Py.Int(item["platz"]);
        var standard = Spielstaende.Standardname(platz);
        var name = Py.Str(item["name"]);
        var feld = Eingabe(name != standard ? name : "", standard, laenge: Spielstaende.NameMax);
        var inhalt = Stapel(10, [T(PyFormat.Prozent("Neuer Name für Platz %d (leer lassen = „%s“):", platz, standard), 14, "text_dim"), feld]);
        if (await Dialog.Zeigen("Platz umbenennen", inhalt, "Abbrechen", "Speichern") == 1)
        {
            Verwalten(() => _s.Verwaltung.PlatzUmbenennen(platz, feld.Text));
            Zeichnen();
        }
    }

    // -- Spielfigur ----------------------------------------------------------

    private IEnumerable<Control?> FigurBearbeiten(Spielstand stand)
    {
        var profil = stand.Profil ?? new PyObjekt { { "name", "" }, { "aussehen", Aussehen.Voreinstellung() } };
        var aussehen = Aussehen.Normalisieren(Get(_entwurf, "aussehen") ?? profil["aussehen"]);
        Py.Setzen(_entwurf, "aussehen", aussehen);
        var vorschau = new ContentControl { Content = new Avatar(aussehen, 120), HorizontalAlignment = HorizontalAlignment.Center };
        var name = Eingabe(Get(_entwurf, "name") as string ?? Py.Str(profil["name"]), "Wie heißt deine Figur?",
            text => Py.Setzen(_entwurf, "name", text));
        var teile = new List<Control?> { vorschau, Label("Name"), name };
        foreach (var (teil, beschriftung) in SpielBilder.AussehenNamen)
        {
            var wahl = Aussehen.Teile.First(t => t.Teil == teil).Wahl;
            teile.Add(Label(beschriftung));
            teile.Add(Pillen(wahl.Select(w => (w.Kennung, w.Name)), Py.Str(aussehen[teil]), wert =>
            {
                Py.Setzen(aussehen, teil, wert);
                vorschau.Content = new Avatar(aussehen, 120);
            }));
        }
        var knoepfe = new List<Control?>
        {
            Knopf(stand.Profil is null ? "Los geht's" : "Speichern", () => FigurSpeichern(name.Text ?? "", aussehen)),
        };
        if (stand.Profil is not null)
        {
            knoepfe.Add(Knopf("Abbrechen", FigurAbbrechen, "ghost"));
        }
        else if (_s.Verwaltung.AktivAnzeige().Length > 0)
        {
            // Neuer Durchgang: zurueck zur Auswahl, ohne eine Figur anzulegen
            knoepfe.Add(Knopf("Platz wechseln", PlatzWechseln, "ghost"));
        }
        if (stand.Profil is null)
        {
            var gebaeude = Objekt(I["gebaeude"]);
            yield return Karte("Willkommen im Spiel", [
                T(Py.Str(Objekt(I["story"])["intro"]), 14, "text_soft"),
                T(PyFormat.Prozent("%s · Kunde: %s", gebaeude["firma"], gebaeude["kunde"]), 11, "muted"),
            ], "accent2");
        }
        yield return Karte("Deine Spielfigur", teile, "accent");
        if (stand.Profil is null && stand.Schwierigkeitsgrad is null)
        {
            yield return SchwierigkeitKarte();
        }
        yield return Reihe(knoepfe);
    }

    /// <summary>_difficulty_card: Schwierigkeitsgrad beim Spielstart (fest fuer den Durchgang).</summary>
    private Control SchwierigkeitKarte()
    {
        var stufen = Spielstaende.SchwierigkeitStufen(I);
        var texte = stufen.ToDictionary(s => Py.Str(s.Id), s => Py.Str(s.Text));
        if (!Py.Hat(_entwurf, "schwierigkeit"))
        {
            Py.Setzen(_entwurf, "schwierigkeit", Spielstaende.SchwierigkeitVoreinstellung(I));
        }
        var gewaehlt = Py.Str(_entwurf["schwierigkeit"]);
        var hinweis = T(texte.GetValueOrDefault(gewaehlt, ""), 13, "text_soft");
        var erste = texte.ContainsKey(gewaehlt) ? gewaehlt : Py.Str(stufen[0].Id);
        return Karte("Schwierigkeitsgrad", [
            Pillen(stufen.Select(s => (Py.Str(s.Id), Py.Str(s.Name))), erste, wert =>
            {
                Py.Setzen(_entwurf, "schwierigkeit", wert);
                hinweis.Text = texte.GetValueOrDefault(wert, "");
            }),
            hinweis,
            T("Gilt für den ganzen Spielstand. Ändern geht nur mit „Spielstand zurücksetzen“. Die Lernplattform ist davon nicht betroffen.", 12, "muted"),
        ], "accent2");
    }

    private void FigurSpeichern(string name, PyObjekt aussehen)
    {
        if (Tun(a => a.Profil(name, aussehen, Get(_entwurf, "schwierigkeit"))) is null)
        {
            return;
        }
        _entwurf.Clear();
        Fortschritt();
    }

    /// <summary>_edit_profile</summary>
    private void FigurAendern()
    {
        _entwurf.Clear();
        Py.Setzen(_entwurf, "edit", true);
        _stapel.Clear();
        Zeichnen();
    }

    private void FigurAbbrechen()
    {
        _entwurf.Clear();
        Zeichnen();
    }
}
