using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static csg3mf.CSG;
using static csg3mf.Viewer.D3DView;

namespace csg3mf
{
  internal unsafe class Node
  {
    internal Node() { Transform = Rational.Matrix.Identity(); }
    internal string Name;
    internal Rational.Matrix Transform;
    internal IMesh Mesh;
    internal byte[] Texture;
    internal float2[] Texcoords;
    internal uint Color;
    internal Node Parent;
    internal int IndexCount, StartIndex;

    internal float3x4 trans;
    internal VertexBuffer vertexbuffer;
    internal IndexBuffer indexbuffer;
    internal Texture texture;

    internal float3x4 gettrans(Node rel = null)
    {
      var m = trans;
      for (var p = Parent; p != rel && p.Parent != null; p = p.Parent) m *= p.trans; return m;
    }
    internal void getbox(in float3x4 m, float3* box, float2* ab = null)
    {
      //if (Mesh == null) return;
      //var nv = Mesh.VertexCount; var vv = (float3*)StackPtr;
      //var vp = new Variant(&vv->x, 3, nv); Mesh.CopyBuffer(0, 0, ref vp);
      if (vertexbuffer == null) return;
      var vv = vertexbuffer.GetPoints(); var nv = vv.Length;
      for (int i = 0; i < nv; i++)
      {
        var p = vv[i] * m; if (ab == null) { boxadd(&p, box); continue; }
        box[0].x = Math.Min(box[0].x, p.x + p.z * ab->x);
        box[0].y = Math.Min(box[0].y, p.y + p.z * ab->y);
        box[0].z = Math.Min(box[0].z, p.z);
        box[1].x = Math.Max(box[1].x, p.x - p.z * ab->x);
        box[1].y = Math.Max(box[1].y, p.y - p.z * ab->y);
        box[1].z = Math.Max(box[1].z, p.z);
      }
    }

    internal void update()
    {
      trans = (float3x4)Transform;// fixed(float3x4* p = &trans) Transform.GetValues(new Variant(&p->_11, 12));
      if (Mesh == null) return;
      var nv = Mesh.VertexCount; var vv = (double3*)StackPtr; StackPtr += nv * sizeof(double3);
      var ni = Mesh.IndexCount; var ii = (int*)StackPtr; StackPtr += ni * sizeof(int);
      var vp = new Variant(&vv->x, 3, nv); Mesh.CopyBuffer(0, 0, ref vp);
      var ip = new Variant(ii, 1, ni); Mesh.CopyBuffer(1, 0, ref ip);
      for (int i = 0; i < ni; i++) ((ushort*)ii)[i] = (ushort)ii[i];
      texture = Texture != null ? GetTexture(Texture) : null;
      if (texture != null) fixed (float2* tt = Texcoords) GetMesh(ref vertexbuffer, ref indexbuffer, vv, nv, (ushort*)ii, ni, 0.3f, tt, 2);
      else GetMesh(ref vertexbuffer, ref indexbuffer, vv, nv, (ushort*)ii, ni, 0.3f);
      StackPtr = (byte*)vv;
    }

    static void unscale(List<Node> nodes, Node parent)
    {
      for (int i = 0; i < nodes.Count; i++)
      {
        var p = nodes[i]; if (p.Parent != parent) continue;
        var lsq = p.Transform.mx.LengthSq;
        if (lsq != 1)
        {
          var s = (Rational)(decimal)Math.Sqrt((double)lsq);
          p.Transform = Rational.Matrix.Scaling(1 / s) * p.Transform;
          if (p.StartIndex == 0) p.Mesh?.Transform(s);
          for (int t = 0; t < nodes.Count; t++)
            if (nodes[t].Parent == p)
              nodes[t].Transform = nodes[t].Transform * Rational.Matrix.Scaling(s);
        }
        unscale(nodes, p);
      }
    }
    internal static float3box getboxest(IEnumerable<Node> nodes, Node root = null)
    {
      float3box box; boxempty((float3*)&box);
      foreach (var p in nodes)
      {
        if (p.Mesh == null) continue;
        var m = (float3x4)p.Transform; for (var t = p.Parent; t != root; t = t.Parent) m *= (float3x4)t.Transform;
        foreach (var v in p.Mesh.VerticesF3()) boxadd(&v, &m, &box.min);
      }
      return box;
    }

    internal static List<Node> Import3MF(string path, out string script)
    {
      using (var package = System.IO.Packaging.Package.Open(path))
      {
        var xml = package.GetPart(new Uri("/3D/3dmodel.model", UriKind.Relative));
        XDocument doc; using (var str = xml.GetStream()) doc = XDocument.Load(str);
        var model = doc.Root; var ns = model.Name.Namespace;
        var unit = (string)model.Attribute("unit");
        var scale = unit == "micron" ? 0.000001m : unit == "millimeter" ? 0.001m : unit == "centimeter" ? 0.01m : unit == "inch" ? 0.0254m : unit == "foot" ? 0.3048m : 1;
        var res = model.Element(ns + "resources");
        var build = model.Element(ns + "build");
        var nodes = new List<Node>();
        var root = new Node(); nodes.Add(root);
        root.Transform[0u] = root.Transform[4u] = root.Transform[8u] = scale;//root.Transform = Rational.Matrix.Scaling(scale); 
        foreach (var p in build.Elements(ns + "item").Select(item => convert(item))) { p.Parent = root; nodes.Add(p); }
        /////////////
        if (nodes.Count > 1 && dot(((float3x4)nodes[1].Transform)[0]) > 10) //curiosity bug fix
          foreach (var p in nodes) p.Transform *= Rational.Matrix.Scaling(1 / (decimal)Math.Sqrt((double)p.Transform.mx.LengthSq));
        //var box = getboxest(nodes.Skip(1), nodes[0]);
        /////////////
        var uri = new Uri("/Metadata/csg.cs", UriKind.Relative);
        if (!package.PartExists(uri)) { script = null; return nodes; }
        using (var str = package.GetPart(uri).GetStream())
        using (var sr = new StreamReader(str)) script = sr.ReadToEnd();
        return nodes;
        Node convert(XElement e)
        {
          var oid = (string)e.Attribute("objectid");
          var obj = res.Elements(ns + "object").First(p => (string)p.Attribute("id") == oid);
          var mesh = obj.Element(ns + "mesh");
          var node = new Node();
          node.Name = (string)obj.Attribute("name");
          var tra = (string)e.Attribute("transform");
          if (tra != null)
          {
            var np = tra.Length; var pp = (char*)StackPtr; fixed (char* p = tra) Native.memcpy(pp, p, (void*)((np + 1) * sizeof(char)));
            var nl = 0; var ll = (char**)(pp + np + 1); ll[0] = null;
            for (int i = 0; i < np; i++) { if (pp[i] <= ' ') { pp[i] = (char)0; if (ll[nl] != null) ll[++nl] = null; } else if (ll[nl] == null) ll[nl] = pp + i; }
            node.Transform.SetValues(new Variant(ll, nl + 1));
            //var ss = tra.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            //for (int i = 0, n = Math.Min(ss.Length, 12); i < n; i++) node.Transform[i] = ss[i];
          }
          var cmp = obj.Element(ns + "components");
          if (cmp != null) foreach (var p in cmp.Elements(ns + "component").Select(item => convert(item))) { p.Parent = node; nodes.Add(p); }
          if (mesh == null) return node;
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
          node.Color = color;
          //
          var ver = mesh.Element(ns + "vertices").Elements(ns + "vertex");
          var tri = mesh.Element(ns + "triangles").Elements(ns + "triangle");
          var kk = tri.OrderBy(p => p.Attribute("pid")?.Value).ToArray();
          var mm = kk.Select(p => p.Attribute("pid")?.Value).Distinct().ToArray();
          var ii = (int*)StackPtr;
          for (int i = 0, k = 0; i < kk.Length; i++) { var p = kk[i]; ii[k++] = (int)p.Attribute("v1"); ii[k++] = (int)p.Attribute("v2"); ii[k++] = (int)p.Attribute("v3"); }
          var rmesh = Factory.CreateMesh();
          rmesh.Update(ver.Count(), new Variant(ii, 1, kk.Length * 3));
          var sv = (char**)StackPtr; int l = 0;
          foreach (var p in ver) fixed (char* sx = (string)p.Attribute("x"), sy = (string)p.Attribute("y"), sz = (string)p.Attribute("z"))
            { sv[0] = sx; sv[1] = sy; sv[2] = sz; rmesh.SetVertex(l++, new Variant(sv, 3)); }
#if (DEBUG)
          //var xx = rmesh.Check(); if (xx != 0) Debug.WriteLine("mesh.Check: " + xx);
#endif
          var tt = (float2[])null; var ms = (XNamespace)"http://schemas.microsoft.com/3dmanufacturing/material/2015/02";
          for (int i = 0, ab = 0, bis = 1; i < mm.Length; i++, ab = bis)
          {
            var pid = mm[i]; for (; bis < kk.Length && kk[bis - 1].Attribute("pid")?.Value == pid; bis++) ;
            var dm = node; if (i != 0) nodes.Add(dm = new Node { Color = color, Parent = node });
            dm.Mesh = rmesh; //dm.Points = vv; dm.Indices = ii;
            if (mm.Length != 1) { dm.IndexCount = bis * 3 - (dm.StartIndex = ab * 3); }
            if (pid == null) continue;
            var basematerials = res.Elements(ns + "basematerials").FirstOrDefault(p => (string)p.Attribute("id") == pid);
            if (basematerials != null)
            {
              var mat = basematerials.Elements(ns + "base").ElementAt((int)kk[ab].Attribute("p1"));
              var sco = (string)mat.Attribute("displaycolor"); if (sco[0] != '#') continue;
              var col = uint.Parse(sco.Substring(1), System.Globalization.NumberStyles.HexNumber);
              if (sco.Length == 9) col = (col >> 8) | (col << 24); else col |= 0xff000000; dm.Color = col; continue;
            }
            var texture2dgroup = res.Elements(ms + "texture2dgroup").FirstOrDefault(p => (string)p.Attribute("id") == pid);
            if (texture2dgroup == null) continue;
            var pp = texture2dgroup.Elements(ms + "tex2coord").Select(p => new float2((float)p.Attribute("u"), -(float)p.Attribute("v"))).ToArray();
            if (tt == null) tt = new float2[kk.Length * 3];
            for (int t = ab; t < bis; t++)
            {
              var p1 = kk[t].Attribute("p1"); tt[t * 3 + 0] = pp[(int)p1];
              var p2 = kk[t].Attribute("p2"); tt[t * 3 + 1] = pp[(int)(p2 ?? p1)];
              var p3 = kk[t].Attribute("p3"); tt[t * 3 + 2] = pp[(int)(p3 ?? p1)];
            }
            dm.Texcoords = tt;
            var texid = (string)texture2dgroup.Attribute("texid");
            var texture2d = res.Elements(ms + "texture2d").Where(t => (string)t.Attribute("id") == texid).First();
            var texpath = (string)texture2d.Attribute("path");
            var texpart = package.GetPart(new Uri(texpath, UriKind.Relative));
            using (var str = texpart.GetStream()) { var a = new byte[str.Length]; str.Read(a, 0, a.Length); dm.Texture = a; }
          }
          return node;
        };
      }
    }
    internal static void Export3MF(List<Node> nodes, string path, Bitmap prev, string script)
    {
      var ns = (XNamespace)"http://schemas.microsoft.com/3dmanufacturing/core/2015/02";
      var ms = (XNamespace)"http://schemas.microsoft.com/3dmanufacturing/material/2015/02";
      var doc = new XElement(ns + "model");
      doc.Add(new XAttribute(XNamespace.Xml + "lang", "en-US"));
      doc.SetAttributeValue("unit", "meter");
      //doc.SetAttributeValue("wp", string.Format("{0} {1} {2}", System.Xml.XmlConvert.ToString(wp.x), System.Xml.XmlConvert.ToString(wp.y), System.Xml.XmlConvert.ToString(wp.z)));
      doc.Add(new XAttribute(XNamespace.Xmlns + "m", ms.NamespaceName));
      var resources = new XElement(ns + "resources"); doc.Add(resources);
      var build = new XElement(ns + "build"); doc.Add(build);
      var uid = 1; var points = new Dictionary<Rational.Vector3, int>(); var texccords = new Dictionary<float2, int>();
      var textures = new List<(byte[] bin, int id)>();
      var basematerials = new XElement(ns + "basematerials"); resources.Add(basematerials);
      var bmid = uid++; basematerials.SetAttributeValue("id", bmid);
      void add(Node group, XElement dest)
      {
        var obj = new XElement(ns + "object"); obj.SetAttributeValue("id", 0);
        if (group.Name != null) obj.SetAttributeValue("name", group.Name);
        //obj.SetAttributeValue("type", "model");
        var desc = nodes.Where(p => p.Parent == group);
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
          bs.SetAttributeValue("displaycolor", $"#{(group.Color << 8) | (group.Color >> 24):X8}");

          var mesh = new XElement(ns + "mesh"); tag.Add(mesh);
          var vertices = new XElement(ns + "vertices"); mesh.Add(vertices);
          var triangles = new XElement(ns + "triangles"); mesh.Add(triangles);
          var rmesh = group.Mesh;
          var texture2dgroup = (XElement)null; var texgid = 0;
          if (group.Texture != null)
          {
            int texid; int i = 0; for (; i < textures.Count && !Native.Equals(textures[i].bin, group.Texture); i++) ;
            if (i != textures.Count) texid = textures[i].id;
            else
            {
              texid = uid++; textures.Add((group.Texture, texid));
              var texture2d = new XElement(ms + "texture2d"); resources.Add(texture2d);
              texture2d.SetAttributeValue("id", texid);
              texture2d.SetAttributeValue("path", "/3D/Textures/" + texid + ".png");
              texture2d.SetAttributeValue("contenttype", "image/png");
            }
            texture2dgroup = new XElement(ms + "texture2dgroup"); resources.Add(texture2dgroup);
            texture2dgroup.SetAttributeValue("id", texgid = uid++);
            texture2dgroup.SetAttributeValue("texid", texid);
          }

          for (int i = group.StartIndex, ni = group.IndexCount != 0 ? i + group.IndexCount : rmesh.IndexCount; i < ni; i += 3)
          {
            var triangle = new XElement(ns + "triangle"); triangles.Add(triangle);
            for (int t = 0; t < 3; t++)
            {
              var p = rmesh.GetVertexR3(rmesh.GetIndex(i + t));
              if (!points.TryGetValue(p, out int v)) points.Add(p, v = points.Count);
              triangle.SetAttributeValue(t == 0 ? "v1" : t == 1 ? "v2" : "v3", v);
            }
            if (group.Texture != null)
            {
              triangle.SetAttributeValue("pid", texgid); var tt = group.Texcoords;
              for (int t = 0; t < 3; t++)
              {
                //if (!texccords.TryGetValue(tt[ii[i + t]], out int v)) texccords.Add(tt[ii[i + t]], v = texccords.Count);
                if (!texccords.TryGetValue(tt[i + t], out int v)) texccords.Add(tt[i + t], v = texccords.Count);
                triangle.SetAttributeValue(t == 0 ? "p1" : t == 1 ? "p2" : "p3", v);
              }
            }
          }
          foreach (var p in points.Keys)
          {
            var vertex = new XElement(ns + "vertex"); vertices.Add(vertex);
            vertex.SetAttributeValue("x", p.x.ToString(63, 0));
            vertex.SetAttributeValue("y", p.y.ToString(63, 0));
            vertex.SetAttributeValue("z", p.z.ToString(63, 0));
          }
          if (group.Texture != null)
            foreach (var p in texccords.Keys)
            {
              var tex2coord = new XElement(ms + "tex2coord"); texture2dgroup.Add(tex2coord);
              tex2coord.SetAttributeValue("u", +p.x);
              tex2coord.SetAttributeValue("v", -p.y);
            }
          points.Clear(); texccords.Clear();
        }
        if (components != null) { obj.Add(components); foreach (var p in desc) add(p, components); }
        resources.Add(obj);
        var item = new XElement(ns + (dest.Name.LocalName == "build" ? "item" : "component"));
        var objectid = uid++; obj.SetAttributeValue("id", objectid);
        item.SetAttributeValue("objectid", objectid);
        var m = group.trans;
        item.SetAttributeValue("transform", string.Format("{0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10} {11}", System.Xml.XmlConvert.ToString(m._11), System.Xml.XmlConvert.ToString(m._12), System.Xml.XmlConvert.ToString(m._13), System.Xml.XmlConvert.ToString(m._21), System.Xml.XmlConvert.ToString(m._22), System.Xml.XmlConvert.ToString(m._23), System.Xml.XmlConvert.ToString(m._31), System.Xml.XmlConvert.ToString(m._32), System.Xml.XmlConvert.ToString(m._33), System.Xml.XmlConvert.ToString(m._41), System.Xml.XmlConvert.ToString(m._42), System.Xml.XmlConvert.ToString(m._43)));
        dest.Add(item);
      };
      foreach (var group in nodes.Where(p => p.Parent == nodes[0])) add(group, build);
      //doc.Save("C:\\Users\\cohle\\Desktop\\text.xml"); //!!!
      var memstr = new MemoryStream();
      using (var package = System.IO.Packaging.Package.Open(memstr, FileMode.Create))
      {
        var packdoc = package.CreatePart(new Uri("/3D/3dmodel.model", UriKind.Relative), "application/vnd.ms-package.3dmanufacturing-3dmodel+xml");
        using (var str = packdoc.GetStream()) doc.Save(str);
        package.CreateRelationship(packdoc.Uri, System.IO.Packaging.TargetMode.Internal, "http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel", "rel0");
        foreach (var (bin, id) in textures)
        {
          var pack = package.CreatePart(new Uri("/3D/Textures/" + id + ".png", UriKind.Relative), "image/png");
          using (var bmp = Image.FromStream(new MemoryStream(bin)))
          using (var str = pack.GetStream()) bmp.Save(str, System.Drawing.Imaging.ImageFormat.Png);
          packdoc.CreateRelationship(pack.Uri, System.IO.Packaging.TargetMode.Internal, "http://schemas.microsoft.com/3dmanufacturing/2013/01/3dtexture", "rel" + id);
        }
        if (prev != null)
        {
          var packpng = package.CreatePart(new Uri("/Metadata/thumbnail.png", UriKind.Relative), "image/png");
          using (var str = packpng.GetStream()) prev.Save(str, System.Drawing.Imaging.ImageFormat.Png);
          package.CreateRelationship(packpng.Uri, System.IO.Packaging.TargetMode.Internal, "http://schemas.openxmlformats.org/package/2006/relationships/metadata/thumbnail", "rel1");
        }
        if (script != null)
        {
          var packpng = package.CreatePart(new Uri("/Metadata/csg.cs", UriKind.Relative), System.Net.Mime.MediaTypeNames.Text.Plain);
          using (var str = packpng.GetStream()) using (var sw = new StreamWriter(str)) sw.Write(script);
        }
      }
      File.WriteAllBytes(path, memstr.ToArray());
    }

  }
}
