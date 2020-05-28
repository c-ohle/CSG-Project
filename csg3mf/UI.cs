using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace csg3mf
{
  public class UIForm : Form
  {
    internal UIForm()
    {
      mainframe = this;
      BackColor = Color.FromArgb(120, 134, 169);
      Application.Idle += (p, e) =>
      {
        for (int i = 0; i < Controls.Count; i++)
        {
          var frame = Controls[i] as Frame; if (frame == null) continue;
          var ctrls = frame.Controls;
          for (int k = 0; k < ctrls.Count; k++)
          {
            var t = ctrls[k]; if (!t.Visible) continue;
            if (t is ICommandTarget c) c.OnCommand(0, this); break;
          }
        }
      };
    }
    protected virtual int OnCommand(int id, object test)
    {
      if (activeframe != null)
        if (activeframe.ActiveControl is ICommandTarget c)
        {
          var x = c.OnCommand(id, test);
          if (x != 0) return x;
        }
      //for (int i = 0; i < Controls.Count; i++)
      //{
      //  var p = Controls[i] as Frame; if (p == null || p == activeframe) continue;
      //  for (int k = 0; k < p.Controls.Count; k++)
      //  {
      //    var t = p.Controls[k]; if (!t.Visible) continue;
      //    if (!(t is IComparable<(int id, object test)> c)) break;
      //    var x = c.CompareTo((id, test));
      //    if (x != 0) return x;
      //    break;
      //  }
      //}
      return 0;
    }
    public static Control ShowView(Type t, object tag = null, DockStyle prefdock = DockStyle.Right, int prefsize = 0)
    {
      Frame dockat = null; int firstframe = 0;
      for (int i = mainframe.Controls.Count - 1; i >= 0; i--)
      {
        var p = mainframe.Controls[i] as Frame; if (p == null) { firstframe = i; continue; }
        if (dockat == null && p.Dock == prefdock) dockat = p;
        for (int k = 0; k < p.Controls.Count; k++)
        {
          var v = p.Controls[k];
          if (v.GetType() == t && v.Tag == tag) { p.activate(k); return v; }
        }
      }
      if (dockat == null)
      {
        dockat = new Frame { Dock = prefdock };
        if (prefdock == DockStyle.Left || prefdock == DockStyle.Right) dockat.Width = prefsize != 0 ? prefsize : mainframe.ClientSize.Width / 4;
        else dockat.Height = prefsize != 0 ? prefsize : mainframe.ClientSize.Height / 4;
        mainframe.SuspendLayout();
        mainframe.Controls.Add(dockat); 
        mainframe.Controls.SetChildIndex(dockat, prefdock == DockStyle.Fill ? 0 : firstframe);
        mainframe.ResumeLayout(); mainframe.PerformLayout();
      }
      var ctrl = (Control)Activator.CreateInstance(t); ctrl.Tag = tag; ctrl.Visible = false;
      dockat.Controls.Add(ctrl); dockat.PerformLayout();
      dockat.activate(dockat.Controls.Count - 1); //dockat.ActiveControl.Update(); 
      return ctrl;
    }
    int TryCommand(object sender, int id, object test)
    {
      try
      {
        if (sender != null)
        {
          if (sender is ICommandTarget c)
          {
            var x = c.OnCommand(id, test);
            if (x != 0) return x;
          }
          return 0;
        }
        return OnCommand(id, test);
      }
      catch (Exception e)
      {
        if (e.GetType().IsNestedPrivate) throw;
        if (test != null) { Debug.WriteLine(e); return -1; }
        MessageBox.Show(e.Message, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error); return -1;
      }
    }
    static UIForm mainframe; static Frame activeframe;
    static int rand = 8, title = 28; static Point sp; static Rectangle sr;
    static Brush brush1 = new SolidBrush(Color.FromArgb(80, 85, 130));
    static Brush brush2 = new SolidBrush(Color.FromArgb(176, 179, 208));
    public class Frame : UserControl
    {
      Rectangle rc; int wo;
      public Frame() { DoubleBuffered = true; }
      protected override void WndProc(ref Message m)
      {
        if (m.Msg == 0x21 && activeframe != this) Invalidate(); //WM_MOUSEACTIVATE
        base.WndProc(ref m);
      }
      protected override void OnPaint(PaintEventArgs e)
      {
        var gr = e.Graphics;
        var ctrls = Controls; var font = SystemFonts.MenuFont;
        if (/*ContainsFocus*/ mainframe.ActiveControl == this && activeframe != this) { if (activeframe != null) activeframe.Invalidate(); activeframe = this; }
        var focus = activeframe == this;
        if (Dock == DockStyle.Fill)
        {
          for (int i = 0, x = rc.X, n = ctrls.Count; i < n; i++)
          {
            var p = ctrls[i]; var s = p.Text; bool active = p.Visible || (wo & ~2) == ((i << 8) | 1);
            var dx = Math.Min(rc.Width - x, Math.Max(128, Math.Min(250, TextRenderer.MeasureText(s, font).Width + 10)));
            gr.FillRectangle(!active ? brush1 : p.Visible ? (focus ? Brushes.NavajoWhite : SystemBrushes.GradientInactiveCaption) : brush2, new Rectangle(x, rc.Top - title, dx, title));
            if (i != 0) gr.FillRectangle(Brushes.Gray, new Rectangle(x, rc.Top - title, 1, title));
            TextRenderer.DrawText(gr, s, font, new Rectangle(x + 3, rc.Top - title, dx - title, title), active ? Color.Black : Color.LightGray,
              TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
            if (active) TextRenderer.DrawText(gr, "🗙", font, new Rectangle(x + dx - title, rc.Y - title - 2, title, title),
              p.Visible ?
                (wo == ((i << 8) | 3) ? Color.Gray : Color.FromArgb(0xbb, 0xbb, 0xbb)) :
                (wo == ((i << 8) | 3) ? Color.Gray : Color.LightGray), TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            x += dx; p.TabIndex = Math.Max(0, x);
          }
        }
        else
        {
          gr.FillRectangle(focus ? Brushes.NavajoWhite : brush1, rc.X, rc.Y - title, rc.Width, title);
          for (int i = 0, x = rc.X, n = ctrls.Count; i < n; i++)
          {
            var p = ctrls[i]; var s = p.Text; bool active = p.Visible;
            if (active) TextRenderer.DrawText(gr, s, font, new Rectangle(rc.X + 3, rc.Y - title, rc.Width - 6, title), focus ? Color.Black : Color.White, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(gr, "🗙", font, new Rectangle(rc.Right - title, rc.Y - title - 2, title, title),
              (wo & 2) != 0 ? Color.LightGray : Color.FromArgb(0xbb, 0xbb, 0xbb), TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            if (n == 1) break; active |= (wo & ~2) == ((i << 8) | 1);
            var dx = Math.Min(rc.Width - x, Math.Max(64, Math.Min(150, TextRenderer.MeasureText(s, font).Width + 10)));
            gr.FillRectangle(!active ? brush1 : p.Visible ? Brushes.White : brush2, new Rectangle(x, rc.Bottom, dx, title));
            if (i != 0) gr.FillRectangle(Brushes.Gray, new Rectangle(x, rc.Bottom + 2, 1, title));
            TextRenderer.DrawText(gr, s, font, new Rectangle(x, rc.Bottom, dx, title), active ? Color.Black : Color.LightGray, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            x += dx; p.TabIndex = Math.Max(0, x);
          }
        }
      }
      protected override void OnLayout(LayoutEventArgs levent)
      {
        // if (!IsHandleCreated) return;
        if (Parent == null) return;
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
        //Debug.WriteLine($"{kkkk++} wo {wo:x8} {Capture}");
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
            Parent.PerformLayout(); Parent.Update();//Update();
            return;
          }
          if ((wo & 0x8f) == 1)
          {
            if (dx * dx + dy * dy < 10) return;
            new DragFrame(this); return;
          }
        }
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
      protected override void OnMouseCaptureChanged(EventArgs e)
      {
        if (!Capture & (wo & 0x80) != 0) { wo = 0; Cursor = Cursors.Default; }
      }
      protected override void OnSizeChanged(EventArgs e)
      {
        base.OnSizeChanged(e); Invalidate();
      }
      internal void activate(int i)
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
        if (ctrls[i] is ICommandTarget p && p.OnCommand(65301, null) != 0) return;
        if (ctrls.Count <= 1) { Dispose(); return; }
        if (ctrls[i].Visible) { ctrls[i].Visible = false; ctrls[i + 1 < ctrls.Count ? i + 1 : i - 1].Visible = true; }
        ctrls[i].Dispose(); Invalidate();
      }
      class DragFrame : Form
      {
        int wo, newindex; Frame frame, drop; UIForm destform; int firstframe; DockStyle docnew;
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
          drop = null; docnew = 0; newindex = 0; destform = (UIForm)frame.FindForm();

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
              //destform.DoubleBuffered = true;
              destform.Controls.SetChildIndex(frame, firstframe);
              frame.Dock = docnew; frame.Size = Size;
              foreach (var t in destform.Controls.OfType<Control>()) { t.PerformLayout(); t.Invalidate(); }
              //destform.DoubleBuffered = false;
              return;
            }
            else
            {
              drop = new Frame { Dock = docnew, Size = Size };
              destform.Controls.Add(drop);
              destform.Controls.SetChildIndex(drop, firstframe + 1);
              foreach (var t in destform.Controls.OfType<Control>()) { t.PerformLayout(); t.Invalidate(); }
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
    public class MenuItem : ToolStripMenuItem
    {
      private int id; //internal static Func<int, object, int> CommandRoot;
      public MenuItem(string text, params ToolStripItem[] items)
      {
        Text = text; DropDownItems.AddRange(items);
      }
      public MenuItem(int id, string text, Keys keys = Keys.None)
      {
        this.id = id; Text = text; ShortcutKeys = keys;
      }
      protected override void OnDropDownShow(EventArgs e)
      {
        base.OnDropDownShow(e);
        var items = DropDownItems;
        Update(items);
      }
      public static void Update(ToolStripItemCollection items)
      {
        for (int i = 0; i < items.Count; i++)
        {
          var mi = items[i] as MenuItem;
          if (mi == null)
          {
            var bu = items[i] as Button;
            if (bu != null) bu.Enabled = (mainframe.TryCommand(bu.Tag, bu.id, bu) & 1) != 0; continue;
          }
          if (mi.id == 0) continue;
          var hr = mainframe.TryCommand(null, mi.id, mi); mi.Enabled = (hr & 1) != 0; mi.Checked = (hr & 2) != 0;
          if (!mi.HasDropDownItems) continue; mi.Visible = false;
          foreach (var e in mi.DropDownItems.OfType<ToolStripMenuItem>()) items.Insert(++i, new MenuItem(mi.id, e.Text) { Tag = e.Tag, Checked = e.Checked });
          mi.DropDownItems.Clear();
        }
      }
      protected override void OnDropDownClosed(EventArgs e)
      {
        base.OnDropDownClosed(e); var items = DropDownItems;
        for (int i = 0; i < items.Count; i++) { items[i].Enabled = true; if (items[i].Tag != null) items.RemoveAt(i--); }
      }
      protected override void OnClick(EventArgs e)
      {
        if (id != 0) mainframe.TryCommand(null, id, Tag);
      }
    }
    public class Button : ToolStripButton
    {
      internal int id;
      public Button(int id, string text, Image img)
      {
        this.id = id; Text = text; Image = img; DisplayStyle = ToolStripItemDisplayStyle.Image;
        AutoSize = false; Size = new Size(40, 40);
      }
      protected override void OnClick(EventArgs e) => mainframe.TryCommand(Tag, id, null);
    }
    public new class ContextMenu : ContextMenuStrip
    {
      internal ContextMenu() : base() { }
      internal ContextMenu(IContainer container) : base(container) { }
      protected override void OnOpening(CancelEventArgs e)
      {
        base.OnOpening(e);
        var v = Tag as CodeEditor; if (v != null) { Items.Clear(); v.OnContextMenu(Items); }
        MenuItem.Update(Items); e.Cancel = Items.Count == 0;
      }
    }
    public interface ICommandTarget
    {
      int OnCommand(int id, object test);
    }
  }
}
