using System;
using System.Runtime.InteropServices;

namespace Lernplattform.Desktop;

/// <summary>
/// Patch A 0.37.0 (Sammelliste 204, Weisung P11-34 Antwort 3a): fragt am Mac, ob das Programm sichtbar ist
/// (<c>NSApp.occlusionState</c>, Bit NSApplicationOcclusionStateVisible). macOS drosselt die Zeitgeber eines
/// verdeckten Programms (App Nap); die Haenger-Diagnose meldet eine Blockade dann nur mit Zusatz in haenger.log.
/// Liest nur: die globale Variable NSApp (null, solange es noch kein Programmobjekt gibt, dann "unbekannt"), keine
/// neue Instanz, nichts wird veraendert. Jeder Fehler ergibt "unbekannt" (= wie sichtbar, also streng).
/// </summary>
internal static class MacSichtbarkeit
{
    private const string AppKit = "/System/Library/Frameworks/AppKit.framework/AppKit";
    private const string Objc = "/usr/lib/libobjc.A.dylib";
    private const ulong Sichtbar = 1UL << 1;   // NSApplicationOcclusionStateVisible

    private static IntPtr _nsApp = IntPtr.Zero;   // Adresse der globalen Variable NSApp
    private static IntPtr _auswahl = IntPtr.Zero;

    [DllImport(Objc, EntryPoint = "sel_registerName")]
    private static extern IntPtr Selektor(string name);

    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    private static extern ulong SendenZahl(IntPtr empfaenger, IntPtr selektor);

    /// <summary>true sichtbar, false verdeckt, null unbekannt.</summary>
    public static bool? Abfragen()
    {
        try
        {
            if (_nsApp == IntPtr.Zero)
            {
                _nsApp = NativeLibrary.GetExport(NativeLibrary.Load(AppKit), "NSApp");
                _auswahl = Selektor("occlusionState");
            }
            var app = Marshal.ReadIntPtr(_nsApp);
            if (app == IntPtr.Zero)
            {
                return null;
            }
            return (SendenZahl(app, _auswahl) & Sichtbar) != 0;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
