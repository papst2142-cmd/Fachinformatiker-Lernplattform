using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Design;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Views;
using static Lernplattform.Core.Spiel.SpielDaten;
using static Lernplattform.Spiel.Tafel.Bau;

namespace Lernplattform.Spiel.Tafel;

/// <summary>
/// Firma, zweiter Teil: Gebaeude (mit Sonderraeumen und Filiale), Finanzen (mit Umsatzsteuer),
/// Marketing, Zertifizierungen und Kredite wie _firm_gebaeude bis _repay_loan in mobile/src/spiel.py.
/// Grundrisse (FloorPlan) und das kleine Modell der Filiale (model_preview) zeigt in 3D die Stadt selbst.
/// </summary>
public sealed partial class SpielTafel
{
    // Material Icons CHECK_CIRCLE und CANCEL (Apache-2.0 wie fisi_symbole.otf); die Zeichen fehlen in der Teilschrift
    private static readonly Geometry HakenKreis = Geometry.Parse(
        "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z");

    private static readonly Geometry KreuzKreis = Geometry.Parse(
        "M12 2C6.47 2 2 6.47 2 12s4.47 10 10 10 10-4.47 10-10S17.53 2 12 2zm5 13.59L15.59 17 12 13.41 8.41 17 7 15.59 10.59 12 7 8.41 8.41 7 12 10.59 15.59 7 17 8.41 13.59 12 17 15.59z");

    private long? _kreditSumme;
    private long _kreditLaufzeit = 50;
    private readonly HashSet<string> _planFuer = new(StringComparer.Ordinal);

    // -- Gebaeude -----------------------------------------------------------------

    /// <summary>_firm_gebaeude</summary>
    private IEnumerable<Control?> FirmaGebaeude(Spielstand stand)
    {
        var stufe = stand.FirmenStufe();
        var regeln = Objekt(Betrieb.FirmaRegeln(I)["gebaeude"]);
        yield return Karte(Py.Str(regeln["name"]), [
            T(PyFormat.Prozent("%s %s", regeln["text"], stufe["text"]), 13, "text_soft"),
            T(PyFormat.Prozent("Stufe %d · %d Arbeitsplätze · Nebenkosten %s pro Arbeitstag", stufe["stufe"], stand.OrtKapazitaet(OrtBuero),
                Euro(stufe["nebenkosten"])), 12, "text_dim"),
            Reihe(Knopf("Büro öffnen", () => OrtOeffnen(OrtBuero), "ghost", 38)),
        ], "green");
        var naechste = stand.NaechsteStufe();
        List<Control?> mehr;
        if (naechste is null)
        {
            mehr = [T("Der Gewerbehof ist fertig ausgebaut. Mehr Ausbau gibt es in einem der nächsten Updates.", 14, "text_soft")];
        }
        else
        {
            mehr = [
                T(PyFormat.Prozent("Stufe %d · %s: %s", naechste["stufe"], naechste["name"], naechste["text"]), 14, "text_soft"),
                T(PyFormat.Prozent("Kosten %s · danach %d Arbeitsplätze · Nebenkosten %s pro Arbeitstag", Euro(naechste["preis"]),
                    Liste(naechste["plaetze"]).Count, Euro(naechste["nebenkosten"])), 12, "text_dim"),
                Breit(Knopf("Ausbauen", () => Ausbauen(naechste), frei: stand.Geld >= Py.Int(naechste["preis"]))),
            ];
        }
        yield return Karte("Ausbau", mehr, "accent");
        yield return Sonderraeume(stand);
        yield return FilialKarte(stand);
    }

    /// <summary>open_site: in 3D zum Ort in der Stadt gehen.</summary>
    private void OrtOeffnen(string ort)
    {
        AllesSchliessen();
        _s.Spiel.GeheZu(ort);
        Geaendert?.Invoke();
    }

    /// <summary>_firm_branch: Voraussetzung und Eroeffnung, danach Ausbau der Filiale.</summary>
    private Control? FilialKarte(Spielstand stand)
    {
        var regeln = Betrieb.FilialRegeln(I);
        if (!Py.Wahr(regeln))
        {
            return null;
        }
        var lage = Betrieb.FilialStand(stand, I);
        var offen = Py.Wahr(lage["offen"]);
        var stufe = lage["stufe"] as PyObjekt;
        var naechste = lage["naechste"] as PyObjekt;
        var unter = offen ? PyFormat.Prozent("Stufe %d · %d von %d Plätzen", stufe!["stufe"], lage["belegt"], lage["plaetze"]) : "zweiter Standort";
        var teile = new List<Control?>
        {
            Stapel(2, [T(Py.Str(offen ? lage["name"] : Get(regeln, "name", "")), 16, "purple", true), T(Py.Str(Get(regeln, "text", "")), 12, "text_soft")]),
            T(PyFormat.Prozent("Vorteile: %s.", Betrieb.FilialVorteilText(I)), 12, "text_dim"),
        };
        if (!offen)
        {
            teile.Add(T(PyFormat.Prozent("Eröffnung: %s · %d Arbeitsplätze · Nebenkosten %s pro Arbeitstag", Euro(naechste!["preis"]),
                Liste(naechste["plaetze"]).Count, Euro(naechste["nebenkosten"])), 12, "text_dim"));
            var fehlt = ListeOderLeer(lage, "fehlt");
            if (fehlt.Count > 0)
            {
                teile.Add(T("Noch nicht so weit:", 14, "yellow", true));
                teile.AddRange(fehlt.Select(z => (Control?)T("·  " + Py.Str(z), 13, "text_soft")));
            }
            else
            {
                var name = Eingabe(Py.Str(Get(regeln, "name_vorschlag", "")), "Wie heißt deine Filiale?");
                var problem = Py.Wahr(lage["problem"]);
                teile.Add(Label("Filialname"));
                teile.Add(name);
                teile.Add(Breit(Knopf("Filiale eröffnen", () => FilialeEroeffnen(name.Text), frei: !problem)));
                if (problem)
                {
                    teile.Add(T(Py.Str(lage["problem"]), 12, "muted"));
                }
            }
            return Karte("Filiale", teile, "purple", unter);
        }
        teile.Add(T(PyFormat.Prozent("Nebenkosten %s pro Arbeitstag", Euro(stufe!["nebenkosten"])), 12, "text_dim"));
        teile.Add(Reihe(Knopf("Filiale öffnen", () => OrtOeffnen(OrtFiliale), "ghost", 38)));
        if (naechste is null)
        {
            teile.Add(T("Die Filiale ist fertig ausgebaut.", 13, "text_dim"));
        }
        else
        {
            teile.Add(T(PyFormat.Prozent("Stufe %d · %s: %s", naechste["stufe"], naechste["name"], naechste["text"]), 14, "text_soft"));
            teile.Add(T(PyFormat.Prozent("Kosten %s · danach %d Arbeitsplätze · Nebenkosten %s pro Arbeitstag", Euro(naechste["preis"]),
                Liste(naechste["plaetze"]).Count, Euro(naechste["nebenkosten"])), 12, "text_dim"));
            teile.Add(Breit(Knopf("Filiale ausbauen", () => FilialeAusbauen(naechste), frei: !Py.Wahr(lage["problem"]))));
        }
        return Karte("Filiale", teile, "purple", unter);
    }

    /// <summary>_open_branch</summary>
    private void FilialeEroeffnen(string? name)
    {
        var stufe = Betrieb.FilialStufe(1L, I)!;
        var anzeige = string.Join(" ", (name ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        Bestaetigen("Filiale eröffnen", PyFormat.Prozent("„%s“ für %s eröffnen? Die Nebenkosten steigen um %s pro Arbeitstag.", anzeige,
            Euro(stufe["preis"]), Euro(stufe["nebenkosten"])), () =>
        {
            if (Tun(a => a.FilialeEroeffnen(name)) is not { } daten)
            {
                return;
            }
            FirmaGeaendert();
            _s.Spiel.NotizSetzen("Willkommen in Lindenau", Betrieb.FilialeEroeffnetText(Py.Str(daten["name"]), I));
        });
    }

    /// <summary>_expand_branch</summary>
    private void FilialeAusbauen(PyObjekt stufe) =>
        Bestaetigen("Filiale ausbauen", PyFormat.Prozent("„%s“ für %s bauen?", stufe["name"], Euro(stufe["preis"])), () =>
        {
            if (FirmaTun(a => a.FilialeAusbauen()))
            {
                FirmaGeaendert();
            }
        });

    /// <summary>_firm_rooms: je Sonderraum Vorteil, Preis und Zustand.</summary>
    private Control? Sonderraeume(Spielstand stand)
    {
        var raeume = Betrieb.RaumStand(stand, I);
        if (raeume.Count == 0)
        {
            return null;
        }
        var kaesten = new List<Control?> { T("Frei wählbar, sobald die Ausbaustufe erreicht ist.", 12, "text_dim") };
        var firmenStufe = Py.Int(Objekt(stand.Firma)["stufe"]);
        foreach (var eintrag in raeume)
        {
            var teile = new List<Control?>
            {
                T(Py.Str(eintrag["name"]), 15, "text", true),
                T(Py.Str(eintrag["vorteil"]), 13, "text_soft"),
                T(Betrieb.RaumStandText(eintrag), 12, "text_dim"),
            };
            if (Py.Wahr(eintrag["gebaut"]))
            {
                teile.Add(T("ausgebaut", 13, "green", true));
            }
            else if (firmenStufe < Py.Int(eintrag["ab_stufe"]))
            {
                teile.Add(T(PyFormat.Prozent("ab Stufe %d", eintrag["ab_stufe"]), 13, "muted", true));
            }
            else
            {
                teile.Add(Reihe(Knopf("Ausbauen", () => RaumBauen(eintrag), hoehe: 38, frei: !Py.Wahr(eintrag["problem"]))));
            }
            kaesten.Add(PersonKasten(teile));
        }
        return Karte("Sonderräume", kaesten, "purple");
    }

    /// <summary>_build_room</summary>
    private void RaumBauen(PyObjekt eintrag) =>
        Bestaetigen("Ausbauen", PyFormat.Prozent("„%s“ für %s ausbauen? Die Nebenkosten steigen um %s pro Arbeitstag.", eintrag["name"],
            Euro(eintrag["preis"]), Euro(eintrag["nebenkosten"])), () =>
        {
            if (FirmaTun(a => a.RaumBauen(eintrag["id"])))
            {
                FirmaGeaendert();
            }
        });

    /// <summary>_expand</summary>
    private void Ausbauen(PyObjekt stufe) =>
        Bestaetigen("Ausbauen", PyFormat.Prozent("„%s“ für %s bauen?", stufe["name"], Euro(stufe["preis"])), () =>
        {
            if (FirmaTun(a => a.Ausbauen()))
            {
                FirmaGeaendert();
            }
        });

    // -- Finanzen -----------------------------------------------------------------

    /// <summary>_firm_finanzen</summary>
    private IEnumerable<Control?> FirmaFinanzen(Spielstand stand)
    {
        var (namen, werte) = Betrieb.KontoVerlauf(stand);
        var diagramm = new LinienDiagramm(180, "green");
        diagramm.Daten(namen, "", [.. werte.Select(w => Core.Mathe.PythonRunden.Runde1(w / 1000.0))]);
        var konto = new List<Control?>
        {
            T(Euro(stand.Geld), 24, stand.Geld >= 0 ? "text" : "red", true),
            T("Verlauf in Tausend Euro", 11, "muted"),
            diagramm,
        };
        if (Py.Wahr(stand.Firma))
        {
            konto.Add(T(Wirtschaft.FesteKostenText(stand), 12, "text_dim"));
        }
        var zeilen = new List<Control?>();
        foreach (var tag in Betrieb.FinanzTage(stand))
        {
            var einzeln = Objekt(tag["ein"]).Select(p => PyFormat.Prozent("%s +%s", p.Key, Euro(p.Value)))
                .Concat(Objekt(tag["aus"]).Select(p => PyFormat.Prozent("%s -%s", p.Key, Euro(p.Value))));
            var gewinn = Py.Int(tag["gewinn"]);
            var kopf = Zeile(T(PyFormat.Prozent("Arbeitstag %d", tag["tag"]), 14, "text", true),
                T((gewinn >= 0 ? "+" : "-") + Euro(Math.Abs(gewinn)), 14, gewinn >= 0 ? "green" : "red", true));
            zeilen.Add(Flaeche(Stapel(2, [kopf, T(string.Join(" · ", einzeln), 11, "text_dim")]), innen: new Thickness(12, 8)));
        }
        if (zeilen.Count == 0)
        {
            zeilen.Add(T("Noch nichts gebucht.", 14, "text_soft"));
        }
        if (stand.Kredite.Count > 0)
        {
            zeilen.Insert(0, T("Gewinn je Tag ohne Kredit und Tilgung, die Zinsen zählen als Kosten.", 12, "text_dim"));
        }
        var verloren = Betrieb.VerlorenAn(stand, I).Select(v => (Control?)Zeile(T(Py.Str(v.Name), 13, "text", true),
            T(v.Anzahl == 1 ? "1 Auftrag" : PyFormat.Prozent("%d Aufträge", v.Anzahl), 12, "text_dim"))).ToList();
        if (verloren.Count == 0)
        {
            verloren.Add(T("Bisher hast du keinen Auftrag an einen Mitbewerber verloren.", 14, "text_soft"));
        }
        yield return Karte("Kontostand", konto, "accent");
        yield return Karte("Einnahmen und Ausgaben", zeilen, "green", "letzte 7 Arbeitstage");
        if (Wirtschaft.SteuerAktiv(stand))
        {
            yield return SteuerKarte(stand);
            // W7b, Text 5 (Nachtrag 2 zu P7-13)
            yield return T("Die Umsatzsteuer siehst du auch auf dem Amt.", 12, "text_dim");
        }
        yield return Karte("Gegen wen verloren", verloren, "pink", "an Mitbewerber");
    }

    /// <summary>_tax_card</summary>
    private Control SteuerKarte(Spielstand stand)
    {
        var e = Wirtschaft.SteuerStand(stand, I);
        var teile = new List<Control?>
        {
            T(PyFormat.Prozent("%s · %d %% · Voranmeldung alle %d Arbeitstage", e["finanzamt"], e["satz"], e["faellig_tage"]), 12, "text_dim"),
            T("Steuerrücklage " + Euro(e["ruecklage"]), 20, "text", true),
            T(PyFormat.Prozent("Deine Einnahmen enthalten %d %% Umsatzsteuer. Die Vorsteuer aus Nebenkosten, Material, Ausbau, Weiterbildung, Werbung " +
                "und Zertifizierungen wird abgezogen. Die Zahllast legt die Firma jeden Feierabend automatisch zurück, sie gehört dem Finanzamt.",
                e["satz"]), 13, "text_soft"),
        };
        teile.AddRange(Wirtschaft.SteuerZeilen(e).Select(z => (Control?)T(z, 12, "text_dim")));
        foreach (var warnung in new[] { Wirtschaft.SteuerSchuldText(stand, I), Wirtschaft.MahnText(stand, I) })
        {
            if (warnung.Length > 0)
            {
                teile.Add(T(warnung, 13, "red", true));
            }
        }
        var letzte = ListeOderLeer(e, "letzte");
        if (letzte.Count > 0)
        {
            teile.Add(Label("Letzte Voranmeldungen"));
            teile.AddRange(letzte.Select(d => (Control?)T("• " + Wirtschaft.VoranmeldungText(Objekt(d)), 12, "text_soft")));
        }
        return Karte("Umsatzsteuer", teile, "orange");
    }

    // -- Marketing ----------------------------------------------------------------

    /// <summary>_firm_marketing</summary>
    private IEnumerable<Control?> FirmaMarketing(Spielstand stand)
    {
        yield return Karte("Werbung", [
            T(Wirtschaft.MarketingText(stand, I), 14, "text_soft"),
            T("Jede Werbeform kannst du laufend buchen (Kosten pro Arbeitstag, jederzeit kündbar) oder einmalig (sofort bezahlt, wirkt eine feste " +
                "Zahl von Arbeitstagen). Die Wirkung beginnt am nächsten Arbeitstag.", 12, "text_dim"),
        ], "pink");
        var kaesten = new List<Control?>();
        foreach (var eintrag in Wirtschaft.WerbeStand(stand, I))
        {
            var teile = new List<Control?>
            {
                T(Py.Str(eintrag["name"]), 15, "text", true),
                T(Py.Str(eintrag["text"]), 13, "text_soft"),
                T("Wirkung: " + Py.Str(eintrag["wirkung"]), 12, "text_soft", true),
                T(Wirtschaft.WerbePreisText(eintrag), 12, "text_dim"),
            };
            if (Py.Wahr(eintrag["stand"]))
            {
                teile.Add(T(Py.Str(eintrag["stand"]), 13, "green", true));
            }
            var buchung = eintrag["buchung"] as PyObjekt;
            var gebucht = buchung is not null && Py.Wahr(buchung);
            if (gebucht && Py.Gleich(buchung!["art"], Wirtschaft.WerbungLaufend) && buchung["ende"] is null)
            {
                teile.Add(Breit(Knopf("Kündigen", () => WerbungKuendigen(eintrag), "ghost", 38)));
            }
            else if (!gebucht)
            {
                var angebote = Objekt(eintrag["angebote"]);
                var knoepfe = new List<Control?>();
                foreach (var (art, text) in Wirtschaft.WerbeArten)
                {
                    var angebot = Objekt(angebote[art]);
                    knoepfe.Add(Knopf(text, () => WerbungBuchen(angebot), art == Wirtschaft.WerbungLaufend ? "primary" : "ghost", 38,
                        !Py.Wahr(angebot["problem"])));
                }
                teile.Add(Reihe(knoepfe));
                var problem = Objekt(angebote[Wirtschaft.WerbungEinmalig])["problem"] is { } p1 && Py.Wahr(p1) ? p1
                    : Objekt(angebote[Wirtschaft.WerbungLaufend])["problem"];
                if (Py.Wahr(problem))
                {
                    teile.Add(T(Py.Str(problem), 12, "yellow"));
                }
            }
            kaesten.Add(PersonKasten(teile, gebucht ? "green" : "border"));
        }
        yield return Karte("Werbeformen", kaesten, "accent");
    }

    /// <summary>_book_ad</summary>
    private void WerbungBuchen(PyObjekt angebot) =>
        Bestaetigen("Werbung buchen", Wirtschaft.WerbeFrage(angebot), () =>
        {
            if (FirmaTun(a => a.WerbungBuchen(angebot["id"], angebot["art"])))
            {
                FirmaGeaendert();
            }
        });

    /// <summary>_stop_ad</summary>
    private void WerbungKuendigen(PyObjekt eintrag) =>
        Bestaetigen("Werbung kündigen", PyFormat.Prozent("„%s“ kündigen? Heute kostet sie noch, ab dem nächsten Arbeitstag nicht mehr.", eintrag["kurz"]), () =>
        {
            if (FirmaTun(a => a.WerbungBeenden(eintrag["id"])))
            {
                FirmaGeaendert();
            }
        });

    // -- Zertifizierungen -----------------------------------------------------------

    /// <summary>_firm_zertifizierungen</summary>
    private IEnumerable<Control?> FirmaZertifizierungen(Spielstand stand)
    {
        var regeln = Wirtschaft.ZertRegeln(I);
        var kopf = new List<Control?>
        {
            T(Wirtschaft.ZertText(stand, I), 14, "text_soft"),
            T(PyFormat.Prozent("Zertifizierungen schalten große und öffentliche Aufträge frei (sie erscheinen unter „Projekte“) und geben dir einen " +
                "Preisvorteil gegen Bitweiche und die anderen Mitbewerber. Alle Vorteile zusammen (Ruf, Zertifizierungen, Werbung) höchstens %d %%. " +
                "Es läuft immer nur eine Zertifizierung gleichzeitig.", Get(regeln, "vorteil_max", 10L)), 12, "text_dim"),
        };
        if (stand.FarmFrei is null)
        {
            kopf.Add(T(Betrieb.FarmFreiHinweis(stand, I), 13, "cyan", true));
        }
        yield return Karte("Zertifizierungen", kopf, "yellow");
        var alle = Wirtschaft.ZertStand(stand, I);
        foreach (var (art, titel, farbe) in new[] { ("firma", "Qualität, Sicherheit und Datenschutz", "cyan"), ("fach", "Fachliche Zertifizierungen", "green") })
        {
            var kaesten = new List<Control?>();
            foreach (var eintrag in alle.Where(e => Py.Str(e["art"]) == art))
            {
                var teile = new List<Control?>
                {
                    T(Py.Str(eintrag["name"]), 15, "text", true),
                    T(Py.Str(eintrag["text"]), 13, "text_soft"),
                    T(PyFormat.Prozent("Kosten %s · Dauer %s", Euro(eintrag["preis"]), Wirtschaft.LaufzeitText(eintrag["tage"])), 12, "text_dim"),
                    T("Vorteil: " + Py.Str(eintrag["vorteil"]), 12, "text_soft", true),
                };
                if (Wirtschaft.FreischaltText(eintrag) is { Length: > 0 } frei)
                {
                    teile.Add(T(frei, 12, "text_dim"));
                }
                var lage = Py.Str(eintrag["stand"]);
                if (lage == "erworben")
                {
                    teile.Add(T(Py.Str(eintrag["stand_text"]), 13, "green", true));
                }
                else if (lage == "laeuft")
                {
                    teile.Add(T(Py.Str(eintrag["stand_text"]), 13, "cyan", true));
                }
                else
                {
                    var angebot = Objekt(eintrag["angebot"]);
                    var problem = Py.Wahr(angebot["problem"]);
                    teile.Add(Reihe(Knopf("Beginnen", () => ZertBeginnen(angebot), hoehe: 38, frei: !problem)));
                    if (problem)
                    {
                        teile.Add(T(Py.Str(angebot["problem"]), 12, "yellow"));
                    }
                }
                kaesten.Add(PersonKasten(teile, lage == "erworben" ? "green" : "border"));
            }
            yield return Karte(titel, kaesten, farbe);
        }
    }

    /// <summary>_start_cert</summary>
    private void ZertBeginnen(PyObjekt angebot) =>
        Bestaetigen("Zertifizierung", Wirtschaft.ZertFrage(angebot), () =>
        {
            if (FirmaTun(a => a.ZertifizierungStarten(angebot["id"])))
            {
                FirmaGeaendert();
            }
        });

    // -- Kredite --------------------------------------------------------------------

    /// <summary>_firm_kredite</summary>
    private IEnumerable<Control?> FirmaKredite(Spielstand stand)
    {
        var regeln = Wirtschaft.KreditRegeln(I);
        var pruefung = Wirtschaft.Bonitaet(stand, I);
        var ok = Py.Wahr(pruefung["ok"]);
        var bank = new List<Control?>
        {
            T(PyFormat.Prozent("Deine Hausbank. Zinsen pro Jahr, ein Spieljahr hat %d Arbeitstage.", regeln["tage_pro_jahr"]), 12, "text_dim"),
        };
        if (_schalter != Schalter.Bank)
        {
            // W7b, Text 5 (Nachtrag 2 zu P7-13): nur in der Firmen-Tafel, nicht am Schalter
            bank.Add(T("Das geht auch in der Bank.", 12, "text_dim"));
        }
        if (ok)
        {
            bank.Add(T("Kreditrahmen " + Euro(pruefung["rahmen"]), 20, "text", true));
            bank.Add(T("davon frei " + Euro(pruefung["frei"]), 14, "green", true));
            bank.Add(T(PyFormat.Prozent("Der Rahmen richtet sich nach dem Umsatz der letzten %d Arbeitstage und deinem Ansehen. Das Geld ist frei " +
                "verwendbar, die Raten werden jeden Feierabend automatisch abgebucht.", Objekt(regeln["bonitaet"])["umsatz_tage"]), 13, "text_soft"));
            bank.Add(T(Wirtschaft.RahmenText(pruefung), 13, "text_soft", true));
        }
        else
        {
            bank.Add(T(Wirtschaft.WarteText(stand, pruefung), 14, "text_soft"));
        }
        foreach (var punkt in Liste(pruefung["punkte"]).Select(Objekt))
        {
            var erfuellt = Py.Wahr(punkt["ok"]);
            var farbe = erfuellt ? "green" : "yellow";
            var zeichen = new Avalonia.Controls.Shapes.Path { Data = erfuellt ? HakenKreis : KreuzKreis, Fill = Pinsel(farbe), Width = 16, Height = 16, Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0) };
            bank.Add(Zeile(T(Py.Str(punkt["text"]), 13, farbe, !erfuellt), null, 8, zeichen));
        }
        if (Wirtschaft.MahnText(stand, I) is { Length: > 0 } warnung)
        {
            bank.Add(T(warnung, 13, "red", true));
        }
        yield return Karte(Py.Str(regeln["bank"]), bank, "cyan");
        yield return LaufendeKredite(stand);

        var pakete = new List<Control?>();
        foreach (var angebot in Wirtschaft.KreditPakete(stand, I))
        {
            var problem = Py.Wahr(angebot["problem"]);
            pakete.Add(PersonKasten([
                T(Py.Str(angebot["name"]), 15, "text", true),
                T(Wirtschaft.AngebotText(angebot), 12, "text_dim"),
                T(Py.Str(angebot["text"]), 13, "text_soft"),
                problem && ok ? T(Py.Str(angebot["problem"]), 12, "yellow") : null,
                Reihe(Knopf("Aufnehmen", () => KreditAufnehmen(angebot), hoehe: 38, frei: !problem)),
            ]));
        }
        yield return Karte("Kreditpakete", pakete, "green", "feste Angebote");

        var (_, _, schritt) = Wirtschaft.FreiGrenzen(stand, I);
        _kreditSumme ??= Py.Int(Objekt(regeln["frei"])["start"]);
        var summe = Wirtschaft.SummeBegrenzen(stand, _kreditSumme, I);
        var laufzeiten = Wirtschaft.Laufzeiten(I).Select(t => (t.ToString(System.Globalization.CultureInfo.InvariantCulture), PyFormat.Prozent("%d Tage", t))).ToList();
        var gewaehlt = _kreditLaufzeit.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!laufzeiten.Any(l => l.Item1 == gewaehlt))
        {
            gewaehlt = laufzeiten[0].Item1;
        }
        var frei = Wirtschaft.KreditAngebot(stand, summe, _kreditLaufzeit, null, I, pruefung);
        var schritte = new (string, long)[] { ("−5.000", -5 * schritt), ("−1.000", -schritt), ("+1.000", schritt), ("+5.000", 5 * schritt) }
            .Select(s => (Control?)Knopf(s.Item1, () => KreditSchritt(s.Item2), "ghost", 38));
        var freiTeile = new List<Control?>
        {
            T("Summe und Laufzeit selbst wählen, bis zum freien Kreditrahmen.", 12, "text_dim"),
            T(Euro(summe), 22, "text", true),
            Reihe(schritte),
            Label("Laufzeit"),
            Pillen(laufzeiten, gewaehlt, t => { _kreditLaufzeit = long.Parse(t, System.Globalization.CultureInfo.InvariantCulture); Neu(); }),
            T(Wirtschaft.AngebotText(frei), 14, "text_soft"),
        };
        if (Py.Wahr(frei["problem"]))
        {
            freiTeile.Add(T(Py.Str(frei["problem"]), 13, "yellow"));
        }
        freiTeile.Add(Breit(Knopf("Aufnehmen", () => KreditAufnehmen(frei), frei: !Py.Wahr(frei["problem"]))));
        yield return Karte("Freie Kredithöhe", freiTeile, "accent");

        var fertig = stand.FertigeKredite();
        if (fertig.Count > 0)
        {
            yield return Karte("Abgeschlossene Kredite", fertig.Select(k => (Control?)T(PyFormat.Prozent("%s über %s · %s an Arbeitstag %d · Zinsen %s",
                k["name"], Euro(k["summe"]), Py.Str(k["ende_art"]) == "abgeloest" ? "abgelöst" : "zurückgezahlt", k["ende"], Euro(k["bezahlt_zins"])),
                12, "text_dim")), "muted");
        }
    }

    /// <summary>_running_loans</summary>
    private Control LaufendeKredite(Spielstand stand)
    {
        var kredite = stand.LaufendeKredite().Select(k => Wirtschaft.KreditStand(stand, k, I)).ToList();
        if (kredite.Count == 0)
        {
            return Karte("Laufende Kredite", [T("Du hast gerade keinen Kredit.", 14, "text_soft")], "orange");
        }
        var kaesten = new List<Control?>
        {
            T(PyFormat.Prozent("Restschuld %s · Raten %s pro Arbeitstag", Euro(Py.Summe(kredite.Select(k => k["rest"]))), Euro(stand.KreditRaten())),
                13, "text_soft", true),
        };
        foreach (var eintrag in kredite)
        {
            var teile = new List<Control?>
            {
                T(Py.Str(eintrag["name"]), 15, "text", true),
                T(PyFormat.Prozent("aufgenommen an Arbeitstag %d", eintrag["tag"]), 12, "muted"),
            };
            teile.AddRange(Wirtschaft.KreditZeilen(eintrag).Select(z => (Control?)T(z, 12, z.StartsWith("Geplatzte", StringComparison.Ordinal) ? "red" : "text_dim")));
            var id = Py.Str(eintrag["id"]);
            var offen = _planFuer.Contains(id);
            if (offen)
            {
                teile.Add(Tilgungsplan(eintrag));
            }
            var abloesen = Knopf("Ablösen", () => KreditAbloesen(eintrag), "ghost", 38, stand.Geld >= Py.Int(Objekt(eintrag["abloesen"])["gesamt"]));
            var plan = Knopf(offen ? "Plan ausblenden" : "Tilgungsplan", () =>
            {
                if (!_planFuer.Remove(id))
                {
                    _planFuer.Add(id);
                }
                Neu();
            }, "ghost", 38);
            teile.Add(Knopfreihe(plan, abloesen));
            kaesten.Add(PersonKasten(teile, Py.Wahr(eintrag["ausfaelle"]) ? "red" : "border"));
        }
        return Karte("Laufende Kredite", kaesten, "orange");
    }

    /// <summary>_loan_plan: Spalten so breit wie ihr breitester Betrag, waagrecht wischbar.</summary>
    private static Control Tilgungsplan(PyObjekt eintrag)
    {
        var (zeilen, ausgelassen) = Wirtschaft.PlanZeilen(eintrag);
        string[] kopf = ["Tag", "Rate", "Zinsen", "Tilgung", "Rest"];
        var werte = zeilen.Select(z => (string[])[PyFormat.Prozent("%d", z[0]), .. Enumerable.Range(1, z.Count - 1).Select(i => Euro(z[i]))]).ToList();
        List<string[]> erster = ausgelassen > 0 ? [kopf, .. werte.Take(werte.Count - 1)] : [kopf, .. werte];
        List<List<string[]>> bloecke = ausgelassen > 0 ? [erster, [werte[^1]]] : [erster];
        // ein gemeinsames Gitter haelt die Spalten beider Bloecke gleich breit (wie die unsichtbare Zeile in Python)
        var gitter = new Grid { ColumnSpacing = 12, RowSpacing = 3 };
        for (var i = 0; i < kopf.Length; i++)
        {
            gitter.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        }
        var reihe = 0;
        void Zeilen(IEnumerable<string[]> liste)
        {
            foreach (var zeile in liste)
            {
                gitter.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var istKopf = ReferenceEquals(zeile, kopf);
                for (var i = 0; i < zeile.Length; i++)
                {
                    var zelle = T(zeile[i], 11, istKopf ? "muted" : "text_soft", istKopf);
                    zelle.TextWrapping = TextWrapping.NoWrap;
                    zelle.HorizontalAlignment = HorizontalAlignment.Right;
                    Grid.SetRow(zelle, reihe);
                    Grid.SetColumn(zelle, i);
                    gitter.Children.Add(zelle);
                }
                reihe++;
            }
        }
        Zeilen(bloecke[0]);
        if (ausgelassen > 0)
        {
            gitter.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var luecke = T(PyFormat.Prozent("… %d weitere Raten …", ausgelassen), 11, "muted");
            Grid.SetRow(luecke, reihe++);
            Grid.SetColumnSpan(luecke, kopf.Length);
            gitter.Children.Add(luecke);
            Zeilen(bloecke[1]);
        }
        gitter.HorizontalAlignment = HorizontalAlignment.Left;
        var rolle = new ScrollViewer { Content = gitter, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        return Stapel(3, [rolle, T("Noch zu zahlende Zinsen, wenn jede Rate klappt: " + Euro(eintrag["zinsen_noch"]), 12, "text_dim")]);
    }

    /// <summary>_loan_step</summary>
    private void KreditSchritt(long schritt)
    {
        var stand = Stand;
        _kreditSumme = Wirtschaft.SummeBegrenzen(stand, Wirtschaft.SummeBegrenzen(stand, _kreditSumme ?? 0, I) + schritt, I);
        Neu();
    }

    /// <summary>_take_loan</summary>
    private void KreditAufnehmen(PyObjekt angebot) =>
        Bestaetigen("Kredit aufnehmen", Wirtschaft.KreditFrage(angebot, I), () =>
        {
            var ok = Py.Gleich(angebot["paket"], Wirtschaft.FreierKredit)
                ? FirmaTun(a => KreditAufnehmenAm(a, angebot["summe"], angebot["laufzeit"]))
                : FirmaTun(a => KreditAufnehmenAm(a, 0L, 0L, angebot["paket"]));
            if (ok)
            {
                FirmaGeaendert();
            }
        });

    /// <summary>_repay_loan</summary>
    private void KreditAbloesen(PyObjekt eintrag) =>
        Bestaetigen("Kredit ablösen", Wirtschaft.AbloeseText(eintrag, I), () =>
        {
            if (FirmaTun(a => KreditAbloesenAm(a, eintrag["id"])))
            {
                FirmaGeaendert();
            }
        });
}
