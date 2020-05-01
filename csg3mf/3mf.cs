﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
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
  public unsafe class Node //: Neuron
  {
    internal Node() { Transform = Rational.Matrix.Identity(); }
    internal Node Parent;
    public string Name;
    public Rational.Matrix Transform;
    public IMesh Mesh;
    public float2[] Texcoords;
    public Material[] Materials;
    public struct Material
    {
      public int StartIndex, IndexCount;
      public uint Color;
      public byte[] Texture;
      public Texture texture;
    }
    internal float3x4 transform;
    internal VertexBuffer vertexbuffer;
    internal IndexBuffer indexbuffer;
    internal float3x4 gettrans(Node rel = null)
    {
      var m = transform;
      for (var p = Parent; p != rel && p.Parent != null; p = p.Parent) m *= p.transform; return m;
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
      transform = (float3x4)Transform; if (Mesh == null) return;
      var nv = Mesh.VertexCount; var vv = (double3*)StackPtr; StackPtr += nv * sizeof(double3);
      var ni = Mesh.IndexCount; var ii = (int*)StackPtr; StackPtr += ni * sizeof(int);
      Mesh.CopyBuffer(0, 0, new Variant(&vv->x, 3, nv));
      Mesh.CopyBuffer(1, 0, new Variant(ii, 1, ni));
      for (int i = 0; i < ni; i++) ((ushort*)ii)[i] = (ushort)ii[i];
      for (int i = 0; i < Materials.Length; i++)
      {
        ref var m = ref Materials[i];
        if (m.Texture != null && m.texture == null) m.texture = GetTexture(m.Texture);
      }
      if (Texcoords != null) fixed (float2* tt = Texcoords) GetMesh(ref vertexbuffer, ref indexbuffer, vv, nv, (ushort*)ii, ni, 0.3f, tt, 2);
      else GetMesh(ref vertexbuffer, ref indexbuffer, vv, nv, (ushort*)ii, ni, 0.3f);
      StackPtr = (byte*)vv;
    }

    //static void unscale(List<Node> nodes, Node parent)
    //{
    //  for (int i = 0; i < nodes.Count; i++)
    //  {
    //    var p = nodes[i]; if (p.Parent != parent) continue;
    //    var lsq = p.Transform.mx.LengthSq;
    //    if (lsq != 1)
    //    {
    //      var s = (Rational)(decimal)Math.Sqrt((double)lsq);
    //      p.Transform = Rational.Matrix.Scaling(1 / s) * p.Transform;
    //      if (p.StartIndex == 0) p.Mesh?.Transform(s);
    //      for (int t = 0; t < nodes.Count; t++)
    //        if (nodes[t].Parent == p)
    //          nodes[t].Transform = nodes[t].Transform * Rational.Matrix.Scaling(s);
    //    }
    //    unscale(nodes, p);
    //  }
    //}
    //internal static float3box getboxest(IEnumerable<Node> nodes, Node root = null)
    //{
    //  float3box box; boxempty((float3*)&box);
    //  foreach (var p in nodes)
    //  {
    //    if (p.Mesh == null) continue;
    //    var m = (float3x4)p.Transform; for (var t = p.Parent; t != root; t = t.Parent) m *= (float3x4)t.Transform;
    //    foreach (var v in p.Mesh.VerticesF3()) boxadd(&v, &m, &box.min);
    //  }
    //  return box;
    //}
    static int strcpy(char* p, string s) { int n = s.Length; for (int i = 0; i < n; i++) p[i] = s[i]; return n; }

    internal static List<Node> Import3MF(string path, out string script)
    {
      using (var package = System.IO.Packaging.Package.Open(path))
      {
        var xml = package.GetPart(new Uri("/3D/3dmodel.model", UriKind.Relative));
        XDocument doc; using (var str = xml.GetStream()) doc = XDocument.Load(str); //doc.Save("C:\\Users\\cohle\\Desktop\\test1.xml");
        var model = doc.Root; var ns = model.Name.Namespace;
        var unit = (string)model.Attribute("unit");
        var scale = unit == "micron" ? 0.000001m : unit == "millimeter" ? 0.001m : unit == "centimeter" ? 0.01m : unit == "inch" ? 0.0254m : unit == "foot" ? 0.3048m : 1;
        var res = model.Element(ns + "resources");
        var build = model.Element(ns + "build");
        var nodes = new List<Node>();
        var root = new Node(); nodes.Add(root);
        root.Transform[0u] = root.Transform[4u] = root.Transform[8u] = scale; //root.Transform = Rational.Matrix.Scaling(scale); 
        foreach (var p in build.Elements(ns + "item").Select(item => convert(item))) { p.Parent = root; nodes.Add(p); }
        /////////////
        if (nodes.Count > 1 && dot(((float3x4)nodes[1].Transform)[0]) > 10) //curiosity bug fix
          foreach (var p in nodes) p.Transform *= Rational.Matrix.Scaling(1 / (decimal)Math.Sqrt((double)p.Transform.mx.LengthSq));
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
          if (tra != null) fixed (char* p = tra) node.Transform.SetValues(new Variant(p, 12));
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
          var vertices = mesh.Element(ns + "vertices").Elements(ns + "vertex");
          var triangles = mesh.Element(ns + "triangles").Elements(ns + "triangle");
          var kk = triangles.OrderBy(p => p.Attribute("pid")?.Value).ToArray();
          var mm = kk.Select(p => p.Attribute("pid")?.Value).Distinct().ToArray();
          var ii = (int*)StackPtr;
          for (int i = 0, k = 0; i < kk.Length; i++) { var p = kk[i]; ii[k++] = (int)p.Attribute("v1"); ii[k++] = (int)p.Attribute("v2"); ii[k++] = (int)p.Attribute("v3"); }
          var me = Factory.CreateMesh(); me.Update(vertices.Count(), new Variant(ii, 1, kk.Length * 3));
#if(true) 
          int l = 0; var ss = (char*)StackPtr;
          foreach (var p in vertices)
          {
            var n = strcpy(ss, p.Attribute("x").Value); ss[n++] = ' ';
            n += strcpy(ss + n, p.Attribute("y").Value); ss[n++] = ' ';
            n += strcpy(ss + n, p.Attribute("z").Value); ss[n++] = '\0';
            me.SetVertex(l++, new Variant(ss, 3));
          }
#else // or
          int l = 0;
          foreach (var p in vertices)
            fixed (char* t = $"{p.Attribute("x").Value} {p.Attribute("y").Value} {p.Attribute("z").Value}")
              me.SetVertex(l++, new Variant(t, 3));
#endif
          node.Mesh = me; node.Materials = new Material[mm.Length];
          var ms = (XNamespace)"http://schemas.microsoft.com/3dmanufacturing/material/2015/02";
          for (int i = 0, ab = 0, bis = 1; i < mm.Length; i++, ab = bis)
          {
            var pid = mm[i]; for (; bis < kk.Length && kk[bis - 1].Attribute("pid")?.Value == pid; bis++) ;
            ref var ma = ref node.Materials[i];
            ma.Color = color; ma.IndexCount = bis * 3 - (ma.StartIndex = ab * 3);
            if (pid == null) continue;
            var basematerials = res.Elements(ns + "basematerials").FirstOrDefault(p => (string)p.Attribute("id") == pid);
            if (basematerials != null)
            {
              var mat = basematerials.Elements(ns + "base").ElementAt((int)kk[ab].Attribute("p1"));
              var sco = (string)mat.Attribute("displaycolor"); if (sco[0] != '#') continue;
              var col = uint.Parse(sco.Substring(1), System.Globalization.NumberStyles.HexNumber);
              if (sco.Length == 9) col = (col >> 8) | (col << 24); else col |= 0xff000000; ma.Color = col; continue;
            }
            var texture2dgroup = res.Elements(ms + "texture2dgroup").FirstOrDefault(p => (string)p.Attribute("id") == pid);
            if (texture2dgroup == null) continue;
            var pp = texture2dgroup.Elements(ms + "tex2coord").Select(p => new float2((float)p.Attribute("u"), -(float)p.Attribute("v"))).ToArray();
            var tt = node.Texcoords ?? (node.Texcoords = new float2[kk.Length * 3]);
            for (int t = ab; t < bis; t++)
            {
              var p1 = kk[t].Attribute("p1"); tt[t * 3 + 0] = pp[(int)p1];
              var p2 = kk[t].Attribute("p2"); tt[t * 3 + 1] = pp[(int)(p2 ?? p1)];
              var p3 = kk[t].Attribute("p3"); tt[t * 3 + 2] = pp[(int)(p3 ?? p1)];
            }
            var texid = (string)texture2dgroup.Attribute("texid");
            var texture2d = res.Elements(ms + "texture2d").Where(t => (string)t.Attribute("id") == texid).First();
            var bin = texture2d.Annotation<byte[]>();
            if (bin == null)
            {
              var texpath = (string)texture2d.Attribute("path");
              var texpart = package.GetPart(new Uri(texpath, UriKind.Relative));
              using (var str = texpart.GetStream()) { bin = new byte[str.Length]; str.Read(bin, 0, bin.Length); texture2d.AddAnnotation(bin); }
            }
            ma.Texture = bin;
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
      doc.Add(new XAttribute(XNamespace.Xmlns + "m", ms.NamespaceName));
      var resources = new XElement(ns + "resources"); doc.Add(resources);
      var build = new XElement(ns + "build"); doc.Add(build);
      var uid = 1; var textures = new List<(byte[] bin, int id, XElement e)>();
      var basematerials = new XElement(ns + "basematerials"); resources.Add(basematerials);
      var bmid = uid++; basematerials.SetAttributeValue("id", bmid);
      void add(Node group, XElement dest)
      {
        var obj = new XElement(ns + "object"); obj.SetAttributeValue("id", 0);
        if (group.Name != null) obj.SetAttributeValue("name", group.Name);
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
          var color = group.Materials[0].Color; //group.Color 
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
            var p = (char*)StackPtr; rmesh.GetVertex(i, new Variant(p, 3));
            for (int k = 0, n; k < 3; k++)
            {
              for (n = 0; p[n] > ' '; n++) ; var s = new string(p, 0, n); p += n + 1;
              vertex.SetAttributeValue(k == 0 ? "x" : k == 1 ? "y" : "z", s);
            }
          }
          for (int k = 0; k < group.Materials.Length; k++)
          {
            ref var ma = ref group.Materials[k]; var texgid = 0;
            if (ma.Texture != null)
            {
              int texid; int i = 0; for (; i < textures.Count && !Native.Equals(textures[i].bin, ma.Texture); i++) ;
              if (i != textures.Count) texid = textures[i].id;
              else
              {
                texid = uid++;
                var texture2d = new XElement(ms + "texture2d"); resources.Add(texture2d);
                texture2d.SetAttributeValue("id", texid);
                string typ; switch (ma.Texture[0]) { case 0x89: typ = "png"; break; case 0xff: typ = "jpeg"; break; default: typ = "bmp"; break; }
                texture2d.SetAttributeValue("path", $"/3D/Textures/{texid}.{typ}");
                texture2d.SetAttributeValue("contenttype", $"image/{typ}");
                textures.Add((ma.Texture, texid, texture2d));
              }
              var texture2dgroup = new XElement(ms + "texture2dgroup"); resources.Add(texture2dgroup);
              texture2dgroup.SetAttributeValue("id", texgid = uid++);
              texture2dgroup.SetAttributeValue("texid", texid);
              foreach (var p in group.Texcoords)
              {
                var tex2coord = new XElement(ms + "tex2coord"); texture2dgroup.Add(tex2coord);
                tex2coord.SetAttributeValue("u", +p.x);
                tex2coord.SetAttributeValue("v", -p.y);
              }
            }
            for (int i = 0; i < ma.IndexCount; i += 3)
            {
              var triangle = new XElement(ns + "triangle"); triangles.Add(triangle);
              for (int t = 0; t < 3; t++)
                triangle.SetAttributeValue(t == 0 ? "v1" : t == 1 ? "v2" : "v3", rmesh.GetIndex(ma.StartIndex + i + t));
              if (texgid == 0) continue;
              triangle.SetAttributeValue("pid", texgid);
              for (int t = 0; t < 3; t++) triangle.SetAttributeValue(t == 0 ? "p1" : t == 1 ? "p2" : "p3", ma.StartIndex + i + t);
            }
          }
        }
        if (components != null) { obj.Add(components); foreach (var p in desc) add(p, components); }
        resources.Add(obj);
        var item = new XElement(ns + (dest.Name.LocalName == "build" ? "item" : "component"));
        var objectid = uid++; obj.SetAttributeValue("id", objectid);
        item.SetAttributeValue("objectid", objectid);
        var ss = (char*)StackPtr; group.Transform.GetValues(new Variant(ss, 12));
        item.SetAttributeValue("transform", new string(ss));
        dest.Add(item);
      };
      foreach (var group in nodes.Where(p => p.Parent == nodes[0])) add(group, build);
      //doc.Save("C:\\Users\\cohle\\Desktop\\test2.xml");
      var memstr = new MemoryStream();
      using (var package = System.IO.Packaging.Package.Open(memstr, FileMode.Create))
      {
        var packdoc = package.CreatePart(new Uri("/3D/3dmodel.model", UriKind.Relative), "application/vnd.ms-package.3dmanufacturing-3dmodel+xml");
        using (var str = packdoc.GetStream()) doc.Save(str);
        package.CreateRelationship(packdoc.Uri, System.IO.Packaging.TargetMode.Internal, "http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel", "rel0");
        foreach (var (bin, id, e) in textures)
        {
          var pack = package.CreatePart(new Uri((string)e.Attribute("path"), UriKind.Relative), (string)e.Attribute("contenttype"));
          using (var str = pack.GetStream()) str.Write(bin, 0, bin.Length); //using (var bmp = Image.FromStream(new MemoryStream(bin))) using (var str = pack.GetStream()) bmp.Save(str, System.Drawing.Imaging.ImageFormat.Png);
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
