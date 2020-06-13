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
using System.Xml.Linq;

namespace csg3mf
{
  class CodeEditor : UserControl, UIForm.ICommandTarget
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
      set
      {
        text = value; if (!IsHandleCreated) return;
        sela = selb = Math.Min(sela, text.Length);
        ranges?.Clear(); _format(); _updateranges(); _updatelineflags();
      }
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
    protected override void OnLoad(EventArgs e) { }
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
      switch (m.Msg)
      {
        case 0x0113: //WM_TIMER
          if (curticks++ < 5) return; curticks = 0; careton ^= true; Invalidate(rcaret);
          if (tipticks != 0 && --tipticks == 0) ontip();
          return;
        case 0x0014: // WM_ERASEBKGND
          m.Result = (IntPtr)1; return;
        case 0x0005: //WM_SIZE
        case 0x0114: //WM_HSCROLL
          Invalidate();
          break;
      }
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
    protected override void OnLayout(LayoutEventArgs e)
    {
      base.OnLayout(e);
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
    protected static bool IsLetter(char c) { return char.IsLetter(c) || c == '_'; }
    protected static bool IsLetterOrDigit(char c) { return char.IsLetterOrDigit(c) || c == '_'; }
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

  static class TypeHelper
  {
    internal static List<WeakReference> cache = new List<WeakReference>(); //dynamics only
    internal static bool IsBlittable(Type t)
    {
      if (t.IsPrimitive) return true; if (!t.IsValueType) return false;
      var a = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      for (int i = 0; i < a.Length; i++) if (!IsBlittable(a[i].FieldType)) return false;
      return true;
    }
    static string xname(Type type)
    {
      if (type.IsByRef) return string.Format("{0} {1}", "ref", shortname(type.GetElementType()));
      if (type.IsArray) return shortname(type.GetElementType()) + "[]";
      if (type.IsGenericTypeDefinition)
      {
        var t1 = type.GetGenericArguments();
        var t2 = string.Format("{0}<{1}>", type.Name.Split('`')[0], string.Join(", ", t1.Select(t => t.Name)));
        return t2;
      }
      if (type.IsGenericType)
      {
        //if (!type.IsPublic && type.IsNested && type.ReflectedType == typeof(Dynamic)) { }
        var t1 = type.GetGenericArguments();//var t2 = type.GetGenericTypeDefinition();
        var t2 = string.Format("{0}<{1}>", type.Name.Split('`')[0], string.Join(", ", t1.Select(t => shortname(t))));
        //type.IsGenericType ? "<" + string.Join(", ", type.GetGenericArguments().Select((hp, hi) => "T" + hi)) + ">"
        return t2;
      }
      return type.Name;
    }
    internal static string shortname(object p, bool buildin = true)
    {
      if (p == null) return string.Empty;
      var type = p as Type;
      if (type != null)
      {
        //if (resolver != null) type = resolver(type);
        if (buildin && !type.IsEnum)
          switch (Type.GetTypeCode(type))
          {
            case TypeCode.Object: if (type == typeof(object)) return "object"; break;
            //case TypeCode.Empty: return "void";
            case TypeCode.Boolean: return "bool";
            case TypeCode.Char: return "char";
            case TypeCode.SByte: return "sbyte";
            case TypeCode.Byte: return "byte";
            case TypeCode.Int16: return "short";
            case TypeCode.UInt16: return "ushort";
            case TypeCode.Int32: return "int";
            case TypeCode.UInt32: return "uint";
            case TypeCode.Int64: return "long";
            case TypeCode.UInt64: return "ulong";
            case TypeCode.Single: return "float";
            case TypeCode.Double: return "double";
            case TypeCode.Decimal: return "decimal";
            case TypeCode.String: return "string";
          }
        if (type == typeof(void)) return "void";
        if (type.IsGenericParameter) return type.Name;
        if (type.IsNested)
        {
          //if (type == typeof(Compiler.__null)) return "<null>";
          //if (type.DeclaringType == typeof(Compiler)) return type.Name;//dynamic
          return string.Format("{0}.{1}", shortname(type.DeclaringType), xname(type));
        }
        return xname(type);
      }
      var mi = p as MethodInfo; if (mi != null) return string.Format("{0} {1}.{2}{3}({4})", shortname(mi.ReturnType), shortname(mi.DeclaringType), mi.Name, mi.IsGenericMethod ? "<>" : null, shortname(mi.GetParameters()));
      var fi = p as FieldInfo; if (fi != null) return string.Format("{0} {1}.{2}", shortname(fi.FieldType), shortname(fi.DeclaringType), fi.Name);//XType.FieldName(fi));
      var pi = p as PropertyInfo; if (pi != null) return string.Format("{0} {1}.{2}", shortname(pi.PropertyType), shortname(pi.DeclaringType), pi.Name);
      var ei = p as EventInfo; if (ei != null) return string.Format("{0} {1}.{2}", shortname(ei.EventHandlerType), shortname(ei.DeclaringType), ei.Name);
      var pp = p as ParameterInfo[]; if (pp != null) return string.Join(", ", pp.Select(t => shortname(t)));
      var ci = p as ConstructorInfo;
      if (ci != null) return string.Format("{0} {1}({2})", shortname(ci.DeclaringType), ci.Name, shortname(ci.GetParameters()));
      return p.ToString();
    }
    static string shortname(ParameterInfo t)
    {
      var s = string.Format("{0} {1}", shortname(t.ParameterType), t.Name);
      if (t.IsDefined(typeof(ParamArrayAttribute), false)) s = string.Format("{0} {1}", "params", s);
      if (t.Position == 0 && t.Member.IsDefined(typeof(ExtensionAttribute), true)) s = string.Format("{0} {1}", "this", s);
      var v = t.DefaultValue; if (v != DBNull.Value && t.Member.DeclaringType != null) s = string.Format("[{0} = {1}]", s, v != null ? shortname(v) : "null");
      return s;
    }
    internal static string fullname(Type t)
    {
      if (!t.IsEnum) { var tc = Type.GetTypeCode(t); if (tc != TypeCode.Object || t == typeof(object)) return shortname(t, false); }
      //var x = resolver != null ? resolver(t) : t; if (x != t) return x.Name;
      return string.Format("{0}.{1}", t.Namespace, shortname(t));
    }
    internal static int image(MemberInfo p)
    {
      switch (p.MemberType)
      {
        case MemberTypes.Method:
          {
            var t = p as MethodInfo;
            return t.IsPublic ? 0 : 26;
          }
        case MemberTypes.Property:
          {
            var t = p as PropertyInfo;
            var m = t.CanRead ? t.GetGetMethod(true) : t.GetSetMethod(true);
            return m.IsPublic ? 18 : m.IsStatic ? 20 : 23;
          }
        case MemberTypes.Event:
          return 2;
        case MemberTypes.Field:
          {
            var t = p as FieldInfo;
            if (t.IsLiteral) return 5;
            return t.IsStatic ? 22 : t.IsPublic ? 8 : t.IsPrivate ? 15 : 21;
          }
        case MemberTypes.TypeInfo:
        case MemberTypes.NestedType:
          {
            var t = p as Type;
            if (t.IsInterface) return 7;
            if (t.IsEnum) return 5;
            if (t.IsSubclassOf(typeof(Delegate))) return 4;
            //if (t.DeclaringType == typeof(Compiler)) return 8;
            //if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Script.PD<>)) return 18;
            var pub = p.MemberType == MemberTypes.NestedType ? t.IsNestedPublic : t.IsPublic;
            return pub ? 8 : 15;
          }
      }
      return 0;
    }
    static string tocref(Type type)
    {
      if (type.IsGenericParameter)
      {
        if (type.DeclaringMethod != null) return string.Format("``{0}", type.GenericParameterPosition);
        if (type.DeclaringType != null) return string.Format("`{0}", type.GenericParameterPosition);
      }
      var ss = type.FullName != null ? type.FullName.Replace('+', '.') : type.Name;
      if (type.IsGenericType && !type.IsGenericTypeDefinition) { int t = ss.LastIndexOf("`"); if (t >= 0) ss = ss.Substring(0, t); }
      if (type.IsGenericType) ss = string.Format("{0}{{{1}}}", ss, string.Join(",", type.GetGenericArguments().Select(t => tocref(t)).ToArray()));
      return ss;
    }
    static string tocref(ParameterInfo p)
    {
      var t = p.ParameterType; return t.Name.EndsWith("&") ? tocref(t.GetElementType()) + '@' : tocref(t);
    }
    static string tocref(MethodInfo mi)
    {
      var dt = mi.DeclaringType;
      if (dt == null) { return mi.ToString(); }
      if (dt.IsGenericType && !dt.IsGenericTypeDefinition)
      {
        dt = dt.GetGenericTypeDefinition();
        mi = dt.GetMethods().FirstOrDefault(m => m.Name == mi.Name && m.GetParameters().Length == mi.GetParameters().Length);
      }
      var ss = $"M:{dt.FullName.Replace('+', '.')}.{mi.Name}";
      if (mi.IsGenericMethodDefinition) ss += '`';
      var ga = mi.GetGenericArguments(); if (ga != null && ga.Length > 0) ss = string.Format("{0}`{1}", ss, ga.Length);
      var pp = mi.GetParameters();
      if (pp != null && pp.Length > 0) ss = string.Format("{0}({1})", ss, string.Join(",", pp.Select(p => tocref(p)).ToArray()));
      return ss.ToString();
    }
    static string tocref(ConstructorInfo mi)
    {
      var dt = mi.DeclaringType;
      var ss = string.Format("M:{0}.{1}.{2}", dt.Namespace, dt.Name, "#ctor");
      if (mi.IsGenericMethodDefinition) ss += '`';
      var pp = mi.GetParameters();
      if (pp != null && pp.Length > 0) ss = string.Format("{0}({1})", ss, string.Join(",", pp.Select(p => tocref(p)).ToArray()));
      return ss.ToString();
    }
    static Tuple<Type, string> tocref(object p)
    {
      var type = p as Type; if (type != null) return Tuple.Create(type, "T:" + type.FullName.Replace('+', '.'));
      var mi = p as MethodInfo; if (mi != null) return mi.DeclaringType != null ? Tuple.Create(mi.DeclaringType, tocref(mi)) : null;
      var ci = p as ConstructorInfo; if (ci != null) return Tuple.Create(ci.DeclaringType, tocref(ci));
      var pi = p as MemberInfo;
      if (pi != null) return Tuple.Create(pi.DeclaringType, string.Format("{0}:{1}.{2}.{3}",
       pi.MemberType == MemberTypes.Field ? "F" : pi.MemberType == MemberTypes.Event ? "E" : "P",
         pi.DeclaringType.Namespace, pi.DeclaringType.Name, pi.Name));
      return null;
    }
    static string fromcref(string s)
    {
      return s.Length > 1 && s[1] == ':' ? s.Substring(2) : s;
    }
    static string docu(object p)
    {
      var doc = tocref(p); if (doc == null) return null;
      var assembly = doc.Item1.Assembly; if (assembly.IsDynamic) return null;
      var xml = cache.Select(t => t.Target).OfType<XElement>().FirstOrDefault(t => t.Annotation<Assembly>() == assembly);
      if (xml == null)
      {
        try
        {
          var file = Directory.EnumerateFiles(assembly.GlobalAssemblyCache ?
              Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Reference Assemblies\\Microsoft\\Framework\\.NETFramework\\v4.0") :
              Path.GetDirectoryName(assembly.Location), Path.ChangeExtension(Path.GetFileName(assembly.Location), ".xml"), SearchOption.AllDirectories).FirstOrDefault();
          //Debug.WriteLine("load: " + file);
          if (file != null) xml = XElement.Load(file);
          var s = (string)xml.Attribute("redirect");
          if (s != null)
          {
            s = Environment.ExpandEnvironmentVariables(s);
            if (s.Contains('%')) s = Environment.ExpandEnvironmentVariables(s.Replace("DIR%", "(X86)%\\"));
            xml = XElement.Load(s);
          }
        }
        catch { }
        if (xml == null) xml = new XElement("null"); xml.AddAnnotation(assembly); TypeHelper.cache.Add(new WeakReference(xml));
      }
      var members = xml.Element("members"); if (members == null) return null;
      var member = members.Elements("member").FirstOrDefault(u => u.Attribute("name").Value == doc.Item2); if (member == null) return null;
      var summary = member.Element("summary"); if (summary == null) return null;
      var ss = evalsee(summary);
      var paras = member.Elements("param");
      if (paras.Any()) ss = string.Format("{0}\n\nParameter:\n{1}", ss, string.Join("\n",
        paras.Select(pa => string.Format("  {0}: {1}", pa.Attribute("name").Value, evalsee(pa)))));
      return ss;
    }
    static string evalsee(XElement xe)
    {
      return string.Concat(xe.Nodes().Select(e =>
      {
        var t1 = e as XText; if (t1 != null) return t1.Value;
        var t2 = e as XElement; if (t2 != null && t2.Name == "see") { var t3 = t2.Attribute("cref"); if (t3 != null) return fromcref(t3.Value); }
        return string.Empty;
      })).Trim();
    }
    internal static string tooltip(object p)
    {
      var infos = p as MemberInfo[];
      if (infos != null) p = infos.Length > 1 && infos[0] is MethodInfo ? infos.OrderByDescending(x => ((MethodInfo)x).GetParameters().Length).Last() : infos[0];// infos[infos.Length - 1];

      var type = p as Type;
      if (type != null)
      {
        //if (type.DeclaringType == typeof(Compiler)) return string.Format("{0}\n{1}", type.Name, "Represents an object whose operation will be resolved at runtime.");
        return string.Format("{0} {1}\n{2}",
         type.IsSubclassOf(typeof(Delegate)) ? "delegate" :
         type.IsEnum ? "enum" :
         type.IsInterface ? "interface" :
         type.IsClass ? "class" : "struct",
         fullname(type),
         docu(type.IsGenericType ? type.GetGenericTypeDefinition() : type));
      }
      var mi = p as MethodInfo;
      if (mi != null) return string.Format("{0} {1}\n{2}",
        mi.IsDefined(typeof(ExtensionAttribute), false) ? string.Format("({0}) {1}", "extension", shortname(mi)) : shortname(mi),
        infos != null && infos.Length > 1 ? string.Format("(+ {0} overload(s))", infos.Length - 1) : string.Empty,
        docu(mi));

      var ci = p as ConstructorInfo;
      if (ci != null)
        return string.Format("{0}.{0}({1})\n{2}", shortname(ci.DeclaringType), shortname(ci.GetParameters()), docu(ci));

      var fi = p as MemberInfo;
      if (fi != null) return string.Format("{0}\n{1}", shortname(fi), docu(fi));

      var xx = p as Func<string>; if (xx != null) return xx();

      return string.Format("{0} {1}", "namespace", p);
      //return p.ToString();
    }
    internal static string tooltip((int i, int n, int v, object p) tpos, string text, bool skipdef = true)
    {
      switch (tpos.v & 0x0f)
      {
        case 0x00:
        case 0x03:
          if (tpos.p is string) return null; //inline xml
          var name = text.Substring(tpos.i, tpos.n);
          if (tpos.p is DynamicMethod[])
          {
            if (skipdef && (tpos.v & 0x80) != 0) return null;
            var acc = (DynamicMethod[])tpos.p;
            if (acc[0] != null) return string.Format("{0} {1}", shortname(acc[0].ReturnType), name);
            if (acc[1] != null) return string.Format("{0} {1}", shortname(acc[1].GetParameters()[1].ParameterType), name);
            return null;
          }
          if (tpos.p is DynamicMethod)
          {
            if (skipdef && (tpos.v & 0x80) != 0) return null;
            var mi = (DynamicMethod)tpos.p;
            return string.Format("{0} {1}({2})", shortname(mi.ReturnType), name, shortname(mi.GetParameters().Skip(1).ToArray()));
          }
          return tpos.n == 0 ? null : tooltip(tpos.p);
        case 0x02: return string.Format("({0}) {1} {2}", "const", shortname(tpos.p as Type), text.Substring(tpos.i, tpos.n));
        case 0x04: return string.Format("({0}) {1} {2}", "local variable", shortname(tpos.p as Type), text.Substring(tpos.i, tpos.n));
        case 0x05: return string.Format("({0}) {1} {2}", "parameter", shortname(tpos.p as Type), text.Substring(tpos.i, tpos.n));
        case 0x06: return string.Format("({0}) {1} {2}", "variable", shortname((Type)tpos.p), text.Substring(tpos.i, tpos.n));
        case 0x07: return string.Format("({0}) {1} {2}", "property", shortname(((Type)tpos.p).GetGenericArguments()[0]), text.Substring(tpos.i, tpos.n));
        case 0x08: return string.Format("{0} {1}", "namespace", tpos.p);
        case 0x01: return "(dynamic expression)\nThis operation will be resolved at runtime.";
      }
      return null;
    }
    static Bitmap icons;
    internal static void drawicon(Graphics g, int x, int y, int i)
    {
      if (icons == null) { icons = Properties.Resources.typicons; icons.MakeTransparent(); }
      var d = CodeEditor.dpiscale(20);
      g.DrawImage(icons, new Rectangle(x, y, d, d), new Rectangle(i * 20, 0, 20, 20), GraphicsUnit.Pixel);
    }
    internal static bool IsComDisposed(object p)
    {
      //There's no way to determine whether an object is disposed other than using
      //it and getting an ObjectDisposedException
      if (!p.GetType().IsCOMObject) return false;
      try { Marshal.Release(Marshal.GetIUnknownForObject(p)); return false; }
      catch { return true; }
    }
    static MethodInfo __filter(MethodInfo mi, bool priv)
    {
      if (mi == null) return mi; var at = mi.Attributes;
      if ((at & MethodAttributes.SpecialName) != 0) return null;
      if (priv) { if (mi.Name.Contains('.')) return null; return mi; }
      if (mi.IsStatic && mi.ReflectedType.IsEnum) return null;
      //if (!priv && mi.IsStatic && mi.ReflectedType.IsEnum) return null;
      switch (at & MethodAttributes.MemberAccessMask)
      {
        case MethodAttributes.Family: // protected
        case MethodAttributes.FamORAssem: //internal protected
        case MethodAttributes.Public: return mi;
      }
      return null;
    }
    static PropertyInfo __filter(PropertyInfo pi, bool priv)
    {
      if (priv || pi == null) return pi;
      var mi = pi.CanRead ? pi.GetGetMethod(true) : pi.GetSetMethod(true);
      var at = mi.Attributes; //if((at & MethodAttributes.SpecialName) != 0) return null;
      var ma = at & MethodAttributes.MemberAccessMask;
      switch (ma)
      {
        case MethodAttributes.Family: // protected
        case MethodAttributes.FamORAssem: //internal protected
        case MethodAttributes.Public: return pi;
      }
      return null;
    }
    static FieldInfo __filter(FieldInfo fi, bool priv)
    {
      if (priv || fi == null) return fi;
      var at = fi.Attributes;
      if ((at & FieldAttributes.SpecialName) != 0) return null;
      switch (at & FieldAttributes.FieldAccessMask)
      {
        case FieldAttributes.Family: // protected
        case FieldAttributes.FamORAssem: //internal protected
        case FieldAttributes.Public: return fi;
      }
      return null;
    }
    static Type __filter(Type ti, bool priv)
    {
      if (priv || ti == null) return ti;
      var ma = ti.Attributes & TypeAttributes.VisibilityMask;
      switch (ma)
      {
        case TypeAttributes.NestedFamily: // protected
        case TypeAttributes.NestedFamORAssem: //internal protected
        case TypeAttributes.NestedPublic:
          return ti;
      }
      return null;
    }
    internal static bool __filter(MemberInfo p, bool priv)
    {
      switch (p.MemberType)
      {
        case MemberTypes.Method: return __filter((MethodInfo)p, priv) != null;
        case MemberTypes.Property: return __filter((PropertyInfo)p, priv) != null;
        case MemberTypes.Field: return __filter((FieldInfo)p, priv) != null;
        case MemberTypes.NestedType: return __filter((Type)p, priv) != null;
        case MemberTypes.Event: return true;
      }
      return false;
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
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern bool InvertRect(IntPtr hDC, ref Rectangle lprc);
    [DllImport("gdi32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern int SetBkMode(IntPtr hDC, int mode);
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern IntPtr SetTimer(IntPtr hWnd, IntPtr nIDEvent, uint uElapse, IntPtr lpTimerFunc);
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern bool KillTimer(IntPtr hWnd, IntPtr uIDEvent);
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern bool IsClipboardFormatAvailable(int format);
    [DllImport("gdi32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern bool GdiAlphaBlend(IntPtr hdc, int x, int y, int dx, int dy, IntPtr sdc, int sx, int sy, int sdx, int sdy, int bf);
    [DllImport("gdi32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern uint SetPixel(IntPtr hdc, int X, int Y, uint crColor);
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    public static extern IntPtr ShowWindow(IntPtr h, int f);
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern IntPtr WindowFromPoint(System.Drawing.Point p);
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    internal static extern bool PostMessage(IntPtr hWnd, int m, void* w, void* l);
    [DllImport("ntdll.dll", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
    internal static extern IntPtr memcpy(void* d, void* s, void* n);
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
  }
}



