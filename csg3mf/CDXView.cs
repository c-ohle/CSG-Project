using csg3mf.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static csg3mf.CDX;

namespace csg3mf
{
  class CodeView : UserControl
  {
    public CodeView()
    {
      Text = "Turbo# Editor";
      var tb1 = new ToolStrip()
      {
        Margin = new Padding(15),
        ImageScalingSize = new Size(24, 24),
        GripStyle = ToolStripGripStyle.Hidden
      };
      tb1.Items.Add(new MenuItem.Button(5011, "Run", Resources.run));
      tb1.Items.Add(new ToolStripSeparator());
      tb1.Items.Add(new MenuItem.Button(5010, "Run Debug", Resources.rund));
      tb1.Items.Add(new MenuItem.Button(5016, "Step", Resources.stepover));
      tb1.Items.Add(new MenuItem.Button(5015, "Step Into", Resources.stepin));
      tb1.Items.Add(new MenuItem.Button(5017, "Step Out", Resources.stepout));
      tb1.Items.Add(new ToolStripSeparator());
      tb1.Items.Add(new MenuItem.Button(5013, "Stop", Resources.stop));
      Controls.Add(new CodeEditor() { Dock = DockStyle.Fill });
      Controls.Add(tb1);
      //Application.Idle += (p, e) => { MenuItem.Update(tb1.Items); tb1.Update(); };
    }
  }

  class FrameForm : Form
  {
    internal FrameForm()
    {
      Icon = Icon.FromHandle(Native.LoadIcon(Marshal.GetHINSTANCE(GetType().Module), (IntPtr)32512));
      Text = "FrameForm";
      StartPosition = FormStartPosition.WindowsDefaultBounds;
      MainMenuStrip = new MenuStrip(); MenuItem.CommandRoot = TryCommand;
      MainMenuStrip.Items.AddRange(new ToolStripItem[]
      {
        new MenuItem("&File",
          new MenuItem(1000, "&New", Keys.Control | Keys.N),
          new MenuItem(1010, "&Open...", Keys.Control | Keys.O),
          new MenuItem(1020, "&Save", Keys.Control | Keys.S),
          new MenuItem(1025, "Save &as...", Keys.Control | Keys.Shift | Keys.S),
          new ToolStripSeparator(),
          new MenuItem(1040, "&Print...", Keys.Control | Keys.P),
          new ToolStripSeparator(),
          new MenuItem(1050, "Last Files"),
          new ToolStripSeparator(),
          new MenuItem(1100, "&Exit")),
        new MenuItem("&Edit",
          new MenuItem (2010, "&Undo", Keys.Back|Keys. Alt) { Visible = false },
          new MenuItem(2010, "&Undo", Keys.Z|Keys.Control ),
          new MenuItem(2011, "&Redo", Keys.Back|Keys.Control) { Visible = false },
          new MenuItem(2011, "&Redo", Keys.Y|Keys.Control ),
          new ToolStripSeparator(),
          new MenuItem(2020, "&Cut", Keys.X|Keys.Control ),
          new MenuItem(2030, "Cop&y", Keys.C|Keys.Control ),
          new MenuItem(2040, "&Paste", Keys.V|Keys.Control ),
          new MenuItem(2015, "Delete", Keys.Delete) { Visible = false },
          new ToolStripSeparator(),
          new MenuItem(2045, "Make Uppercase", Keys.U|Keys.Control|Keys.Shift ),
          new MenuItem(2046, "Make Lowercase", Keys.U|Keys.Control ),
          new MenuItem(2060, "Select &all", Keys.A|Keys.Control ),
          new ToolStripSeparator(),
          new MenuItem(2065, "&Find...", Keys.F|Keys.Control ),
          new MenuItem(2066, "Find forward", Keys.F3|Keys.Control ),
          new MenuItem(2067, "Find next", Keys.F3 ),
          new MenuItem(2068, "Find prev", Keys.F3|Keys.Shift ),
          new ToolStripSeparator(),
          new MenuItem(2088, "Rename...", Keys.R|Keys.Control ),
          new ToolStripSeparator(),
          new MenuItem(2062, "Toggle", Keys.T|Keys.Control ),
          new MenuItem(2063, "Toggle all", Keys.T|Keys.Shift|Keys.Control ),
          new ToolStripSeparator(),
          new MenuItem(5027, "Goto &Definition", Keys.F12 )
        ),
        new MenuItem("&Script",
          new MenuItem(5011, "&Run", Keys.F5|Keys.Control ),
          new ToolStripSeparator(),
          new MenuItem(5010, "Run &Debug", Keys.F5 ),
          new MenuItem(5016, "Ste&p", Keys.F10 ),
          new MenuItem(5015, "Step &Into", Keys.F11 ),
          new MenuItem(5017, "Step Ou&t", Keys.F11|Keys.Shift ),
          new ToolStripSeparator(),
          new MenuItem(5014, "&Compile", Keys.F6 ),
          new ToolStripSeparator(),
          new MenuItem(5020, "Toggle &Breakpoint", Keys.F9 ),
          new MenuItem(5021, "Delete All Breakpoints", Keys.F9|Keys.Shift|Keys.Control ),
          //new ToolStripSeparator(),
          //new MenuItem(5040 ,"Break at Exceptions"),
          new ToolStripSeparator(),
          new MenuItem(5013, "&Stop", Keys.F5|Keys.Shift )
        ),
        new MenuItem("&Extra",
          new MenuItem(5100, "&Format", Keys.E|Keys.Control ),
          new ToolStripSeparator(),
          new MenuItem(5110 , "Remove Unused Usings"),
          new MenuItem(5111 , "Sort Usings"),
          new ToolStripSeparator(),
          new MenuItem(1120, "Show &3MF..."),
          new MenuItem(5025 ,"Show &IL Code..."),
          new ToolStripSeparator(),
          new MenuItem(5105 , "&Protect...")
        ),
        new MenuItem("&View",
          new MenuItem(2300, "&Center", Keys.Alt | Keys.C),
          new ToolStripSeparator(),
          new MenuItem(2214, "Outlines"),
          new MenuItem(2210, "Bounding Box"),
          new MenuItem(2211, "Pivot"),
          new MenuItem(2213, "Wireframe"),
          new MenuItem(2212, "Normals"),
          new ToolStripSeparator(),
          new MenuItem(2220, "Shadows")),
        new MenuItem("&Settings",
          new MenuItem(3015, "Driver"),
          new ToolStripSeparator(),
          new MenuItem(3016, "Samples"),
          new ToolStripSeparator(),
          new MenuItem(3017, "Set as Default")),
        new MenuItem("&Help",
          new MenuItem(5050, "&Help", Keys.F1 ),
          new ToolStripSeparator(),
          new MenuItem(5070 , "&About...")
        ),
       });
      BackColor = Color.FromArgb(120, 134, 169);

      var frame1 = new Frame() { Dock = DockStyle.Left, Width = 350, };
      frame1.Controls.Add(new CodeView());
      frame1.Controls.Add(propgrid = new PropertyGrid() { Text = "Properties", Visible = false });

      var frame2 = new Frame() { Dock = DockStyle.Bottom, Height = 150, };
      frame2.Controls.Add(propgrid = new PropertyGrid());
      frame2.Controls.Add(new UserControl() { Text = "Long document text with description", BackColor = Color.WhiteSmoke, Visible = false });

      var frame3 = new Frame() { Dock = DockStyle.Bottom, Height = 150, };
      frame3.Controls.Add(propgrid = new PropertyGrid());
      //frame3.Controls.Add(new UserControl() { BackColor = Color.WhiteSmoke, Visible = false });

      var frame4 = new Frame() { Dock = DockStyle.Right, Width = 350, };
      frame4.Controls.Add(new CodeEditor() { Text = "Script" });// new UserControl() { BackColor = Color.WhiteSmoke, Visible = false });
      frame4.Controls.Add(propgrid = new PropertyGrid() { Text = "Properties", Visible = false });

      var frame5 = new Frame() { Dock = DockStyle.Fill };
      frame5.Controls.Add(new CodeEditor() { Text = "Script 1", Visible = false });
      frame5.Controls.Add(new CodeEditor() { Text = "Script 2" });
      frame5.Controls.Add(new CodeEditor() { Text = "Script 3", Visible = false });

      SuspendLayout();
      Controls.Add(frame5);
      Controls.Add(frame4);
      Controls.Add(frame3);
      Controls.Add(frame2);
      Controls.Add(frame1);
      Controls.Add(MainMenuStrip);
      //Controls.Add(new StatusBar { BackColor = Color.FromArgb(80, 85, 130) });
      ResumeLayout();

      //Application.Idle += Application_Idle;
    }

    //[DllImport("user32.dll")]//, CallingConvention = CallingConvention.Winapi)]
    //static extern IntPtr GetFocus();
    //Control GetFocusedControl() {  var p = GetFocus(); return p != IntPtr.Zero ? FromHandle(p) : null; }
    //Control focus;
    //void Application_Idle(object sender, EventArgs e)
    //{
    //  var p = GetFocusedControl(); if (p == focus) return;
    //  focus = p; Debug.WriteLine($"focus: {p}");
    //}

    PropertyGrid propgrid;
    static int rand = 7, title = 28; static Point sp; static Rectangle sr;
    static Brush brush1 = new SolidBrush(Color.FromArgb(80, 85, 130));
    static Brush brush2 = new SolidBrush(Color.FromArgb(176, 179, 208));

    class Frame : UserControl
    {
      Rectangle rc; int wo; 
      public Frame() { DoubleBuffered = true; }
      protected override void OnPaint(PaintEventArgs e)
      {
        var gr = e.Graphics;
        var ctrls = Controls; var font = SystemFonts.MenuFont;
        if (Dock == DockStyle.Fill)
        {
          for (int i = 0, x = rc.X, n = ctrls.Count; i < n; i++)
          {
            var p = ctrls[i]; var s = p.Text; bool active = p.Visible || (wo & ~2) == ((i << 8) | 1);
            var dx = Math.Min(rc.Width - x, Math.Max(128, Math.Min(250, TextRenderer.MeasureText(s, font).Width + 10)));
            gr.FillRectangle(!active ? brush1 : p.Visible ? Brushes.White : brush2, new Rectangle(x, rc.Top - title, dx, title));
            if (i != 0) gr.FillRectangle(Brushes.Gray, new Rectangle(x, rc.Top - title, 1, title));
            TextRenderer.DrawText(gr, s, font, new Rectangle(x + 3, rc.Top - title, dx - title, title), active ? Color.Black : Color.LightGray,
              TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
            if (active) TextRenderer.DrawText(gr, "🗙", font, new Rectangle(x + dx - title, rc.Y - title - 2, title, title),
              p.Visible ?
                (wo == ((i << 8) | 3) ? Color.Gray : Color.FromArgb(0xbb, 0xbb, 0xbb)) :
                (wo == ((i << 8) | 3) ? Color.Gray : Color.LightGray), TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            x += dx; //if (tabx == null || tabx.Length != n) tabx = new int[n]; tabx[i] = x;

            p.TabIndex = x;
          }
        }
        else
        {
          gr.FillRectangle(brush1, rc.X, rc.Y - title, rc.Width, title);
          for (int i = 0, x = rc.X, n = ctrls.Count; i < n; i++)
          {
            var p = ctrls[i]; var s = p.Text; bool active = p.Visible;
            if (active) TextRenderer.DrawText(gr, s, font, new Rectangle(rc.X + 3, rc.Y - title, rc.Width - 6, title), Color.White, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(gr, "🗙", font, new Rectangle(rc.Right - title, rc.Y - title - 2, title, title),
              (wo & 2) != 0 ? Color.LightGray : Color.FromArgb(0xbb, 0xbb, 0xbb), TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            if (n == 1) break; active |= (wo & ~2) == ((i << 8) | 1);
            var dx = Math.Min(rc.Width - x, Math.Max(64, Math.Min(150, TextRenderer.MeasureText(s, font).Width + 10)));
            gr.FillRectangle(!active ? brush1 : p.Visible ? Brushes.White : brush2, new Rectangle(x, rc.Bottom, dx, title));
            if (i != 0) gr.FillRectangle(Brushes.Gray, new Rectangle(x, rc.Bottom + 2, 1, title));
            TextRenderer.DrawText(gr, s, font, new Rectangle(x, rc.Bottom, dx, title), active ? Color.Black : Color.LightGray, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            x += dx; //if (tabx == null || tabx.Length != n) tabx = new int[n]; tabx[i] = x;
            p.TabIndex = x;
          }
        }
      }
      protected override void OnLayout(LayoutEventArgs levent)
      {
        if (!IsHandleCreated) return;
        var ctrls = Parent.Controls; int ff = 0;
        for (int i = ctrls.Count - 1; i >= 0; i--)
        {
          var p = ctrls[i] as Frame;
          if (p == null) continue; if (p == this) break;
          ff |= 1 << (int)p.Dock;
        }
        ctrls = Controls; var d = Dock;
        var rl = (ff & (1 << (int)DockStyle.Left)) == 0 || d == DockStyle.Right;
        var rt = (ff & (1 << (int)DockStyle.Top)) == 0 || d == DockStyle.Bottom;
        var rr = (ff & (1 << (int)DockStyle.Right)) == 0 || d == DockStyle.Left;
        var rb = (ff & (1 << (int)DockStyle.Bottom)) == 0 || d == DockStyle.Top;
        var rc = ClientRectangle;
        this.rc = Rectangle.FromLTRB(
            rc.Left + (rl ? rand : 0),
            rc.Top + (rt ? rand : 0) + title,
            rc.Right - (rr ? rand : 0),
            rc.Bottom - (rb ? rand : 0) - (Controls.Count > 1 && d != DockStyle.Fill ? title : 0));
        for (int i = 0; i < ctrls.Count; i++) ctrls[i].Bounds = this.rc;
      }
      protected override void OnMouseMove(MouseEventArgs e)
      {
        if (e.Button == MouseButtons.Left)
        {
          var p2 = Cursor.Position; var dx = p2.X - sp.X; var dy = p2.Y - sp.Y;
          if ((wo & 0x80) != 0)
          {
            switch (wo)
            {
              case 0x080: Width = sr.Width + dx; break;
              case 0x180: Height = sr.Height + dy; break;
              case 0x280: Width = sr.Width - dx; break;
              case 0x380: Height = sr.Height - dy; break;
            }
            Parent.PerformLayout(); Parent.Update();
            return;
          }
          if ((wo & 0x8f) == 1)
          {
            if (dx * dx + dy * dy < 10) return;
            new DragFrame(this); return;
          }
        }
        //Debug.WriteLine($"wo {wo:x8}"); 
        if (e.Button != 0) return;
        var wi = 0; var r = ClientRectangle; var p = e.Location; var ctrls = Controls;
        if (r.Contains(p))
        {
          switch (Dock)
          {
            case DockStyle.Left: if (p.X > r.Right - rand) wi = 0x080; break;
            case DockStyle.Top: if (p.Y > r.Bottom - rand) wi = 0x180; break;
            case DockStyle.Right: if (p.X < r.Left + rand) wi = 0x280; break;
            case DockStyle.Bottom: if (p.Y < r.Top + rand) wi = 0x380; break;
          }
          if (wi == 0)
          {
            if (Dock == DockStyle.Fill)
            {
              if (p.Y >= rc.Top - title && p.Y <= rc.Top)
                for (int i = 0; i < ctrls.Count; i++)
                  if (p.X < ctrls[i].TabIndex) { wi = (i << 8) | 1; if (p.X > ctrls[i].TabIndex - title) wi |= 2; break; }
            }
            else
            {
              if (p.Y >= rc.Top - title && p.Y <= rc.Top)
              {
                wi = (getactive() << 8) | 0x41;
                if (p.X > rc.Right - title) wi |= 2;
              }
              if (ctrls.Count > 1 && p.Y >= rc.Bottom && p.Y < rc.Bottom + title)
                for (int i = 0; i < ctrls.Count; i++)
                  if (p.X < ctrls[i].TabIndex) { wi = (i << 8) | 1; break; }
            }
          }
        }
        if (wo == wi) return; wo = wi; Invalidate();
        Capture = wo != 0; Cursor = (wo & 0x80) == 0 ? Cursors.Default : ((wo >> 8) & 1) == 0 ? Cursors.SizeWE : Cursors.SizeNS;
      }
      protected override void OnMouseDown(MouseEventArgs e)
      {
        sr = Bounds; sp = Cursor.Position;
        if ((wo & 0x8f) == 1) activate(wo >> 8);
      }
      protected override void OnMouseUp(MouseEventArgs e)
      {
        if (e.Button != MouseButtons.Left) return;
        if ((wo & 2) != 0) { var x = wo; wo = 0; Invalidate(); Capture = false; close(x >> 8); }
      }
      protected override void OnSizeChanged(EventArgs e)
      {
        base.OnSizeChanged(e); Invalidate();
      }
      void activate(int i)
      {
        var ctrls = Controls; Invalidate();
        for (int k = 0; k < ctrls.Count; k++) if (ctrls[k].Visible = k == i) ctrls[k].Focus();
      }
      int getactive()
      {
        var ctrls = Controls; for (int i = 0, n = ctrls.Count; i < n; i++) if (ctrls[i].Visible) return i; return -1;
      }
      void close(int i)
      {
        var ctrls = Controls;
        if (ctrls.Count <= 1) { Dispose(); return; }
        if (ctrls[i].Visible) { ctrls[i].Visible = false; ctrls[i + 1 < ctrls.Count ? i + 1 : i - 1].Visible = true; }
        ctrls[i].Dispose(); Invalidate();
      }
      class DragFrame : Form
      {
        int wo, newindex; Frame frame, drop; Form destform; int firstframe; DockStyle docnew;
        internal DragFrame(Frame p)
        {
          wo = (frame = p).wo; frame.wo = 0;
          FormBorderStyle = FormBorderStyle.None;
          ShowInTaskbar = false;
          Opacity = 0.5; BackColor = Color.LightBlue;
          StartPosition = FormStartPosition.Manual;
          Size = new Size(); // Bounds = frame.RectangleToScreen(frame.rc);
          Show(); Capture = true;
        }
        protected override bool ShowWithoutActivation => true;
        protected override void OnMouseMove(MouseEventArgs e)
        {
          var p2 = Cursor.Position; var dx = p2.X - sp.X; var dy = p2.Y - sp.Y;
          drop = null; docnew = 0; newindex = 0; destform = frame.FindForm();

          if ((wo & 0xff) == 1)
          {
            var pc = frame.PointToClient(p2);
            if (frame.ClientRectangle.Contains(pc))
            {
              var rc = frame.rc;
              var y1 = frame.Dock == DockStyle.Fill ? rc.Y - title : rc.Bottom;
              var y2 = frame.Dock == DockStyle.Fill ? rc.Y : rc.Bottom + title;
              if (pc.Y >= y1 && pc.Y <= y2)
              {
                var ctrls = frame.Controls;
                for (int i = 0; i < ctrls.Count; i++)
                  if (pc.X <= ctrls[i].TabIndex)
                  {
                    Bounds = frame.RectangleToScreen(Rectangle.FromLTRB(i != 0 ? ctrls[i - 1].TabIndex : rc.X, y1, ctrls[i].TabIndex, y2));
                    newindex = i + 1; return;
                  }
              }
            }
          }

          var rh = destform.RectangleToScreen(destform.ClientRectangle);
          if (rh.Contains(p2))
          {
            for (int i = destform.Controls.Count - 1; i >= 0; i--)
            {
              var p = destform.Controls[i]; if (p is Frame f) { firstframe = i; break; }
              switch (p.Dock)
              {
                case DockStyle.Left: rh = Rectangle.FromLTRB(rh.X + p.Width, rh.Y, rh.Right, rh.Bottom); break;
                case DockStyle.Top: rh = Rectangle.FromLTRB(rh.X, rh.Y + p.Height, rh.Right, rh.Bottom); break;
                case DockStyle.Right: rh = Rectangle.FromLTRB(rh.X, rh.Y, rh.Right - p.Width, rh.Bottom); break;
                case DockStyle.Bottom: rh = Rectangle.FromLTRB(rh.X, rh.Y, rh.Right, rh.Bottom - p.Height); break;
              }
            }
            if (p2.X > rh.Right - 32) { var d = Math.Min(frame.Width, rh.Width / 3); rh.X = rh.Right - d; rh.Width = d; Bounds = rh; docnew = DockStyle.Right; return; }
            if (p2.X < rh.Left + 32) { var d = Math.Min(frame.Width, rh.Width / 3); rh.Width = d; Bounds = rh; docnew = DockStyle.Left; return; }
            if (p2.Y > rh.Bottom - 32) { var d = Math.Min(frame.Height, rh.Height / 3); rh.Y = rh.Bottom - d; rh.Height = d; Bounds = rh; docnew = DockStyle.Bottom; return; }
            for (int i = 0; i < destform.Controls.Count; i++)
            {
              var p = destform.Controls[i];
              var rt = p.RectangleToScreen(p.ClientRectangle);
              if (!rt.Contains(p2)) continue;
              if (!(p is Frame f) || f == frame) break;
              Bounds = p.RectangleToScreen(f.rc); drop = f;
              return;
            }
          }
          var r = frame.RectangleToScreen(frame.rc); r.Offset(dx, dy); Bounds = r;
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
          Dispose();
          if (newindex != 0)
          {
            frame.Controls.SetChildIndex(frame.Controls[frame.getactive()], newindex - 1);
            frame.Invalidate(); return;
          }
          if (docnew != 0)
          {
            if ((wo & 0x40) != 0)
            {
              if (destform.Controls[firstframe] == frame && frame.Dock == docnew) return;
              destform.SuspendLayout();
              destform.Controls.SetChildIndex(frame, firstframe);
              frame.Dock = docnew; frame.Size = Size;
              foreach (var t in destform.Controls.OfType<Control>()) t.PerformLayout();
              destform.ResumeLayout(); destform.PerformLayout();
              return;
            }
            else
            {
              destform.SuspendLayout();
              drop = new Frame { Dock = docnew, Size = Size };
              drop.Parent = destform;
              destform.Controls.SetChildIndex(drop, firstframe);
              foreach (var t in destform.Controls.OfType<Control>()) t.PerformLayout();
              destform.ResumeLayout(); destform.PerformLayout();
            }
          }
          if (drop == null) return;
          var i = frame.getactive();
          var p = frame.Controls[i]; var n = frame.Controls.Count;
          if (n > 1) { frame.Controls[i + 1 < n ? i + 1 : i - 1].Visible = true; frame.Invalidate(); }
          p.Parent = drop; p.SendToBack(); if (n == 1) frame.Dispose();
          drop.activate(drop.Controls.Count - 1);
        }
      }
    }
    int TryCommand(int id, object test) { return 0; }
  }

}
