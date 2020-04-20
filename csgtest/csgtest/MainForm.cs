using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace csgtest
{
  public partial class MainForm : Form
  {
    public MainForm()
    {
      this.Text = $"CSG Test {IntPtr.Size << 3} Bit {(COM.DEBUG ? "Debug" : "Release")}";
      this.AutoScaleDimensions = new SizeF(8F, 16F);
      this.AutoScaleMode = AutoScaleMode.Font;
      this.ClientSize = new Size(800, 450);
      this.StartPosition = FormStartPosition.WindowsDefaultBounds;
      this.DoubleBuffered = true;

      var ca1 = new CheckBox() { Text = "Correct T-Junctions", Location = new Point(32, 380), Width = 500, Checked = GLU.Tesselator.correctTJunctions };
      this.Controls.Add(ca1); ca1.Click += (p, e) => { GLU.Tesselator.correctTJunctions = ca1.Checked; min1 = long.MaxValue; Invalidate(); };
      var ca2 = new CheckBox() { Text = "Outlines", Location = new Point(32, 380 + 24), Width = 500, Checked = gluoutlines };
      this.Controls.Add(ca2); ca2.Click += (p, e) => { gluoutlines = ca2.Checked; min1 = long.MaxValue; Invalidate(); };

      var cb2 = new CheckBox() { Text = "Delaunay optimized", Location = new Point(32 + 500 + 32, 380), Width = 500, Checked = delauny };
      this.Controls.Add(cb2); cb2.Click += (p, e) => { delauny = cb2.Checked; min2 = long.MaxValue; Invalidate(); };
      var cb3 = new CheckBox() { Text = "Outlines", Location = new Point(32 + 500 + 32, 380 + 1 * 24), Width = 500, Checked = outlines };
      this.Controls.Add(cb3); cb3.Click += (p, e) => { outlines = cb3.Checked; min2 = long.MaxValue; Invalidate(); };
      //var cb4 = new CheckBox() { Text = "Rational", Location = new Point(32 + 500 + 32, 380 + 2 * 24), Width = 500 };
      //this.Controls.Add(cb4); cb4.Click += (p, e) => { csgtess = CSG.Factory.CreateTessalator(cb4.Checked ? CSG.Unit.Rational : CSG.Unit.Double); min2 = long.MaxValue; Invalidate(); };
      var cb5 = new Button() { Text = "CSG Demo...", Location = new Point(32 + 500 + 32, 380 + 3 * 24), Width = 150 };
      this.Controls.Add(cb5); cb5.Click += (p, e) => new TestCSGForm().ShowDialog(this); 
    }

    static PointF[] polygon = new PointF[3];
    static Matrix mat1 = new Matrix(1, 0, 0, 1, 32, 32);
    static Matrix mat2 = new Matrix(1, 0, 0, 1, 32 + 500 + 32, 32);
    static Stopwatch sw = new Stopwatch();
    static Pen penl = new Pen(Color.Black, 2);
    long min1 = long.MaxValue, min2 = long.MaxValue; bool delauny = true, outlines = true, gluoutlines = true;
    CSG.ITesselator csgtess = CSG.Factory.CreateTessalator(CSG.Unit.Double);

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
        var tess = csgtess; var ver = CSG.Factory.Version;
        gr.DrawString($"CSG Tesselator {((ver & 0x100) != 0 ? "Debug" : "Release")} Build", SystemFonts.MenuFont, Brushes.Black, new PointF(0, -25));
        tess.Mode = CSG.Mode.Positive | (delauny ? CSG.Mode.Fill : CSG.Mode.FillFast) | (outlines ? CSG.Mode.Outline : 0) | CSG.Mode.NoTrim;
        sw.Restart();
        demo1(tess);
        sw.Stop();
        for (int i = 0; i < tess.IndexCount; i += 3)
        {
          polygon[0] = (PointF)tess.GetVertex(tess.IndexAt(i + 0));
          polygon[1] = (PointF)tess.GetVertex(tess.IndexAt(i + 1));
          polygon[2] = (PointF)tess.GetVertex(tess.IndexAt(i + 2));
          gr.FillPolygon(Brushes.LightGray, polygon);
        }
        for (int i = 0; i < tess.IndexCount; i += 3)
        {
          polygon[0] = (PointF)tess.GetVertex(tess.IndexAt(i + 0));
          polygon[1] = (PointF)tess.GetVertex(tess.IndexAt(i + 1));
          polygon[2] = (PointF)tess.GetVertex(tess.IndexAt(i + 2));
          gr.DrawLine(Pens.Gray, polygon[0], polygon[1]);
          gr.DrawLine(Pens.Gray, polygon[1], polygon[2]);
          gr.DrawLine(Pens.Gray, polygon[2], polygon[0]);
        }
        for (int i = 0, l = 0; i < tess.OutlineCount; i++)
        {
          var t1 = tess.OutlineAt(i); var last = (t1 & 0x40000000) != 0;
          var t2 = tess.OutlineAt(last ? l : i + 1); if (last) l = i + 1;
          gr.DrawLine(penl, (PointF)tess.GetVertex(t1 & 0x00ffffff), (PointF)tess.GetVertex(t2 & 0x00ffffff));
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
