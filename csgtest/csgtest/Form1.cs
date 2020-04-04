using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace csgtest
{
  public partial class Form1 : Form
  {
    public Form1()
    {
      InitializeComponent();
      this.Text = $"CsgTest {IntPtr.Size << 3} Bit {(Program.DEBUG ? "Debug" : "Release")}";
      this.DoubleBuffered = true;
      var ca1 = new CheckBox() { Text = "Correct T-Junctions", Location = new Point(32, 380), Width = 500, Checked = GLU.Tesselator.correctTJunctions };
      this.Controls.Add(ca1); ca1.Click += (p, e) => { GLU.Tesselator.correctTJunctions = ca1.Checked; min1 = long.MaxValue; Invalidate(); };
      var ca2 = new CheckBox() { Text = "Outlines", Location = new Point(32, 380 + 24), Width = 500, Checked = gluoutlines };
      this.Controls.Add(ca2); ca2.Click += (p, e) => { gluoutlines = ca2.Checked; min1 = long.MaxValue; Invalidate(); };

      var cb2 = new CheckBox() { Text = "Delaunay optimized", Location = new Point(32 + 500 + 32, 380), Width = 500, Checked = delauny };
      this.Controls.Add(cb2); cb2.Click += (p, e) => { delauny = cb2.Checked; min2 = long.MaxValue; Invalidate(); };
      var cb3 = new CheckBox() { Text = "Outlines", Location = new Point(32 + 500 + 32, 380 + 24), Width = 500, Checked = outlines };
      this.Controls.Add(cb3); cb3.Click += (p, e) => { outlines = cb3.Checked; min2 = long.MaxValue; Invalidate(); };
    }

    static PointF[] polygon = new PointF[3];
    static Matrix mat1 = new Matrix(1, 0, 0, 1, 32, 32);
    static Matrix mat2 = new Matrix(1, 0, 0, 1, 32 + 500 + 32, 32);
    static Stopwatch sw = new Stopwatch();
    static Pen penl = new Pen(Color.Black, 2);
    long min1 = long.MaxValue, min2 = long.MaxValue; bool delauny = true, outlines = true, gluoutlines = true;

    protected override void OnMouseMove(MouseEventArgs e)
    {
      Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
      base.OnPaint(e);
      foreach (var p in Controls.Cast<Control>()) p.Update();
      var gr = e.Graphics;
      gr.SmoothingMode = SmoothingMode.AntiAlias;
      gr.Transform = mat1;
      {
        var tess = GLU.Tesselator;
        gr.DrawString($"GLU Tesselator", SystemFonts.MenuFont, Brushes.Black, new PointF(0, -25));
        tess.SetWinding(GLU.Winding.Positive);
        sw.Restart();
        demo1(tess);
        sw.Stop();
        for (int i = 0; i < tess.IndexCount; i += 3)
        {
          polygon[0] = (PointF)tess.VertexAt(tess.IndexAt(i + 0) & 0x00ffffff);
          polygon[1] = (PointF)tess.VertexAt(tess.IndexAt(i + 1) & 0x00ffffff);
          polygon[2] = (PointF)tess.VertexAt(tess.IndexAt(i + 2) & 0x00ffffff);
          gr.FillPolygon(Brushes.LightGray, polygon);
        }
        for (int i = 0; i < tess.IndexCount; i += 3)
        {
          polygon[0] = (PointF)tess.VertexAt(tess.IndexAt(i + 0) & 0x00ffffff);
          polygon[1] = (PointF)tess.VertexAt(tess.IndexAt(i + 1) & 0x00ffffff);
          polygon[2] = (PointF)tess.VertexAt(tess.IndexAt(i + 2) & 0x00ffffff);
          gr.DrawLine(/*(tess.IndexAt(i + 0) & 0x10000000) != 0 ? penl :*/ Pens.Gray, polygon[0], polygon[1]);
          gr.DrawLine(/*(tess.IndexAt(i + 1) & 0x10000000) != 0 ? penl :*/ Pens.Gray, polygon[1], polygon[2]);
          gr.DrawLine(/*(tess.IndexAt(i + 2) & 0x10000000) != 0 ? penl :*/ Pens.Gray, polygon[2], polygon[0]);
        }
        if (gluoutlines)
        {
          sw.Start();
          tess.SetBoundaryOnly(true);
          demo1(tess);
          tess.SetBoundaryOnly(false);
          sw.Stop();
          for (int i = 0, l = 0; i < tess.IndexCount; i++)
          {
            var t1 = tess.IndexAt(i); var last = (t1 & 0x40000000) != 0;
            var t2 = tess.IndexAt(last ? l : i + 1); if (last) l = i + 1;
            gr.DrawLine(penl, (PointF)tess.VertexAt(t1 & 0x00ffffff), (PointF)tess.VertexAt(t2 & 0x00ffffff));
          }
        }
        gr.DrawString($"{sw.ElapsedMilliseconds} ms min: {min1 = Math.Min(min1, sw.ElapsedMilliseconds)} ms",
          SystemFonts.MenuFont, Brushes.Black, new PointF(0, 310));
      }
      gr.Transform = mat2;
      {
        var tess = CSG.Tesselator;
        var ver = tess.Version;
        gr.DrawString($"CSG Tesselator {((ver & 0x100) != 0 ? "Debug" : "Release")} Build", SystemFonts.MenuFont, Brushes.Black, new PointF(0, -25));
        tess.Mode = CSG.Mode.Positive | (delauny ? CSG.Mode.Fill : CSG.Mode.FillFast) | (outlines ? CSG.Mode.Outline : 0) | CSG.Mode.NoTrim;
        sw.Restart();
        demo1(tess);
        sw.Stop();
        for (int i = 0; i < tess.IndexCount; i += 3)
        {
          polygon[0] = (PointF)tess.VertexAt(tess.IndexAt(i + 0));
          polygon[1] = (PointF)tess.VertexAt(tess.IndexAt(i + 1));
          polygon[2] = (PointF)tess.VertexAt(tess.IndexAt(i + 2));
          gr.FillPolygon(Brushes.LightGray, polygon);
        }
        for (int i = 0; i < tess.IndexCount; i += 3)
        {
          polygon[0] = (PointF)tess.VertexAt(tess.IndexAt(i + 0));
          polygon[1] = (PointF)tess.VertexAt(tess.IndexAt(i + 1));
          polygon[2] = (PointF)tess.VertexAt(tess.IndexAt(i + 2));
          gr.DrawLine(Pens.Gray, polygon[0], polygon[1]);
          gr.DrawLine(Pens.Gray, polygon[1], polygon[2]);
          gr.DrawLine(Pens.Gray, polygon[2], polygon[0]);
        }
        for (int i = 0, l = 0; i < tess.OutlineCount; i++)
        {
          var t1 = tess.OutlineAt(i); var last = (t1 & 0x40000000) != 0;
          var t2 = tess.OutlineAt(last ? l : i + 1); if (last) l = i + 1;
          gr.DrawLine(penl, (PointF)tess.VertexAt(t1 & 0x00ffffff), (PointF)tess.VertexAt(t2 & 0x00ffffff));
        }
        gr.DrawString($"{sw.ElapsedMilliseconds} ms min: {min2 = Math.Min(min2, sw.ElapsedMilliseconds)} ms",
          SystemFonts.MenuFont, Brushes.Black, new PointF(0, 310));
      }
    }

    static void demo1(CSG.ITesselator tess)
    {
      for (int i = 0; i < 20; i++)
      {
        tess.BeginPolygon();
        tess.BeginContour();
        tess.AddVertex(0, 0); tess.AddVertex(500, 0); tess.AddVertex(500, 300); tess.AddVertex(0, 300);
        tess.EndContour();
        tess.BeginContour();
        tess.AddVertex(20, 20); tess.AddVertex(20, 80); tess.AddVertex(80, 80); tess.AddVertex(80, 20);
        tess.EndContour();
        for (int x = 0; x < 10; x++)
        {
          var xx = 30 + x * 60;
          for (int y = 0; y < 5; y++)
          {
            var yy = 30 + y * 60;
            tess.BeginContour();
            for (int t = 0; t < 20; t++)
            {
              var a = -t * (2 * Math.PI / 20);
              tess.AddVertex(xx + Math.Cos(a) * 20, yy + Math.Sin(a) * 20);
            }
            tess.EndContour();
          }
        }
        tess.EndPolygon();
      }
    }
  }
}
