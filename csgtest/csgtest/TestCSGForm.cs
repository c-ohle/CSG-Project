using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml.Linq;
using static csgtest.CSG;

namespace csgtest
{
  public unsafe partial class TestCSGForm : Form
  {
    IEnumerable<IMesh> Run()
    {
      textout.AppendText("Build a native box...\r\n"); yield return null;
      stopwatch.Restart();
      var box = CreateBox(0.1m, 0.1m, 0.01m);
      stopwatch.Stop(); textout.AppendText($"{box.VertexCount} vertices {box.IndexCount / 3} polygones {stopwatch.ElapsedMilliseconds} ms\r\n\r\n");

      textout.AppendText("Build a cylinder...\r\n"); yield return box;
      stopwatch.Restart();
      var cylinder = CreateCylinder(0.01m, 0.01m);
      stopwatch.Stop(); textout.AppendText($"{cylinder.VertexCount} vertices {cylinder.IndexCount / 3} polygones {stopwatch.ElapsedMilliseconds} ms\r\n\r\n");

      textout.AppendText("Substract cylinder from box...\r\n"); yield return cylinder;
      cylinder.Transform(Rational.Matrix3x4.Translation(0.05m, 0.05m, 0.005m));
      stopwatch.Restart();
      Tesselator.Join(box, cylinder, JoinOp.Difference);
      stopwatch.Stop(); textout.AppendText($"{box.VertexCount} vertices {box.IndexCount / 3} polygones {stopwatch.ElapsedMilliseconds} ms\r\n\r\n");

      textout.AppendText("Move cylinder in x and substract again...\r\n"); yield return box;
      cylinder.Transform(Rational.Matrix3x4.Translation(0.03m, 0, 0));
      stopwatch.Restart();
      Tesselator.Join(box, cylinder, JoinOp.Difference);
      stopwatch.Stop(); textout.AppendText($"{box.VertexCount} vertices {box.IndexCount/3} polygones {stopwatch.ElapsedMilliseconds} ms\r\n\r\n");

      textout.AppendText("Move cylinder in y and substract again...\r\n"); yield return box;
      cylinder.Transform(Rational.Matrix3x4.Translation(0, 0.03m, 0));
      stopwatch.Restart();
      Tesselator.Join(box, cylinder, JoinOp.Difference);
      stopwatch.Stop(); textout.AppendText($"{box.VertexCount} vertices {box.IndexCount / 3} polygones {stopwatch.ElapsedMilliseconds} ms\r\n\r\n");

      textout.AppendText("Move cylinder in x bax and build union with box...\r\n"); yield return box;
      cylinder.Transform(Rational.Matrix3x4.Translation(-0.03m, 0, 0));
      stopwatch.Restart();
      Tesselator.Join(box, cylinder, JoinOp.Union);
      stopwatch.Stop(); textout.AppendText($"{box.VertexCount} vertices {box.IndexCount / 3} polygones {stopwatch.ElapsedMilliseconds} ms\r\n\r\n");

      textout.AppendText("Cut box on a plane...\r\n"); yield return box;
      var plane = Rational.Vector4.PlaneFromPointNormal(new Rational.Vector3(0.08m, 0.08m, 0), new Rational.Vector3(1, 1, 1));
      stopwatch.Restart();
      Tesselator.Cut(box, plane);
      stopwatch.Stop(); textout.AppendText($"{box.VertexCount} vertices {box.IndexCount / 3} polygones {stopwatch.ElapsedMilliseconds} ms\r\n\r\n");

      textout.AppendText("Create a pipe and build unit with box...\r\n"); yield return box;
      var pipe = CreatePipe(0.04m, 0.035m, 0.012m);
      pipe.Transform(Rational.Matrix3x4.Translation(0.05m, 0.05m, 0.03m));
      stopwatch.Restart();
      Tesselator.Join(box, pipe, JoinOp.Union);
      stopwatch.Stop(); textout.AppendText($"{box.VertexCount} vertices {box.IndexCount / 3} polygones {stopwatch.ElapsedMilliseconds} ms\r\n\r\n");
      
      textout.AppendText("Move pipe down and substaract from box...\r\n"); yield return box;
      pipe.Transform(Rational.Matrix3x4.Translation(0, 0, -0.03m));
      stopwatch.Restart();
      Tesselator.Join(box, pipe, JoinOp.Difference);
      stopwatch.Stop(); textout.AppendText($"{box.VertexCount} vertices {box.IndexCount / 3} polygones {stopwatch.ElapsedMilliseconds} ms\r\n\r\n"); 
      yield return box;
    }

    static IMesh CreateBox(Rational dx, Rational dy, Rational dz)
    {
      var mesh = Factory.CreateMesh();
      mesh.CreateBox(new Rational.Vector3(0, 0, 0), new Rational.Vector3(dx, dy, dz)); return mesh;
    }
    static IMesh CreateCylinder(Rational radius, Rational height, int segs = 66)
    {
      var tess = Tesselator;
      tess.Mode = Mode.NonZero | Mode.Fill | Mode.Outline;
      tess.BeginPolygon();
      tess.BeginContour(); var f = 2 * Math.PI / segs;
      for (int i = 0; i < segs; i++) tess.AddVertex(Rational.Vector2.SinCos(i * f) * radius);
      tess.EndContour();
      tess.EndPolygon();
      var mesh = Factory.CreateMesh();
      tess.Update(mesh, height);
      return mesh;
    }
    static IMesh CreatePipe(Rational radius1, Rational radius2, Rational height, int segs = 66)
    {
      var tess = Tesselator;
      tess.Mode = Mode.Positive | Mode.Fill | Mode.Outline;
      tess.BeginPolygon();
      tess.BeginContour(); var f = 2 * Math.PI / segs;
      for (int i = 0; i < segs; i++) tess.AddVertex(Rational.Vector2.SinCos(i * +f) * radius1);
      tess.EndContour();
      tess.BeginContour();
      for (int i = 0; i < segs; i++) tess.AddVertex(Rational.Vector2.SinCos(i * -f) * radius2);
      tess.EndContour();
      tess.EndPolygon();
      var mesh = Factory.CreateMesh();
      tess.Update(mesh, height);
      return mesh;
    }
    static void SaveAs3MF(IMesh imesh, string path, string name = "Unknown", uint color = 0xff808080)
    {
      var ns = (XNamespace)"http://schemas.microsoft.com/3dmanufacturing/core/2015/02";
      var ms = (XNamespace)"http://schemas.microsoft.com/3dmanufacturing/material/2015/02";
      var doc = new XElement(ns + "model");
      doc.Add(new XAttribute(XNamespace.Xml + "lang", "en-US"));
      doc.SetAttributeValue("unit", "meter");
      doc.Add(new XAttribute(XNamespace.Xmlns + "m", ms.NamespaceName));
      var resources = new XElement(ns + "resources"); doc.Add(resources);
      var build = new XElement(ns + "build"); doc.Add(build); var uid = 1;
      var basematerials = new XElement(ns + "basematerials"); resources.Add(basematerials);
      var bmid = uid++; basematerials.SetAttributeValue("id", bmid);
      var obj = new XElement(ns + "object"); obj.SetAttributeValue("id", 0);
      obj.SetAttributeValue("name", name);
      obj.SetAttributeValue("type", "model");
      obj.SetAttributeValue("pid", bmid); var im = basematerials.Elements().Count(); obj.SetAttributeValue("pindex", im++);
      var bs = new XElement(ns + "base"); basematerials.Add(bs); bs.SetAttributeValue("name", "Material" + im);
      bs.SetAttributeValue("displaycolor", $"#{(color << 8) | (color >> 24):X8}");
      var mesh = new XElement(ns + "mesh"); obj.Add(mesh);
      var vertices = new XElement(ns + "vertices"); mesh.Add(vertices);
      var triangles = new XElement(ns + "triangles"); mesh.Add(triangles);
      for (int i = 0; i < imesh.IndexCount; i += 3)
      {
        var triangle = new XElement(ns + "triangle"); triangles.Add(triangle);
        for (int t = 0; t < 3; t++) triangle.SetAttributeValue(t == 0 ? "v1" : t == 1 ? "v2" : "v3", imesh.GetIndex(i + t));
      }
      for (int i = 0; i < imesh.VertexCount; i++)
      {
        var p = imesh.GetVertexR3(i);
        var vertex = new XElement(ns + "vertex"); vertices.Add(vertex);
        vertex.SetAttributeValue("x", p.x.ToString(100, 0));
        vertex.SetAttributeValue("y", p.y.ToString(100, 0));
        vertex.SetAttributeValue("z", p.z.ToString(100, 0));
      }
      resources.Add(obj);
      var item = new XElement(ns + "item");
      var objectid = uid++; obj.SetAttributeValue("id", objectid);
      item.SetAttributeValue("objectid", objectid);
      item.SetAttributeValue("transform", string.Join(" ", "1 0 0 0 1 0 0 0 1 0 0 0"));
      build.Add(item);
      var memstr = new MemoryStream();
      using (var package = System.IO.Packaging.Package.Open(memstr, FileMode.Create))
      {
        var packdoc = package.CreatePart(new Uri("/3D/3dmodel.model", UriKind.Relative), "application/vnd.ms-package.3dmanufacturing-3dmodel+xml");
        using (var str = packdoc.GetStream()) doc.Save(str);
        package.CreateRelationship(packdoc.Uri, System.IO.Packaging.TargetMode.Internal, "http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel", "rel0");
      }
      File.WriteAllBytes(path, memstr.ToArray());
    }
    static IMesh LoadFrom3MF(string path)
    {
      XDocument doc;
      using (var package = System.IO.Packaging.Package.Open(path))
      {
        var xml = package.GetPart(new Uri("/3D/3dmodel.model", UriKind.Relative));
        using (var str = xml.GetStream()) doc = XDocument.Load(str);
      }
      var model = doc.Root; var ns = model.Name.Namespace;
      var unit = (string)model.Attribute("unit");
      var scale = unit == "micron" ? 0.000001m : unit == "millimeter" ? 0.001m : unit == "centimeter" ? 0.01m : unit == "inch" ? 0.0254m : unit == "foot" ? 0.3048m : 1;
      var first = model.Descendants(ns + "mesh").First();
      var ii = first.Element(ns + "triangles").Elements(ns + "triangle").SelectMany(p => new int[] { (int)p.Attribute("v1"), (int)p.Attribute("v2"), (int)p.Attribute("v3") }).ToArray();
      var vv = first.Element(ns + "vertices").Elements(ns + "vertex").ToArray();
      var pv = Factory.CreateVector(vv.Length * 3);
      for (int i = 0, k = 0; i < vv.Length; i++)
      {
        pv.SetValue(k++, (string)vv[i].Attribute("x"));
        pv.SetValue(k++, (string)vv[i].Attribute("y"));
        pv.SetValue(k++, (string)vv[i].Attribute("z"));
      }
      var mesh = Factory.CreateMesh();
      fixed (int* pi = ii) mesh.Update(new Variant(pv, 3, vv.Length), new Variant(pi, 1, ii.Length));
      Marshal.ReleaseComObject(pv);
      if (scale != 1) mesh.Transform(Rational.Matrix3x4.Scaling(scale, scale, scale));
      return mesh;
    }

    public TestCSGForm()
    {
      InitializeComponent();
      run = Run().GetEnumerator(); run.MoveNext();
    }
    IEnumerator<IMesh> run;
    Stopwatch stopwatch = new Stopwatch();
    void Step(object sender, EventArgs e)
    {
      if(!run.MoveNext()) run = Run().GetEnumerator();

      if (run.Current == null) return;
      var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "test.3mf");
      SaveAs3MF(run.Current, path); Process.Start(path); //show test.3mf in default 3mf viewer
    }

  }
}
