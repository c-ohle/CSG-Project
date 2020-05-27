using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace csg3mf
{
  class CodeEditor : UserControl, IComparable<(int id, object test)>
  {
    static CodeEditor()
    {
      var font = new System.Drawing.Font("Consolas", 11); //"Courier New" 10
      CodeEditor.linehight = font.Height + 1;
      CodeEditor.charwidth = (TextRenderer.MeasureText("00", font) - TextRenderer.MeasureText("0", font)).Width;
      CodeEditor.font = font.ToHfont();
      sidedx = dpiscale(32); sidedu = dpiscale(12);
    }
    internal static int dpiscale(int i) { return CodeEditor.charwidth > 16 ? i * 2 : i; }
    static IntPtr font;
    static int charwidth;
    static int linehight;
    static int sidedx, sidedu;
    protected string text = string.Empty;
    protected byte[] lineflags;
    protected byte[] charcolor;
    protected int[] colormap;
    protected Rectangle Caret { get { return rcaret; } }
    int sela, selb, curticks, tipticks;
    bool careton, selloop;
    Rectangle rcaret; int lastx;
    int iundo, xundo; List<Action<CodeEditor>> undos = new List<Action<CodeEditor>>();
    static string search; bool _readonly;
    List<Range> ranges; const int tabwidth = 2;
    bool selvert; Point selva, selvb;
    public CodeEditor()
    {
      SetStyle(ControlStyles.Selectable, true);
      BackColor = SystemColors.Window;
      AutoScroll = true;
      DoubleBuffered = true;
    }
    public override bool Equals(object p)
    {
      //if (p is Archive) { Serialize((Archive)p); return true; }
      return base.Equals(p);
    }
    public override int GetHashCode()
    {
      return base.GetHashCode();
    }
    public int Tabwidth
    {
      get { return tabwidth; }
      //set { tabwidth = value; }
    }
    public void Select(int a)
    {
      Select(a, a);
    }
    public void Select(int a, int b)
    {
      if ((sela == a) && (selb == b) && !selvert) return;
      sela = a; selb = b; selvert = false; showcaret(); Invalidate(); OnSelChanged(); lastx = 0;
    }
    void Select(Point b, bool bonly)
    {
      __select(bonly ? selva : b, b);
    }
    public void ScrollVisible()
    {
      var size = ClientSize;
      if (lineflags == null) _updatelineflags();
      int line = selvert ? Math.Min(selvb.Y, lineflags.Length - 1) : LineFromPos(selb);
      if ((lineflags[line] & 1) == 1 && !selvert)
      {
        foreach (var range in ranges)
          if (range.hidden && range.a <= selb && range.b >= selb)
            range.hidden = false;
        _updatelineflags();
        _updatescrolls();
        Invalidate();
      }
      Update();

      int y = 4;// + line * linehight;
      for (int i = 0; i < line; i++) if ((lineflags[i] & 1) == 0) y += linehight;

      if (y + AutoScrollPosition.Y < 0)
      {
        AutoScrollPosition = new Point(-AutoScrollPosition.X, y);
      }
      else
        if (y + linehight + AutoScrollPosition.Y > size.Height)
      {
        AutoScrollPosition = new Point(-AutoScrollPosition.X, y + linehight - size.Height);
      }

      Update();
      if (rcaret.Width > 0)
      {
        if (rcaret.X < (sidedx + 16))
        {
          Invalidate();
          AutoScrollPosition = new Point(-AutoScrollPosition.X + rcaret.X - (sidedx + 16), -AutoScrollPosition.Y);
        }
        else
          if (rcaret.X + 16 > size.Width)
        {
          Invalidate();
          AutoScrollPosition = new Point(-AutoScrollPosition.X + (rcaret.X - size.Width + 100), -AutoScrollPosition.Y);
        }
      }
    }
    public int SelectionStart
    {
      get { return Math.Min(sela, selb); }
      set { Select(value, Math.Max(value, selb)); }
    }
    public int SelectionLength
    {
      get { return Math.Abs(sela - selb); }
      set { Select(sela, Math.Max(sela, Math.Min(sela + value, TextLength))); }
    }
    public int TextLength { get { return text.Length; } }
    public int LineCount { get { return lineflags.Length; } }
    public void Paste(string s)
    {
      Replace(s);
    }
    void Replace(string s)
    {
      if (selvert) { vreplace(s, false); return; };
      int a = SelMin, b = SelMax, n = b - a;
      undoex(e =>
      {
        var t = n != 0 ? e.text.Substring(a, n) : null;
        e.Replace(a, n, s ?? string.Empty);
        n = s != null ? s.Length : 0; s = t;
        e.Select(a + n); e.ScrollVisible();
      });
    }
    Point __line(int y)
    {
      int a = 0, b = 0, l = 0, n = text.Length;
      for (; ; b++) if (b == n || text[b] == '\n') { if (l++ == y || b == n) break; a = b + 1; }
      return new Point(l - 1 == y ? a : b, b);
    }
    void __select(Point va, Point vb)
    {
      if (selva == va && selvb == vb && selvert) return;
      selva = va; selvb = vb; selvert = true;
      var la = __line(va.Y); sela = la.X + Math.Min(va.X, la.Y - la.X);
      var lb = __line(vb.Y); selb = lb.X + Math.Min(vb.X, lb.Y - lb.X);
      showcaret(); Invalidate();
    }
    void vreplace(string s, bool ar)
    {
      int x1 = Math.Min(selva.X, selvb.X), x2 = Math.Max(selva.X, selvb.X);
      int y1 = Math.Min(selva.Y, selvb.Y), y2 = Math.Max(selva.Y, selvb.Y);
      var ss = ar ? s.Split('\n') : null; if (ar) y2 = y1 + ss.Length - 2;
      var list = new List<Action<CodeEditor>>();
      var nl = text.Count(c => c == '\n') + 1;
      if (y1 > nl) list.Add(undo(text.Length, 0, new string('\n', y1 - nl)));
      for (int y = y1; y <= y2; y++)
      {
        var l = __line(y); var si = ar ? ss[y - y1] : s;
        if (l.Y > l.X + x1) { list.Add(undo(l.X + x1, Math.Min(x2 - x1, l.Y - (l.X + x1)), si)); continue; }
        list.Add(undo(l.Y, 0, (l.X == text.Length && y != 0 ? "\n" : string.Empty) + new string(' ', (l.X + x1) - l.Y) + si));
      }
      var aa = list.ToArray(); var x = x1 + (ar ? ss[0].Length : s.Length);
      var a1 = selva; var a2 = new Point(x, y1);
      var b1 = selvb; var b2 = new Point(x, y2);
      undoex(e =>
      {
        for (int i = aa.Length; i-- != 0;) aa[i](e); Array.Reverse(aa);
        __select(a2, b2); var t = a2; a2 = a1; a1 = t; t = b2; b2 = b1; b1 = t; //Invalidate(); 
      });
    }
    string vcopy()
    {
      int x1 = Math.Min(selva.X, selvb.X), x2 = Math.Max(selva.X, selvb.X);
      int y1 = Math.Min(selva.Y, selvb.Y), y2 = Math.Max(selva.Y, selvb.Y);
      var sb = new StringBuilder();
      for (int y = y1; y <= y2; y++)
      {
        var l = __line(y); var c = l.Y > l.X + x1 ? Math.Min(x2 - x1, l.Y - (l.X + x1)) : 0;
        sb.Append(text, l.X + x1, c); sb.Append(' ', x2 - x1 - c); sb.Append('\n');
      }
      return sb.ToString();
    }
    protected void Replace(IEnumerable<Point> r, string s)
    {
      var a = r.OrderBy(p => p.X).Select(p => undo(p.X, p.Y, s)).ToArray(); undoex(a);
    }
    void undoex(Action<CodeEditor> p)
    {
      p(this); if (undos.Count > iundo) undos.RemoveRange(iundo, undos.Count - iundo);
      undos.Add(p); iundo++;
    }
    internal void undoex(Action<CodeEditor>[] a)
    {
      undoex(e => { for (int i = a.Length; i-- != 0;) a[i](e); Array.Reverse(a); });
    }
    internal static Action<CodeEditor> undo(int i, int n, string s)
    {
      return e =>
      {
        if (e.sela > i) if (e.sela >= i + n) e.sela += s.Length - n; else e.sela = Math.Min(e.sela, i + s.Length);
        if (e.selb > i) if (e.selb >= i + n) e.selb += s.Length - n; else e.selb = Math.Min(e.selb, i + s.Length);
        var t = e.text.Substring(i, n); e.Replace(i, n, s); n = s.Length; s = t;
      };
    }
    public bool ReadOnly
    {
      get { return _readonly; }
      set { if (_readonly == value) return; _readonly = value; }
    }
    public string EditText
    {
      get { return text; }
      set { text = value; }
    }
    public bool IsModified
    {
      get { return iundo != xundo; }
      set { if (!value) { xundo = iundo; } }
    }
    public void ClearUndo()
    {
      undos.Clear(); iundo = xundo = 0;
    }
    string GetSelectedText()
    {
      int a = Math.Min(sela, selb);
      int b = Math.Max(sela, selb);
      return text.Substring(a, b - a);
    }
    protected int SelMin { get { return SelectionStart; } }
    protected int SelMax { get { return Math.Max(sela, selb); } }
    public virtual int OnCommand(int id, object test)
    {
      switch (id)
      {
        case 2010: return OnUndo(test);
        case 2011: return OnRedo(test);
        case 2015: return OnClear(test);
        case 2020: return OnCut(test);
        case 2030: return OnCopy(test);
        case 2040: return OnPaste(test);
        case 2060: return OnSelectAll(test);
        case 2065: return OnFind(test);
        case 2066: return OnFindForward(test); //Strg F3  text="Find forward" 
        case 2067: return OnFindNext(test); //F3  text="Find next"  
        case 2068: return OnFindPrev(test); //Shift F3  text="Find prev" 
        case 2062: return OnToggle(test);
        case 2063: return OnToggleAll(test);
        case 2045: return OnUpper(test, true);
        case 2046: return OnUpper(test, false);
      }
      return 0;
    }
    int OnUndo(object test)
    {
      if (_readonly) return 0;
      if (iundo == 0) return 0;
      if (test != null) return 1;
      undos[--iundo](this);
      return 1;
    }
    int OnRedo(object test)
    {
      if (_readonly) return 0;
      if (iundo >= undos.Count) return 0;
      if (test != null) return 1;
      undos[iundo++](this);
      return 1;
    }
    int OnCut(object test)
    {
      if (_readonly) return 0;
      if (sela == selb) return 0;
      if (test != null) return 1;
      OnCopy(test); Replace(string.Empty);
      return 1;
    }
    int OnCopy(object test)
    {
      if (selvert ? selva == selvb : sela == selb) return 0;
      if (test != null) return 1;
      //Clipboard.SetText((selvert ? vcopy() : text.Substring(SelMin, SelMax - SelMin)).Replace("\n", "\r\n"));
      var data = new System.Windows.Forms.DataObject();
      data.SetData(DataFormats.Text, true, (selvert ? vcopy() : text.Substring(SelMin, SelMax - SelMin)).Replace("\n", "\r\n"));
      if (selvert) data.SetData("MSDEVColumnSelect", true, new object());
      Clipboard.SetDataObject(data, true); //var a1 = Clipboard.GetDataObject().GetFormats();
      return 1;
    }
    int OnPaste(object test)
    {
      if (_readonly) return 0;
      if (!Native.IsClipboardFormatAvailable(1)) return 0;
      if (test != null) return 1;
      var s = adjust(Clipboard.GetText());
      if (Clipboard.GetData("MSDEVColumnSelect") != null)
      {
        var y = LineFromPos(SelMin); var x = SelMin - GetLineStart(y);
        if (!selvert) __select(new Point(x, y), new Point(x, y));
        vreplace(s, true); return 1;
      }
      Replace(s); return 1;
    }
    int OnUpper(object test, bool upper)
    {
      if (_readonly) return 0;
      if (selvert ? selva == selvb : sela == selb) return 0;
      if (test != null) return 1;
      var s1 = selvert ? vcopy() : text.Substring(SelMin, SelMax - SelMin);
      var s2 = upper ? s1.ToUpper() : s1.ToLower(); if (s1 == s2) return 1;
      if (selvert) { var t3 = selva; var t4 = selvb; vreplace(s2, true); selva = t3; selvb = t4; return 1; }
      var t1 = sela; var t2 = selb; Replace(s2); Select(t1, t2);
      return 1;
    }
    protected static string adjust(string s)
    {
      return s.Replace("\r\n", "\n").Replace("\t", "  ");
    }
    int OnClear(object test)
    {
      if (_readonly) return 0;
      if (sela == selb)
      {
        if (selb == text.Length) return 0;
        if (test != null) return 1;
        selb++;// = text[selb] == '\n' ? selb + 2 : selb + 1;
      }
      if (test != null) return 1;
      Replace(string.Empty);
      return 0;
    }
    int OnSelectAll(object test)
    {
      if (test != null) return 1;
      Select(0, text.Length); return 1;
    }
    int OnFindForward(object test) //Strg F3
    {
      if ((search == null) && (sela == selb)) return 0x10;
      if (test != null) return 1;
      if (sela != selb) search = GetSelectedText();
      return OnFindNext(test);
    }
    int OnFindNext(object test) //F3
    {
      if (search == null) return 0x10;
      if (test != null) return 1;
      int t = Math.Max(sela, selb);

      int i = text.IndexOf(search, t);
      if (i < 0) i = text.IndexOf(search, 0);

      if (i >= 0)
      {
        Select(i, i + search.Length);
        ScrollVisible();
        return 1;
      }
      //MessageBeep(-1);
      return 1;
    }
    int OnFindPrev(object test) //Shift F3
    {
      if (search == null) return 0x10;
      if (test != null) return 1;

      int t = Math.Min(sela, selb);

      int last = -1;
      for (int i = t; ((i = text.IndexOf(search, i)) >= 0);) { last = i; i += search.Length; }
      for (int i = 0; ((i = text.IndexOf(search, i)) >= 0) && (i < t);) { last = i; i += search.Length; }

      if (last >= 0)
      {
        Select(last, last + search.Length); ScrollVisible();
        return 1;
      }
      //MessageBeep(-1);
      return 1;
    }
    int OnFind(object test)
    {
      if (test != null) return 1;
      Native.FindReplace.Dialog(this, search, (s, f) =>
      { search = s; if ((f & 0x0001) != 0) OnFindNext(null); else OnFindPrev(null); });
      return 1;
    }
    int OnToggleAll(object test)
    {
      if (test != null) return 1;
      bool hide = false; foreach (var range in ranges) if (!range.hidden) { hide = true; break; }
      ranges = null;
      _updateranges();
      foreach (var range in ranges) range.hidden = hide;
      _updatelineflags();
      _updatescrolls();
      Invalidate();
      return 1;
    }
    int OnToggle(object test)
    {
      Range range = null;
      for (int i = ranges.Count - 1, l = SelMax; i >= 0; i--)
      {
        range = ranges[i];
        if (range.a <= l && range.b >= l)
        {
          if (test != null) return range.hidden ? 3 : 1;
          range.hidden ^= true;
          _updatelineflags();
          _updatescrolls();
          Invalidate();
          return 1;
        }
      }
      return 0;
    }
    protected unsafe int LineFromPos(int t)
    {
      if (t > text.Length) t = text.Length; int l = 0; fixed (char* s = text) for (int i = 0; i < t; i++) if (s[i] == '\n') l++; return l;
    }
    protected unsafe int GetLineStart(int line)
    {
      if (line == 0) return 0; int j = 0;
      fixed (char* s = text)
        for (int l = 0, n = text.Length; j < n; j++)
        {
          if (l == line) return j;
          if (s[j] == '\n') l++;
        }
      return j;
    }
    protected unsafe int GetLineEnd(int line)
    {
      int j = 0;
      fixed (char* s = text)
        for (int l = 0, n = text.Length; j < n; j++)
        {
          if (s[j] == '\n')
          {
            if (l == line) break;// (j > 0) && (s[j - 1] == '\n') ? j - 1 : j;
            l++;
          }
        }
      return j;
    }
    protected unsafe int LineOffset(int line)
    {
      var ly = AutoScrollPosition.Y + 4; fixed (char* s = text)
        for (int ta = 0, tb = 0, tn = text.Length, li = 0; ; tb++)
          if (tb == tn || s[tb] == '\n')
          {
            if (li == line) { if ((lineflags[li] & 1) != 0) ly -= linehight; break; }
            if ((lineflags[li] & 1) == 0) ly += linehight;
            if (tb == tn) break;
            ta = tb + 1; li++;
          }
      return ly;
    }
    protected Rectangle TextBox(int i, int n = 0)
    {
      int l = LineFromPos(i), i0 = GetLineStart(l), x = AutoScrollPosition.X + sidedx;
      int a = x + (i - i0) * charwidth, b = x + (i + n - i0) * charwidth;
      var y = LineOffset(l); return new Rectangle(a, y, b - a, linehight);
    }
    protected int PosFromPoint(Point p)
    {
      for (int ta = 0, tb = 0, tn = text.Length, li = 0, lx = AutoScrollPosition.X + sidedx, ly = AutoScrollPosition.Y + 4; ; tb++)
        if (tb == tn || text[tb] == '\n')
        {
          if ((lineflags[li] & 1) == 0)
          {
            if (p.Y < ly) return ta;
            if (p.Y < ly + linehight)
            {
              for (int t = ta, x1 = 0; t < tb; t++)
              {
                var x2 = x1 + charwidth;
                if (p.X < lx + x1 + ((x2 - x1) >> 1)) return t;
                x1 = x2;
              }
              return tb;
            }
            ly += linehight;
          }
          if (tb == tn) break;
          ta = tb + 1; li++;
        }
      return text.Length;
    }
    protected Point WordFromPoint(int i)
    {
      if (i == text.Length) return Point.Empty;
      int a = i; for (; a > 0; a--) if (!IsWordChar(text[a - 1])) break;
      int b = i; for (; b < text.Length; b++) if (!IsWordChar(text[b])) break;
      return new Point(a, b);
    }
    protected static bool IsWordChar(char c)
    {
      return char.IsLetterOrDigit(c) || c == '_';
    }
    protected internal virtual void OnTextChanged() { }
    protected internal virtual void OnSelChanged() { if ((tipflags & 2) != 0) EndToolTip(); }
    protected internal virtual void OnContextMenu(ToolStripItemCollection items)
    {
      items.Add(new UIForm.MenuItem(2010, "&Undo"));
      items.Add(new UIForm.MenuItem(2011, "&Redo"));
      items.Add(new ToolStripSeparator());
      items.Add(new UIForm.MenuItem(2020, "&Cut"));
      items.Add(new UIForm.MenuItem(2030, "Cop&y"));
      items.Add(new UIForm.MenuItem(2040, "&Paste"));
      //items.Add(new ToolStripSeparator());
      //items.Add(new MenuItem { Text = "&Find...", Id = 2065, ShortcutKeys = Keys.F|Keys.Control }); 
      //items.Add(new MenuItem { Text = "Find forward", Id = 2066, ShortcutKeys = Keys.F3|Keys.Control }); 
      //items.Add(new MenuItem { Text = "Find next", Id = 2067, ShortcutKeys = Keys.F3 });
      //items.Add(new MenuItem { Text = "Find prev", Id = 2068, ShortcutKeys = Keys.F3 | Keys.Shift });
    }
    protected override void OnHandleCreated(EventArgs e)
    {
      _format(); _updateranges(); _updatelineflags();
      ContextMenuStrip = new UIForm.ContextMenu { Tag = this };
    }
    protected override void OnHandleDestroyed(EventArgs e)
    {
      ContextMenuStrip.Dispose(); EndToolTip();
    }
    protected unsafe override void OnPaint(PaintEventArgs e)
    {
      var text = this.text;
      var aspx = AutoScrollPosition.X;
      var g = e.Graphics;
      var rcc = g.ClipBounds;
      var rc = new Rectangle((int)rcc.Left, (int)rcc.Top, (int)rcc.Width + 1, (int)rcc.Height + 1);
      int sela = SelMin, selb = SelMax, lastcolor = 0; ;
      var hdc = g.GetHdc();
      var oldfont = Native.SelectObject(hdc, font);
      Native.SetBkMode(hdc, 1); //rcaret.Width = 0;

      fixed (char* s = text)
      {
        int tn = text.Length, lx = aspx + sidedx, asy = AutoScrollPosition.Y + 4;
        for (int ta = 0, tb = 0, li = 0, ly = asy; ; tb++)
          if (tb == tn || s[tb] == '\n')
          {
            if (ly > rc.Bottom) break;

            if ((lineflags[li] & 1) == 0)
            {
              if (ly + linehight > rc.Top)
              {
                //////////////////
                for (int aa = ta, bb = ta; ; bb++)
                  if (bb == tb || charcolor[aa] != charcolor[bb])
                  {
                    var ns = bb - aa; var cc = charcolor[aa];
                    var color = (lineflags[li] & 2) != 0 ? 0x808080 : colormap[cc & 0x0f];
                    var bk = (color >> 24) & 0x0f;
                    if (color != lastcolor) { lastcolor = color; Native.SetTextColor(hdc, bk == 0 ? color : bk == 1 ? 0xeeeeee : 0); }
                    if (bk != 0)
                    {
                      int i1 = lx + (aa - ta) * charwidth, i2 = lx + (bb - ta) * charwidth + 1;
                      Native.SetPixel(hdc, 0, 0, (uint)(color & 0xffffff));
                      Native.GdiAlphaBlend(hdc, i1, ly, i2 - i1, linehight, hdc, 0, 0, 1, 1, 0x00ff0000);
                    }
                    _textout(hdc, s, lx, ly, rc.Right, ta, aa, ns);
                    if ((cc >> 4) != 0)
                    {
                      int i1 = lx + (aa - ta) * charwidth, i2 = lx + (bb - ta) * charwidth + 1;
                      if ((cc & 0x80) != 0)
                      {
                        Native.SetPixel(hdc, 0, 0, 0x000000);
                        Native.GdiAlphaBlend(hdc, i1, ly, i2 - i1, linehight, hdc, 0, 0, 1, 1, 0x00100000);
                      }
                      if ((cc & 0x70) != 0)
                      {
                        var t1 = Native.SetTextColor(hdc, colormap[(cc >> 4) & 0x7] & 0xffffff); var dd = charwidth - dpiscale(4);
                        var yy = ly + dpiscale(9);
                        for (var c = '~'; i1 < i2 - dd; i1 += dd) Native.TextOutW(hdc, i1, yy, &c, 1);
                        Native.SetTextColor(hdc, t1);
                      }
                    }

                    if (bb == tb) break; aa = bb;
                  }
                //////////////////
                if (selb >= ta && sela <= tb && !selvert)
                {
                  int i1 = Math.Max(sela, ta), i2 = Math.Min(selb, tb);
                  Rectangle rr = Rectangle.FromLTRB(lx + (i1 - ta) * charwidth, ly, lx + (i2 - ta) * charwidth + 1, ly + linehight);
                  if (i1 != i2 || selb > i2) blends(hdc, rr.X, rr.Y, rr.Width + (selb > i2 ? dpiscale(8) : 0), rr.Height);
                  if (i1 == this.selb || i2 == this.selb) { rcaret = rr; if (i2 == this.selb) rcaret.X = rcaret.Right; rcaret.Width = 1; rcaret.X--; }
                }
              }
              ly += linehight;
            }
            if (tb == tn) break;
            ta = tb + 1; li++;
          }

        if (selvert)
        {
          if (selva != selvb)
          {
            int x1 = Math.Min(selva.X, selvb.X), y1 = Math.Min(selva.Y, selvb.Y), x2 = Math.Max(selva.X, selvb.X), y2 = Math.Max(selva.Y, selvb.Y);
            for (int t = Math.Min(y2, lineflags.Length - 1); t >= 0; t--) if ((lineflags[t] & 1) != 0) { if (y1 >= t) y1--; y2--; }
            blends(hdc, aspx + sidedx + x1 * charwidth, asy + y1 * linehight, Math.Max((x2 - x1) * charwidth, 2), (y2 - y1 + 1) * linehight);
          }
          var y = selvb.Y; for (int t = Math.Min(y, lineflags.Length - 1); t >= 0; t--) if ((lineflags[t] & 1) != 0) y--;
          rcaret = new Rectangle(aspx + sidedx + selvb.X * charwidth, asy + y * linehight, 1, linehight);
        }
        if (careton && Focused) { var rrc = new Rectangle(rcaret.X, rcaret.Y, rcaret.X + rcaret.Width, rcaret.Y + rcaret.Height); Native.InvertRect(hdc, ref rrc); }

        Native.SelectObject(hdc, oldfont);
        g.ReleaseHdc(hdc);

        g.FillRectangle(SystemBrushes.Control, Rectangle.FromLTRB(0, 0, sidedx - dpiscale(14), Height));//new Rectangle(0, 0, sidedx - 12, Height));
        g.FillRectangle(SystemBrushes.Window, Rectangle.FromLTRB(sidedx - dpiscale(14), 0, sidedx, Height));
        var pen = Pens.Gray;

        for (int ta = 0, tb = 0, li = 0, ly = asy + dpiscale(4), xx = sidedx - dpiscale(12), irang = 0; ; tb++)
          if (tb == tn || s[tb] == '\n')
          {
            if (ly > rc.Bottom) break;

            if ((lineflags[li] & 1) == 0)
            {
              if ((lineflags[li] & 4) != 0)
              {
                var plus = (lineflags[li] & 2) != 0; int yy = ly + dpiscale(2);
                if (ly + linehight > rc.Top)
                {
                  var rk = new Rectangle(xx, yy, dpiscale(8), dpiscale(8));
                  g.FillRectangle(plus ? Brushes.LightGray : SystemBrushes.Window, rk);
                  g.DrawRectangle(pen, rk);
                  g.DrawLine(pen, xx + dpiscale(2), yy + dpiscale(4), xx + dpiscale(6), yy + dpiscale(4));
                  if (plus) g.DrawLine(pen, xx + dpiscale(4), yy + dpiscale(2), xx + dpiscale(4), yy + dpiscale(6));
                }
                if (!plus)
                  for (; irang < ranges.Count; irang++)
                    if (ranges[irang].line == li)
                    {
                      int bis = ranges[irang].b, uu = yy + linehight + 5, ll = li;
                      for (int t = tb + 1; t < bis; t++) if (s[t] == '\n') if ((lineflags[++ll] & 1) == 0) uu += linehight;
                      if (uu > rc.Top) { g.DrawLine(pen, xx + dpiscale(4), yy + dpiscale(9), xx + dpiscale(4), uu); g.DrawLine(pen, xx + dpiscale(4), uu, xx + dpiscale(8), uu); }
                      break;
                    }
              }
              ly += linehight;
            }
            if (tb == tn) break;
            ta = tb + 2; li++;
          }
      }
    }
    void blends(IntPtr hdc, int x, int y, int dx, int dy)
    {
      Native.SetPixel(hdc, 0, 0, 0xff8000); if (x < 1) { dx += x - 1; x = 1; }
      Native.GdiAlphaBlend(hdc, x, y, dx, dy, hdc, 0, 0, 1, 1, Focused ? 0x00400000 : 0x00100000);
    }
    Point virtpos(Point p)
    {
      //var t1 = PosFromPoint(p); var t2 = LineFromPos(t1);
      var x = Math.Max(0, (p.X + (charwidth >> 1) - (AutoScrollPosition.X + sidedx)) / charwidth);
      var y = Math.Max(0, (p.Y - (AutoScrollPosition.Y + 4)) / linehight);
      for (int t = 0; t <= y && t < lineflags.Length; t++) if ((lineflags[t] & 1) != 0) y++;
      return new Point(x, y);
    }
    void showcaret()
    {
      if (Focused) { Native.SetTimer(Handle, IntPtr.Zero, 100, IntPtr.Zero); curticks = 0; careton = true; }
    }
    protected override void WndProc(ref Message m)
    {
      if (m.Msg == 0x0113) //WM_TIMER
      {
        if (curticks++ < 5) return; curticks = 0; careton ^= true; Invalidate(rcaret);
        if (tipticks != 0 && --tipticks == 0) ontip(); return;
      }
      if (m.Msg == 0x0005 || m.Msg == 0x0114) Invalidate(); // WM_SIZE, WM_HSCROLL
      base.WndProc(ref m);
    }
    protected override void OnScroll(ScrollEventArgs se)
    {
      if (se.ScrollOrientation == ScrollOrientation.VerticalScroll) rcaret.Offset(0, se.OldValue - se.NewValue);
      EndToolTip(); base.OnScroll(se); Update();
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
      base.OnMouseDown(e); if (HasChildren) Focus();
      var textpos = PosFromPoint(e.Location);
      if (e.Button == System.Windows.Forms.MouseButtons.Left)
      {
        if (e.X < sidedx - 3)
        {
          if (e.X > sidedx - dpiscale(13))
          {
            int l = LineFromPos(textpos);
            for (int i = 0; i < ranges.Count; i++)
              if (ranges[i].line == l)
              {
                ranges[i].hidden ^= true;
                _updatelineflags();
                _updatescrolls();
                Invalidate();
                break;
              }
          }
          else
          {
            int l = LineFromPos(textpos), x = GetLineStart(l), t1 = sela, t2 = selb;
            sela = selb = x; OnCommand(5020, null); //onbreakpoint
            if (sela == x && selb == x) { sela = t1; selb = t2; }
          }
          return;
        }

        selloop = true;
        if ((ModifierKeys & Keys.Alt) == Keys.Alt) { Select(virtpos(e.Location), false); sela = selb = textpos; }
        else Select(ModifierKeys == Keys.Shift ? sela : textpos, textpos);
      }
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
      int i = PosFromPoint(e.Location); //System.Diagnostics.Debug.WriteLine(i);
      if (Capture && selloop)
      {
        if (e.Button == System.Windows.Forms.MouseButtons.Left)
        {
          if (selvert) { Select(virtpos(e.Location), true); selb = i; } else Select(sela, i);
          ScrollVisible();
        }
      }
      Cursor = selloop || e.X >= sidedx - 3 ? Cursors.IBeam : Cursors.Arrow;
      base.OnMouseMove(e);
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
      selloop = false;
      base.OnMouseUp(e);
    }
    protected override void OnMouseLeave(EventArgs e)
    {
      Cursor = Cursors.Arrow;
    }
    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
      base.OnMouseDoubleClick(e);
      var r = WordFromPoint(PosFromPoint(e.Location)); if (r.X != r.Y) Select(r.X, r.Y);
    }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
      if (Native.WindowFromPoint(PointToScreen(e.Location)) != Handle) return;
      EndToolTip(); var a = AutoScrollPosition; base.OnMouseWheel(e); var b = AutoScrollPosition;
      rcaret.Offset(b.X - a.X, b.Y - a.Y); if (b.X != a.X) Invalidate();
    }
    protected override void OnGotFocus(EventArgs e)
    {
      Invalidate(); showcaret(); base.OnGotFocus(e);
    }
    protected override void OnLostFocus(EventArgs e)
    {
      Invalidate(); Native.KillTimer(Handle, IntPtr.Zero); base.OnLostFocus(e);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
      base.OnKeyDown(e); if (tipkey(e)) return;
      switch (e.KeyCode)
      {
        case Keys.Delete: OnClear(null); break;
        case Keys.Left:
          {
            var i = selb - 1;
            if (i >= 0 && (e.Modifiers & Keys.Control) != 0)
            {
              var o = char.IsLetterOrDigit(text[i]);
              for (; i > 0 && char.IsLetterOrDigit(text[i - 1]) == o; i--) ;
            }
            if ((e.Modifiers & Keys.Shift) != 0) Select(sela, Math.Max(i, 0));
            else Select(Math.Max(sela == selb ? i : Math.Min(sela, selb), 0));
            ScrollVisible();
          }
          break;
        case Keys.Right:
          {
            var i = selb + 1;
            if (i < text.Length && (e.Modifiers & Keys.Control) != 0)
            {
              var o = char.IsLetterOrDigit(text[i - 1]);
              for (; i < text.Length && char.IsLetterOrDigit(text[i]) == o; i++) ;
            }
            if ((e.Modifiers & Keys.Shift) != 0) Select(sela, Math.Min(i, text.Length));
            else Select(Math.Min(sela == selb ? i : Math.Max(sela, selb), text.Length));
            ScrollVisible();
          }
          break;
        case Keys.Up:
        case Keys.PageUp:
          {
            var dy = e.KeyCode == Keys.PageUp ? Height : linehight - 1;
            Point P = rcaret.Location; P.Y -= dy; if (lastx != 0) P.X = lastx;
            if ((e.Modifiers & Keys.Shift) != 0) Select(sela, PosFromPoint(P)); else Select(PosFromPoint(P));
            ScrollVisible(); lastx = P.X;
          }
          break;
        case Keys.Down:
        case Keys.PageDown:
          {
            var dy = e.KeyCode == Keys.PageDown ? Height : linehight + 1;
            Point P = rcaret.Location; P.Y += dy; if (lastx != 0) P.X = lastx;
            if ((e.Modifiers & Keys.Shift) != 0) Select(sela, PosFromPoint(P)); else Select(PosFromPoint(P));
            ScrollVisible(); lastx = P.X;
          }
          break;
        case Keys.Home:
          {
            var t = GetLineStart(LineFromPos(SelMin));
            if ((e.Modifiers & Keys.Shift) != 0) Select(SelMax, t); else Select(t); ScrollVisible();
          }
          break;
        case Keys.End:
          {
            var t = GetLineEnd(LineFromPos(SelMax));
            if ((e.Modifiers & Keys.Shift) != 0) Select(SelMin, t); else Select(t); ScrollVisible();
          }
          break;
      }
    }
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
      base.OnKeyPress(e);
      if (_readonly) return;
      if (e.KeyChar == (char)13)
      {
        var s = text;
        int a = SelMin; for (; (a > 0) && (s[a - 1] != '\n'); a--) ;
        int b = a; for (; b < s.Length && s[b] == ' '; b++) ;
        Replace("\n" + s.Substring(a, b - a)); return;
      }
      if (e.KeyChar == (char)8)
      {
        if (selvert)
        {
          if (selva.X == selvb.X) { if (selva.X == 0) return; selva.X--; }
        }
        else
        {
          if (sela == selb) { if (sela == 0) return; sela--; }
        }
        Replace(string.Empty); return;
      }
      if (e.KeyChar == '\t')
      {
        int x1 = selvert ? Math.Min(selva.X, selvb.X) : SelMin;
        int l1 = LineFromPos(x1), l2 = LineFromPos(SelMax);
        if (!selvert && l1 != l2)
        {
          int i1 = GetLineStart(l1), i2 = GetLineEnd(l2);
          var list = new List<Action<CodeEditor>>();
          var si = (ModifierKeys & Keys.Shift) != Keys.Shift ? new string(' ', tabwidth) : null;
          for (int y = l1; y <= l2; y++)
          {
            var l = __line(y);
            if (si != null) { list.Add(undo(l.X, 0, si)); i2 += si.Length; continue; }
            var t = 0; for (; t < tabwidth && l.X + t < l.Y && text[l.X + t] == ' '; t++) ;
            if (t != 0) { list.Add(undo(l.X, t, string.Empty)); i2 -= t; }
          }
          if (list.Count == 0) return;
          var aa = list.ToArray();
          var a1 = sela; var a2 = i1;
          var b1 = selb; var b2 = i2;
          undoex(ve =>
          {
            for (int i = aa.Length; i-- != 0;) aa[i](ve); Array.Reverse(aa);
            Select(a2, b2); var t = a2; a2 = a1; a1 = t; t = b2; b2 = b1; b1 = t;
          });
          return;
        }
        Replace(new string(' ', tabwidth - (x1 - GetLineStart(l1)) % tabwidth)); return;
      }
      if (!char.IsControl(e.KeyChar))
        Replace(e.KeyChar.ToString());
    }
    protected override bool IsInputKey(Keys keyData)
    {
      if ((keyData & (Keys.Control | Keys.Alt)) != 0) return false;
      return true;
    }
    Rectangle tiprect; int tipflags; Func<int, object> tipobj; ToolTip thetip;
    protected void EndToolTip()
    {
      tipticks = 0; tipflags = 0; tiprect = Rectangle.Empty; tipobj = null; if (thetip != null) { thetip.Dispose(); thetip = null; }
    }
    protected void SetToolTip(Rectangle r, Func<int, object> p, int flags = 0)
    {
      r.Inflate(4, 4); if ((flags & 2) == 0 && tiprect == r) return;
      if ((tipflags & 2) != 0) return;
      EndToolTip(); tiprect = r; tipobj = p; tipflags = flags; tipticks = 1; // Debug.WriteLine("SetToolTip " + p);
    }
    void ontip()
    {
      var r = RectangleToScreen(tiprect);
      var p = Cursor.Position; if ((tipflags & 2) == 0 && !r.Contains(p)) { EndToolTip(); return; }//Debug.WriteLine("ontip " + tipobj); 
      var s = tipobj(0) as string; if (string.IsNullOrEmpty(s)) return;
      if ((tipflags & 1) != 0) { var c = Cursor.Current; p.Y += c.Size.Height - c.HotSpot.Y - 12; p = PointToClient(p); } else p = new Point(tiprect.X, tiprect.Bottom);
      thetip = new ToolTip { ToolTipTitle = tipobj(1) as string, };
      thetip.SetToolTip(this, s); thetip.Show(s, this, p);
    }
    bool tipkey(KeyEventArgs e)
    {
      if ((tipflags & 2) == 0) return false;
      switch (e.KeyCode)
      {
        case Keys.Up:
        case Keys.Down:
          tipobj((int)e.KeyCode); if (thetip == null) break; thetip.ToolTipTitle = tipobj(1) as string;
          thetip.Show(tipobj(0) as string, this, new Point(tiprect.X, tiprect.Bottom));
          return true;
      }
      EndToolTip(); return false;
    }
    protected virtual int GetRange(int x)
    {
      return 0;
    }
    protected virtual void UpdateSyntaxColors()
    {
      Array.Resize(ref colormap, 1); colormap[0] = 0;
      Array.Resize(ref charcolor, text.Length + 1);
    }
    protected virtual void Replace(int i, int n, string s)
    {
      text = text.Substring(0, i) + s + text.Substring(i + n, text.Length - (i + n));
      if (ranges != null)
      {
        int ns = s.Length; bool bigcheck = s.Length != 1 || n > 0 || !char.IsLetterOrDigit(s[0]);
        for (int t = 0; t < ranges.Count; t++)
        {
          var range = ranges[t];
          if (range.b <= i) continue;
          if (bigcheck) range.line = -1;
          if (range.a >= i + n) { range.a += ns - n; range.b += ns - n; continue; }
          if (range.a <= i && range.b > i + n) { range.b += ns - n; continue; }
          ranges.RemoveAt(t--);
        }
        if (bigcheck) _updateranges();
      }
      OnTextChanged(); _format();
    }
    void _format()
    {
      sela = Math.Min(sela, text.Length);
      selb = Math.Min(selb, text.Length);
      _updatelineflags();
      _updatescrolls();
      UpdateSyntaxColors();
      Invalidate();
    }
    void _updatescrolls()
    {
      int ly = 0, maxx = 0;
      for (int ta = 0, tb = 0, tn = text.Length, li = 0; ; tb++)
        if (tb == tn || text[tb] == '\n')
        {
          if ((lineflags[li] & 1) == 0)
          {
            maxx = Math.Max(maxx, (tb - ta) * charwidth);
            ly += linehight;
          }
          if (tb == tn) break;
          ta = tb + 1; li++;
        }

      AutoScrollMinSize = new Size(maxx + 32, ly + linehight);
    }
    void _updateranges()
    {
      if (ranges == null) ranges = new List<Range>();
      for (int ta = 0, tb = 0, tn = text.Length, li = 0, tj = -1; ; tb++)
        if (tb == tn || text[tb] == '\n')
        {
          int t = tj < ta ? GetRange(ta) : 0; if (t < 0) { tj = t = -t; }
          if (t > tb)
          {
            for (int l = 0; l < ranges.Count; l++)
            {
              var range = ranges[l];
              if (range.a == ta && range.b == t) { range.line = li; goto next; }
            }
            ranges.Add(new Range { line = li, a = ta, b = t, hidden = false });
          next:;
          }
          if (tb == tn) break;
          ta = tb + 1; li++;
        }
      for (int t = 0; t < ranges.Count; t++) if (ranges[t].line < 0) ranges.RemoveAt(t--);
      ranges.Sort((a, b) => a.line - b.line);
      for (int t = 1; t < ranges.Count; t++) if (ranges[t - 1].line == ranges[t].line) ranges.RemoveAt(t--);
    }
    void _updatelineflags()
    {
      int n = 1; for (int i = 0; i < text.Length; i++) if (text[i] == '\n') n++;
      Array.Resize(ref lineflags, n);
      for (int i = 0; i < lineflags.Length; i++) lineflags[i] = 0;
      if (ranges == null) return;
      for (int i = ranges.Count - 1; i >= 0; i--)
      {
        var range = ranges[i];
        lineflags[range.line] |= (byte)(4 | (range.hidden ? 2 : 0));
        if (!range.hidden) continue;
        var bis = LineFromPos(range.b);
        for (int t = range.line + 1; t <= bis; t++) lineflags[t] |= 1;
        if (selb > range.a && selb <= range.b && selb > (bis = GetLineEnd(LineFromPos(range.a)))) Select(bis);
      }
    }
    class Range
    {
      internal int line, a, b;
      internal bool hidden;
    }
    unsafe void _textout(IntPtr hdc, char* s, int X, int Y, int rX, int la, int ab, int len)
    {
      int x = 0, xa = 0, ia = 0, na = 0;
      for (int t = la, ll = ab + len; t < ll && X + x < rX; t++)
      {
        if (t >= ab && X + x - sidedx + charwidth >= 0) { if (na == 0) { xa = x; ia = t; } na++; }
        x += charwidth;
      }
      if (na > 0) Native.TextOutW(hdc, X + xa, Y, s + ia, na);
    }
    int IComparable<(int id, object test)>.CompareTo((int id, object test) p) => OnCommand(p.id, p.test);
  }

  class XmlEditor : CodeEditor
  {
    static Regex par0 = new Regex("<\\??/?\\s*([a-z0-9_\\.\\-]*)\\s*(.*?)/?\\??>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    static Regex par1 = new Regex("([a-z0-9_\\.\\-]*)\\s*=\\s*\"(.*?)\"", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    static Regex par2 = new Regex("<!--(.*?)-->", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    void color(Capture p, byte c) { for (int i = 0; i < p.Length; i++) charcolor[p.Index + i] = c; }
    protected override void UpdateSyntaxColors()
    {
      if (colormap == null) colormap = new int[] { 0, 0xff0000, 0x000088, 0x0000ff, 0xff0000, 0x007000 };
      int n = text.Length; Array.Resize(ref charcolor, n + 1);
      for (var i = 0; i < n; i++) charcolor[i] = 0;
      for (var t = par0.Match(text); t.Success; t = t.NextMatch())
      {
        color(t.Groups[0], 1); color(t.Groups[1], 2);
        for (var x = par1.Match(text, t.Groups[2].Index, t.Groups[2].Length); x.Success; x = x.NextMatch())
        {
          color(x.Groups[1], 3); var p = x.Groups[2]; charcolor[p.Index - 1] = charcolor[p.Index + p.Length] = 0;
        }
      }
      for (var t = par2.Match(text); t.Success; t = t.NextMatch()) { color(t.Groups[0], 1); color(t.Groups[1], 5); }
    }
    protected override int GetRange(int x)
    {
      bool intag = false, para = false, endtag = false; char lastchar = (char)0;
      for (int i = x, level = 0, n = text.Length; i < n; i++)
      {
        var c = text[i];
        if (c == '\n' && lastchar == (char)0) return 0;
        if (c <= ' ') continue;
        if (!intag)
        {
          if (c == '<' && (i + 1 >= n || text[i + 1] != '!')) { intag = true; endtag = false; lastchar = ' '; }
          continue;
        }
        if (c == '"') { para ^= true; continue; }
        if (para) continue;
        if (c == '>')
        {
          if (lastchar != '/')
          {
            if (endtag) level--; else level++;
          }
          if (level == 0) return i + 1;
          if (level < 0) return 0;
          intag = false;
          continue;
        }
        if (lastchar == ' ' && c == '/') endtag = true;
        lastchar = c;
      }
      return 0;
    }
    protected override void OnHandleCreated(EventArgs e)
    {
      //base.EditText = ((Entity)this.Tag).Save().ToString().Replace("\r", string.Empty);
      base.OnHandleCreated(e);
    }
    public override int OnCommand(int id, object test)
    {
      //switch (id)
      //{
      //  case 0250: // GetPropList
      //    { var list = (System.Collections.IList)test; list.Add(this.Tag); }
      //    return 1;
      //  case 0010: return OnContextMenu((System.Collections.IList)test);
      //}
      return base.OnCommand(id, test);
    }
  }

  class NeuronEditor : CodeEditor
  {
    Neuron neuron; Compiler compiler;
    object[] data, dbgdata; Compiler.map[] spots; Compiler.map[] typemap; Compiler.map[] errors; String[] usings, usingsuse;
    int overid, recolor, rebuild, maxerror, state, ibreak; IntPtr threadstack;
    static NeuronEditor first; NeuronEditor next; bool ignorexceptions;
    internal static NeuronEditor GetEditor(Neuron neuron)
    {
      var p = first; for (; p != null && p.neuron != neuron; p = p.next) ; return p;
    }
    //object[] getdata() { return (object[])neuron.Invoke(0, null); }
    //void setdata(object[] a) { neuron.Invoke(1, a); }
    static void setdata(Neuron neuron, object[] b)
    {
      var a = (object[])neuron.Invoke(0, null); //getdata
      if (isrunning(a)) neuron.Invoke(4, null); //onstop Invoke("Dispose");
      neuron.Invoke(1, b); //setdata
      var runs = isrunning(b);
      var editor = GetEditor(neuron);
      if (editor != null) { editor.data = b; editor.ReadOnly = runs; }
      if (!runs) return;
      try { neuron.Invoke(3, null); }
      catch (DebugStop) { editor?.stop(); return; }
      catch (Exception e) { MessageBox.Show(e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
      if (neuron.Invoke(5, null) != null) { neuron.Invoke(6, 0); editor?.onstop(null); }
    }
    protected override void UpdateSyntaxColors()
    {
      if (colormap == null) colormap = new int[] { 0, 0x00007000, 0x00000088, 0x00ff0000, 0x11463a96, 0x2200ddff, 0x00af912b, 0x000000ff };
      int n = text.Length; Array.Resize(ref charcolor, n + 1);
      for (int i = 0; i < n; i++)
      {
        var c = text[i];
        if (c <= ' ') { charcolor[i] = 0; continue; }
        if (c == '/' && i + 1 < n)
        {
          if (text[i + 1] == '/') { for (; i < n && text[i] != 10; i++) charcolor[i] = 1; continue; }
          if (text[i + 1] == '*') { var t = text.IndexOf("*/", i + 2); t = t >= 0 ? t + 2 : n; for (; i < t; i++) charcolor[i] = 1; i = t - 1; continue; }
        }
        if (c == '"' || c == '\'')
        {
          var x = i; for (++i; i < n; i++) { var t = text[i]; if (t == '\\') { i++; continue; } if (t == c) break; }
          for (; x <= i; x++) charcolor[x] = 2; continue;
        }
        if (c == '<') //inline xml
        {
          int k = i - 1; for (; k >= 0 && (text[k] <= ' ' || charcolor[k] == 1); k--) ;
          if (k < 0 || "=+(,;}?".Contains(text[k]))
          {
            for (int z = 0, g, d = 0; i < n; i++)
            {
              if ((c = text[i]) == '<')
              {
                for (k = i + 1; k < n && text[k] != '>'; k++) ; if (k == n) break;
                charcolor[i++] = charcolor[g = k--] = 3;
                if (text[i] == '!')
                {
                  for (k = g; k < n && (text[k] != '>' || text[k - 1] != '-' || text[k - 2] != '-'); k++) ; charcolor[g = k--] = 3;
                  charcolor[i++] = 3;
                  for (int f = 0; f < 2; f++) { if (text[i] == '-') charcolor[i++] = 3; if (text[k] == '-') charcolor[k--] = 3; }
                  for (; i <= k; i++) charcolor[i] = 1;
                  i = g; continue;
                }
                if (text[i] == '/') { z--; charcolor[i++] = 3; }
                else if (text[k] != '/') z++; else charcolor[k--] = 3;
                for (; i <= k && text[i] > ' '; i++) charcolor[i] = 2;
                for (; i <= k; i++)
                {
                  if ((c = text[i]) == '"')
                  {
                    charcolor[i++] = 0; d = 3;
                    for (; i < k && text[i] != '"'; i++)
                    {
                      if (text[i] == '&') d = 7;
                      charcolor[i] = (byte)d; if (d == 7 && (text[i] == ';' || text[i] <= ' ')) d = 3;
                    }
                    if (i <= k) charcolor[i] = 0; continue;
                  }
                  charcolor[i] = (byte)(c == '=' ? 0 : 7);
                }
                i = g; d = 0; continue;
              }
              if (z == 0 && c > ' ') { i--; break; }
              if (c == '&') d = 7; charcolor[i] = (byte)d; if (d == 7 && (c == ';' || c <= ' ')) d = 0;
            }
            continue;
          }
        }

        var l = i; for (; l < n && IsLetter(text[l]); l++) ;
        var r = l - i; if (r == 0) { charcolor[i] = 0; continue; }
        byte color = 0;
        if (r > 1)
          for (int t = 0; t < Compiler.keywords.Length; t++)
          {
            var kw = Compiler.keywords[t];
            if (kw.Length == r && kw[0] == text[i] && string.Compare(text, i, kw, 0, kw.Length, true) == 0) { color = 3; break; }
          }
        for (; i < l; i++) charcolor[i] = color; i--;
      }
      if (typemap == null) return;

      for (int i = 0; i < typemap.Length; i++)
      {
        var p = typemap[i]; if ((p.v & 0xf) == 0 && charcolor[p.i] != 3) color(p.i, p.n, 6);
        if (overid != 0 && (overid & 0x0f) < 8 && overid == (p.v & ~0xf0)) color2(p.i, p.n, 0x80);
      }
      for (int i = 0; i < spots.Length; i++) { var p = spots[i]; if ((p.v & 1) != 0) color(p.i, p.n, 4); }
      for (int i = 0; i < spots.Length; i++) { var p = spots[i]; if ((p.v & 2) != 0) color(p.i, p.n, 5); }
      for (int i = 0; i < errors.Length; i++) { var p = errors[i]; color2(p.i, p.n, (errors[i].v & 4) != 0 ? 0x70 : 0x10); }
    }
    void color(int i, int n, int c)
    {
      for (int b = i + n; i < b; i++) charcolor[i] = (byte)c;
    }
    void color2(int i, int n, int c)
    {
      for (int b = i + n; i < b; i++) charcolor[i] |= (byte)c;
    }
    int nextls(int i)
    {
      for (; i < text.Length && text[i] != 10; i++) ;
      for (; i < text.Length && text[i] <= 32; i++) ;
      return i;
    }
    bool startsw(int i, string s)
    {
      return i + s.Length <= text.Length && string.CompareOrdinal(s, 0, text, i, s.Length) == 0;
    }
    protected override int GetRange(int x)
    {
      for (int i = x, n = text.Length; i < n; i++)
      {
        if (text[i] == 10 || text[i] == ';') break;
        if (text[i] == '/' && i + 1 < n)
        {
          if (text[i + 1] == '*') { for (i += 2; i < n - 1; i++) if (text[i] == '*' && text[i + 1] == '/') break; return -i; }
          if (text[i + 1] == '/') { for (int t; (t = nextls(i)) < n - 1 && text[t] == '/' && text[t + 1] == '/'; i = t) ; return -i; }
        }
        if (text[i] == '[') { for (int t; (t = nextls(i)) < n && text[t] == '['; i = t) ; return -i; }
        if (IsLetter(text[i]))
        {
          if (startsw(i, "using")) { for (int t; (t = nextls(i)) < n && startsw(t, "using"); i = t) ; return -i; }
          for (; i < n; i++)
          {
            if (text[i] == ';') break;
            if (text[i] == '{')
            {
              for (int k = 0; i < n; i++)
              {
                if (text[i] == '{') { k++; continue; }
                if (text[i] == '}') { if (--k == 0) return i; }
              }
              break;
            }
          }
          break;
        }
        if (text[i] == '<') //inline xml
        {
          if (i + 1 == n || text[i + 1] == '/') break;
          for (int z = 0, k; i < n; i++)
          {
            if (text[i] == '<')
            {
              for (k = i + 1; k < n && text[k] != '>'; k++) ; if (k == n) break;
              if (text[i + 1] == '!') { i = k; continue; }
              if (text[i + 1] == '/') z--; else if (text[k - 1] != '/') z++;
              i = k; continue;
            }
            if (z == 0) return i;
          }
        }
      }
      return 0;
    }
    public override void Refresh() { UpdateSyntaxColors(); Invalidate(); }
    protected override unsafe void OnPaint(PaintEventArgs e)
    {
      base.OnPaint(e); if (spots == null) return; int k = -1;
      for (int i = 0; i < spots.Length; i++)
      {
        var p = spots[i]; if ((p.v & 2) != 0) k = i; if ((p.v & 1) == 0) continue;
        var y = LineOffset(LineFromPos(p.i));
        TypeHelper.drawicon(e.Graphics, -1, y, 11);
      }
      if (k != -1) TypeHelper.drawicon(e.Graphics, -2, LineOffset(LineFromPos(spots[k].i)) + 1, 12);
    }
    public override int OnCommand(int id, object test)
    {
      switch (id)
      {
        case 5010: return onstep(0, test); // Run Debugging F5
        case 5011: return onstep(8, test); // Run Without Debugging Strg+F5
        case 5014: return onstep(9, test); // Compile
        case 5015: return onstep(2, test); // Step Into F11
        case 5016: return onstep(1, test); // Step Over F10
        case 5017: return onstep(3, test); // Step Out Shift F11 
        case 5013: return onstop(test);    // Stop Debugging
        case 5020: return onbreakpoint(test);
        case 5021: return onclearbreaks(test);
        case 5025: return onshowil(test);
        case 5027: return ongotodef(test);
        case 5040: { if (test == null) ignorexceptions ^= true; return 1 | (ignorexceptions ? 0 : 2); }
        case 5050: return onhelp(test);
        case 5100: return onformat(test);
        case 5105: return onprotect(test);
        case 5110: return onremusings(test);
        case 5111: return onsortusings(test);
        //case 5201: return onstopexcept(test);
        case 2088: return onrename(test);
        case 2020: // OnCut
        case 2040: // OnPaste 
          if (test == null && ReadOnly) askstop(); break;
      }
      return base.OnCommand(id, test);
    }
    protected override void OnHandleCreated(EventArgs e)
    {
      neuron = (Neuron)Tag;
      data = (object[])neuron.Invoke(0, null); //getdata();
      text = data != null ? decompress((byte[])((object[])data[0])[0]) : string.Empty;
      //if (text.Contains("\r\n") || text.Contains("\t")) text = adjust(text);
      ReadOnly = isrunning();
      compiler = new Compiler();
      dbgdata = compiler.Compile(neuron.GetType(), text, 1);
      spots = compiler.spots.ToArray();
      typemap = compiler.typemap();
      usings = compiler.usings.ToArray();
      usingsuse = compiler.usingsuse.ToArray();
      errors = compiler.errors.Select(er => _converr(er)).ToArray(); maxerror = compiler.maxerror;
      compiler.Reset();
      if (isdebug()) updspots();
      base.OnHandleCreated(e);
      next = first; first = this;
    }
    protected override void OnHandleDestroyed(EventArgs e)
    {
      if (first == this) first = next;
      else for (var p = first; ; p = p.next) if (p.next == this) { p.next = next; break; }
      next = null;
      EndFlyer(); base.OnHandleDestroyed(e);
    }
    void __save()
    {
      if (text.Length == 0) { setdata(neuron, null); return; }
      data[0] = new object[] { compress(text) };
      if (isdebug()) setspots(); setdata(neuron, data);
    }

    int onrename(object test)
    {
      if (base.ReadOnly || overid == 0 || (overid & 0xf) == 8) return 0;
      if (test != null) return 1;
      var a = typemap.Where(p => (p.v & ~0xf0) == overid);
      var f = a.FirstOrDefault(); if (f.n == 0) return 1;
      var s = text.Substring(f.i, f.n); var r = Caret;
      var tb = new TextBox { Text = s, Location = new Point(r.X, r.Bottom) };
      Controls.Add(tb); tb.Focus();
      tb.LostFocus += (_, e) => tb.Dispose();
      tb.KeyDown += (_, e) =>
      {
        if (e.KeyCode == Keys.Escape) { tb.Dispose(); return; }
        if (e.KeyCode != Keys.Return) return;
        var ss = tb.Text; tb.Dispose(); e.Handled = true; if (ss == s) return;
        Replace(a.Select(p => new Point(p.i, p.n)), ss);
      };
      return 1;
    }
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
      if (flyer != null) { flyer.OnKeyPress(e); if (e.Handled) return; }
      if (ReadOnly)
      {
        if ((ModifierKeys & (Keys.Control | Keys.Alt)) != 0) return;
        askstop(); return;
      }
      base.OnKeyPress(e);
      if (flyer != null && flyer.onpostkeypress != null) flyer.onpostkeypress(e);
    }
    internal bool askstop()
    {
      if (!ReadOnly) return false;
      if (state == 7 && !(neuron.Invoke(5, null) != null)) return true;
      if (MessageBox.Show(this, "Stop debugging?", Parent.Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return true;
      onstop(null); return false;
    }
    internal unsafe void OnFormClosing(Form f)
    {
      if (state != 7) return;
      Native.PostMessage(f.Handle, 0x0010, null, null); //WM_CLOSE 
      IsModified = false; throw new DebugStop();
    }
    internal static byte[] compress(string s)
    {
      var a = Encoding.UTF8.GetBytes(s);
      var m = new MemoryStream(); //m.WriteByte(0);
      using (var z = new GZipStream(m, CompressionMode.Compress, true)) z.Write(a, 0, a.Length);
      return m.ToArray();
    }
    internal static string decompress(byte[] a)
    {
      if (a == null) return string.Empty;
      var b = new byte[((a.Length >> 11) + 1) << 12]; int n = 0;
      var m = new MemoryStream(a); //m.ReadByte();
      using (var z = new GZipStream(m, CompressionMode.Decompress))
        while ((n = n + z.Read(b, n, b.Length - n)) == b.Length) Array.Resize(ref b, b.Length << 1);
      return Encoding.UTF8.GetString(b, 0, n);
    }
    int onstep(int id, object test)
    {
      if (state == 7)
      {
        if (id == 8) return 0;
        if (id == 9) return 0;
        if (test != null) return 1;
        EndFlyer(); ontimer = null;
        //it works for now, when it starts with internal optimizations using internal Localloc, then it needs a system 
        if (id == 1 && text.IndexOf("stackalloc", spots[ibreak].i, spots[ibreak].n) != -1) id = 2; //stepin as stack correct
        state = id; //Application.RaiseIdle(null);
        return 1;
      }
      if (isrunning())
      {
        if (id == 0 && isdebug()) return 2;
        if (id == 8 && !isdebug()) return 2;
        return 0;
      }
      if (test != null)
      {
        if (id == 3) return 0;
        return 1;
      }
      if (text == string.Empty) { IsModified = false; setdata(neuron, null); return 1; }
      if (rebuild != 0) build();
      if (id != 9) data = dbgdata;
      if ((maxerror & 4) != 0)
      {
        var e = errors.Where(p => (p.v & 4) != 0).First(); Select(e.i, e.i + e.n); ScrollVisible();
        MessageBox.Show(this, _error(e), Parent.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        return 1;
      }
      if (id == 9) return 1;
      if (id == 8) { data = compiler.Compile(neuron.GetType(), text, 0); compiler.Reset(); }
      ((object[])data[0])[0] = compress(text); if (id != 8) setspots();

      ignorexceptions = false;
      Neuron.state = state = id != 8 ? id : 0; setdata(neuron, data); //IsModified = false;
      return 1;
    }
    internal static void InitNeuron(Neuron p, string code)
    {
      p.Invoke(1, new object[] { new object[] { compress(code) } });
    }
    void updspots()
    {
      var pp = (int[])data[1]; var np = pp.Length << 5;
      if (np >= spots.Length) for (int i = 0; i < spots.Length; i++) if ((pp[(i >> 5)] & (1 << (i & 31))) != 0) spots[i].v |= 1;
    }
    void setspots()
    {
      var np = spots.Length;
      var pp = new int[(np >> 5) + ((np & 31) != 0 ? 1 : 0)];
      for (int i = 0; i < np; i++) if ((spots[i].v & 1) != 0) pp[(i >> 5)] |= (1 << (i & 31));
      data[1] = pp;
    }
    internal bool skip(Exception e)
    {
      return e != null && ignorexceptions && Neuron.state == 0 && (spots[Neuron.dbgpos].v & 1) == 0;
    }
    internal unsafe void Show(Exception e)
    {
      if (Neuron.state != 7 && e == null) return; neuron.Invoke(6, e);
      spots[ibreak = Neuron.dbgpos].v |= 2; threadstack = (IntPtr)Neuron.stack; Refresh();
      Select(spots[ibreak].i); ScrollVisible(); //stacktrace();
      if (e != null) MessageBox.Show(this, e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
      var cap = Native.SetCapture(IntPtr.Zero); DebugStop stop = null;
      try { for (state = 7; state == 7;) { Native.WaitMessage(); Application.DoEvents(); Application.RaiseIdle(null); } }
      catch (DebugStop p) { state = 0; stop = p; }
      if (e != null && state == 1) state = 2; // F10 -> F11 step into exception blocks
      Native.SetCapture(cap);
      Neuron.state = state;
      spots[ibreak].v &= ~2; threadstack = IntPtr.Zero; Refresh(); Update(); if (stop != null) throw stop;
    }
    void build()
    {
      //var sw = new Stopwatch(); sw.Start();
      dbgdata = compiler.Compile(neuron.GetType(), text, 1);
      //sw.Stop(); FindForm().Text = sw.ElapsedMilliseconds + " ms";
      usings = compiler.usings.ToArray();
      usingsuse = compiler.usingsuse.ToArray();
      errors = compiler.errors.Select(er => _converr(er)).ToArray(); maxerror = compiler.maxerror;
      if (maxerror < 4)
      {
        var oldspots = spots;
        spots = compiler.spots.ToArray();
        typemap = compiler.typemap();
        for (int t = 0; t < oldspots.Length; t++)
          if ((oldspots[t].v & 1) != 0)
            for (int j = 0; j < spots.Length; j++)
              if (spots[j].i == oldspots[t].i && spots[j].n == oldspots[t].n) { spots[j].v |= 1; break; }
      }
      else
      {
        typemap = compiler.typemap();
        //typemap = typemap.Concat(compiler.typemap).Distinct().ToArray();
        //typemap = typemap.Where(p => !errors.Any(e => e.i == p.i)).Concat(compiler.typemap).Distinct().ToArray();
      }
      compiler.Reset(); overid = 0; rebuild = 0; Refresh();
    }
    protected override void Replace(int i, int n, string s)
    {
      update(ref spots, i, n, s);
      update(ref typemap, i, n, s);
      update(ref errors, i, n, s);
      base.Replace(i, n, s);
      rebuild = s.Length == 1 && (s[0] == '.' || s[0] == '(') ? 2 : 5;
    }
    void update(ref Compiler.map[] a, int i, int n, string s)
    {
      var ok = true;
      for (int t = 0; t < a.Length; t++)
      {
        if (a[t].i + a[t].n <= i) continue;
        if (a[t].i >= i + n) { a[t].i += s.Length - n; continue; }
        a[t].n += s.Length - n; if (a == typemap) a[t].n = 0; if (a[t].n <= 0) ok = false;
      }
      if (!ok) a = a.Where(p => p.n > 0).ToArray();
    }
    internal static bool isrunning(object[] data)
    {
      return data != null && ((object[])data[0]).Length > 2;
    }
    internal static bool isdebug(object[] data)
    {
      if (data == null) return false;
      var s = (object[])data[0]; if (s.Length < 2) return false;
      var b = (byte[])s[1]; //if (b.Length < 1) return false;
      return (b[0] & 1) != 0;
    }
    bool isrunning() { return isrunning(data); }
    bool isdebug() { return isdebug(data); }
    int onstop(object test)
    {
      if (!isrunning() && !ReadOnly) return 0;
      if (state == 7 && !(neuron.Invoke(5, null) != null)) return 0;
      if (test != null) return 1;
      if (state == 7) throw new DebugStop();
      stop(); return 1;
    }
    class DebugStop : Exception { }
    void stop()
    {
      var data = this.data;
      setdata(neuron, new object[] { new object[] { ((object[])data[0])[0] } });
    }
    int onbreakpoint(object test)
    {
      int x = SelectionStart, l = LineFromPos(x), a = GetLineStart(l);
      var sp = spots.Where(p => p.i <= x && p.i + p.n >= x && (p.v & 1) != 0).OrderBy(p => p.n).LastOrDefault();
      if (sp.n == 0) sp = spots.Where(p => (x == a ? p.i < x : p.i <= x) && p.i + p.n >= x).OrderBy(p => p.n).FirstOrDefault();
      if (sp.n == 0) sp = spots.Where(p => LineFromPos(p.i) == l && (p.v & 1) != 0).OrderBy(p => p.i).FirstOrDefault();
      if (sp.n == 0) sp = spots.Where(p => LineFromPos(p.i) == l).OrderBy(p => p.i).FirstOrDefault();
      if (sp.n == 0) return 0; if (test != null) return 1;
      var askdebug = isrunning() && !isdebug() && !IsModified && spots.All(p => p.v == 0);
      spots[Array.IndexOf(spots, sp)].v ^= 1;
      if (isdebug()) setspots(); Refresh();
      if (askdebug && MessageBox.Show("Start debug session?", ParentForm.Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes) { stop(); onstep(0, null); }
      return 1;
    }
    int onclearbreaks(object test)
    {
      int count = 0;
      for (int i = 0; i < spots.Length; i++)
      {
        if ((spots[i].v & 1) == 0) continue;
        if (test != null) return 1; spots[i].v ^= 1; count++;
      }
      if (count == 0 || test != null) return 0;
      if (isdebug()) setspots(); Refresh(); return 1;
    }
    int ongotodef(object test)
    {
      if (overid == 0 || (overid & 0xf) == 8) return 0;
      if ((overid & 0x80000000) != 0) return 0;
      if (test != null) return 1;
      var t = typemap.FirstOrDefault(p => (p.v & ~0xf0) == overid && (p.v & 0x80) != 0);
      if (t.n == 0) t = typemap.FirstOrDefault(p => (p.v & ~0xf0) == overid);
      Select(t.i, t.i + t.n); ScrollVisible();
      return 1;
    }
    int onhelp(object test)
    {
      var ep = errors.FirstOrDefault(p => SelMin >= p.i && SelMax <= p.i + p.n);
      if ((ep.v >> 8) != 0) { if (test != null) return 1; TypeHelper.msdn("CS" + (ep.v >> 8).ToString("0000")); return 1; }
      var tpos = typemap.FirstOrDefault(p => SelMin >= p.i && SelMax <= p.i + p.n);
      var m = tpos.p as MemberInfo;
      if (m == null)
      {
        var ps = WordFromPoint(SelMin); if (ps.X == ps.Y || SelMax > ps.Y) return 0;
        var ss = text.Substring(ps.X, ps.Y - ps.X); if (!Compiler.keywords.Contains(ss)) return 0;
        if (test != null) return 1; TypeHelper.msdn(ss + "_CSHARPKEYWORD"); return 1;
      }
      var t = m as Type ?? m.DeclaringType; if (t == null) return 1; //dm
      if (t.DeclaringType == typeof(Compiler)) { if (test == null) TypeHelper.msdn(t.Name + "_CSHARPKEYWORD"); return 1; }
      if (!t.Assembly.GlobalAssemblyCache) return 0; //todo: navigate to own documentation
      if (test != null) return 1; TypeHelper.msdn(string.Format(m != t ? "{0}.{1}.{2}" : "{0}.{1}", t.Namespace, t.Name, m.Name)); return 1;
    }
    static unsafe object __m128(object p, Type type) //bad trick but startpoint c++ SSE interop
    {
      if (p == null) return p; //Nullable<>
      if (p.GetType() == type) return p;
      if (type.IsGenericType) return p; //Nullable<>
      decimal m; Marshal.StructureToPtr(p, (IntPtr)(void*)&m, false);
      p = Marshal.PtrToStructure((IntPtr)(void*)&m, type); return p;
    }
    unsafe Tuple<object, object> stackeval(Compiler.map tpos)
    {
      var v = tpos.v & 0x0f; var id = tpos.v >> 8;
      if (v == 1) //dynamic
      {
        if (!(tpos.p is string)) return null;
        var tp = prevmap(tpos); if ((tp.v & 0x0f) != 1) return null;
        var la = stackeval(prevmap(tp)); if (la == null || la.Item1 == null) return null;
        var va = Neuron.get(la.Item1, (string)tpos.p);
        return Tuple.Create(va, (object)(va != null ? va.GetType() : typeof(object)));
      }
      if (v == 6)
      {
        var p = data[id >> 12]; id &= 0x0fff; if (id != 0x0fff) p = __m128(((Array)p).GetValue(id), (Type)tpos.p);
        return Tuple.Create(p, tpos.p);
      }
      if (v == 3)
      {
        if (equals(tpos, "this") || equals(tpos, "base")) return Tuple.Create((object)neuron, tpos.p);
        if (tpos.p is MethodInfo) return null;
        var acc = tpos.p as DynamicMethod[];
        if (acc != null)
        {
          if (acc[0] == null) return null;
          Neuron.state = 7; return Tuple.Create(acc[0].Invoke(null, new object[] { neuron }), (object)acc[0].ReturnType);
        }
        var fi = tpos.p as FieldInfo;
        if (fi != null && fi.IsStatic) return Tuple.Create(fi.GetValue(null), (object)fi.FieldType);
        var pi = tpos.p as PropertyInfo;
        if (pi != null) { if (!pi.CanRead) return null; if (pi.GetGetMethod(true).IsStatic) return Tuple.Create(pi.GetValue(null, null), (object)pi.PropertyType); }
        if (pi == null && fi == null) return null; // EventInfo...
        var rt = pi != null ? pi.ReflectedType : fi.ReflectedType;

        object ta = null; tpos = prevmap(tpos);
        if ((tpos.v & 0x0f) == 1) //'.'
        {
          tpos = prevmap(tpos);
          var la = stackeval(tpos); if (la == null) return null; ta = la.Item1;
          if (ta is int && !rt.IsAssignableFrom(ta.GetType()))
          {
            var ax = prevmap(tpos); var av = stackeval(ax); if (av == null) return null;
            var a = av.Item1 as Array;
            if (a != null) { var i = (int)ta; if (i < 0 || i >= a.Length) return null; ta = a.GetValue(i); } //todo: IList,...
          }
        }
        else ta = this.neuron;
        if (ta == null) return null;
        if (pi != null) return rt.IsAssignableFrom(ta.GetType()) || ta.GetType().IsCOMObject ? Tuple.Create(pi.GetValue(ta, null), (object)pi.PropertyType) : null;
        if (fi != null) return rt.IsAssignableFrom(ta.GetType()) ? Tuple.Create(fi.GetValue(ta), (object)fi.FieldType) : null;
        //if (pi != null) return Tuple.Create(pi.GetValue(ta, null), (object)pi.PropertyType); 
        //if (fi != null) return Tuple.Create(fi.GetValue(ta), (object)fi.FieldType);
      }
      if (v == 4 || v == 5)
      {
        if ((tpos.v & 0x40) != 0)
        {
          var ri = (Compiler.RepInfo)tpos.p;
          for (var p = (int*)threadstack; p != null; p = p[1] != 0 ? p + p[1] : null)
            if ((p[0] & 0xffff) == (ri.id & 0xffff))
            {
              TypedReference tr; var pp = (IntPtr*)&tr; var type = typeof(object[]);
              pp[0] = (IntPtr)(p + (p[0] >> 16)); pp[1] = type.TypeHandle.Value;
              var a = (object[])TypedReference.ToObject(tr); for (int j = 0; j < (ri.id >> 16); j++) a = (object[])a[0];
              var o = a[ri.index & 0xffff]; return Tuple.Create(ri.type.IsValueType ? __m128(((Array)o).GetValue(ri.index >> 16), ri.type) : o, (object)ri.type);
            }
          return null;
        }
        for (var p = (int*)threadstack; p != null; p = p[1] != 0 ? p + p[1] : null)
          if ((p[0] & 0xffff) == id)
          {
            TypedReference tr; var pp = (IntPtr*)&tr; var type = (Type)tpos.p;
            pp[0] = (IntPtr)(p + (p[0] >> 16)); pp[1] = type.TypeHandle.Value;
            if (type.IsByRef) { pp[0] = *(IntPtr*)pp[0]; pp[1] = (type = type.GetElementType()).TypeHandle.Value; }
            return Tuple.Create(TypedReference.ToObject(tr), (object)type);
          }
      }
      return null;
    }
    Compiler.map prevmap(Compiler.map tpos)
    {
      tpos = typemap.Where(t => t.i + t.n <= tpos.i).OrderBy(t => t.i + t.n).LastOrDefault();
      return tpos;
    }
    bool equals(Compiler.map m, string s)
    {
      return m.n == s.Length && string.Compare(s, 0, text, m.i, m.n) == 0;
    }
    int lastt, laste; TypeExplorer flyer;
    protected override void OnMouseMove(MouseEventArgs e)
    {
      base.OnMouseMove(e);
      var i = PosFromPoint(e.Location);
      var tpos = typemap.FirstOrDefault(t => i >= t.i && i <= t.i + t.n && ((t.v & 0xf) != 1 || this.text[t.i] != '.') && !(t.v == 0 && t.p is string));
      var epos = errors.FirstOrDefault(t => i >= t.i && i <= t.i + t.n);
      if (lastt == tpos.i && laste == epos.i) return;
      lastt = tpos.i; laste = epos.i;
      var text = this.text; if (equals(tpos, "{")) return;
      if (state == 7 && (tpos.v & 0x0f) >= 1 && (tpos.v & 0x0f) < 8)
      {
        Tuple<object, object> p = null;
        try { p = stackeval(tpos); }
        catch { }
        if (p != null)
        {
          EndToolTip(); EndFlyer(); //ToolTip(text.Substring(tpos.i, tpos.n) + " = " + p.ToString());
          var ri = RectangleToScreen(TextBox(tpos.i, tpos.n)); int ticks = 0;
          ontimer = () =>
          {
            if (ticks++ < 5) return;
            ontimer = null; EndToolTip(); EndFlyer();
            flyer = new TypeExplorer { Location = new Point(ri.X, ri.Bottom) }; var mi = tpos.p as MemberInfo;
            flyer.items = new TypeExplorer.Item[] { new TypeExplorer.Item { icon = mi != null ? TypeHelper.image(mi) : 8 /*18*/, text = text.Substring(tpos.i, tpos.n), obj = p.Item1, info = p.Item2 } };
            flyer.Show(); flyer.ontooltip = (r, item) => SetToolTip(RectangleToClient(r), x => x == 0 ? item.pv as string ?? item.sv : null);
          };
          return;
        }
      }

      ontimer = null;
      if (tpos.n == 0)
      {
        if (epos.n != 0)
        {
          SetToolTip(TextBox(epos.i, epos.n), t =>
          {
            if (t != 0) return null;
            var em = _error(epos);
            switch (epos.v >> 8)
            {
              case 1031: //Type expected
              case 0103: //The name '{0}' does not exist in the current context
              case 0246: //The type or namespace name '{0}' could not be found
                var p = WordFromPoint(epos.i); if (p.X == p.Y) break;
                var ss = text.Substring(p.X, p.Y - p.X); //if (!ss.EndsWith("Attribute") && text[p.X - 1] == '[' ) ss += "Attribute";
                for (int j = 0; j < 2; j++)
                {
                  var ts = string.Join("\n", TypeHelper.Assemblys.SelectMany(a => a.GetTypes()).Where(a => !a.IsNested && a.Name == ss).Select(a => a.FullName));
                  if (ts.Length == 0) { ss += "Attribute"; continue; }
                  em = string.Format("{0}\n\nconsider using:\n{1}", em, ts); break;
                }
                break;
            }
            return string.Format("{0} {1}", (epos.v & 4) != 0 ? "Error" : "Warning", em);
          }, 1);
        }
        return;
      }
      SetToolTip(TextBox(tpos.i, tpos.n), t =>
      {
        if (t != 0) return null;
        var s = TypeHelper.tooltip(tpos, text);// +" " + tpos.v.ToString("X8");
        if (epos.n != 0) s = string.Format("{0}{1}:\n  {2}", s != null ? string.Format("{0}\n\n", s.Trim()) : null, (epos.v & 4) != 0 ? "Error" : "Warning", _error(epos));
        return s;
      });

    }
    string _error(Compiler.map epos)
    {
      var pp = (object[])epos.p;
      return string.Format((string)pp[0], text.Substring(epos.i, epos.n), TypeHelper.shortname(pp[1]), TypeHelper.shortname(pp[2]));
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
      EndToolTip(); EndFlyer(); base.OnMouseDown(e);
    }
    protected override void OnLostFocus(EventArgs e)
    {
      EndToolTip(); EndFlyer(); base.OnLostFocus(e);
    }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
      if (flyer != null)
      {
        var p = Native.WindowFromPoint(PointToScreen(e.Location));
        if (p != IntPtr.Zero) { var t = Control.FromHandle(p) as TypeExplorer; if (t != null) { t.OnMouseWheel(e); return; } }
        EndFlyer();
      }
      base.OnMouseWheel(e);
    }
    private void EndFlyer()
    {
      if (flyer != null) { flyer.Dispose(); flyer = null; }
    }
    Action ontimer;
    protected override void OnKeyDown(KeyEventArgs e)
    {
      if (flyer != null) { flyer.OnKeyDown(e); if (e.Handled) return; }
      if (e.KeyCode == Keys.Delete) askstop();
      base.OnKeyDown(e);
      if (flyer != null && flyer.onpostkeydown != null) flyer.onpostkeydown(e);
    }
    protected override void OnScroll(ScrollEventArgs se)
    {
      EndFlyer(); base.OnScroll(se);
    }
    protected internal override void OnSelChanged()
    {
      base.OnSelChanged(); checkover();
    }
    void checkover()
    {
      int i1 = SelectionStart, i2 = i1 + SelectionLength;
      var t = typemap.Length; while (--t >= 0) { var p = typemap[t]; if ((p.v & 0x0f) != 1 && i1 >= p.i && i2 <= p.i + p.n) break; }
      int id = t >= 0 ? typemap[t].v : 0; id = id & ~0xf0; //id = (id & 0xff) != 0x45 ? id & ~0xf0 : 0; //&& (typemap[t].v & 0xff) != 0x45 ? typemap[t].v & ~0xf0 : 0;  
      if (overid != id)
      {
        if (overid != 0) { overid = 0; Refresh(); }
        overid = id; lastt = -1;
        if (overid != 0) { recolor = 5; }
      }
    }
    protected override void WndProc(ref Message m)
    {
      base.WndProc(ref m);
      if (m.Msg == 0x0113)//WM_TIMER
      {
        if (rebuild != 0 && --rebuild == 0) { build(); PostBuild(); }
        if (recolor != 0 && --recolor == 0) Refresh();
        if (ontimer != null) ontimer();
      }
    }
    static bool IsLetter(char c) { return char.IsLetter(c) || c == '_'; }
    static bool IsLetterOrDigit(char c) { return char.IsLetterOrDigit(c) || c == '_'; }
    void PostBuild()
    {
      checkover(); if (SelectionLength != 0) return;
      var s = text; var i1 = SelectionStart; var i2 = i1; var cv = i1 != 0 ? s[i1 - 1] : '\0';

      if (cv == '(')
      {
        var px = typemap.Where(p => p.i + p.n <= i1 - 1).OrderBy(p => p.i + p.n).LastOrDefault();
        if (px.n != 0 && px.v == 0 && text.Substring(px.i + px.n, i1 - 1 - (px.i + px.n)).Trim().Length == 0)
        {
          var cc = ((Type)px.p).GetConstructors(); if (cc.Length == 0) return; int l = 0;
          SetToolTip(Caret, t =>
          {
            if (t == 0) return TypeHelper.tooltip(cc[l]);
            if (t == 1) return string.Format("{0} of {1}", l + 1, cc.Length);
            if (t == 40) { if (++l == cc.Length) l = 0; }
            if (t == 38) { if (l-- == 0) l = cc.Length - 1; }
            return null;
          }, 2);
          return;
        }

        var type = neuron.GetType(); var sm = wordbefore(i1);
        var pt = typemap.Where(p => p.i < i1 - 1 && (p.v & 0xf) == 1).OrderBy(p => p.i).LastOrDefault();
        if (!(pt.n < 1 || pt.n > 2)) type = (Type)pt.p;
        var items = type.GetMethods(((pt.v & 0x40) == 0 ? BindingFlags.Instance : BindingFlags.Static) | BindingFlags.Public).Where(p => p.Name == sm);
        if ((pt.v & 0x40) == 0) items = items.Concat(Extensions(type, sm));
        var mm = items.ToArray(); if (mm.Length == 0) return; int x = Array.IndexOf(mm, px.p); if (x < 0) x = 0;
        SetToolTip(Caret, t =>
        {
          if (t == 0) return TypeHelper.tooltip(mm[x]);
          if (t == 1) return string.Format("{0} of {1}", x + 1, mm.Length);
          if (t == 40) { if (++x == mm.Length) x = 0; }
          if (t == 38) { if (x-- == 0) x = mm.Length - 1; }
          return null;
        }, 2);
        return;
      }
      if (flyer != null) return;
      if (cv == '.' || cv == '>' || cv == '#')
      {
        var dp = cv == '>' ? 2 : 1;
        var tp = typemap.FirstOrDefault(p => p.i == i1 - dp && p.n == dp);
        if (tp.n != 0)
        {
          var type = tp.p as Type; if (type == null) return;
          var items = type.GetMembers(((tp.v & 0x40) == 0 ? BindingFlags.Instance : BindingFlags.Static | BindingFlags.FlattenHierarchy) |
            (cv == '#' || neuron.GetType().IsOrIsSubclassOf(type) ? BindingFlags.Public | BindingFlags.NonPublic : BindingFlags.Public)).
            Where(p => Compiler.__filter(p, cv == '#')).GroupBy(p => p.Name).Select(p => p.ToArray()).
            Select(p => new TypeExplorer.Item { icon = TypeHelper.image(p[0]), text = p[0].Name, info = p });
          if ((tp.v & 0x40) == 0) items = items.Concat(Extensions(type).GroupBy(p => p.Name).Select(p => p.ToArray()).
            Select(p => new TypeExplorer.Item { icon = 24, text = p[0].IsGenericMethod ? p[0].Name + "<>" : p[0].Name, info = p }));
          if ((tp.v & 0x40) == 0 && type == neuron.GetType() && equals(prevmap(tp), "this"))
            items = items.Concat(typemap.Where(p => (p.v & 0x8f) == 0x86 || (p.v & 0xff) == 0xc3 || (p.v & 0xff) == 0x83).Select(p => new TypeExplorer.Item
            {
              icon = (p.v & 0x8f) == 0x86 ? 22 : (p.v & 0xff) == 0xc3 ? 23 : 25,
              text = text.Substring(p.i, p.n),
              info = (Func<string>)(() => TypeHelper.tooltip(p, text, false))
            }));
          EditFlyer(i1, i2, items.OrderBy(p => p.text).ToArray());
          return;
        }
        tp = typemap.FirstOrDefault(p => p.i + p.n == i1 - 1 && (p.v & 0xf) == 0x08);
        if (tp.n != 0)
        {
          var ns = (string)tp.p; var tt = compiler.GetTypes();
          var items = tt.Select(p => p.Namespace).
            Where(p => p.Length > ns.Length && p[ns.Length] == '.' && p.StartsWith(ns)).
            Where(p => (tp.v & 0x0f) == 0x08 || usings.Contains(p)).
            Select(p => p.Substring(ns.Length + 1, (int)Math.Min((uint)p.IndexOf('.', ns.Length + 1), (uint)p.Length) - (ns.Length + 1))).
            Distinct().Select(p => new TypeExplorer.Item { icon = 3, text = p, info = string.Format("{0}.{1}", ns, p) });
          if (tp.v == 8) items = items.Concat(tt.
             Where(p => p.Namespace == ns).
             GroupBy(p => p.Name.Contains('`') ? p.Name.Substring(0, p.Name.IndexOf('`') + 1) : p.Name).
             Select(p => p.ToArray()).
             Select(p => new TypeExplorer.Item { icon = TypeHelper.image(p[0]), text = TypeHelper.shortname(p[0], false), info = p }));
          EditFlyer(i1, i2, items.OrderBy(p => p.text).ToArray());
          return;
        }
      }
      if (IsLetter(cv) && (i1 < 2 || !IsLetterOrDigit(s[i1 - 2])) && (i1 == s.Length || !IsLetterOrDigit(s[i1])))
      {
        if (i1 != 0 && charcolor[i1 - 1] == 1) return;
        if (typemap.Any(p => p.i <= i1 && p.i + p.n >= i1)) return;
        var tt = compiler.GetTypes(); cv = char.ToUpper(cv);
        var items = tt.Select(p => p.Namespace).Where(p => char.ToUpper(p[0]) == cv).Distinct().
          Select(p => p.Substring(0, (int)Math.Min((uint)p.IndexOf('.'), p.Length))).Distinct().
          Select(p => new TypeExplorer.Item { icon = 3, text = p, info = p });
        if (wordbefore(i1) != "using")
        {
          items = items.Concat(tt.Where(t => char.ToUpper(t.Name[0]) == cv && usings.Contains(t.Namespace)).
            GroupBy(p => p.Name.Contains('`') ? p.Name.Substring(0, p.Name.IndexOf('`')) : p.Name).
            Select(p => p.ToArray()).
            Select(p => new TypeExplorer.Item { icon = TypeHelper.image(p[0]), text = p[0].Name.Contains('`') ? p[0].Name.Substring(0, p[0].Name.IndexOf('`')) : p[0].Name, info = p }));
          items = items.Concat(Compiler.keywords.TakeWhile(p => p != "true").
            Where(p => char.ToUpper(p[0]) == cv).
            Select(p => new TypeExplorer.Item { icon = 9, text = p }));
          items = items.Concat(neuron.GetType().GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic).Where(p =>
             Compiler.__filter(p, false) && char.ToUpper(p.Name[0]) == cv).
            GroupBy(p => p.Name).Select(p => p.ToArray()).
            Select(p => new TypeExplorer.Item { icon = TypeHelper.image(p[0]), text = p[0].Name, info = p }));
          //items = items.Concat(script.GetMethods().
          //  Where(p => char.ToUpper(p.Method.Name[0]) == cv).
          //  Select(p => new RtExplorer.Item { icon = 0, text = p.Method.Name }));
        }

        EditFlyer(i1 - 1, i2, items.OrderBy(p => p.text).ToArray());
        return;
      }
    }
    string wordbefore(int i)
    {
      int b = i; for (; b > 1 && !IsLetterOrDigit(text[b - 2]); b--) ;
      int a = b; for (; a > 1 && IsLetterOrDigit(text[a - 2]); a--) ;
      return a != 0 && b != a ? text.Substring(a - 1, b - a) : string.Empty;
    }
    IEnumerable<MethodInfo> Extensions(Type type, string name = null)
    {
      return compiler.GetTypes().
        Where(t => t.IsAbstract && t.IsSealed && !t.IsGenericType &&
          t.IsDefined(typeof(ExtensionAttribute), false) && usings.Contains(t.Namespace)).
        SelectMany(t => t.GetMembers(BindingFlags.Static | BindingFlags.Public | BindingFlags.InvokeMethod)).OfType<MethodInfo>().
        Where(m =>
        {
          if (name != null && m.Name != name) return false;
          if (!m.IsDefined(typeof(ExtensionAttribute), false)) return false;
          var t = m.GetParameters()[0].ParameterType;
          if (!m.IsGenericMethod) { if (t.IsAssignableFrom(type)) return true; return false; }
          if (t.IsAssignableFrom(type)) return true;
          if (type.GetInterface(t.Name) != null) return true;
          if (t.IsGenericType && type.IsGenericType && type.GetGenericTypeDefinition() == t.GetGenericTypeDefinition()) return true;
          return false;
        });
    }
    void EditFlyer(int i1, int i2, TypeExplorer.Item[] items)
    {
      if (items.Length == 0) return; var t2 = i2;
      ontimer = null; EndFlyer(); EndToolTip();
      flyer = new TypeExplorer(); var ri = RectangleToScreen(Caret);
      flyer.Location = new Point(ri.X, ri.Bottom);
      flyer.items = items; flyer.noeval = true;
      flyer.Show();
      flyer.onclick = e => { EndFlyer(); Select(i1, i2); Paste(e.text.Split('<')[0]); return true; };
      flyer.onpostkeydown = e => { var x = SelectionStart; if (x < i1 || x > i2 || SelectionLength != 0) EndFlyer(); };
      flyer.onpostkeypress = a =>
      {
        i2 = SelectionStart; if (i2 < t2) { EndFlyer(); return; }
        var ss = text.Substring(i1, i2 - i1);
        var xx = items.Where(x => x.text.StartsWith(ss, true, null)).ToArray();
        if (xx.Length == 0) { EndFlyer(); return; }
        if (flyer.items.SequenceEqual(xx)) return;
        flyer.items = xx; flyer.Format(); flyer.select(xx[0]); flyer.Invalidate();
      };
      flyer.ontooltip = (r, item) => SetToolTip(RectangleToClient(r), t => t == 0 ? TypeHelper.tooltip(item.info) : null, 1);
    }
    protected override void OnMouseLeave(EventArgs e)
    {
      base.OnMouseLeave(e); if (ClientRectangle.Contains(PointToClient(Cursor.Position))) return;
      EndToolTip(); EndFlyer();
    }
    protected internal override void OnContextMenu(ToolStripItemCollection items)
    {
      base.OnContextMenu(items);
      if (isrunning())
      {
        items.Add(new ToolStripSeparator());
        items.Add(new UIForm.MenuItem(5020, "Toggle &Breakpoint"));
        items.Add(new ToolStripSeparator());
        items.Add(new UIForm.MenuItem(5013, "&Stop"));
      }
      else
      {
        items.Add(new ToolStripSeparator());
        items.Add(new UIForm.MenuItem(5010, "Start &Debug"));
        items.Add(new UIForm.MenuItem(5016, "Step &Over"));
        items.Add(new UIForm.MenuItem(5015, "Step &Into"));
        items.Add(new UIForm.MenuItem(5017, "Step Ou&t"));
        items.Add(new ToolStripSeparator());
        items.Add(new UIForm.MenuItem(5020, "Toggle &Breakpoint"));
        items.Add(new ToolStripSeparator());
        items.Add(new UIForm.MenuItem(5011, "&Start"));
      }
    }
    Compiler.map _converr(Compiler.map p)
    {
      int i = p.i, n = p.n;
      if (n == 0) { while (i < text.Length && text[i] < ' ') i++; if (i < text.Length) n = 1; else for (n = 1; --i >= 0 && text[i] < ' ';) ; }
      p.i = i; p.n = n; return p;
    }
    int onshowil(object test)
    {
      if ((maxerror & 4) != 0) return 0; if (state == 7) return 0; if (test != null) return 1;
      compiler.Compile(neuron.GetType(), text, 2);
      var form = new Form
      {
        Text = string.Format($"{ParentForm.Text} - {"ILCode"}"),
        //Icon = ParentForm.Icon,
        StartPosition = FormStartPosition.Manual,
        Location = Parent.Location + new Size(32, 64),
        Size = Parent.Size, //Width = Width * 2 / 3, Height = Height * 2 / 3, 
        ShowInTaskbar = false,
        ShowIcon = false
      };
      var edit = new CodeEditor { Dock = DockStyle.Fill, EditText = compiler._trace_.ToString(), ReadOnly = true };
      form.Controls.Add(edit);
      form.Controls.Add(form.MainMenuStrip = new MenuStrip());
      form.MainMenuStrip.Items.AddRange(new ToolStripItem[]
      {
        new MenuItem("&Edit",
          new MenuItem(2030, "Cop&y", Keys.C|Keys.Control ),
          new ToolStripSeparator(),
          new MenuItem(2060, "Select &all", Keys.A|Keys.Control ),
          new ToolStripSeparator(),
          new MenuItem(2065, "&Find...", Keys.F|Keys.Control ),
          new MenuItem(2066, "Find forward", Keys.F3|Keys.Control ),
          new MenuItem(2067, "Find next", Keys.F3 ),
          new MenuItem(2068, "Find prev", Keys.F3|Keys.Shift ),
          new ToolStripSeparator(),
          new MenuItem(5027, "Goto &Definition", Keys.F12 )
        )
       });
      var t = MenuItem.CommandRoot; MenuItem.CommandRoot = edit.OnCommand;
      form.StartPosition = FormStartPosition.CenterParent; form.ShowDialog(this); MenuItem.CommandRoot = t;
      //form.Show(this);
      return 1;
    }
    int onprotect(object test)
    {
      return 0;
    }
    int onremusings(object test)
    {
      if (ReadOnly || (maxerror & 4) != 0) return 0;
      if (usings.Length == usingsuse.Length) return 0;
      if (test != null) return 1;
      repusings(usings.Where(s => usingsuse.Contains(s))); return 1;
    }
    int onsortusings(object test)
    {
      if (ReadOnly || (maxerror & 4) != 0) return 0;
      //var u = usings.OrderBy(s => Array.IndexOf(usingsuse, s));
      var u = usings.OrderBy(s => s).OrderBy(s => !s.StartsWith("System"));
      if (usings.SequenceEqual(u)) return 0;
      if (test != null) return 1;
      repusings(u); return 1;
    }
    private void repusings(IEnumerable<string> u)
    {
      var a = typemap.First(p => p.v == 0x88);
      var b = typemap.Last(p => p.v == 0x88);
      var list = new List<Action<CodeEditor>>();
      list.Add(undo(a.i, b.i + b.n - a.i, string.Join(";\nusing ", u)));
      undoex(list.ToArray());
    }
    int onformat(object test)
    {
      if (ReadOnly || (maxerror & 4) != 0) return 0;
      if (test != null) return 1;
      var s = text; var list = new List<Action<CodeEditor>>();
      var n = s.Length; for (; n > 0 && s[n - 1] <= ' '; n--) ;
      for (int a = 0, b = 0, k = 0; ; b++)
      {
        if (b == n || s[b] == '\n')
        {
          var c = a; for (; c < b && s[c] == ' '; c++) ;
          if (c == b) { list.Add(undo(a, b - a, string.Empty)); }
          else if (s[c] == '<') { } //inline xml //todo: xml format
          else
          {
            var f = charcolor[c];
            if (!((f == 1 && !(s[c] == '/' || s[c] == '*')) || f == 2))
            {
              if (f == 0 && s[c] == '}') k -= 2;
              var x = Math.Max(0, startsw(c, "case") || startsw(c, "default") ? k - 2 : k);
              var h = c - a; if (h != x) list.Add(undo(a, x < h ? h - x : 0, x < h ? string.Empty : new string(' ', x - h)));
            }
            for (int t = c; t < b; t++)
            {
              if (s[t] == ' ')
              {
                var x = t; for (; x < b && s[x] == ' '; x++) ;
                if (x == b) { list.Add(undo(t, x - t, string.Empty)); break; }
                if (charcolor[t] == 0)
                {
                  var l = x - t; var o = 1;
                  var c1 = t > c ? s[t - 1] : '\0'; var c2 = s[x];
                  if (c1 == '.' || c2 == '.' || c1 == '(' || c2 == ')' || c1 == '[' || c2 == ']' || c2 == ',') o = 0;
                  if (l != o) list.Add(undo(t, o < l ? l - o : 0, o < l ? string.Empty : new string(' ', o - l)));
                }
                t = x;
              }
              if (charcolor[t] != 0) continue;
              if (t + 1 < b && s[t + 1] != ' ' && (s[t] == ',' || s[t] == ';'))
                list.Add(undo(t + 1, 0, " "));
              if (s[t] == '{') k += 2; else if (t != c && s[t] == '}') k -= 2;
            }
          }
          if (b == n) break; a = b + 1; continue;
        }
      }
      if (n < s.Length) list.Add(undo(n, s.Length - n, string.Empty));
      if (list.Count != 0) undoex(list.ToArray());
      return 1;
    }
  }

  class TypeExplorer : UserControl
  {
    protected override CreateParams CreateParams
    {
      get
      {
        CreateParams p = base.CreateParams;
        p.ExStyle |= 0x00000080 | 0x08000000 | 0x00000008 | 0x02000000;
        p.ClassStyle = 0x00020000;
        p.Parent = IntPtr.Zero;
        return p;
      }
    }
    public new void Show()
    {
      if (this.Handle == IntPtr.Zero) base.CreateControl();
      Native.SetParent(base.Handle, IntPtr.Zero);
      Native.ShowWindow(base.Handle, 1);
    }
    internal class Item { internal int icon; internal string text, sv; internal object obj, info, pv; }
    int linedy, textofs;
    internal Item[] items; TypeExplorer parent, drop; Item openitem, overitem; int midx;
    internal bool noeval; Point orgpos;
    internal Func<Item, bool> onclick;
    internal Action<KeyEventArgs> onpostkeydown;
    internal Action<KeyPressEventArgs> onpostkeypress;
    internal Action<Rectangle, Item> ontooltip;
    protected override void OnHandleCreated(EventArgs e)
    {
      linedy = CodeEditor.dpiscale(23); // Font.Height + CodeEditor.dpiscale(6);
      textofs = 1; // (linedy - 2 - Font.Height) >> 1;
      BorderStyle = BorderStyle.FixedSingle;
      BackColor = SystemColors.Window;
      DoubleBuffered = true; Format();
      base.OnHandleCreated(e);
    }
    internal void Format()
    {
      int x1 = 16, x2 = 16;
      for (int i = 0; i < items.Length; i++)
      {
        var item = items[i];
        x1 = Math.Max(x1, TextRenderer.MeasureText(item.text, Font).Width); if (noeval) continue;
        x2 = Math.Max(x2, TextRenderer.MeasureText(item.sv = evals(item.pv = evalp(item.obj, item.info), item.info), Font).Width);
      }
      midx = CodeEditor.dpiscale(36) + x1;
      var dy = items.Length * linedy;
      Width = midx + Math.Min(x2, Screen.GetBounds(this).Width - 100) + 4 + (Height < dy ? CodeEditor.dpiscale(24) : 0); Height = Math.Min(CodeEditor.dpiscale(400), dy);
      AutoScrollMinSize = new System.Drawing.Size(0, Height < dy ? dy : 0);
      if (orgpos.Y != 0) { Location = new Point(orgpos.X, orgpos.Y - Height - linedy); return; }
      var r1 = Bounds;
      var r2 = SystemInformation.VirtualScreen;
      if (r1.Bottom > r2.Bottom) { orgpos = r1.Location; Location = new Point(orgpos.X, orgpos.Y - Height - linedy); }
    }
    protected override void OnHandleDestroyed(EventArgs e)
    {
      closedrop(); base.OnHandleDestroyed(e);
    }
    protected override void OnScroll(ScrollEventArgs se)
    {
      closedrop(); base.OnScroll(se);
    }
    void closedrop()
    {
      if (drop != null) { drop.Dispose(); drop = null; openitem = null; BackColor = SystemColors.Window; }// Invalidate(); }
    }
    protected override void OnPaint(PaintEventArgs e)
    {
      var g = e.Graphics; int yl = AutoScrollPosition.Y; var rc = g.ClipBounds;
      for (int i = 0; i < items.Length; i++, yl += linedy)
      {
        if (yl > rc.Bottom) break; if (yl + linedy <= rc.Top) continue;
        var item = items[i];
        if (item.pv != null && Type.GetTypeCode(item.pv.GetType()) == TypeCode.Object)
          TypeHelper.drawicon(g, CodeEditor.dpiscale(2), yl + CodeEditor.dpiscale(4), openitem == item ? 13 : 14);
        if (item == overitem) g.FillRectangle(Brushes.LightGray, CodeEditor.dpiscale(17), yl, 4096, linedy);
        TypeHelper.drawicon(g, CodeEditor.dpiscale(18), yl + CodeEditor.dpiscale(1), item.icon);
        TextRenderer.DrawText(g, item.text, Font, new Point(CodeEditor.dpiscale(37), yl + textofs), Color.Black); if (noeval) continue;
        TextRenderer.DrawText(g, item.sv.Length > 256 ? item.sv.Substring(0, 256) : item.sv, Font, new Point(midx + CodeEditor.dpiscale(2), yl + textofs), Color.Black);
      }
      if (!noeval) g.DrawLine(Pens.LightGray, midx, 0, midx, 4096);
    }
    bool cmsopen; static bool showhex;
    protected override void OnMouseLeave(EventArgs e)
    {
      if (cmsopen || drop != null) return;
      if (overitem != null) { overitem = null; Invalidate(); }
    }
    internal Item selection() { return overitem; }
    internal void select(Item item)
    {
      if (overitem == item) return;
      overitem = item; Invalidate();
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
      if (cmsopen || drop != null) return;
      var p = e.Location; var i = (p.Y - AutoScrollPosition.Y) / linedy;
      var item = (uint)i < (uint)items.Length ? items[i] : null;
      select(item);
      if (item != null && ontooltip != null)
      {
        if (!noeval && TextRenderer.MeasureText(item.sv, Font).Width < ClientSize.Width - midx) return;
        ontooltip(RectangleToScreen(new Rectangle(noeval ? 0 : midx, i * linedy + AutoScrollPosition.Y, Width, linedy)), item);
      }
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
      if (e.Button != MouseButtons.Left) return;
      var p = e.Location; var i = (p.Y - AutoScrollPosition.Y) / linedy;
      if ((uint)i >= (uint)items.Length) return;
      var item = items[i];
      if (onclick != null && onclick(item)) return;
      if (drop != null) { var x = openitem; closedrop(); if (x == item) return; }
      if (p.X > CodeEditor.dpiscale(18)) return;
      if (item.pv == null || Type.GetTypeCode(item.pv.GetType()) != TypeCode.Object) return;
      drop = new TypeExplorer();
      try { drop.items = evalitems(item.pv, item.info).ToArray(); }
      catch { }
      drop.parent = this; drop.ontooltip = ontooltip;
      drop.Location = PointToScreen(new Point(CodeEditor.dpiscale(18), (i + 1) * linedy - 2 + AutoScrollPosition.Y));
      drop.Show(); openitem = item; BackColor = SystemColors.Control;// Color.FromArgb(0xe8, 0xe8, 0xe8);// Invalidate();

    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
      if (e.Button == MouseButtons.Right)
      {
        if (noeval) return;
        var cms = new ContextMenuStrip();
        cms.Items.Add("Copy", null, (p, t) => Clipboard.SetDataObject(overitem.text, true));
        cms.Items.Add("Copy Value", null, (p, t) => Clipboard.SetDataObject(string.Format("{0}", overitem.pv), true));
        cms.Items.Add("Copy Expression", null, (p, t) => Clipboard.SetDataObject(string.Format("{0}\r\n{1}", overitem.text, overitem.pv), true));
        cms.Items.Add(new ToolStripSeparator());
        cms.Items.Add(new ToolStripMenuItem("Hexadecimal Display", null, (p, t) => { showhex ^= true; for (var x = this; x != null; x = x.parent) { x.Format(); x.Invalidate(); } }) { Checked = showhex });
        cms.Opening += (p, t) => cmsopen = true;
        cms.Closing += (p, t) => cmsopen = false;
        cms.Show(this, e.Location);
        return;
      }
      base.OnMouseUp(e);
    }
    internal new void OnMouseWheel(MouseEventArgs e)
    {
      base.OnMouseWheel(e);
    }
    internal new void OnKeyDown(KeyEventArgs e)
    {
      if (drop != null) { drop.OnKeyDown(e); return; }
      if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
      {
        var i = Array.IndexOf(items, overitem);
        //i = (i = i < 0 ? 0 : e.KeyCode == Keys.Up ? i - 1 : i + 1) < 0 ? items.Length - 1 : i >= items.Length ? 0 : i;
        i = Math.Max(0, Math.Min(items.Length - 1, e.KeyCode == Keys.Up ? i - 1 : i + 1));
        select(items[i]);
        var y = linedy * i; var size = ClientSize;
        if (y + AutoScrollPosition.Y < 0)
          AutoScrollPosition = new Point(0, y);
        else
          if (y + linedy + AutoScrollPosition.Y > size.Height)
          AutoScrollPosition = new Point(0, y + linedy - size.Height);
        e.Handled = true; return;
      }

    }
    internal new void OnKeyPress(KeyPressEventArgs e)
    {
      if (e.KeyChar == 13 || e.KeyChar == ' ')
      {
        if (overitem != null && onclick != null && onclick(overitem)) { e.Handled = true; return; }
      }
    }
    static IEnumerable<Item> evalitems(object obj, object info)
    {
      if (TypeHelper.IsComDisposed(obj)) yield break;
      var typo = obj.GetType();
      if (obj is IEnumerable<Item>) { foreach (var x in ((IEnumerable<Item>)obj).OrderBy(p => p.text)) yield return x; yield break; }
      var type = info as Type ?? (info is FieldInfo fi ? fi.FieldType : info is PropertyInfo pi ? pi.PropertyType : typo);
      if (type.IsArray)
      {
        if (type.GetElementType().IsPointer) yield break; //todo: it works probably, enable after test
        var a = (Array)obj; for (int i = 0; i < a.Length; i++) yield return new Item { icon = 8, obj = a.GetValue(i), text = string.Format("[{0}]", i) }; yield break;
      }
      if (type.IsPointer)
      {
        var p = (UIntPtr)obj; if (p == UIntPtr.Zero) yield break;
        obj = Marshal.PtrToStructure(tointptr(p), type.GetElementType());
        yield return new Item { icon = 8, obj = obj, text = string.Format("[{0}]", 0) }; yield break;
      }
      //var dtp = type.GetCustomAttributes(typeof(DebuggerTypeProxyAttribute), true); //the MS approach
      if (info != null)
      {
        var list = obj as System.Collections.IList;
        if (list != null)
        {
          for (int i = 0; i < list.Count; i++) { var o = list[i]; yield return new Item { icon = 8, obj = o, info = o != null ? o.GetType() : null, text = string.Format("[{0}]", i) }; }
          yield return new Item { icon = 8, text = "Raw View", obj = obj }; yield break;
        }
      }

      if (type != typo && (type.IsInterface || type.IsInstanceOfType(typo)))
        yield return new Item { icon = 8, text = string.Format("[{0}]", TypeHelper.fullname(obj.GetType())), obj = obj };

      var bt = type.BaseType;
      if (bt != null && bt != typeof(object) && bt != typeof(ValueType))
        yield return new Item { icon = 8, text = string.Format("base {{{0}}}", TypeHelper.fullname(bt)), obj = obj, info = type.BaseType };

      foreach (var x in
        type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).
        Where(p => p.CanRead && isbrowsable(p) && p.GetIndexParameters().Length == 0).
        Select(p => new Item { text = p.Name, obj = obj, info = p, icon = TypeHelper.image(p) }).Concat(
        type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).
        Where(p => isbrowsable(p)).
        Select(p => new Item { text = p.Name, obj = obj, info = p, icon = TypeHelper.image(p) })).
        OrderBy(p => p.text)) yield return x;

      var stats = type.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).
        Where(p => p.CanRead && p.GetIndexParameters().Length == 0).
        Select(p => new Item { text = p.Name, obj = obj, info = p, icon = TypeHelper.image(p)/*23*/ }).Concat(
        type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).
        Where(p => !p.Name.Contains('$')).
        Select(p => new Item { text = p.Name, obj = obj, info = p, icon = TypeHelper.image(p)/* 22*/ }));
      if (stats.Any()) yield return new Item { icon = 17, text = "Static members", obj = stats, info = type.BaseType };

      if (typo.IsCOMObject)
      {
        //work around for MS bug in NET4,  
        ((System.Collections.Hashtable)typeof(TypeDescriptor).GetField("_defaultProviders", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null)).Clear();
        foreach (var pr in
           TypeDescriptor.GetProperties(obj).OfType<PropertyDescriptor>(). //Where(p => p.IsBrowsable).
           Select(p => new Item { text = p.Name, obj = obj, info = p }). //icon = TypeHelper.image(p) }).
           OrderBy(p => p.text)) yield return pr;
        //var idisp = obj as IReflect;
        //if (idisp != null)
        //  foreach (var pr in 
        //    idisp.GetProperties(BindingFlags.Instance). 
        //    Where(p => p.CanRead).
        //    Select(p => new Item { text = p.Name, obj = obj, info = p, icon = TypeHelper.image(p) }).
        //    OrderBy(p => p.text)) yield return pr;
      }
    }
    static bool isbrowsable(MemberInfo mi)
    {
      var p = mi.GetCustomAttribute<DebuggerBrowsableAttribute>();
      if (p != null && p.State == DebuggerBrowsableState.Never) return false;
      return true;
    }
    static unsafe IntPtr tointptr(UIntPtr p)
    {
      return new IntPtr(p.ToPointer());
    }
    static unsafe string evals(object p, object rt)
    {
      if (p == null) return "null"; if (TypeHelper.IsComDisposed(p)) return "disposed";
      var t = p.GetType();
      if (t.IsCOMObject) return (rt as Type ?? (rt is FieldInfo fi ? fi.FieldType : rt is PropertyInfo pi ? pi.PropertyType : t)).FullName;
      //var dda = t.GetCustomAttributes(typeof(DebuggerDisplayAttribute), true).OfType<DebuggerDisplayAttribute>().FirstOrDefault();
      //if (dda != null) {  var aa = dda.Value.Split('{','}'); /}
      if (t.IsArray) { var s = TypeHelper.shortname(t); return s.Insert(s.IndexOf(']'), ((Array)p).Length.ToString()); }
      //if (t.IsArray) return string.Format("{0}[{1}]", TypeHelper.shortname(t.GetElementType()), t.GetProperty("Length").GetValue(p, null));
      if (p is IEnumerable<Item>) return string.Empty;
      if (p is Delegate) return string.Format("{{{0} = {{{1}}}}}", TypeHelper.fullname(t), ((Delegate)p).Method.ToString()); //not standard
      if (p is MethodBase) return string.Format("{{Method = {{{0}}}}}", ((MethodBase)p).ToString());
      if (p is string) return string.Format("\"{0}\"", unescape((string)p));
      if (p is Exception) return string.Format("{{{0}}}", unescape(((Exception)p).Message));
      if (p is System.Collections.IList) return rt != null ? string.Format("Count = {0}", evals(((System.Collections.IList)p).Count, null)) : string.Empty;
      if (t.IsGenericType /*|| Archive.IsRtType(t)*/) return string.Format("{{{0}}}", TypeHelper.shortname(t));
      var xt = rt as Type; if (xt != null && xt.IsInterface) return TypeHelper.fullname(xt);
      var tc = Type.GetTypeCode(t);
      switch (tc)
      {
        case TypeCode.Byte: return showhex ? "0x" + ((byte)p).ToString("X1") : p.ToString();
        case TypeCode.SByte: return showhex ? "0x" + ((sbyte)p).ToString("X1") : p.ToString();
        case TypeCode.Int16: return showhex ? "0x" + ((short)p).ToString("X4") : p.ToString();
        case TypeCode.UInt16: return showhex ? "0x" + ((ushort)p).ToString("X4") : p.ToString();
        case TypeCode.Int32: return showhex ? "0x" + ((int)p).ToString("X8") : p.ToString();
        case TypeCode.UInt32: return showhex ? "0x" + ((uint)p).ToString("X8") : p.ToString();
        case TypeCode.Int64: return showhex ? "0x" + ((long)p).ToString("X16") : p.ToString();
        case TypeCode.UInt64: return showhex ? "0x" + ((ulong)p).ToString("X16") : p.ToString();
        case TypeCode.Single: return ((float)p).ToString(CultureInfo.InvariantCulture);// +'f';
        case TypeCode.Double: return ((double)p).ToString(CultureInfo.InvariantCulture);
        case TypeCode.Decimal: return ((decimal)p).ToString(CultureInfo.InvariantCulture) + 'm';
      }
      if (p is IntPtr) return "0x" + (IntPtr.Size == 8 ? ((IntPtr)p).ToInt64().ToString("X16") : ((IntPtr)p).ToInt32().ToString("X8"));
      if (p is UIntPtr) return "0x" + (UIntPtr.Size == 8 ? ((UIntPtr)p).ToUInt64().ToString("X16") : ((UIntPtr)p).ToUInt32().ToString("X8"));
      try { var ss = p.ToString(); return t.IsPrimitive ? ss : string.Format("{{{0}}}", unescape(ss)); }
      catch { return p.GetType().ToString(); }
    }
    static object evalp(object p, object info)
    {
      try
      {
        Control.CheckForIllegalCrossThreadCalls = false;
        var pi = info as PropertyInfo;
        if (pi != null) return pi.GetValue(p, null);
        var fi = info as FieldInfo; if (fi != null) return fi.GetValue(p);
        var pd = info as PropertyDescriptor;
        if (pd != null) return pd.GetValue(p);
        return p;
      }
      catch (Exception e) { return e; }
      finally { Control.CheckForIllegalCrossThreadCalls = true; }
    }
    static string unescape(string s)
    {
      if (s != null && s.Contains('\n')) s = s.Replace("\n", "\\n").Replace("\r", "\\r");//.Replace("\t", "\\t");
      return s;
    }
  }

  static unsafe class Native
  {
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern bool SetProcessDPIAware();
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern IntPtr LoadIcon(IntPtr h, IntPtr id);
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern IntPtr SetParent(IntPtr h, IntPtr p);
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern bool WaitMessage();
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("gdi32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern IntPtr SelectObject(IntPtr hDC, IntPtr h);
    [DllImport("gdi32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern int SetTextColor(IntPtr hDC, int color);
    [DllImport("gdi32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern int TextOutW(IntPtr hDC, int x, int y, char* s, int n);
    [DllImport("gdi32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern bool DeleteObject(IntPtr hObject);
    [DllImport("gdi32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern IntPtr CreateSolidBrush(int color);
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern int FillRect(IntPtr hDC, ref Rectangle r, IntPtr brush);
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern bool InvertRect(IntPtr hDC, ref Rectangle lprc);
    [DllImport("gdi32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern int SetBkMode(IntPtr hDC, int mode);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode), SuppressUnmanagedCodeSecurity]
    internal static extern int SetWindowTheme(IntPtr hWnd, string appName, string partList);
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern IntPtr SetTimer(IntPtr hWnd, IntPtr nIDEvent, uint uElapse, IntPtr lpTimerFunc);
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern bool KillTimer(IntPtr hWnd, IntPtr uIDEvent);
    [DllImport("kernel32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, int dwFlags);
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern bool IsClipboardFormatAvailable(int format);
    //[DllImport("user32"), SuppressUnmanagedCodeSecurity]
    //internal static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnableWindow(IntPtr h, bool p);
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern IntPtr SetCapture(IntPtr h);
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern bool ValidateRect(IntPtr hWnd, IntPtr lpRect);
    [DllImport("gdi32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern bool GdiAlphaBlend(IntPtr hdc, int x, int y, int dx, int dy, IntPtr sdc, int sx, int sy, int sdx, int sdy, int bf);
    [DllImport("gdi32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern uint SetPixel(IntPtr hdc, int X, int Y, uint crColor);
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    public static extern IntPtr ShowWindow(IntPtr h, int f);
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern bool BringWindowToTop(IntPtr h);
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern IntPtr GetParent(IntPtr h);
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern IntPtr WindowFromPoint(System.Drawing.Point p);
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern bool PostMessage(IntPtr hWnd, int m, void* w, void* l);
    [DllImport("user32.dll")]
    public static extern int PeekMessage(void* msg, IntPtr window, int fmin, int fmax, int remove);
    [DllImport("ntdll.dll", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
    internal static extern IntPtr memcpy(void* d, void* s, void* n);
    [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
    internal static extern IntPtr memset(void* p, int v, void* n);
    [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
    internal static extern int memcmp(void* a, void* b, void* n);
    [DllImport("comdlg32.dll"), SuppressUnmanagedCodeSecurity]
    static extern IntPtr FindTextW(FindReplace* p);
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    static extern int SetForegroundWindow(IntPtr hWnd);
    internal unsafe struct FindReplace
    {
      int size; IntPtr owner, inst; int flags; char* find, replace; short nfind, nreplace; IntPtr cust, hook, temp;
      delegate void* Hook(void* h, int m, void* w, void* l);
      static EventHandler dlg;
      public static void Dialog(Control owner, string find, Action<string, int> act)
      {
        if (dlg != null) { dlg(act, null); return; }
        var ft = (FindReplace*)Marshal.AllocCoTaskMem(4096).ToPointer(); Hook hook;
        ft->size = sizeof(FindReplace); ft->inst = ft->cust = ft->temp = IntPtr.Zero; ft->replace = null; *(&ft->nreplace) = 0;
        ft->owner = owner.Handle; ft->flags = 0x105; //FR_DOWN | FR_MATCHCASE | FR_ENABLEHOOK
        ft->hook = Marshal.GetFunctionPointerForDelegate(hook = (a, b, c, d) => b == 0x0110 ? (void*)1 : null);
        ft->nfind = 1024; var ns = (find ?? (find = String.Empty)).Length;
        fixed (char* ps = find) Native.memcpy(ft->find = (char*)(ft + 1), ps, (void*)(Math.Min(ns + 1, ft->nfind) << 1));
        var hr = FindTextW(ft); Application.Idle += dlg = (p, e) =>
        {
          if (e == null) { act = (Action<string, int>)p; SetForegroundWindow(hr); return; }
          if ((ft->flags & 0x08) != 0) { ft->flags ^= 0x08; act(new string(ft->find), ft->flags); }//FR_FINDNEXT
          if ((ft->flags & 0x40) != 0) { Application.Idle -= dlg; Marshal.FreeCoTaskMem((IntPtr)ft); hook = null; dlg = null; } //FR_DIALOGTERM
        };
      }
    }
    internal static bool Equals(byte[] a, byte[] b)
    {
      if (a == b) return true; if (a.Length != b.Length) return false;
      fixed (byte* pa = a, pb = b) return memcmp(pa, pb, (void*)a.Length) == 0;
    }
  }
}



