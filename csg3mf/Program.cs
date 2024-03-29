﻿using csg3mf.Properties;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Drawing;
using System.Drawing.Design;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.Design;
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
          new MenuItem(2035, "&Group", Keys.Control | Keys.G),
          new MenuItem(2036, "U&ngroup", Keys.Control | Keys.U),
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
          new MenuItem(1121, "Project Explorer..."),
          new MenuItem(1122, "Propview..."),
          new MenuItem(1120, "3MF Content..."),
          new MenuItem(5025 ,"Expressions..."),
          new ToolStripSeparator(),
          new MenuItem(5100, "&Format", Keys.E|Keys.Control ),
          new ToolStripSeparator(),
          new MenuItem(5110 , "Remove Unused Usings"),
          new MenuItem(5111 , "Sort Usings"),
          new ToolStripSeparator(),
          #if(DEBUG)
          new ToolStripMenuItem("GC.Collect", null, (p,e) => {
            Debug.WriteLine("GC.Collect()");
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers(); Inval(); }) { ShortcutKeys = Keys.Control|Keys.Shift|Keys.Alt|Keys.F12 },
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
    string path; IScene scene; internal CDXView view;
    void UpdateTitle()
    {
      Text = string.Format("{0} - {1} {2} Bit {3}", path ?? string.Format("({0})", "Untitled"), Application.ProductName, Environment.Is64BitProcess ? "64" : "32", COM.DEBUG ? "Debug" : "Release");
    }
    void Open(string path)
    {
      if (path == null) XScene.From(scene = Factory.CreateScene());
      else scene = Import3MF(path, out _);
      DoubleBuffered = true;
      while (Controls[0] is Frame f) f.Dispose();
      this.path = path; UpdateTitle();
      if (scene.Tag is XScene) ShowView(typeof(ScriptEditor), scene, DockStyle.Left, ClientSize.Width / 2);
      view = (CDXView)ShowView(typeof(CDXView), scene, DockStyle.Fill);
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
        case 1121: if (test == null) ShowView(typeof(ProjectView), view, DockStyle.Left); return 1;
        case 1122: if (test == null) ShowView(typeof(PropView), view, DockStyle.Left); return 1;
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
      //if (edit != null && edit.askstop()) return 1;
      if (!AskSave()) return 1;
      Open(null); return 1;
    }
    int OnOpen(object test)
    {
      if (test != null) return 1; //if (edit != null && edit.askstop()) return 1;
      var dlg = new OpenFileDialog() { Filter = "3MF files|*.3mf|All files|*.*" };
      if (path != null) dlg.InitialDirectory = Path.GetDirectoryName(path);
      if (dlg.ShowDialog(this) != DialogResult.OK) return 1;
      //if (edit != null && edit.askstop()) return 1;
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
      scene.Export3MF(s, str, null);
      if (path != s) { path = s; UpdateTitle(); mru(path, path); }
      IsModified = false; Cursor.Current = Cursors.Default;
      return 1;
    }
    int OnLastFiles(object test)
    {
      if (test is string path)
      {
        //if (edit != null && edit.askstop()) return 1;
        if (!AskSave()) return 1;
        mru(null, path); Cursor.Current = Cursors.WaitCursor; Open(path);
        return 1;
      }
      var item = test as MenuItem; foreach (var s in mru(null, null)) item.DropDownItems.Add(s).Tag = s;
      return 0;
    }
    bool AskSave()
    {
      if (scene == null) return true;
      if (!IsModified) return true;
      switch (MessageBox.Show(this, path == null ? "Save changings?" : $"Save changings in {path}?", Application.ProductName, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Exclamation))
      {
        case DialogResult.No: return true;
        case DialogResult.Yes: OnCommand(1020, null); return !IsModified;
      }
      return false;
    }
    bool IsModified
    {
      get => view.IsModified;
      set { view.IsModified = false; }
    }
    int OnShow3MF(object test)
    {
      if (test != null) return 1;
      Cursor.Current = Cursors.WaitCursor;
      var el = scene.Export3MF(null, null, null);
      var view = (XmlEditor)ShowView(typeof(XmlEditor), scene, DockStyle.Fill);
      view.Text = "3MF Content"; view.EditText = el.ToString();
      return 1;
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
      if (e.Cancel = !AskSave()) return; IsModified = false; //edit?.OnFormClosing(this);
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
    int OnScript(object test)
    {
      var a = view.view.Scene.Selection();
      if (a.Take(2).Count() != 1) return 0;
      if (test != null) return 1;
      ShowView(typeof(ScriptEditor), a.First(), DockStyle.Left);
      return 1;
    }
    internal static int inval;
    internal static void Inval()
    {
      //inval |= 1;
      ((MainFrame)MainFrame).view.Invalidate();
    }
  }

  public interface IExchange
  {
    bool Group(string name);
    bool Exchange<T>(string name, ref T value, string fmt = null);
    //bool Edit(object p = null);
  }

  abstract class XObject : IExchange, ICustomTypeDescriptor
  {
    internal abstract string Title { get; }
    internal string code, props; object[] funcs;
    internal string Code
    {
      get => code;
      set
      {
        var e = getprops();
        if (string.IsNullOrEmpty(value)) { code = null; pdc = null; funcs = null; MainFrame.Inval(); return; }
        var expr = Script.Compile(GetType(), value);
        var ctor = expr.Compile(); funcs = ctor(this);
        if (e != null) { props = null; setprops(e); }
        code = value; pdc = null; MainFrame.Inval();
      }
    }
    internal T GetMethod<T>() where T : Delegate
    {
      if (funcs == null)
      {
        if (code == null) return null;
        try
        {
          var expr = Script.Compile(GetType(), code);
          var ctor = expr.Compile(); funcs = ctor(this);
          if (props != null) { var e = XElement.Parse(props); props = null; setprops(e); }
        }
        catch (Exception e) { funcs = new object[] { e }; }
      }
      for (int i = 1; i < funcs.Length; i++) if (funcs[i] is T f) return f;
      return null;
    }

    internal XElement getprops()
    {
      if (funcs == null) return props != null ? XElement.Parse(props) : null;
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
      todo = 1; exid = 0; setid = id; param = null; Exchange(this);
      return param;
    }
    internal void setprop(int id, object value)
    {
      todo = 2; exid = 0; setid = id; param = value; Exchange(this); pdc = null;
    }
    //internal object editprop(int id, object value)
    //{
    //  todo = 5; exid = 0; setid = id; param = value; Exchange(this); return param;
    //}
    internal virtual void Exchange(IExchange ex)
    {
      GetMethod<Action<IExchange>>()?.Invoke(ex);
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
            var tc = TypeDescriptor.GetConverter(typeof(T)); if (!tc.CanConvertFrom(typeof(string))) return false;
            var s = getval((XElement)param, name); if (s == null) return false;
            var p = TypeDescriptor.GetConverter(typeof(T)).ConvertFrom(null, CultureInfo.InvariantCulture, s);
            if (p != null) value = (T)p; return false;
          }
      }
      return false;
    }
    //bool IExchange.Edit(object p)
    //{
    //  if (todo == 0) ((List<PD>)param).Last().Add(new EditorAttribute(typeof(PD.edit), typeof(UITypeEditor)));
    //  if (todo == 5 && setid == exid) return true;
    //  if (p != null) param = p; return false;
    //}

    static string getval(XElement e, string name)
    {
      var c = e.Elements().FirstOrDefault(p => (string)p.Attribute("name") == name);
      return c != null ? (string)c.Attribute("value") : null;
    }

    AttributeCollection ICustomTypeDescriptor.GetAttributes() => TypeDescriptor.GetAttributes(this, true);
    string ICustomTypeDescriptor.GetClassName() => TypeDescriptor.GetClassName(this, true);
    string ICustomTypeDescriptor.GetComponentName() => TypeDescriptor.GetComponentName(this, true);
    TypeConverter ICustomTypeDescriptor.GetConverter() => TypeDescriptor.GetConverter(this, true);
    EventDescriptor ICustomTypeDescriptor.GetDefaultEvent() => TypeDescriptor.GetDefaultEvent(this, true);
    PropertyDescriptor ICustomTypeDescriptor.GetDefaultProperty() => TypeDescriptor.GetDefaultProperty(this, true);
    object ICustomTypeDescriptor.GetEditor(Type editorBaseType) => editorBaseType == typeof(ComponentEditor) ? new edit() : TypeDescriptor.GetEditor(this, editorBaseType, true);
    class edit : ComponentEditor
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
      if (this is XNode xn)
      {
        var pds = new List<PD>(); todo = exid = setid = 0; param = pds; Exchange(this);
        return pdc = new PropertyDescriptorCollection(pds.ToArray());
      }
      else
      {
        pdc = TypeDescriptor.GetProperties(this, attributes, true);
        var m = GetMethod<Action<IExchange>>(); if (m == null) return pdc;
        var pds = new List<PD>(); todo = exid = setid = 0; param = pds; m(this);
        if (pds.Count != 0) pdc = new PropertyDescriptorCollection(pdc.Cast<PropertyDescriptor>().Concat(pds).ToArray());
        return pdc;
      }
    }
    PropertyDescriptorCollection pdc;
    class PD : PropertyDescriptor
    {
      internal PD(int id, string name, Type t, string c, string fmt) : base(name, null)
      {
        this.id = id; type = t; category = c; this.fmt = fmt;
        //if (fmt != null && fmt.Contains("c;")) base.AttributeArray = new Attribute[] { new EditorAttribute(typeof(edit), typeof(UITypeEditor)) };
      }
      //internal void Add(Attribute p) { var a = AttributeArray; Array.Resize(ref a, a.Length + 1); a[a.Length - 1] = p; AttributeArray = a; }
      internal int id; Type type; string category, fmt;
      public override string Category => category ?? base.Category;
      public override string Description => fmt != null ? fmt.Substring(fmt.LastIndexOf(';') + 1) : base.Description;
      public override Type ComponentType => typeof(XNode);
      public override bool IsReadOnly => fmt != null && fmt.Contains("r;");
      public override Type PropertyType => type;
      public override bool CanResetValue(object component) => false;
      public override void ResetValue(object component) { }
      public override bool ShouldSerializeValue(object component) => fmt != null && fmt.Contains("b;");
      public override object GetValue(object component) => ((XNode)component).getprop(id);
      public override void SetValue(object component, object value) => ((XNode)component).setprop(id, value);
      //public override object GetEditor(Type editorBaseType) { var t = base.GetEditor(editorBaseType); return t; }
      public override TypeConverter Converter
      {
        get { var t = base.Converter; if (t.GetType() == typeof(ReferenceConverter)) return new TypeConverter(); return t; }
      }
      //internal class edit : UITypeEditor
      //{
      //  public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
      //  {
      //    return UITypeEditorEditStyle.Modal;
      //  }
      //  public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
      //  {
      //    var pd = context.PropertyDescriptor; var f = pd.GetType().GetField("descriptors", BindingFlags.Instance | BindingFlags.NonPublic);
      //    if (f == null) return ((XNode)context.Instance).editprop(((PD)pd).id, value);
      //    var dd = (PropertyDescriptor[])f.GetValue(pd);
      //    return ((XNode)((object[])context.Instance)[0]).editprop(((PD)dd[0]).id, value);
      //  }
      //}
    }
    internal static Action undo(PropertyDescriptor pd, object p, object v)
    {
      var x = pd is PD sd ? (object)sd.id : pd.Name;
      return () =>
      {
        if (x is string s)
        {
          var pi = p.GetType().GetProperty(s);
          var t = pi.GetValue(p); pi.SetValue(p, v); v = t;
        }
        else
        {
          var id = (int)x; var n = (XNode)p;
          var t = n.getprop(id); n.setprop(id, v); v = t;
        }
      };
    }
  }

  class XScene : XObject
  {
    internal override string Title => "Scene";
    internal static XScene From(IScene p) => p.Tag as XScene ?? new XScene(p);
    XScene(IScene p) { p.Tag = this; Marshal.Release(unk = Marshal.GetIUnknownForObject(p)); }
    public IScene Nodes => (IScene)Marshal.GetObjectForIUnknown(unk); IntPtr unk;
    public readonly List<string> Infos = new List<string>();
    public Unit BaseUnit { get => Nodes.Unit; set => Nodes.Unit = value; }
  }

  class XNode : XObject
  {
    internal override void Exchange(IExchange ex)
    {
      if (ex.Group("General"))
      {
        { var v = Node.Name; if (ex.Exchange("Name", ref v)) Node.Name = v; }
        { var v = Node.IsStatic; if (ex.Exchange("Static", ref v)) Node.IsStatic = v; }
      }
      if (ex.Group("Material"))
      {
        { var v = Color.FromArgb(unchecked((int)Node.Color)); if (ex.Exchange("Color", ref v)) Node.Color = (uint)v.ToArgb(); }
        { var v = Node.MaterialCount; ex.Exchange("MaterialCount", ref v, "r;"); }
        {
          var v = Texture;
          if (ex.Exchange("Texture", ref v/*, "c;"*/)) { Texture = v; }
          //if (ex.Edit())
          //{
          //  var dlg = new OpenFileDialog() { Filter = "Image files|*.png;*.jpg;*.gif|All files|*.*" };
          //  if (dlg.ShowDialog() != DialogResult.OK) return;
          //  v = Factory.GetTexture(COM.Stream(File.ReadAllBytes(dlg.FileName))); ex.Edit(v);
          //}
        }
      }
      if (ex.Group("Transform"))
      {
        { var v = Node.GetTransval(09); if (ex.Exchange("LocationX", ref v)) { var t = Node.Transform; t[09] = v; Node.Transform = t; } }
        { var v = Node.GetTransval(10); if (ex.Exchange("LocationY", ref v)) { var t = Node.Transform; t[10] = v; Node.Transform = t; } }
        { var v = Node.GetTransval(11); if (ex.Exchange("LocationZ", ref v)) { var t = Node.Transform; t[11] = v; Node.Transform = t; } }
        { var v = geteuler().x * (float)(180 / Math.PI); if (ex.Exchange("RotationX", ref v)) { var e = geteuler(); e.x = v * (float)(Math.PI / 180); seteuler(e); } }
        { var v = geteuler().y * (float)(180 / Math.PI); if (ex.Exchange("RotationY", ref v)) { var e = geteuler(); e.y = v * (float)(Math.PI / 180); seteuler(e); } }
        { var v = geteuler().z * (float)(180 / Math.PI); if (ex.Exchange("RotationZ", ref v)) { var e = geteuler(); e.z = v * (float)(Math.PI / 180); seteuler(e); } }
      }
      base.Exchange(ex);
    }

    internal override string Title => Node.Name;
    internal static XNode From(INode p) => p.Tag as XNode ?? new XNode(p);
    XNode(INode p) { p.Tag = this; Marshal.Release(unk = Marshal.GetIUnknownForObject(p)); }
    protected INode Node => (INode)Marshal.GetObjectForIUnknown(unk); IntPtr unk;
    internal bool treeopen;
    //[Category("General")]
    //public string Name { get => Node.Name; set => Node.Name = value; }
    //[Category("General")]
    //public bool Static { get => Node.IsStatic; set => Node.IsStatic = value; }
    //[Category("Material")]
    //public Color Color { get => Color.FromArgb(unchecked((int)Node.Color)); set => Node.Color = (uint)value.ToArgb(); }
    //[Category("Material")]
    //public int MaterialCount { get => Node.MaterialCount; }
    //[Category("Material")]//, TypeConverter(typeof(TextureConverter/*TypeConverter*/)), Editor(typeof(TextureEdit), typeof(UITypeEditor))]
    public ITexture Texture
    {
      get { if (Node.MaterialCount == 0) return null; Node.GetMaterial(0, out _, out _, out _, out var t); return t; }
      set { Node.GetMaterial(0, out var i, out var n, out var c, out var t); Node.SetMaterial(0, i, n, c, value); }
    }
    //class TextureEdit : UITypeEditor
    //{
    //  public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.DropDown;
    //  public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
    //  {
    //    var lb = new ListBox() { IntegralHeight = true, BorderStyle = BorderStyle.None };
    //    lb.Items.Add("Import...");
    //    lb.Items.Add("(none)");
    //    var editorService = (IWindowsFormsEditorService)provider.GetService(typeof(IWindowsFormsEditorService));
    //    lb.SelectionMode = SelectionMode.One;
    //    lb.SelectedValueChanged += (p, e) => editorService.CloseDropDown();
    //    //lb.HandleCreated += (p, e) => { lb.Height = lb.ItemHeight * lb.Items.Count; lb.Parent.PerformLayout(); };
    //    editorService.DropDownControl(lb);
    //    if (lb.SelectedIndex == -1) return value;
    //    if (lb.SelectedIndex == 1) return null;
    //    var dlg = new OpenFileDialog() { Filter = "Image files|*.png;*.jpg;*.gif|All files|*.*" };
    //    if (dlg.ShowDialog(context as Control) != DialogResult.OK) return value;
    //    return Factory.GetTexture(COM.Stream(File.ReadAllBytes(dlg.FileName)));
    //  }
    //}
    //class TextureConverter : TypeConverter
    //{
    //  public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
    //  {
    //    return value != null ? "256 x 256" : "(none)";
    //  }
    //}

    //[Category("Transform")] public CSG.Rational LocationX { get => Node.GetTransval(09); set { var t = Node.Transform; t[09] = value; Node.Transform = t; } }
    //[Category("Transform")] public CSG.Rational LocationY { get => Node.GetTransval(10); set { var t = Node.Transform; t[10] = value; Node.Transform = t; } }
    //[Category("Transform")] public CSG.Rational LocationZ { get => Node.GetTransval(11); set { var t = Node.Transform; t[11] = value; Node.Transform = t; } }
    //[Category("Transform")]
    //public float RotationX
    //{
    //  get => (float)Math.Round(geteuler().x * (float)(180 / Math.PI), 4);
    //  set { var e = geteuler(); e.x = value * (float)(Math.PI / 180); seteuler(e); }
    //}
    //[Category("Transform")]
    //public float RotationY
    //{
    //  get => (float)Math.Round(geteuler().y * (float)(180 / Math.PI), 4);
    //  set { var e = geteuler(); e.y = value * (float)(Math.PI / 180); seteuler(e); }
    //}
    //[Category("Transform")]
    //public float RotationZ
    //{
    //  get => (float)Math.Round(geteuler().z * (float)(180 / Math.PI), 4);
    //  set { var e = geteuler(); e.z = value * (float)(Math.PI / 180); seteuler(e); }
    //}
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
    //#if (__DEBUG)
    //    public string flt_matrix1 => $"{Node.GetTransform().mx}";
    //    public string flt_matrix2 => $"{Node.GetTransform().my}";
    //    public string flt_matrix3 => $"{Node.GetTransform().mz}";
    //    public string flt_matrix4 => $"{Node.GetTransform().mp}";
    //
    //    public string rat_matrix1 => $"{Node.GetTransval(0)} {Node.GetTransval(1)} {Node.GetTransval(2)}";
    //    public string rat_matrix2 => $"{Node.GetTransval(3)} {Node.GetTransval(4)} {Node.GetTransval(5)}";
    //    public string rat_matrix3 => $"{Node.GetTransval(6)} {Node.GetTransval(7)} {Node.GetTransval(8)}";
    //    public string rat_matrix4 => $"{Node.GetTransval(9)} {Node.GetTransval(10)} {Node.GetTransval(11)}";
    //#endif
  }

  class XView
  {
    CDXView p;
    public XView(CDXView view) => this.p = view;
    [Category("General")]
    public Unit BaseUnit { get => p.view.Scene.Unit; set => p.view.Scene.Unit = value; }
    [Category("View")]
    public Color BkColor { get => Color.FromArgb(unchecked((int)p.view.BkColor)); set => p.view.BkColor = (uint)value.ToArgb(); }
    [Category("View")]
    public float Projection { get => p.view.Projection; set => p.view.Projection = value; }
  }

  class PropertyView : PropertyGrid, UIForm.ICommandTarget
  {
    public override string Text { get => "Properties"; set { } }
    protected override void OnHandleCreated(EventArgs e)
    {
      view = (CDXView)Tag; base.OnHandleCreated(e); MainFrame.inval |= 1;
      Font = SystemFonts.MenuFont; DoubleBuffered = true;
      //Native.SetTimer(Handle, IntPtr.Zero, 100, IntPtr.Zero);
      Application.Idle += OnIdle;
    }
    protected override void OnHandleDestroyed(EventArgs e)
    {
      Application.Idle -= OnIdle;
      base.OnHandleDestroyed(e);
    }
    void OnIdle(object sender, EventArgs e)
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
        if (nodes.Length == 0) SelectedObject = new XView(view); //XScene.From(scene);
        else SelectedObjects = nodes.Select(p => (object)XNode.From(p)).ToArray();
      }
      else Refresh();
    }
    //protected unsafe override void WndProc(ref Message m)
    //{
    //  if (m.Msg == 0x0113) //WM_TIMER 
    //  {
    //    if ((MainFrame.inval & 1) == 0) return;
    //    if (!Visible) return;
    //    MainFrame.inval &= ~1;
    //    var scene = view.view.Scene;
    //    if (nodes != null)
    //    {
    //      int i = 0, a = -1, b;
    //      for (; (b = scene.Select(a, 1)) != -1 && i < nodes.Length && scene[b] == nodes[i]; a = b, i++) ;
    //      if (b != -1 || i != nodes.Length) nodes = null;
    //    }
    //    if (nodes == null)
    //    {
    //      nodes = scene.Selection().ToArray();
    //      if (nodes.Length == 0) SelectedObject = new XView(view); //XScene.From(scene);
    //      else SelectedObjects = nodes.Select(p => (object)XNode.From(p)).ToArray();
    //    }
    //    else Refresh();
    //  }
    //  base.WndProc(ref m);
    //}
    CDXView view; INode[] nodes; object[] oldvals;
    int UIForm.ICommandTarget.OnCommand(int id, object test)
    {
      switch (id) { case 65301: return 0; }//can close
      return view.OnCommand(id, test);
    }
    protected override void OnSelectedGridItemChanged(SelectedGridItemChangedEventArgs e)
    {
      var g = e.NewSelection; oldvals = null;
      if (g.GridItemType != GridItemType.Property || g.Value != null) return;
      var t = g.GetType().GetProperty("Instance"); if (t == null) return;
      var a = t.GetValue(g) as object[]; if (a == null) return;
      var d = g.PropertyDescriptor;
      var f = d.GetType().GetField("descriptors", BindingFlags.Instance | BindingFlags.NonPublic);
      var b = (PropertyDescriptor[])f.GetValue(d);
      oldvals = new object[a.Length]; for (int i = 0; i < a.Length; i++) oldvals[i] = b[i].GetValue(a[i]);
    }
    protected override void OnPropertyValueChanged(PropertyValueChangedEventArgs e)
    {
      var g = e.ChangedItem; view.Invalidate();
      var h = g.GetType().GetProperty("Instance");
      var p = h.GetValue(g);
      var d = g.PropertyDescriptor;
      var f = d.GetType().GetField("descriptors", BindingFlags.Instance | BindingFlags.NonPublic);
      if (f == null) { view.AddUndo(XObject.undo(d, p, e.OldValue)); return; }
      var pp = (object[])p; var dd = (PropertyDescriptor[])f.GetValue(d);
      view.AddUndo(CDXView.undo(pp.Select((a, i) => XObject.undo(dd[i], a, e.OldValue ?? oldvals[i]))));
    }
  }

  class ProjectView : UserControl, UIForm.ICommandTarget
  {
    public override string Text { get => "Project"; set { } }
    CDXView view; IScene scene;
    protected override void OnHandleCreated(EventArgs e)
    {
      DoubleBuffered = true; view = (CDXView)Tag; scene = view.view.Scene; //base.OnHandleCreated(e);
      BackColor = SystemColors.Window; Font = SystemFonts.MenuFont;
      Application.Idle += OnIdle;
    }
    protected override void OnHandleDestroyed(EventArgs e)
    {
      Application.Idle -= OnIdle;
      base.OnHandleDestroyed(e);
    }
    void OnIdle(object sender, EventArgs e)
    {
      if ((MainFrame.inval & 2) == 0) return;
      if (!Visible) return; MainFrame.inval &= ~2; Invalidate();
    }
    int UIForm.ICommandTarget.OnCommand(int id, object test)
    {
      switch (id) { case 65301: return 0; }//can close
      return view.OnCommand(id, test);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
      base.OnMouseDown(e); cmd = 1; pt = e.Location; OnPaint(null);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
      var dy = Font.Height; int x = dy * 4 / 3, y = (dy >> 2) + AutoScrollPosition.Y;
      draw(e?.Graphics, -1, scene.Select(-1, -1 << 8), x, ref y);
      if (e != null) AutoScrollMinSize = new System.Drawing.Size(0, y + dy - AutoScrollPosition.Y);
    }
    int cmd; System.Drawing.Point pt; Pen pen = new Pen(Color.Gray) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
    void draw(Graphics g, int f, int a, int x, ref int y)
    {
      var font = Font; var dy = font.Height; int y1 = f == -1 ? y + (dy >> 1) : y, y2 = y, xl = x - (dy >> 1);
      if (g != null) g.DrawLine(pen, xl, y1, xl, y1 + short.MaxValue);
      for (; a != -1; a = scene.Select(a, f << 8))
      {
        var node = scene[a]; var c = scene.Select(-1, a << 8);
        var xnode = c != -1 ? XNode.From(node) : null; var s = node.Name;
        if (g != null)
        {
          g.DrawLine(pen, xl, y2 = y + (dy >> 1), x, y + (dy >> 1));
          if (c != -1)
          {
            var r = new Rectangle(xl - (dy >> 2), y + (dy >> 1) - (dy >> 2), dy >> 1, dy >> 1); y2 = r.Bottom;
            g.FillRectangle(Brushes.White, r); g.DrawRectangle(Pens.Gray, r);
            g.DrawLine(Pens.Black, r.X + 3, (r.Y + r.Bottom) >> 1, r.Right - 3, (r.Y + r.Bottom) >> 1);
            if (!xnode.treeopen) g.DrawLine(Pens.Black, (r.X + r.Right) >> 1, r.Y + 3, (r.X + r.Right) >> 1, r.Bottom - 3);
            //TextRenderer.DrawText(g, xnode.open ? "-" : "+", font, new System.Drawing.Point(x - dy, y), Color.Black);
          }
          if (node.IsSelect) { var dx = TextRenderer.MeasureText(s, font).Width; g.FillRectangle(Focused ? SystemBrushes.GradientInactiveCaption : SystemBrushes.ButtonFace, x, y - 1, dx, dy + 2); }
          TextRenderer.DrawText(g, s, font, new System.Drawing.Point(x, y), Color.Black);
        }
        else
        {
          if (pt.Y < y) return;
          if (pt.Y < y + dy)
          {
            if (cmd == 1) { if (pt.X >= x - 16 && pt.X <= x && c != -1) { xnode.treeopen ^= true; Invalidate(); } }
            if (cmd == 1)
            {
              if (pt.X >= x && pt.X <= x + TextRenderer.MeasureText(s, font).Width)
              {
                if ((ModifierKeys & Keys.Control) != 0) node.IsSelect ^= true; else node.Select();
                MainFrame.Inval();
              }
            }
            y = short.MaxValue; return;
          }
        }
        y += dy; if (c != -1 && xnode.treeopen) draw(g, a, c, x + dy, ref y);
      }
      if (g != null) g.DrawLine(SystemPens.Window, xl, y2 + 1, xl, y2 + short.MaxValue);
    }
    void select(int i) { /*if(ModifierKeys!=Keys.Shift)*/ scene.Select(); scene[i].IsSelect = true; MainFrame.Inval(); }
    int nexsibling(int i)
    {
      var p = scene[i];
      if (p.Tag is XNode x && x.treeopen) { var k = scene.Select(-1, i << 8); if (k != -1) return k; }
      for (; ; )
      {
        var t = p.Parent != null ? p.Parent.Index : -1;
        var n = scene.Select(i, t << 8); if (n != -1) return n;
        if (t == -1) return t; p = scene[i = t];
      }
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
      switch (e.KeyCode)
      {
        case Keys.Left:
        case Keys.Up:
          {
            var i = scene.Select(-1, 1); if (i == -1) { if (scene.Count != 0) select(0); break; }
            if (e.KeyCode == Keys.Left) { var p = scene[i]; if (p.Tag is XNode xn && xn.treeopen) { xn.treeopen = false; Invalidate(); break; } }
            int a = scene.Select(-1, -1 << 8), b;
            for (; a != -1 && (b = nexsibling(a)) != i; a = b) ;
            if (a != -1) select(a);
          }
          break;
        case Keys.Right:
        case Keys.Down:
          {
            var i = scene.Select(-1, 1); if (i == -1) { if (scene.Count != 0) select(0); break; }
            for (int t; (t = scene.Select(i, 1)) != -1; i = t) ;
            if (e.KeyCode == Keys.Right) { var p = scene[i]; if (p.Tag is XNode xn && !xn.treeopen) { xn.treeopen = true; Invalidate(); break; } }
            var x = nexsibling(i); if (x != -1) select(x);
          }
          break;
      }
    }
    protected override bool IsInputKey(Keys keyData)
    {
      if ((keyData & (Keys.Control | Keys.Alt)) != 0) return false;
      return true;
    }
    protected override void OnGotFocus(EventArgs e)
    {
      base.OnGotFocus(e); Invalidate();
    }
    protected override void OnLostFocus(EventArgs e)
    {
      base.OnLostFocus(e); Invalidate();
    }
  }

  class PropView : UserControl, UIForm.ICommandTarget, IExchange, IComparer<int>
  {
    public override string Text { get => "Properties"; set { } }
    CDXView view; IScene scene; Font font, boldfont;
    protected override void OnHandleCreated(EventArgs e)
    {
      DoubleBuffered = true; view = (CDXView)Tag; scene = view.view.Scene; //base.OnHandleCreated(e);
      BackColor = SystemColors.Window; font = SystemFonts.MenuFont;
      boldfont = new Font(font, FontStyle.Bold);
      Application.Idle += OnIdle;
    }
    protected override void OnHandleDestroyed(EventArgs e)
    {
      Application.Idle -= OnIdle;
      base.OnHandleDestroyed(e);
    }
    void OnIdle(object sender, EventArgs e)
    {
      if ((MainFrame.inval & 4) == 0) return;
      if (!Visible) return;
      MainFrame.inval &= ~4; Invalidate();
    }
    int UIForm.ICommandTarget.OnCommand(int id, object test)
    {
      switch (id) { case 65301: return 0; }//can close
      return view.OnCommand(id, test);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
      index = 0;
      for (int a = -1, b; (b = scene.Select(a, 1)) != -1; a = b)
      {
        var p = XNode.From(scene[b]); p.Exchange(this); break;
      }
      for (; np > index; pp[--np] = null, ni = -1) ; //if (np > index) { np = index; ; }
      if (ni == -1)
      {
        ni = np; if (ii == null || ii.Length < ni) Array.Resize(ref ii, ni);
        var cat = "Misc"; for (int i = 0; i < ni; i++) { ii[i] = i; var p = pp[i]; if (p.type == null) cat = p.name; p.category = cat; }
        Array.Sort(ii, 0, ni, this); ni = 0; 
        for (int i = 0; i < np; i++)
        {
          if (pp[ii[i]].type == null && (i + 1 == np || pp[ii[i + 1]].type == null)) continue;
          ii[ni++] = ii[i];
        }
      }

      var g = e.Graphics; int y = 0, dy = font.Height, x1 = dy, x2 = ClientSize.Width >> 1;
      for (int i = 0; i < ni; i++, y += dy)
      {
        var p = pp[ii[i]];
        TextRenderer.DrawText(g, p.name, p.type == null ? boldfont : font, new System.Drawing.Point(x1, y), Color.Black);
        if (p.type == null) continue;
        if (p.sval == null) { p.sval = TypeDescriptor.GetConverter(p.type).ConvertToString(p.pval); }
        TextRenderer.DrawText(g, p.sval, font, new System.Drawing.Point(x2, y), Color.Black);
      }
    }
    int IComparer<int>.Compare(int a, int b)
    {
      var pa = pp[a]; var pb = pp[b];
      var t = string.Compare(pa.category, pb.category); if (t != 0) return t;
      return string.Compare(pa.type != null ? pa.name : string.Empty, pb.type != null ? pb.name : string.Empty);
    }
    class Prop
    {
      internal string name, category, sval;
      internal Type type; internal object pval;
    }
    int index, ni, np; int[] ii; Prop[] pp = new Prop[8];
    bool IExchange.Group(string name) { exprop(name, null); return true; }
    bool IExchange.Exchange<T>(string name, ref T value, string fmt)
    {
      var p = exprop(name, typeof(T));
      if (value == null ? p.pval == null : value.Equals(p.pval)) return false;
      p.pval = value; p.sval = null; return false;
    }
    Prop exprop(string name, Type type)
    {
      int i = index++, k = i; Prop p = null;
      for (; k < np && ((p = pp[k]).name != name || p.type != type); k++) ;
      if (k == np)
      {
        if (np++ == pp.Length) Array.Resize(ref pp, pp.Length << 1);
        for (; k > i; k--) pp[k] = pp[k - 1]; pp[i] = p = new Prop { name = name, type = type }; ni = -1;
      }
      else if (k != i) { for (; k > i; k--) pp[k] = pp[k - 1]; pp[i] = p; ni = -1; }
      return p;
    }
  }

}
