using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using static csg3mf.CSG;

namespace csg3mf
{
  public static unsafe partial class CDX
  {
    public static XElement Export3MF(this IScene scene, string path, COM.IStream prev, string script, float3? dragpt)
    {
      var ns = (XNamespace)"http://schemas.microsoft.com/3dmanufacturing/core/2015/02";
      var ms = (XNamespace)"http://schemas.microsoft.com/3dmanufacturing/material/2015/02";
      var doc = new XElement(ns + "model");
      doc.Add(new XAttribute(XNamespace.Xml + "lang", "en-US"));
      var unit = scene.Unit; doc.SetAttributeValue("unit", (unit != 0 ? unit : CDX.Unit.meter).ToString());
      doc.Add(new XAttribute(XNamespace.Xmlns + "m", ms.NamespaceName));
      if (dragpt.HasValue) doc.SetAttributeValue("dragpt", (string)dragpt.Value);
      var resources = new XElement(ns + "resources"); doc.Add(resources);
      var build = new XElement(ns + "build"); doc.Add(build);
      var uid = 1; var textures = new List<(COM.IStream str, int id, XElement e)>();
      var basematerials = new XElement(ns + "basematerials"); resources.Add(basematerials);
      var bmid = uid++; basematerials.SetAttributeValue("id", bmid);
      var buffer = Marshal.AllocHGlobal(65536);
      try { foreach (var group in scene.Nodes()) add(group, build); }
      finally { Marshal.FreeHGlobal(buffer); }
      void add(INode group, XElement dest)
      {
        var obj = new XElement(ns + "object"); obj.SetAttributeValue("id", 0);
        if (group.Name != null) obj.SetAttributeValue("name", group.Name);
        if (group.IsStatic) obj.SetAttributeValue("static", true);
        var desc = group.Nodes();// nodes.Nodes.Nodes().Where(p => p.Parent == group);
        var components = desc.Any() ? new XElement(ns + "components") : null;
        if (group.Mesh != null)
        {
          var tag = obj;
          if (components != null)
          {
            tag = new XElement(ns + "object"); var id = uid++; tag.SetAttributeValue("id", id); resources.Add(tag);
            var it = new XElement(ns + "component"); it.SetAttributeValue("objectid", id); components.Add(it);
          }
          tag.SetAttributeValue("type", "model");
          tag.SetAttributeValue("pid", bmid); var im = basematerials.Elements().Count(); tag.SetAttributeValue("pindex", im++);
          var bs = new XElement(ns + "base"); basematerials.Add(bs); bs.SetAttributeValue("name", "Material" + im);
          var color = group.Color; //group.Color 
          bs.SetAttributeValue("displaycolor", $"#{(color << 8) | (color >> 24):X8}");
          var mesh = new XElement(ns + "mesh"); tag.Add(mesh);
          var vertices = new XElement(ns + "vertices"); mesh.Add(vertices);
          var triangles = new XElement(ns + "triangles"); mesh.Add(triangles);
          var rmesh = group.Mesh;
          //foreach (var p in rmesh.Vertices())
          //{
          //  var vertex = new XElement(ns + "vertex"); vertices.Add(vertex);
          //  vertex.SetAttributeValue("x", p.x.ToString(63, 0));
          //  vertex.SetAttributeValue("y", p.y.ToString(63, 0));
          //  vertex.SetAttributeValue("z", p.z.ToString(63, 0));
          //}   
          for (int i = 0; i < rmesh.VertexCount; i++)
          {
            var vertex = new XElement(ns + "vertex"); vertices.Add(vertex);
            var p = (char*)buffer.ToPointer(); rmesh.GetVertex(i, new CSG.Variant(p, 3));
            for (int k = 0, n; k < 3; k++)
            {
              for (n = 0; p[n] > ' '; n++) ; var s = new string(p, 0, n); p += n + 1;
              vertex.SetAttributeValue(k == 0 ? "x" : k == 1 ? "y" : "z", s);
            }
          }
          for (int k = 0, nk = group.MaterialCount; k < nk; k++)
          {
            group.GetMaterial(k, out var mastart, out var macount, out var ucolor, out var tex);
            var texgid = 0;
            if (tex != null)
            {
              int texid; int i = 0; for (; i < textures.Count && textures[i].str != tex; i++) ;
              if (i != textures.Count) texid = textures[i].id;
              else
              {
                texid = uid++;
                var texture2d = new XElement(ms + "texture2d"); resources.Add(texture2d);
                texture2d.SetAttributeValue("id", texid);
                tex.Seek(0); byte kb; tex.Read(&kb, 1);
                string typ; switch (kb) { case 0x89: typ = "png"; break; case 0xff: typ = "jpeg"; break; default: typ = "bmp"; break; }
                texture2d.SetAttributeValue("path", $"/3D/Textures/{texid}.{typ}");
                texture2d.SetAttributeValue("contenttype", $"image/{typ}");
                textures.Add((tex, texid, texture2d));
              }
              var texture2dgroup = new XElement(ms + "texture2dgroup"); resources.Add(texture2dgroup);
              texture2dgroup.SetAttributeValue("id", texgid = uid++);
              texture2dgroup.SetAttributeValue("texid", texid);
              group.GetTexturCoords(out var tcs);
              for (int t = 0, nt = ((int*)&tcs)[1]; t < nt; t++)
              {
                var p = (*(float2**)&tcs.vp)[t];
                var tex2coord = new XElement(ms + "tex2coord"); texture2dgroup.Add(tex2coord);
                tex2coord.SetAttributeValue("u", +p.x);
                tex2coord.SetAttributeValue("v", -p.y);
              }
              //foreach (var p in group.Texcoords)
              //  {
              //    var tex2coord = new XElement(ms + "tex2coord"); texture2dgroup.Add(tex2coord);
              //    tex2coord.SetAttributeValue("u", +p.x);
              //    tex2coord.SetAttributeValue("v", -p.y);
              //  }
            }
            for (int i = 0; i < macount; i += 3)
            {
              var triangle = new XElement(ns + "triangle"); triangles.Add(triangle);
              for (int t = 0; t < 3; t++)
                triangle.SetAttributeValue(t == 0 ? "v1" : t == 1 ? "v2" : "v3", rmesh.GetIndex(mastart + i + t));
              if (texgid == 0) continue;
              triangle.SetAttributeValue("pid", texgid);
              for (int t = 0; t < 3; t++) triangle.SetAttributeValue(t == 0 ? "p1" : t == 1 ? "p2" : "p3", mastart + i + t);
            }
          }
        }
        if (components != null) { obj.Add(components); foreach (var p in desc) add(p, components); }
        resources.Add(obj);
        var item = new XElement(ns + (dest.Name.LocalName == "build" ? "item" : "component"));
        var objectid = uid++; obj.SetAttributeValue("id", objectid);
        item.SetAttributeValue("objectid", objectid);
        var ss = (char*)buffer.ToPointer(); group.Transform.GetValues(new Variant(ss, 12));
        item.SetAttributeValue("transform", new string(ss));
        dest.Add(item);
      };
      if (path == null) return doc;//doc.Save("C:\\Users\\cohle\\Desktop\\test2.xml");
      var memstr = new MemoryStream();
      using (var package = System.IO.Packaging.Package.Open(memstr, FileMode.Create))
      {
        var packdoc = package.CreatePart(new Uri("/3D/3dmodel.model", UriKind.Relative), "application/vnd.ms-package.3dmanufacturing-3dmodel+xml",
          System.IO.Packaging.CompressionOption.Normal);
        using (var str = packdoc.GetStream()) doc.Save(str);
        package.CreateRelationship(packdoc.Uri, System.IO.Packaging.TargetMode.Internal, "http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel", "rel0");
        var buff = new byte[4096];
        foreach (var (bin, id, e) in textures)
        {
          var pack = package.CreatePart(new Uri((string)e.Attribute("path"), UriKind.Relative), (string)e.Attribute("contenttype"));
          using (var str = pack.GetStream())
          {
            bin.Seek(0);
            for (; ; ) { int nr; fixed (byte* p = buff) bin.Read(p, 4096, &nr); str.Write(buff, 0, nr); if (nr < 4096) break; }
          }
          packdoc.CreateRelationship(pack.Uri, System.IO.Packaging.TargetMode.Internal, "http://schemas.microsoft.com/3dmanufacturing/2013/01/3dtexture", "rel" + id);
        }
        if (prev != null)
        {
          var packpng = package.CreatePart(new Uri("/Metadata/thumbnail.png", UriKind.Relative), "image/png");
          using (var str = packpng.GetStream())
          {
            prev.Seek(0);
            for (; ; ) { int nr; fixed (byte* p = buff) prev.Read(p, 4096, &nr); str.Write(buff, 0, nr); if (nr < 4096) break; }
          }
          package.CreateRelationship(packpng.Uri, System.IO.Packaging.TargetMode.Internal, "http://schemas.openxmlformats.org/package/2006/relationships/metadata/thumbnail", "rel1");
        }
        if (!string.IsNullOrEmpty(script))
        {
          var packpng = package.CreatePart(new Uri("/Metadata/csg.cs", UriKind.Relative), System.Net.Mime.MediaTypeNames.Text.Plain, System.IO.Packaging.CompressionOption.Normal);
          using (var str = packpng.GetStream()) using (var sw = new StreamWriter(str)) sw.Write(script);
        }
      }
      File.WriteAllBytes(path, memstr.ToArray()); return null;
    }
    public static IScene Import3MF(string path, out string script, out float3 dragpt)
    {
      using (var package = System.IO.Packaging.Package.Open(path))
      {
        var xml = package.GetPart(new Uri("/3D/3dmodel.model", UriKind.Relative));
        XDocument doc; long len; using (var str = xml.GetStream()) { doc = XDocument.Load(str); len = str.Length; } //560280 doc.Save("C:\\Users\\cohle\\Desktop\\test1.xml");
        var model = doc.Root; var ns = model.Name.Namespace;
        var scene = CDX.Factory.CreateScene();
        var pt = model.Attribute("dragpt"); dragpt = pt != null ? (float3)pt.Value : float.NaN;
        switch ((string)model.Attribute("unit"))
        {
          default: scene.Unit = CDX.Unit.meter; break; //1
          case "centimeter": scene.Unit = CDX.Unit.centimeter; break; //0.01
          case "millimeter": scene.Unit = CDX.Unit.millimeter; break; //0.001
          case "micron": scene.Unit = CDX.Unit.micron; break; //0.000001
          case "foot": scene.Unit = CDX.Unit.foot; break; //0.3048
          case "inch": scene.Unit = CDX.Unit.inch; break; //0.0254
        }
        var res = model.Element(ns + "resources");
        var build = model.Element(ns + "build"); var buffer = Marshal.AllocHGlobal((IntPtr)(len * sizeof(char)));
        try { foreach (var p in build.Elements(ns + "item")) convert(scene.AddNode(null), p); }
        finally { Marshal.FreeHGlobal(buffer); }
        /////////////
        if (scene.Count != 0 && scene[0].TransformF.mx.LengthSq > 10) //curiosity bug fix
          foreach (var p in scene.Descendants()) p.Transform *= Rational.Matrix.Scaling(1 / (decimal)Math.Sqrt((double)p.Transform.mx.LengthSq));
        /////////////
        var uri = new Uri("/Metadata/csg.cs", UriKind.Relative);
        if (!package.PartExists(uri)) { script = null; return scene; }
        using (var str = package.GetPart(uri).GetStream())
        using (var sr = new StreamReader(str)) script = sr.ReadToEnd();
        return scene;
        void convert(INode node, XElement e)
        {
          var oid = (string)e.Attribute("objectid");
          var obj = res.Elements(ns + "object").First(p => (string)p.Attribute("id") == oid);
          var mesh = obj.Element(ns + "mesh");
          node.Name = (string)obj.Attribute("name"); var st = obj.Attribute("static"); if (st != null) { node.IsStatic = (bool)st; }
          var tra = (string)e.Attribute("transform");
          if (tra != null) fixed (char* p = tra) node.SetTransform(new Variant(p, 12)); //{ var m = node.Transform; m.SetValues(new Variant(p, 12)); node.Transform = m; }
          var cmp = obj.Element(ns + "components");
          if (cmp != null) foreach (var p in cmp.Elements(ns + "component")) convert(node.AddNode(null), p);
          if (mesh == null) return;
          var mid = (string)obj.Attribute("materialid"); var color = 0xffffffff;
          if (mid != null) //2013/01
          {
            var mat = res.Elements(ns + "material").First(p => (string)p.Attribute("id") == mid);
            var cid = (string)mat.Attribute("colorid");
            var col = res.Elements(ns + "color").First(p => (string)p.Attribute("id") == cid);
            var sco = (string)col.Attribute("value");
            if (sco[0] == '#') color = 0xff000000 | uint.Parse(sco.Substring(1), System.Globalization.NumberStyles.HexNumber);
          }
          else if ((mid = (string)obj.Attribute("pid")) != null) //core/2015/02
          {
            var mat = res.Elements(ns + "basematerials").First(p => (string)p.Attribute("id") == mid).Elements(ns + "base").ElementAt((int)obj.Attribute("pindex"));
            var sco = (string)mat.Attribute("displaycolor");
            if (sco[0] == '#') color = uint.Parse(sco.Substring(1), System.Globalization.NumberStyles.HexNumber);
            if (sco.Length == 9) color = (color >> 8) | (color << 24); else color |= 0xff000000;
          }
          var vertices = mesh.Element(ns + "vertices").Elements(ns + "vertex");
          var triangles = mesh.Element(ns + "triangles").Elements(ns + "triangle");
          var kk = triangles.OrderBy(p => p.Attribute("pid")?.Value).ToArray();
          var mm = kk.Select(p => p.Attribute("pid")?.Value).Distinct().ToArray();
          var ii = (int*)buffer.ToPointer(); var ni = kk.Length * 3;
          for (int i = 0, k = 0; i < kk.Length; i++)
          {
            var p = kk[i];
            ii[k++] = (int)p.Attribute("v1");
            ii[k++] = (int)p.Attribute("v2");
            ii[k++] = (int)p.Attribute("v3");
          }
#if(true) // fast using big buffer      
          var ss = (char*)(ii + ni); var cs = 0; var pw = ss;
          foreach (var p in vertices)
            for (int j = 0; j < 3; j++, cs++)
            {
              var s = p.Attribute(j == 0 ? "x" : j == 1 ? "y" : "z").Value;
              for (int l = 0; l < s.Length; l++) *pw++ = s[l]; *pw++ = ' ';
            }
          var me = CSG.Factory.CreateMesh(); me.Update(new Variant(ss, 3, cs / 3), new Variant(ii, 1, ni));
#else
          var me = CSG.Factory.CreateMesh(); me.Update(vertices.Count(), new Variant(ii, 1, ni));
#if (true) // vertex by vertex 
          int strcpy(char* p, string s) { int n = s.Length; for (int i = 0; i < n; i++) p[i] = s[i]; return n; }
          int l = 0; var ss = (char*)buffer.ToPointer();
          foreach (var p in vertices)
          {
            var n = strcpy(ss, p.Attribute("x").Value); ss[n++] = ' ';
            n += strcpy(ss + n, p.Attribute("y").Value); ss[n++] = ' ';
            n += strcpy(ss + n, p.Attribute("z").Value); ss[n++] = '\0';
            me.SetVertex(l++, new Variant(ss, 3));
          }
#else // or simple with lot of garbage
          int l = 0;
          foreach (var p in vertices)
            fixed (char* t = $"{p.Attribute("x").Value} {p.Attribute("y").Value} {p.Attribute("z").Value}")
              me.SetVertex(l++, new Variant(t, 3));
#endif
#endif
          node.Mesh = me;
          node.MaterialCount = mm.Length; float2[] tt = null;//.Materials = new Node.Material[mm.Length];
          var ms = (XNamespace)"http://schemas.microsoft.com/3dmanufacturing/material/2015/02";
          for (int i = 0, ab = 0, bis = 1; i < mm.Length; i++, ab = bis)
          {
            var pid = mm[i]; for (; bis < kk.Length && kk[bis - 1].Attribute("pid")?.Value == pid; bis++) ;
            COM.IStream bin = null;//ref var ma = ref node.Materials[i];ma.Color = color; ma.IndexCount = bis * 3 - (ma.StartIndex = ab * 3);
            if (pid != null)
            {
              var basematerials = res.Elements(ns + "basematerials").FirstOrDefault(p => (string)p.Attribute("id") == pid);
              if (basematerials != null)
              {
                var mat = basematerials.Elements(ns + "base").ElementAt((int)kk[ab].Attribute("p1"));
                var sco = (string)mat.Attribute("displaycolor"); if (sco[0] != '#') continue;
                var col = uint.Parse(sco.Substring(1), System.Globalization.NumberStyles.HexNumber);
                if (sco.Length == 9) col = (col >> 8) | (col << 24); else col |= 0xff000000; color = col; goto addmat;
              }
              var texture2dgroup = res.Elements(ms + "texture2dgroup").FirstOrDefault(p => (string)p.Attribute("id") == pid);
              if (texture2dgroup == null) goto addmat;
              var pp = texture2dgroup.Elements(ms + "tex2coord").Select(p => new float2((float)p.Attribute("u"), -(float)p.Attribute("v"))).ToArray();
              if (tt == null) tt = new float2[kk.Length * 3];
              for (int t = ab, x; t < bis; t++)
              {
                var p1 = kk[t].Attribute("p1"); x = (int)p1; /*   */ if (x < pp.Length) tt[t * 3 + 0] = pp[x];
                var p2 = kk[t].Attribute("p2"); x = (int)(p2 ?? p1); if (x < pp.Length) tt[t * 3 + 1] = pp[x];
                var p3 = kk[t].Attribute("p3"); x = (int)(p3 ?? p1); if (x < pp.Length) tt[t * 3 + 2] = pp[x];
              }
              var texid = (string)texture2dgroup.Attribute("texid");
              var texture2d = res.Elements(ms + "texture2d").Where(t => (string)t.Attribute("id") == texid).First();
              bin = texture2d.Annotation<COM.IStream>();
              if (bin == null)
              {
                var texpath = (string)texture2d.Attribute("path");
                var texpart = package.GetPart(new Uri(texpath, UriKind.Relative));
                using (var str = texpart.GetStream())
                {
                  var a = new byte[str.Length]; str.Read(a, 0, a.Length);
                  fixed (byte* p = a) bin = COM.SHCreateMemStream(p, a.Length);
                  texture2d.AddAnnotation(bin);
                }
              }
            }
          addmat: node.SetMaterial(i, ab * 3, (bis - ab) * 3, color, bin);
          }
          if (tt != null) fixed (float2* p = tt) node.SetTexturCoords(new Variant(&p->x, 2, tt.Length));
        };
      }
    }
  }
}
