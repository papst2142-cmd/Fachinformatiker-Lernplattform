using System.Globalization;
using System.Text.Json.Nodes;
using Lernplattform.Core.Zufall;

namespace Lernplattform.Core.Inhalte;

/// <summary>
/// Fragenkatalog des Pruefungstrainers wie fisi_core.build_quiz_database:
/// feste Fragen plus automatisch erzeugte Aufgaben (Subnetze, Ports,
/// Abkuerzungen, RAID). Die falschen Antworten waehlt der Python-Zufall mit
/// festen Text-Startwerten, damit jede erzeugte Frage auf allen Geraeten und in
/// beiden Linien gleich ist (Vergleichswerte G12, "erzeugte_fragen").
/// Erzeugte Fragen doppeln nie eine feste Frage (Vergleich ohne Leerraum aussen,
/// klein geschrieben).
/// </summary>
public static class Fragenkatalog
{
    private static readonly (int Praefix, string Maske, int Hosts)[] Praefixe =
    [
        (24, "255.255.255.0", 254), (25, "255.255.255.128", 126), (26, "255.255.255.192", 62),
        (27, "255.255.255.224", 30), (28, "255.255.255.240", 14), (29, "255.255.255.248", 6),
        (30, "255.255.255.252", 2),
    ];

    private static readonly (string Port, string Dienst)[] Ports =
    [
        ("20/21", "FTP"), ("22", "SSH / SFTP"), ("23", "Telnet"), ("25", "SMTP"),
        ("53", "DNS"), ("67/68", "DHCP"), ("80", "HTTP"), ("110", "POP3"),
        ("123", "NTP"), ("143", "IMAP"), ("161/162", "SNMP"), ("389", "LDAP"),
        ("443", "HTTPS"), ("445", "SMB"), ("636", "LDAPS"), ("3389", "RDP"),
    ];

    private static readonly (string Kurz, string Bedeutung, string Thema)[] Sicherheit =
    [
        ("VPN", "Virtual Private Network", "netzsicherheit"), ("IDS", "Intrusion Detection System", "netzsicherheit"),
        ("IPS", "Intrusion Prevention System", "netzsicherheit"), ("MFA", "Multi-Factor Authentication", "zugriff"),
        ("WAF", "Web Application Firewall", "netzsicherheit"), ("DLP", "Data Loss Prevention", "datenschutz"),
        ("CERT", "Computer Emergency Response Team", "notfall"), ("SOC", "Security Operations Center", "haertung"),
    ];

    private static readonly (string Level, string Beschreibung, int Platten)[] Raid =
    [
        ("RAID 0", "Striping ohne Redundanz - maximale Performance, kein Ausfallschutz", 2),
        ("RAID 1", "Spiegelung (Mirroring) - volle Redundanz, halbierte Nutzkapazität", 2),
        ("RAID 5", "Striping mit verteilter einfacher Parität - 1 Plattenausfall verkraftbar", 3),
        ("RAID 6", "Striping mit verteilter doppelter Parität - 2 Plattenausfälle verkraftbar", 4),
        ("RAID 10", "Kombination aus Spiegelung und Striping - hohe Performance und Redundanz", 4),
    ];

    private static readonly (string Kurz, string Bedeutung, string Thema)[] Wirtschaft =
    [
        ("EBIT", "Earnings Before Interest and Taxes", "kalkulation"), ("USt.", "Umsatzsteuer", "kalkulation"),
        ("BGB", "Bürgerliches Gesetzbuch", "recht"), ("HGB", "Handelsgesetzbuch", "recht"),
        ("AGB", "Allgemeine Geschäftsbedingungen", "recht"), ("WBS", "Work Breakdown Structure", "projekt"),
        ("MVP", "Minimum Viable Product", "projekt"), ("JIT", "Just in Time", "beschaffung"),
    ];

    /// <summary>Feste Fragen (schon umgewandelt) plus erzeugte Aufgaben, ohne Dubletten.</summary>
    public static List<JsonObject> Aufbauen(IEnumerable<JsonObject> fest)
    {
        var db = fest.ToList();
        var masken = Praefixe.Select(p => p.Maske).ToList();
        var hosts = Praefixe.Select(p => p.Hosts).ToList();
        foreach (var (praefix, maske, anzahl) in Praefixe)
        {
            var falscheMasken = new PythonZufall(maske).Sample(masken.Where(m => m != maske).ToList(), 3);
            db.Add(Frage(Fachbereiche.Netzwerk, "ipv4",
                F("Wie lautet die Subnetzmaske für die CIDR-Notation /{0}?", praefix),
                [maske, .. falscheMasken], maske, F("/{0} entspricht der Maske {1}.", praefix, maske)));
            var andere = hosts.Where(h => h != anzahl).Distinct().Order().ToList();
            var falscheHosts = new PythonZufall(Zahl(anzahl)).Sample(andere, Math.Min(3, andere.Count));
            db.Add(Frage(Fachbereiche.Netzwerk, "ipv4",
                F("Wie viele nutzbare Host-IP-Adressen bietet ein Subnetz mit /{0}?", praefix),
                [Zahl(anzahl), .. falscheHosts.Select(Zahl)], Zahl(anzahl),
                F("2^(32 - {0}) minus 2 (Netzwerk-Adresse und Broadcast) = {1} Hosts.", praefix, anzahl)));
        }

        foreach (var (port, dienst) in Ports)
        {
            var falsch = new PythonZufall(port).Sample(Ports.Select(p => p.Dienst).Where(d => d != dienst).ToList(), 3);
            db.Add(Frage(Fachbereiche.Netzwerk, "dienste",
                F("Welcher Standard-Dienst verwendet primär den Port {0}?", port),
                [dienst, .. falsch], dienst, F("Der TCP/UDP-Port {0} gehört standardmäßig zu {1}.", port, dienst)));
        }

        foreach (var (kurz, bedeutung, thema) in Sicherheit)
        {
            var falsch = new PythonZufall(kurz).Sample(Sicherheit.Select(s => s.Bedeutung).Where(b => b != bedeutung).ToList(), 3);
            db.Add(Frage(Fachbereiche.Sicherheit, thema,
                F("Wofür steht die Abkürzung {0} im Sicherheitskontext?", kurz),
                [bedeutung, .. falsch], bedeutung, F("{0} steht für {1}.", kurz, bedeutung)));
        }

        var plattenzahlen = Raid.Select(r => r.Platten).Distinct().Order().ToList();
        foreach (var (level, beschreibung, platten) in Raid)
        {
            var falsch = new PythonZufall(level + "d").Sample(Raid.Select(r => r.Beschreibung).Where(b => b != beschreibung).ToList(), 3);
            db.Add(Frage(Fachbereiche.Systeme, "storage",
                F("Welche Eigenschaft beschreibt {0} am besten?", level),
                [beschreibung, .. falsch], beschreibung, F("{0}: {1}.", level, beschreibung)));
            var falschePlatten = plattenzahlen.Where(n => n != platten).Select(Zahl).Take(3).ToList();
            while (falschePlatten.Count < 3)
            {
                falschePlatten.Add(Zahl(platten + falschePlatten.Count + 1));
            }
            db.Add(Frage(Fachbereiche.Systeme, "storage",
                F("Wie viele Festplatten werden für {0} mindestens benötigt?", level),
                [Zahl(platten), .. falschePlatten], Zahl(platten),
                F("{0} benötigt mindestens {1} Festplatten.", level, platten)));
        }

        foreach (var (kurz, bedeutung, thema) in Wirtschaft)
        {
            var falsch = new PythonZufall(kurz).Sample(Wirtschaft.Select(s => s.Bedeutung).Where(b => b != bedeutung).ToList(), 3);
            db.Add(Frage(Fachbereiche.Wirtschaft, thema,
                F("Wofür steht die Abkürzung {0} im wirtschaftlichen Kontext?", kurz),
                [bedeutung, .. falsch], bedeutung, F("{0} steht für {1}.", kurz, bedeutung)));
        }

        var eindeutig = new List<JsonObject>();
        var gesehen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var frage in db)
        {
            if (gesehen.Add(InhaltePruefung.Schluessel((string)frage["q"]!)))
            {
                eindeutig.Add(frage);
            }
        }
        return eindeutig;
    }

    private static JsonObject Frage(string fachbereich, string thema, string frage, List<string> optionen, string antwort, string erklaerung) =>
        new()
        {
            ["cat"] = fachbereich,
            ["thema"] = thema,
            ["q"] = frage,
            ["options"] = new JsonArray(optionen.Select(o => (JsonNode)o).ToArray()),
            ["a"] = antwort,
            ["exp"] = erklaerung,
        };

    private static string F(string format, params object[] werte) => string.Format(CultureInfo.InvariantCulture, format, werte);

    private static string Zahl(int wert) => wert.ToString(CultureInfo.InvariantCulture);
}
