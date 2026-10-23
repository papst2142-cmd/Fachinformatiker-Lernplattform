using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Lernen;
using Lernplattform.Core.Texte;
using Microsoft.Data.Sqlite;

namespace Lernplattform.Core.Hilfe;

/// <summary>Startwerte der Seite "Jetzt einrichten" (setup_values): Name, Abschnitte B/D/E, Termine als TT.MM.JJJJ.</summary>
public sealed record EinrichtenWerte(string Name, bool B, bool D, bool E, string TerminAp1, string TerminAp2)
{
    public bool Abschnitt(string abschnitt) => abschnitt switch
    {
        "B" => B,
        "D" => D,
        _ => E,
    };
}

/// <summary>
/// Phase 4, W4: Rundgang-Merker, Name und "Jetzt einrichten" wie fisi_hilfe 0.63.2 (61-143) und die
/// Rahmenplan-Einstellungen wie fisi_rahmenplan.save_rp_settings. Alles nur in einstellungen.json
/// (nur dieses Geraet). Geschrieben wird ueber eine Aenderung fuer Zustand.Aendere (ein Schreibvorgang,
/// Plan W4 2.7). Geprueft gegen G35 (tests/Vergleich/hilfe.json).
/// </summary>
public static class Einrichten
{
    public const string GesehenSchluessel = "rundgang_gesehen";   // TOUR_SEEN_KEY
    public const string NameSchluessel = "nutzer_name";          // NAME_KEY

    /// <summary>OPTIONAL_SECTIONS mit SECTION_SETTING.</summary>
    public static readonly IReadOnlyList<(string Abschnitt, string Schluessel)> Abschnitte =
        [("B", "rp_abschnitt_b"), ("D", "rp_abschnitt_d"), ("E", "rp_abschnitt_e")];

    public const string GewichtungSchluessel = "rp_gewichtung";
    public const string LernfeldSchluessel = "rp_lernfeld";
    public const string TerminAp1Schluessel = "rp_termin_ap1";
    public const string TerminAp2Schluessel = "rp_termin_ap2";

    /// <summary>Schluessel von RP_SETTINGS (nur diese speichert save_rp_settings).</summary>
    public static readonly IReadOnlyList<string> RahmenplanSchluessel =
        ["rp_abschnitt_b", "rp_abschnitt_d", "rp_abschnitt_e", GewichtungSchluessel, LernfeldSchluessel, TerminAp1Schluessel, TerminAp2Schluessel];

    // -- Datum ------------------------------------------------------------------------------------

    /// <summary>parse_date (die eine Datumsfunktion, RahmenplanGewicht.Datum).</summary>
    public static DateOnly? Datum(string? text) => RahmenplanGewicht.Datum(text);

    /// <summary>date_text: "TT.MM.JJJJ" ("" ohne gueltiges Datum); das Jahr ohne fuehrende Nullen wie strftime unter Linux.</summary>
    public static string DatumText(string? text) => Datum(text) is { } tag ? DatumText(tag) : "";

    public static string DatumText(DateOnly tag) =>
        tag.ToString("dd.MM.", CultureInfo.InvariantCulture) + tag.Year.ToString(CultureInfo.InvariantCulture);

    /// <summary>check_date: ("", null) bei leer, (JJJJ-MM-TT, null) oder (null, DATE_INVALID).</summary>
    public static (string? Iso, string? Fehler) PruefeDatum(string? text)
    {
        text = Lernregeln.Strip(text ?? "");
        if (text.Length == 0)
        {
            return ("", null);
        }
        return Datum(text) is { } tag ? (Iso(tag), null) : (null, RahmenplanTexte.DatumUngueltig);
    }

    public static string Iso(DateOnly tag) => tag.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // -- Name -------------------------------------------------------------------------------------

    /// <summary>clean_name: Leerraum wie str.split() zu einem Leerzeichen, hoechstens 40 Zeichen (Codepunkte).</summary>
    public static string NameBereinigen(string? text) =>
        PyText.Teil(string.Join(' ', PyText.Woerter(text ?? "")), 0, DashboardTexte.NameMax);

    /// <summary>
    /// load_name: str(x or "") fuer Text, null, true/false und ganze Zahlen; Kommazahl, Liste und Objekt
    /// (nur von Hand in die Datei geschrieben) gelten als "kein Name" (Plan W4 2.7).
    /// </summary>
    public static string NameLesen(JsonObject werte) => werte[NameSchluessel] switch
    {
        JsonValue v when v.GetValueKind() == JsonValueKind.String => v.GetValue<string>(),
        JsonValue v when v.GetValueKind() == JsonValueKind.True => "True",
        JsonValue v when v.GetValueKind() == JsonValueKind.Number => GanzeZahl(v.ToJsonString()),
        _ => "",
    };

    private static string GanzeZahl(string roh)
    {
        if (roh.IndexOfAny(['.', 'e', 'E']) >= 0 || !BigInteger.TryParse(roh, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var zahl))
        {
            return "";
        }
        return zahl.IsZero ? "" : zahl.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>save_name: bereinigten Namen setzen; true, wenn sich der gespeicherte Wert aendert.</summary>
    public static bool NameSetzen(JsonObject werte, string? text)
    {
        var name = NameBereinigen(text);
        var alt = NameLesen(werte);
        // wie str(alt or "") != name; Kommazahl, Liste und Objekt (Plan 2.7: "kein Name") werden ersetzt
        var abweichend = alt.Length == 0 && Einstellungen.PythonBool(werte[NameSchluessel]);
        if (alt == name && !abweichend)
        {
            return false;
        }
        werte[NameSchluessel] = name;
        return true;
    }

    // -- Rundgang-Merker --------------------------------------------------------------------------

    /// <summary>tour_seen: bool() des Merkers.</summary>
    public static bool RundgangGesehen(JsonObject werte) => Einstellungen.PythonBool(werte[GesehenSchluessel]);

    /// <summary>tour_due: kein Merker und keine Daten (HatDaten).</summary>
    public static bool RundgangFaellig(JsonObject werte, bool hatDaten) => !RundgangGesehen(werte) && !hatDaten;

    /// <summary>mark_tour_seen: Merker = heutiges Datum (JJJJ-MM-TT).</summary>
    public static void RundgangMerken(JsonObject werte, DateOnly heute) => werte[GesehenSchluessel] = Iso(heute);

    // -- Einrichten -------------------------------------------------------------------------------

    /// <summary>setup_values: Startwerte aus den Einstellungen (rp_settings mit Vorgaben, bool(), Datumspruefung).</summary>
    public static EinrichtenWerte Startwerte(JsonObject werte)
    {
        bool An(string schluessel) => werte.ContainsKey(schluessel) && Einstellungen.PythonBool(werte[schluessel]);
        string Termin(string schluessel, string vorgabe)
        {
            if (!werte.ContainsKey(schluessel))
            {
                return DatumText(vorgabe);
            }
            // str(x or ""): nur Text kann ein Datum sein
            return werte[schluessel] is JsonValue v && v.GetValueKind() == JsonValueKind.String ? DatumText(v.GetValue<string>()) : "";
        }
        return new EinrichtenWerte(NameLesen(werte), An("rp_abschnitt_b"), An("rp_abschnitt_d"), An("rp_abschnitt_e"),
            Termin(TerminAp1Schluessel, RahmenplanWerte.TerminAp1Vorgabe), Termin(TerminAp2Schluessel, RahmenplanWerte.TerminAp2Vorgabe));
    }

    /// <summary>rp_gewichtung wie rp_settings: Vorgabe an, sonst bool().</summary>
    public static bool Gewichtung(JsonObject werte) =>
        !werte.ContainsKey(GewichtungSchluessel) || Einstellungen.PythonBool(werte[GewichtungSchluessel]);

    /// <summary>
    /// save_setup als eine Aenderung: Ist ein Termin ungueltig, kommt der Fehlertext und keine Aenderung
    /// (es wird gar nichts gespeichert). Sonst setzt die Aenderung Name, die uebergebenen Abschnitte, beide
    /// Termine (leer = kein Termin) und, wenn angegeben, den Rundgang-Merker in einem Schreibvorgang.
    /// </summary>
    public static (string? Fehler, Action<JsonObject>? Aenderung) Speichern(string? name, IReadOnlyDictionary<string, bool> abschnitte,
        string? ap1, string? ap2, DateOnly? gesehen = null)
    {
        var termine = new List<(string, string)>();
        foreach (var (schluessel, text) in new[] { (TerminAp1Schluessel, ap1), (TerminAp2Schluessel, ap2) })
        {
            var (iso, fehler) = PruefeDatum(text);
            if (fehler is not null)
            {
                return (fehler, null);
            }
            termine.Add((schluessel, iso!));
        }
        return (null, werte =>
        {
            werte[NameSchluessel] = NameBereinigen(name);
            foreach (var (abschnitt, schluessel) in Abschnitte)
            {
                if (abschnitte.TryGetValue(abschnitt, out var an))
                {
                    werte[schluessel] = an;
                }
            }
            foreach (var (schluessel, iso) in termine)
            {
                werte[schluessel] = iso;
            }
            if (gesehen is { } tag)
            {
                RundgangMerken(werte, tag);
            }
        });
    }

    /// <summary>save_rp_settings: nur bekannte Schluessel setzen, alles andere bleibt. false bei unbekanntem Schluessel.</summary>
    public static bool RahmenplanSetzen(JsonObject werte, string schluessel, JsonNode? wert)
    {
        if (!RahmenplanSchluessel.Contains(schluessel))
        {
            return false;
        }
        werte[schluessel] = wert;
        return true;
    }

    // -- Bestandsschutz ---------------------------------------------------------------------------

    /// <summary>
    /// db_has_data: true, sobald irgendeine Tabelle (ohne sqlite_%) eine Zeile hat. Ein Lesefehler gilt als
    /// "Daten vorhanden" (dann erscheint der Rundgang lieber nicht von selbst). Eigene, nur lesende Verbindung;
    /// eine fehlende Datei hat keine Daten und wird nicht angelegt.
    /// </summary>
    public static bool HatDaten(string pfad)
    {
        if (!File.Exists(pfad))
        {
            return false;
        }
        try
        {
            using var verbindung = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = pfad,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
                DefaultTimeout = Lernstand.Wartezeit,
            }.ToString());
            verbindung.Open();
            var tabellen = new List<string>();
            using (var befehl = verbindung.CreateCommand())
            {
                befehl.CommandText = "PRAGMA query_only = ON";
                befehl.ExecuteNonQuery();
                befehl.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
                using var leser = befehl.ExecuteReader();
                while (leser.Read())
                {
                    tabellen.Add(leser.GetValue(0) is string text ? text : Convert.ToString(leser.GetValue(0), CultureInfo.InvariantCulture) ?? "");
                }
            }
            foreach (var tabelle in tabellen)
            {
                try
                {
                    using var befehl = verbindung.CreateCommand();
                    befehl.CommandText = "SELECT 1 FROM \"" + tabelle.Replace("\"", "\"\"", StringComparison.Ordinal) + "\" LIMIT 1";
                    using var leser = befehl.ExecuteReader();
                    if (leser.Read())
                    {
                        return true;
                    }
                }
                catch (SqliteException)
                {
                    return true;   // default=True
                }
            }
            return false;
        }
        catch (SqliteException)
        {
            return true;
        }
    }
}
