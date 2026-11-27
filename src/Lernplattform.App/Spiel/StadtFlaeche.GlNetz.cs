using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Rendering;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Spiel.Ton;
using Lernplattform.Core.Texte;
using static Avalonia.OpenGL.GlConsts;

namespace Lernplattform.Spiel;

/// <summary>
/// Engine festigen 3 (A 1.16.0, Weisung P12-395/P12-399): Teil von <see cref="StadtFlaeche"/> mit dem Netz auf der
/// Grafikkarte (<c>GlNetz</c>). Reiner Umzug aus StadtFlaeche.cs, nichts geaendert.
/// </summary>
public sealed partial class StadtFlaeche
{
    /// <summary>Ein Dreiecksnetz in Puffern der Grafikkarte (mit Vertex-Array, wo vorhanden).</summary>
    private sealed class GlNetz
    {
        private int _ecken;
        private int _indizes;
        private int _vao;
        private bool _nummern;

        public int Anzahl { get; private set; }

        /// <summary>
        /// Weisung P8-14b: mehrere Netze in einem, jede Ecke mit der Nummer ihres Netzes als zehnte Zahl
        /// (Attribut 3, aNummer), fuer einen Zeichenaufruf mit je einer Lage aus einem Feld.
        /// </summary>
        public static unsafe GlNetz LadenMitNummern(GlInterface gl, IReadOnlyList<Netz> netze)
        {
            var ecken = new List<float>();
            var indizes = new List<uint>();
            for (var n = 0; n < netze.Count; n++)
            {
                var basis = (uint)(ecken.Count / (Netz.Zahlen + 1));
                var quelle = netze[n].EckenFeld;
                for (var e = 0; e < quelle.Length; e += Netz.Zahlen)
                {
                    for (var k = 0; k < Netz.Zahlen; k++)
                    {
                        ecken.Add(quelle[e + k]);
                    }
                    ecken.Add(n);
                }
                foreach (var i in netze[n].IndizesFeld)
                {
                    indizes.Add(basis + i);
                }
            }
            var g = new GlNetz { Anzahl = indizes.Count, _nummern = true };
            if (gl.IsGenVertexArraysAvailable)
            {
                g._vao = gl.GenVertexArray();
                gl.BindVertexArray(g._vao);
            }
            g._ecken = gl.GenBuffer();
            gl.BindBuffer(GL_ARRAY_BUFFER, g._ecken);
            fixed (float* zeiger = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(ecken))
            {
                gl.BufferData(GL_ARRAY_BUFFER, new IntPtr(ecken.Count * sizeof(float)), new IntPtr(zeiger), GL_STATIC_DRAW);
            }
            g._indizes = gl.GenBuffer();
            gl.BindBuffer(GL_ELEMENT_ARRAY_BUFFER, g._indizes);
            fixed (uint* zeiger = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(indizes))
            {
                gl.BufferData(GL_ELEMENT_ARRAY_BUFFER, new IntPtr(indizes.Count * sizeof(uint)), new IntPtr(zeiger), GL_STATIC_DRAW);
            }
            g.Attribute(gl);
            if (gl.IsGenVertexArraysAvailable)
            {
                gl.BindVertexArray(0);
            }
            return g;
        }

        /// <summary>Welle 327: Zellen des Netzes (nur das Paket-Netz), null = immer ganz zeichnen.</summary>
        public IReadOnlyList<Netz.Zelle>? Zellen { get; private init; }

        public static unsafe GlNetz Laden(GlInterface gl, Netz netz)
        {
            var g = new GlNetz { Anzahl = netz.Indizes.Count, Zellen = netz.Zellen };
            if (gl.IsGenVertexArraysAvailable)
            {
                g._vao = gl.GenVertexArray();
                gl.BindVertexArray(g._vao);
            }
            g._ecken = gl.GenBuffer();
            gl.BindBuffer(GL_ARRAY_BUFFER, g._ecken);
            // Weisung P7-16: direkt aus dem Netz hochladen (keine Kopie als neues Feld)
            var ecken = netz.EckenFeld;
            fixed (float* zeiger = ecken)
            {
                gl.BufferData(GL_ARRAY_BUFFER, new IntPtr(ecken.Length * sizeof(float)), new IntPtr(zeiger), GL_STATIC_DRAW);
            }
            g._indizes = gl.GenBuffer();
            gl.BindBuffer(GL_ELEMENT_ARRAY_BUFFER, g._indizes);
            var indizes = netz.IndizesFeld;
            fixed (uint* zeiger = indizes)
            {
                gl.BufferData(GL_ELEMENT_ARRAY_BUFFER, new IntPtr(indizes.Length * sizeof(uint)), new IntPtr(zeiger), GL_STATIC_DRAW);
            }
            g.Attribute(gl);
            if (gl.IsGenVertexArraysAvailable)
            {
                gl.BindVertexArray(0);
            }
            return g;
        }

        private void Attribute(GlInterface gl)
        {
            var schritt = _nummern ? Lagen + sizeof(float) : Lagen;
            gl.VertexAttribPointer(0, 3, GL_FLOAT, 0, schritt, IntPtr.Zero);
            gl.VertexAttribPointer(1, 3, GL_FLOAT, 0, schritt, new IntPtr(3 * sizeof(float)));
            gl.VertexAttribPointer(2, 3, GL_FLOAT, 0, schritt, new IntPtr(6 * sizeof(float)));
            gl.EnableVertexAttribArray(0);
            gl.EnableVertexAttribArray(1);
            gl.EnableVertexAttribArray(2);
            if (_nummern)
            {
                gl.VertexAttribPointer(3, 1, GL_FLOAT, 0, schritt, new IntPtr(Netz.Zahlen * sizeof(float)));
                gl.EnableVertexAttribArray(3);
            }
            else if (_vao == 0)
            {
                // ohne Vertex-Array bliebe Attribut 3 vom Netz der Autos eingeschaltet (GlInterface kennt kein Abschalten)
                AttributAus(gl, 3);
            }
        }

        private static unsafe void AttributAus(GlInterface gl, uint stelle)
        {
            var f = gl.GetProcAddress("glDisableVertexAttribArray");
            if (f != IntPtr.Zero)
            {
                ((delegate* unmanaged[Stdcall]<uint, void>)f)(stelle);
            }
        }

        public void Binden(GlInterface gl)
        {
            if (_vao != 0)
            {
                gl.BindVertexArray(_vao);
                return;
            }
            gl.BindBuffer(GL_ARRAY_BUFFER, _ecken);
            gl.BindBuffer(GL_ELEMENT_ARRAY_BUFFER, _indizes);
            Attribute(gl);
        }

        public void Freigeben(GlInterface gl)
        {
            if (_vao != 0)
            {
                gl.DeleteVertexArray(_vao);
            }
            if (_ecken != 0)
            {
                gl.DeleteBuffer(_ecken);
            }
            if (_indizes != 0)
            {
                gl.DeleteBuffer(_indizes);
            }
            // Weisung P8-55 (W12): ein zweites Freigeben (Vorrat und Anzeige teilen Netze) tut nichts
            (_vao, _ecken, _indizes) = (0, 0, 0);
        }
    }
}
