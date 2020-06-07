using csg3mf.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Design;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using static csg3mf.CDX;

namespace csg3mf
{
  static class Program
  {
    [STAThread]
    static void Main()
    {
      Native.SetProcessDPIAware();
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);
      Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
      Application.Run(new MainFrame());
      Factory.SetDevice(0xffffffff);
    }
  }

  class MainFrame : UIForm
  {
    public MainFrame()
    {
      Icon = Icon.FromHandle(Native.LoadIcon(Marshal.GetHINSTANCE(GetType().Module), (IntPtr)32512));
      StartPosition = FormStartPosition.WindowsDefaultBounds;
      MainMenuStrip = new MenuStrip();
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
          new MenuItem(5027, "Goto &Definition", Keys.F12 ),
          new ToolStripSeparator(),
          new MenuItem(2100, "Properties...", Keys.Alt | Keys.Enter)
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
          new MenuItem(5025 ,"&IL Code..."),
          new MenuItem(1120, "3MF Content..."),
          new ToolStripSeparator(),
#if(DEBUG)
          new ToolStripMenuItem("GC.Collect", null, (p,e) => {
            Debug.WriteLine("GC.Collect()");
            Marshal.CleanupUnusedObjectsInCurrentContext();
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers(); }) { ShortcutKeys = Keys.Control|Keys.Shift|Keys.Alt|Keys.F12 },
          new ToolStripSeparator(),
#endif
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
      Controls.Add(MainMenuStrip);
      //Controls.Add(new StatusBar());// BackColor = Color.FromArgb(80, 85, 130) });
      Neuron.Debugger = (p, v) => ((ScriptView)ShowView(typeof(ScriptView), p, DockStyle.Fill)).edit.Show(v);
    }
    protected override void OnHandleCreated(EventArgs e)
    {
      var reg = Application.UserAppDataRegistry;
      var args = Environment.GetCommandLineArgs();
      if (args.Length > 1)
      {
        try { Open(args[1]); return; }
        catch (Exception ex) { MessageBox.Show(ex.Message, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error); Environment.Exit(0); }
      }
      if (reg.GetValue("lod") is string lod)
      {
        if (!Debugger.IsAttached) reg.DeleteValue("lod", false);
        try { var lwp = reg.GetValue("lwp"); if (lwp is long) setrestore((long)lwp); Open(lod); }
        catch (Exception ex) { Debug.WriteLine(ex.Message); }
      }
      if (path == null) OnNew(null);
      base.OnHandleCreated(e);
    }
    string path; Container cont; internal CDXView view; NeuronEditor edit;
    void UpdateTitle()
    {
      Text = string.Format("{0} - {1} {2} Bit {3}", path ?? string.Format("({0})", "Untitled"), Application.ProductName, Environment.Is64BitProcess ? "64" : "32", COM.DEBUG ? "Debug" : "Release");
    }
    void Open(string path)
    {
      string script = null;
      if (path == null) { cont = new Container(null); script = "using static csg3mf.CSG;\n"; }
      else cont = new Container(Import3MF(path, out script, out _));
      DoubleBuffered = true;
      while (Controls[0] is Frame f) f.Dispose(); edit = null;
      this.path = path; UpdateTitle();
      if (script != null)
      {
        NeuronEditor.NeuronInit(cont, script);
        edit = ((ScriptView)ShowView(typeof(ScriptView), cont, DockStyle.Left, ClientSize.Width / 2)).edit;
      }
      view = (CDXView)ShowView(typeof(CDXView), cont.Nodes, DockStyle.Fill);
      cont.OnUpdate = () => { view.IsModified = false; view.Invalidate(); }; view.infos = cont.Infos;
      Update(); DoubleBuffered = false;
      if (path != null) mru(path, path);

    }
    protected override int OnCommand(int id, object test)
    {
      switch (id)
      {
        case 1000: return OnNew(test);
        case 1010: return OnOpen(test);
        case 1020: return OnSave(test, false);
        case 1025: return OnSave(test, true);
        case 1050: return OnLastFiles(test);
        case 1100: if (test == null) Close(); return 1;
        case 2100: if (test == null) ShowView(typeof(PropertyView), view, DockStyle.Left); return 1;
        //case 2100: if (test == null) { var p = (PropertyGrid)ShowView(typeof(PropertyGrid)); p.SelectedObject = p; } return 1;
        case 1120: return OnShow3MF(test);
        case 5070: if (test == null) OnAbout(); return 1;
        case 3015: //OnDriver(test);
        case 3016: //OnSamples(test);
          return view.OnCommand(id, test);
        case 4021: return OnScript(test);
      }
      return base.OnCommand(id, test);
    }
    unsafe void OnAbout()
    {
      var a = CSG.Factory.Version;
      var b = CDX.Factory.Version;
      MessageBox.Show($"CSG Version {((byte*)&a)[3]}.{((byte*)&a)[2]} {((byte*)&a)[0] << 3} Bit {(((byte*)&a)[1] != 0 ? "Debug" : "Release")}\n" +
                      $"CDX Version {((byte*)&b)[3]}.{((byte*)&b)[2]} {((byte*)&b)[0] << 3} Bit {(((byte*)&b)[1] != 0 ? "Debug" : "Release")}",
        Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    int OnNew(object test)
    {
      if (test != null) return 1;
      if (edit != null && edit.askstop()) return 1;
      if (!AskSave()) return 1;
      Open(null); return 1;
    }
    int OnOpen(object test)
    {
      if (test != null) return 1; if (edit != null && edit.askstop()) return 1;
      var dlg = new OpenFileDialog() { Filter = "3MF files|*.3mf|All files|*.*" };
      if (path != null) dlg.InitialDirectory = Path.GetDirectoryName(path);
      if (dlg.ShowDialog(this) != DialogResult.OK) return 1;
      if (edit != null && edit.askstop()) return 1;
      if (!AskSave()) return 1;
      Cursor.Current = Cursors.WaitCursor; Open(dlg.FileName);
      return 1;
    }
    unsafe int OnSave(object test, bool saveas)
    {
      if (test != null) return 1;
      var s = path;
      if (saveas || s == null)
      {
        var dlg = new SaveFileDialog() { Filter = "3MF file|*.3mf|All files|*.*", DefaultExt = "3mf" };
        if (s != null) { dlg.InitialDirectory = Path.GetDirectoryName(s); dlg.FileName = Path.GetFileName(s); }
        if (dlg.ShowDialog(this) != DialogResult.OK) return 1; s = dlg.FileName;
      }
      Cursor.Current = Cursors.WaitCursor;
      var str = COM.SHCreateMemStream();
      view.view.Thumbnail(256, 256, 4, 0x00ffffff, str);
      cont.Nodes.Export3MF(s, str, edit != null ? edit.EditText : null, null);
      if (path != s) { path = s; UpdateTitle(); mru(path, path); }
      IsModified = false; Cursor.Current = Cursors.Default;
      return 1;
    }
    int OnLastFiles(object test)
    {
      if (test is string path)
      {
        if (edit != null && edit.askstop()) return 1;
        if (!AskSave()) return 1;
        mru(null, path); Cursor.Current = Cursors.WaitCursor; Open(path);
        return 1;
      }
      var item = test as MenuItem; foreach (var s in mru(null, null)) item.DropDownItems.Add(s).Tag = s;
      return 0;
    }
    bool AskSave()
    {
      if (cont == null) return true;
      if (IsModified) return true;
      switch (MessageBox.Show(this, path == null ? "Save changings?" : $"Save changings in {path}?", Application.ProductName, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Exclamation))
      {
        case DialogResult.No: return true;
        case DialogResult.Yes: OnCommand(1020, null); return IsModified;
      }
      return false;
    }
    bool IsModified
    {
      get => (edit == null || !edit.IsModified) && !view.IsModified;
      set { view.IsModified = false; if (edit != null) edit.IsModified = false; }
    }
    int OnShow3MF(object test)
    {
      if (test != null) return 1;
      Cursor.Current = Cursors.WaitCursor;
      var el = cont.Nodes.Export3MF(null, null, null, null);
      var view = (XmlEditor)ShowView(typeof(XmlEditor), cont, DockStyle.Fill);
      view.Text = "3MF Content"; view.EditText = el.ToString();
      return 1;
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
      if (e.Cancel = !AskSave()) return; IsModified = false; edit?.OnFormClosing(this);
      var reg = Application.UserAppDataRegistry;
      reg.SetValue("lwp", getrestore(), Microsoft.Win32.RegistryValueKind.QWord);
      if (path != null) reg.SetValue("lod", path); else reg.DeleteValue("lod", false);
    }
    List<string> mru(string add = null, string rem = null)
    {
      var reg = Application.UserAppDataRegistry; var ss = reg.GetValue("mru") as string[];
      var list = ss != null ? ss.ToList() : new List<string>();
      if (rem != null) list.RemoveAll(s => string.Compare(s, rem, true) == 0);
      if (add != null) { list.Insert(0, add); if (list.Count > 6) list.RemoveRange(6, list.Count - 6); }
      if (rem != null || add != null) reg.SetValue("mru", list.ToArray()); return list;
    }
    unsafe long getrestore()
    {
      var z = WindowState == FormWindowState.Maximized;
      var r = z ? RestoreBounds : DesktopBounds;
      long code; var s = (short*)&code;
      s[0] = (short)r.Left; s[1] = (short)r.Top; s[2] = (short)r.Width; s[3] = (short)(z ? -r.Height : r.Height);
      return code;
    }
    unsafe void setrestore(long code)
    {
      var s = (short*)&code; var r = new Rectangle(s[0], s[1], s[2], s[3]);
      StartPosition = FormStartPosition.Manual; var z = r.Height < 0; if (z) r.Height = -r.Height;
      if (SystemInformation.VirtualScreen.IntersectsWith(r)) DesktopBounds = r; else StartPosition = FormStartPosition.WindowsDefaultLocation;
      WindowState = z ? FormWindowState.Maximized : FormWindowState.Normal;
    }
    protected unsafe override void WndProc(ref Message m)
    { // WM_SYSCOMMAND CLOSE -> WM_CLOSE  
      if (m.Msg == 0x112 && m.WParam == (IntPtr)0xf060) { Native.PostMessage(m.HWnd, 0x0010, null, null); return; }
      base.WndProc(ref m);
    }
    class ScriptView : UserControl, ICommandTarget
    {
      int ICommandTarget.OnCommand(int id, object test)
      {
        if (id == 0) { MenuItem.Update(tb.Items); tb.Update(); } //ui
        return edit.OnCommand(id, test);
      }
      internal NeuronEditor edit = new NeuronEditor { Dock = DockStyle.Fill }; ToolStrip tb;
      protected override void OnPaintBackground(PaintEventArgs e)
      {
        //base.OnPaintBackground(e);
      }
      protected override void OnLoad(EventArgs e)
      {
        tb = new ToolStrip() { Margin = new Padding(15), ImageScalingSize = new Size(24, 24), GripStyle = ToolStripGripStyle.Hidden };
        tb.Items.Add(new Button(5011, "Run", Resources.run) { Tag = edit });
        tb.Items.Add(new ToolStripSeparator());
        tb.Items.Add(new Button(5010, "Run Debug", Resources.rund) { Tag = edit });
        tb.Items.Add(new Button(5016, "Step", Resources.stepover) { Tag = edit });
        tb.Items.Add(new Button(5015, "Step Into", Resources.stepin) { Tag = edit });
        tb.Items.Add(new Button(5017, "Step Out", Resources.stepout) { Tag = edit });
        tb.Items.Add(new ToolStripSeparator());
        tb.Items.Add(new Button(5013, "Stop", Resources.stop) { Tag = edit });
        edit.Tag = Tag;
        Visible = false;
        Controls.Add(edit);
        Controls.Add(tb);
        Visible = true;
      }
      public override string Text { get => (string)((Neuron)edit.Tag).Invoke(2, null); set { } }
    }

    int OnScript(object test)
    {
      var a = view.view.Scene.Selection();
      if (a.Take(2).Count() != 1) return 0;
      if (test != null) return 1;
      ShowView(typeof(ScriptView2), XNode.From(a.First()), DockStyle.Left);
      return 1;
    }

    internal static int inval;

  }

  class XScene
  {
    internal CDXView p;
    [Category("General")]
    public Unit BaseUnit { get => p.view.Scene.Unit; set => p.view.Scene.Unit = value; }
    [Category("Internal")]
    public int Nodes { get => p.view.Scene.Count; }
  }

  class XNode : ICustomTypeDescriptor, IExchange
  {
    internal static XNode From(INode p) => p.Tag is XNode node ? node : new XNode(p);
    XNode(INode p) { p.Tag = this; unk = Marshal.GetIUnknownForObject(p); Marshal.Release(unk); }
    protected INode Node => (INode)Marshal.GetObjectForIUnknown(unk);

    IntPtr unk; PropertyDescriptorCollection pdc;
    internal string code, props; object[] data;
    T GetMethod<T>() where T : Delegate
    {
      if (data == null)
      {
        if (code == null) return null;
        try
        {
          var expr = Script.Compile(typeof(XNode), code);
          var ctor = expr.Compile(); data = ctor(this);
          if (props != null) { var e = XElement.Parse(props); props = null; setprops(e); }
        }
        catch (Exception e) { data = new object[] { e }; }
      }
      for (int i = 1; i < data.Length; i++) if (data[i] is T f) return f;
      return null;
    }

    internal XElement getprops()
    {
      var m = GetMethod<Action<IExchange>>(); if (m == null) return null;
      todo = 3; exid = 0; param = new XElement("props"); m(this); return (XElement)param;
    }
    internal void setprops(XElement e)
    {
      var m = GetMethod<Action<IExchange>>(); if (m == null) return;
      todo = 4; exid = 0; param = e; m(this);
    }
    internal object getprop(int id)
    {
      var m = GetMethod<Action<IExchange>>();
      todo = 1; exid = 0; setid = id; param = null; m(this);
      return param;
    }
    internal void setprop(int id, object value)
    {
      var m = GetMethod<Action<IExchange>>();
      todo = 2; exid = 0; setid = id; param = value; m(this);
    }

    //[Editor(typeof(MultilineStringEditor), typeof(UITypeEditor))]
    internal string Code
    {
      get => code;
      set
      {
        if (code == value) return;
        var props = getprops();
        if (value == null || value.Trim().Length == 0) { code = null; pdc = null; data = null; MainFrame.inval |= 1; return; }
        var expr = Script.Compile(typeof(XNode), value);
        var ctor = expr.Compile(); data = ctor(this); code = value; pdc = null; MainFrame.inval |= 1;
        if (props != null) setprops(props);
      }
    }

    [Category("General")]
    public string Name { get => Node.Name; set => Node.Name = value; }
    [Category("General")]
    public bool Static { get => Node.IsStatic; set => Node.IsStatic = value; }
    [Category("Material")]
    public Color Color { get => Color.FromArgb(unchecked((int)Node.Color)); set => Node.Color = (uint)value.ToArgb(); }
    [Category("Material")]
    public int MaterialCount { get => Node.MaterialCount; }
    [Category("Transform")] public CSG.Rational LocationX { get => Node.GetTransval(09); set { var t = Node.Transform; t[09] = value; Node.Transform = t; } }
    [Category("Transform")] public CSG.Rational LocationY { get => Node.GetTransval(10); set { var t = Node.Transform; t[10] = value; Node.Transform = t; } }
    [Category("Transform")] public CSG.Rational LocationZ { get => Node.GetTransval(11); set { var t = Node.Transform; t[11] = value; Node.Transform = t; } }
    [Category("Transform")]
    public float RotationX
    {
      get => (float)Math.Round(geteuler().x * (float)(180 / Math.PI), 4);
      set { var e = geteuler(); e.x = value * (float)(Math.PI / 180); seteuler(e); }
    }
    [Category("Transform")]
    public float RotationY
    {
      get => (float)Math.Round(geteuler().y * (float)(180 / Math.PI), 4);
      set { var e = geteuler(); e.y = value * (float)(Math.PI / 180); seteuler(e); }
    }
    [Category("Transform")]
    public float RotationZ
    {
      get => (float)Math.Round(geteuler().z * (float)(180 / Math.PI), 4);
      set { var e = geteuler(); e.z = value * (float)(Math.PI / 180); seteuler(e); }
    }
    (float3 a, float3 b)[] ee;
    float3 geteuler()
    {
      var e = euler(Node.GetTransform()); if (ee == null) return e;
      if (ee[ee.Length - 1].a == e) return ee[ee.Length - 1].b;
      if (ee.Length > 1 && ee[ee.Length - 2].a == e) { Array.Resize(ref ee, ee.Length - 1); return ee[ee.Length - 1].b; }
      return e;
    }
    void seteuler(float3 e)
    {
      var m1 = Node.GetTransform(); var m2 = euler(e) * m1.mp; Node.SetTransform(m2);
      if (ee == null || ee[ee.Length - 1].a != euler(m1)) Array.Resize(ref ee, ee != null ? ee.Length + 1 : 1);
      ee[ee.Length - 1] = (euler(m2), e);
    }
    static float3 euler(in float4x3 m)
    {
      var s = m.mx.Length; var t = m._13 / s;
      return new float3(
        +(float)Math.Atan2(m._23, m._33),
        -(float)Math.Asin(t),
        t == 1 || t == -1 ? -(float)Math.Atan2(m._21, m._22) : (float)Math.Atan2(m._12, m._11));
    }
    static float4x3 euler(float3 e) => float4x3.RotationX(e.x) * float4x3.RotationY(e.y) * float4x3.RotationZ(e.z);

#if (__DEBUG)
    public string flt_matrix1 => $"{Node.GetTransform().mx}";
    public string flt_matrix2 => $"{Node.GetTransform().my}";
    public string flt_matrix3 => $"{Node.GetTransform().mz}";
    public string flt_matrix4 => $"{Node.GetTransform().mp}";

    public string rat_matrix1 => $"{Node.GetTransval(0)} {Node.GetTransval(1)} {Node.GetTransval(2)}";
    public string rat_matrix2 => $"{Node.GetTransval(3)} {Node.GetTransval(4)} {Node.GetTransval(5)}";
    public string rat_matrix3 => $"{Node.GetTransval(6)} {Node.GetTransval(7)} {Node.GetTransval(8)}";
    public string rat_matrix4 => $"{Node.GetTransval(9)} {Node.GetTransval(10)} {Node.GetTransval(11)}";
#endif

    AttributeCollection ICustomTypeDescriptor.GetAttributes() => TypeDescriptor.GetAttributes(this, true);
    string ICustomTypeDescriptor.GetClassName() => TypeDescriptor.GetClassName(this, true);
    string ICustomTypeDescriptor.GetComponentName() => TypeDescriptor.GetComponentName(this, true);
    TypeConverter ICustomTypeDescriptor.GetConverter() => TypeDescriptor.GetConverter(this, true);
    EventDescriptor ICustomTypeDescriptor.GetDefaultEvent() => TypeDescriptor.GetDefaultEvent(this, true);
    PropertyDescriptor ICustomTypeDescriptor.GetDefaultProperty() => TypeDescriptor.GetDefaultProperty(this, true);
    object ICustomTypeDescriptor.GetEditor(Type editorBaseType) => editorBaseType == typeof(ComponentEditor) ? new edit() : TypeDescriptor.GetEditor(this, editorBaseType, true);
    public class edit : ComponentEditor
    {
      public override bool EditComponent(ITypeDescriptorContext context, object component)
      {
        ((UIForm)Application.OpenForms[0]).TryCommand(null, 4021, null); return true;
      }
    }
    EventDescriptorCollection ICustomTypeDescriptor.GetEvents() => TypeDescriptor.GetEvents(this, true);
    EventDescriptorCollection ICustomTypeDescriptor.GetEvents(Attribute[] attributes) => TypeDescriptor.GetEvents(this, attributes, true);
    PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties() => TypeDescriptor.GetProperties(this, true);
    object ICustomTypeDescriptor.GetPropertyOwner(PropertyDescriptor pd) => this;
    PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties(Attribute[] attributes)
    {
      if (pdc != null) return pdc;
      pdc = TypeDescriptor.GetProperties(this, attributes, true);
      var m = GetMethod<Action<IExchange>>(); if (m == null) return pdc;
      var pds = new List<PD>(); todo = exid = setid = 0; param = pds; m(this);
      if (pds.Count != 0) pdc = new PropertyDescriptorCollection(pdc.Cast<PropertyDescriptor>().Concat(pds).ToArray());
      return pdc;
    }

    static int todo, exid, setid; static string group; static object param;
    bool IExchange.Group(string name) { exid = ((exid >> 16) + 1) << 16; group = name; return true; }
    bool IExchange.Exchange<T>(string name, ref T value, string fmt)
    {
      exid++;
      switch (todo)
      {
        case 0: ((List<PD>)param).Add(new PD(exid, name, typeof(T), group, fmt)); return false;
        case 1: if (setid == exid) { param = value; } return false;
        case 2: if (setid == exid) { value = (T)param; return true; } return false;
        case 3:
          {
            if (fmt != null && fmt.Contains("r;")) return false;
            var s = TypeDescriptor.GetConverter(typeof(T)).ConvertTo(null, CultureInfo.InvariantCulture, value, typeof(string));
            ((XElement)param).Add(new XElement("prop", new XAttribute("name", name), new XAttribute("value", s))); return false;
          }
        case 4:
          {
            if (fmt != null && fmt.Contains("r;")) return false;
            var s = getval((XElement)param, name); if (s == null) return false;
            var p = TypeDescriptor.GetConverter(typeof(T)).ConvertFrom(null, CultureInfo.InvariantCulture, s);
            if (p != null) value = (T)p; return false;
          }
      }
      return false;
    }
    static string getval(XElement e, string name)
    {
      var c = e.Elements().FirstOrDefault(p => (string)p.Attribute("name") == name);
      return c != null ? (string)c.Attribute("value") : null;
    }

    class PD : PropertyDescriptor
    {
      public PD(int id, string name, Type t, string c, string fmt) : base(name, null) { this.id = id; type = t; category = c; this.fmt = fmt; }
      int id; Type type; string category, fmt;
      public override string Category => category ?? base.Category;
      public override string Description => fmt != null ? fmt.Substring(fmt.LastIndexOf(';')+1) : base.Description;
      public override Type ComponentType => typeof(XNode);
      public override bool IsReadOnly => fmt != null && fmt.Contains("r;");// false;
      public override Type PropertyType => type;
      public override bool CanResetValue(object component) => false;
      public override void ResetValue(object component) { }
      public override bool ShouldSerializeValue(object component) => fmt != null && fmt.Contains("b;");
      public override object GetValue(object component) => ((XNode)component).getprop(id);
      public override void SetValue(object component, object value) => ((XNode)component).setprop(id, value);
    }
  }

  public interface IExchange
  {
    bool Group(string name);
    bool Exchange<T>(string name, ref T value, string fmt = null);
  }

  class PropertyView : PropertyGrid, UIForm.ICommandTarget
  {
    protected override void OnHandleCreated(EventArgs e)
    {
      view = (CDXView)Tag; base.OnHandleCreated(e); MainFrame.inval |= 1;
      Font = SystemFonts.MenuFont; DoubleBuffered = true;
      Native.SetTimer(Handle, IntPtr.Zero, 100, IntPtr.Zero);
    }
    protected unsafe override void WndProc(ref Message m)
    {
      if (m.Msg == 0x0113) //WM_TIMER 
      {
        if ((MainFrame.inval & 1) == 0) return;
        if (!Visible) return;
        MainFrame.inval &= ~1;
        var scene = view.view.Scene;
        if (nodes != null)
        {
          int i = 0, a = -1, b;
          for (; (b = scene.Select(a, 1)) != -1 && i < nodes.Length && scene[b] == nodes[i]; a = b, i++) ;
          if (b != -1 || i != nodes.Length) nodes = null;
        }
        if (nodes == null)
        {
          nodes = scene.Selection().ToArray();
          if (nodes.Length == 0) SelectedObject = new XScene { p = view };
          else SelectedObjects = nodes.Select(p => (object)XNode.From(p)).ToArray();
        }
        else Refresh();
      }
      base.WndProc(ref m);
    }
    CDXView view; INode[] nodes; object[] oldvals;
    int UIForm.ICommandTarget.OnCommand(int id, object test)
    {
      return view.OnCommand(id, test);
    }
    protected override void OnSelectedGridItemChanged(SelectedGridItemChangedEventArgs e)
    {
      var g = e.NewSelection; oldvals = null;
      if (g.GridItemType != GridItemType.Property || g.Value != null) return;
      var t = g.GetType().GetProperty("Instance"); if (t == null) return;
      var a = t.GetValue(g) as object[]; if (a == null) return;
      var d = g.PropertyDescriptor; t = d.ComponentType.GetProperty(d.Name);
      oldvals = a.Select(p => t.GetValue(p)).ToArray();
    }
    protected override void OnPropertyValueChanged(PropertyValueChangedEventArgs e)
    {
      var g = e.ChangedItem;
      var h = g.GetType().GetProperty("Instance");
      var p = h.GetValue(g);
      var d = g.PropertyDescriptor; //var t = d.ComponentType.GetProperty(d.Name);
      var f = d.GetType().GetField("descriptors", BindingFlags.Instance | BindingFlags.NonPublic);
      if (f != null) d = ((PropertyDescriptor[])f.GetValue(d))[0];
      var o = e.OldValue ?? oldvals; //if (!(p is object[]) && t.GetValue(p).Equals(o)) return;
      view.AddUndo(() =>
      {
        if (p is object[] a)
        {
          var v = new object[a.Length];
          for (int i = 0; i < a.Length; i++) { v[i] = d.GetValue(a[i]); d.SetValue(a[i], o is object[] oo ? oo[i] : o); }
          o = v;
        }
        else { var v = d.GetValue(p); d.SetValue(p, o); o = v; }
      });
      view.Invalidate();
    }
  }

  class ScriptView2 : UserControl, UIForm.ICommandTarget
  {
    public override string Text { get => $"Script {((XNode)Tag).Name}"; set { } }
    MyCodeEditor edit; UIForm.ToolStrip tb;
    protected override void OnPaintBackground(PaintEventArgs e) { }
    protected override void OnLoad(EventArgs e)
    {
      tb = new UIForm.ToolStrip() { Margin = new Padding(15), ImageScalingSize = new Size(24, 24), GripStyle = ToolStripGripStyle.Hidden };
      tb.Items.Add(new UIForm.Button(5011, "Run", Resources.run) { Tag = this });
      //tb.Items.Add(new ToolStripSeparator());
      //tb.Items.Add(new Button(5010, "Run Debug", Resources.rund) { Tag = edit });
      //tb.Items.Add(new Button(5016, "Step", Resources.stepover) { Tag = edit });
      //tb.Items.Add(new Button(5015, "Step Into", Resources.stepin) { Tag = edit });
      //tb.Items.Add(new Button(5017, "Step Out", Resources.stepout) { Tag = edit });
      //tb.Items.Add(new ToolStripSeparator());
      tb.Items.Add(new UIForm.Button(5013, "Stop", Resources.stop) { Tag = this });
      edit = new MyCodeEditor { Dock = DockStyle.Fill };
      edit.EditText = ((XNode)Tag).code ?? string.Empty;
      Visible = false;
      Controls.Add(edit);
      Controls.Add(tb);
      Visible = true;
    }
    int UIForm.ICommandTarget.OnCommand(int id, object test)
    {
      switch (id)
      {
        case 5011: return OnRun(test);
      }
      return edit.OnCommand(id, test);
    }
    int OnRun(object test)
    {
      if (test != null) return 1;
      try
      {
        //var expr = Script.Compile(typeof(XNode), edit.EditText);
        //var ctor = expr.Compile(); //data = ctor(this);
        var node = (XNode)Tag;
        var code = edit.EditText; if (node.Code == code) return 1;
        var view = ((MainFrame)FindForm()).view;
        view.Execute(() => { var t = node.Code; node.Code = code; code = t; });
        node.Code = edit.EditText;
      }
      catch (Exception e)
      {
        var p = Script.GetErrorPos(); edit.Select(p.i, p.i + p.n);
        MessageBox.Show(e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
      return 1;
    }

    class MyCodeEditor : CodeEditor
    {
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
          if (r > 1 && !(l < text.Length && char.IsDigit(text[l])))
            for (int t = 0; t < Compiler.keywords.Length; t++)
            {
              var kw = Compiler.keywords[t];
              if (kw.Length == r && kw[0] == text[i] && string.Compare(text, i, kw, 0, kw.Length, true) == 0) { color = 3; break; }
            }
          for (; i < l; i++) charcolor[i] = color; i--;
        }
        //if (typemap == null) return;
        //
        //for (int i = 0; i < typemap.Length; i++)
        //{
        //  var p = typemap[i]; if ((p.v & 0xf) == 0 && charcolor[p.i] != 3) color(p.i, p.n, 6);
        //  if (overid != 0 && (overid & 0x0f) < 8 && overid == (p.v & ~0xf0)) color2(p.i, p.n, 0x80);
        //}
        //for (int i = 0; i < spots.Length; i++) { var p = spots[i]; if ((p.v & 1) != 0) color(p.i, p.n, 4); }
        //for (int i = 0; i < spots.Length; i++) { var p = spots[i]; if ((p.v & 2) != 0) color(p.i, p.n, 5); }
        //for (int i = 0; i < errors.Length; i++) { var p = errors[i]; color2(p.i, p.n, (errors[i].v & 4) != 0 ? 0x70 : 0x10); }
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

      int rebuild = 5; (int i, int n) epos; string error;
      protected override void Replace(int i, int n, string s)
      {
        base.Replace(i, n, s);
        rebuild = s.Length == 1 && (s[0] == '.' || s[0] == '(') ? 2 : 5;
      }
      protected override void WndProc(ref Message m)
      {
        base.WndProc(ref m);
        if (m.Msg == 0x0113)//WM_TIMER
        {
          if (rebuild != 0 && --rebuild == 0) build();
        }
      }
      void build()
      {
        try { error = null; var expr = Script.Compile(typeof(XNode), EditText); }
        catch (Exception e)
        {
          epos = Script.GetErrorPos();
          for (int i = 0; i < epos.n; i++) { charcolor[epos.i + i] |= 0x70; }
          Invalidate(); error = e.Message;
        }
      }
      protected override void OnMouseMove(MouseEventArgs e)
      {
        base.OnMouseMove(e);
        var i = PosFromPoint(e.Location);
        if (error != null && i >= epos.i && i <= epos.i + epos.n)
        {
          var s = error; SetToolTip(TextBox(epos.i, epos.n), t => t == 1 ? "Error" : t == 0 ? s : null);
          return;
        }
        EndToolTip();
      }
    }
  }
}
