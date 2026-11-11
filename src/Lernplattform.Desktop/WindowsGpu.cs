using System;
using System.Runtime.InteropServices;

namespace Lernplattform.Desktop;

/// <summary>
/// Welle Grafik G1 (Weisung P12-182): GPU-Auslastung des eigenen Programms unter Windows aus dem Leistungszaehler
/// „\GPU Engine(*)\Utilization Percentage“ (Summe der 3D-Engines mit „pid_&lt;eigene pid&gt;_“ im Namen). Wird einmal je
/// Sekunde aus <c>Belastung</c> gelesen; der erste Wert braucht zwei Abfragen und ist deshalb null. Jeder Fehler (Zaehler
/// fehlt, Treiber liefert nichts) ergibt null = „nicht verfügbar“, nie eine Schaetzung. Liest nur, schreibt nichts.
/// </summary>
internal static class WindowsGpu
{
    private const string Pdh = "pdh.dll";
    private const uint FormatDouble = 0x00000200;
    private const uint MehrDaten = 0x800007D2;   // PDH_MORE_DATA

    private static IntPtr _abfrage = IntPtr.Zero;
    private static IntPtr _zaehler = IntPtr.Zero;
    private static bool _kaputt;
    private static string _muster = "";

    [DllImport(Pdh, CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQueryW(string? quelle, IntPtr nutzer, out IntPtr abfrage);

    [DllImport(Pdh, CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounterW(IntPtr abfrage, string pfad, IntPtr nutzer, out IntPtr zaehler);

    [DllImport(Pdh)]
    private static extern uint PdhCollectQueryData(IntPtr abfrage);

    [DllImport(Pdh, CharSet = CharSet.Unicode)]
    private static extern uint PdhGetFormattedCounterArrayW(IntPtr zaehler, uint format, ref uint groesse, out uint anzahl, IntPtr daten);

    [StructLayout(LayoutKind.Sequential)]
    private struct Eintrag
    {
        public IntPtr Name;
        public uint Status;
        public uint Fuellung;
        public double Wert;
    }

    /// <summary>Auslastung in Prozent (0–100) oder null.</summary>
    public static double? Abfragen()
    {
        if (!OperatingSystem.IsWindows() || _kaputt)
        {
            return null;
        }
        try
        {
            if (_abfrage == IntPtr.Zero)
            {
                if (PdhOpenQueryW(null, IntPtr.Zero, out _abfrage) != 0
                    || PdhAddEnglishCounterW(_abfrage, @"\GPU Engine(*)\Utilization Percentage", IntPtr.Zero, out _zaehler) != 0)
                {
                    _kaputt = true;
                    return null;
                }
                _muster = "pid_" + Environment.ProcessId + "_";
                PdhCollectQueryData(_abfrage);
                return null;
            }
            if (PdhCollectQueryData(_abfrage) != 0)
            {
                return null;
            }
            uint groesse = 0;
            var r = PdhGetFormattedCounterArrayW(_zaehler, FormatDouble, ref groesse, out var anzahl, IntPtr.Zero);
            if (r != MehrDaten || groesse == 0)
            {
                return null;
            }
            var puffer = Marshal.AllocHGlobal((int)groesse);
            try
            {
                if (PdhGetFormattedCounterArrayW(_zaehler, FormatDouble, ref groesse, out anzahl, puffer) != 0)
                {
                    return null;
                }
                var schritt = Marshal.SizeOf<Eintrag>();
                double summe = 0;
                var gefunden = false;
                for (var i = 0; i < anzahl; i++)
                {
                    var e = Marshal.PtrToStructure<Eintrag>(puffer + i * schritt);
                    var name = Marshal.PtrToStringUni(e.Name) ?? "";
                    if (e.Status == 0 && name.Contains(_muster, StringComparison.Ordinal)
                        && name.Contains("engtype_3D", StringComparison.Ordinal))
                    {
                        summe += e.Wert;
                        gefunden = true;
                    }
                }
                return gefunden ? Math.Clamp(summe, 0, 100) : null;
            }
            finally
            {
                Marshal.FreeHGlobal(puffer);
            }
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or MarshalDirectiveException)
        {
            _kaputt = true;
            return null;
        }
    }
}
