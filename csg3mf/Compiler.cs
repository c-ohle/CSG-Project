using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;

namespace csg3mf
{
  class Compiler
  {
    string code; Type @this; static Compiler compiler;
    Type[] types; int nextid, nglob, lastprop, boxc1, boxc2; FieldInfo fidata; MethodInfo dbgstp, dbgstk;
    List<Token> tokens = new List<Token>(); List<Block> blocks = new List<Block>();
    List<ab> stack = new List<ab>(), marks = new List<ab>(); List<object> dms = new List<object>();
    List<Block> attris = new List<Block>();
    StateMachine mb = new StateMachine(); Type rettype; DynamicMethod chkdm;
    int @return, @continue, @break, @try, @throw, head; Func<int, int> accessor; bool retok, end;
    List<Tuple<DynamicMethod, Block[]>> funsat = new List<Tuple<DynamicMethod, Block[]>>();
    List<Tuple<int, DynamicMethod, ab[], Block>> funcs = new List<Tuple<int, DynamicMethod, ab[], Block>>(); Stack<int> elsestack = new Stack<int>();
    object[] pmap; int[] vmap;
    struct ab { internal int a, b; public override string ToString() { return compiler.tokens[a] + " " + compiler.vmap[a].ToString("X8") + " " + compiler.pmap[a]; } }
    internal class __null { }
    class StateMachine : ILGenerator
    {
      internal StateMachine parent, child; internal int scope, reploc, repid;
      internal List<int> replys = new List<int>(); internal Type reptype;
      internal override void Reset() { replys.Clear(); reptype = null; repid = 0; base.Reset(); }
      internal void Ldthis() { Ldarg(0); for (var p = parent; p != null; p = p.parent) if (p.reptype != null) { Ldc_I4(0); Ldelem(typeof(object)); } }
    }
    internal StringBuilder trace, _trace_;
    internal List<string> usings = new List<string>(), usingsuse = new List<string>();
    internal List<Type> usingst = new List<Type>();
    internal List<map> spots = new List<map>();
    internal List<map> errors = new List<map>(); internal int maxerror;
    internal map[] typemap()
    {
      int c = 0, n = tokens.Count; for (int i = 0; i < n; i++) if (pmap[i] != null) c++; var a = new map[c];
      for (int i = 0, k = 0; i < n; i++) if (pmap[i] != null) a[k++] = new map { i = tokens[i].Position, n = tokens[i].Length, p = pmap[i], v = vmap[i] };
      return a;
    }
    internal static readonly string[] keywords = {
      "void","bool","char","sbyte","byte","int","uint","short","ushort","long","ulong","decimal","float","double","string","object","dynamic", //17
      "var","this","base","public","protected","private","internal","readonly", "static",
      "if", "for","while","foreach","switch","return","using","checked","unchecked",
      "true","false","null","as","is","in","new","typeof","try","throw","catch","else","case","break","default",
      "continue","finally","get","set","value","ref","out","sizeof","stackalloc","class","struct","lock","goto","fixed","const"};
    internal object[] Compile(Type @this, string code, int fl)
    {
      try { return CompileSafe(@this, code, fl); }
      catch (Exception e)
      {
        for (var p = mb; p != null; p = p.child) p.Reset();
        Error(0583, "Internal Compiler Error " + e.Message, tokens[0]); return null;
      }
    }
    object[] CompileSafe(Type @this, string code, int fl)
    {
      if (this.code != null) Reset(); (compiler = this).code = code; this.@this = @this; nextid = 1; //TypeHelper.resolver = null; 
      fidata = typeof(Neuron).GetField("data", BindingFlags.Instance | BindingFlags.NonPublic); head = (fl & 1) != 0 ? 2 : 1;
      dbgstp = (fl & 1) != 0 ? typeof(Neuron).GetMethod("dbgstp", BindingFlags.Instance | BindingFlags.NonPublic) : null;
      dbgstk = (fl & 1) != 0 ? typeof(Neuron).GetMethod("dbgstk", BindingFlags.Static | BindingFlags.NonPublic) : null;
      if ((trace = (fl & 2) != 0 ? this._trace_ ?? (this._trace_ = new StringBuilder()) : null) != null) trace.Clear();
      MakeTokens(); if (pmap == null || pmap.Length < tokens.Count) { pmap = new object[tokens.Count]; vmap = new int[tokens.Count]; }
      MakeBlocks(new Block(0, tokens.Count - 1), true);
      for (int i = 0; i < blocks.Count; i++)
      {
        var b = blocks[i]; if (b.Length == 0) continue;
        if (b.Take("using")) { if (!b.StartsWith('(')) { ParseUsing(b); blocks.RemoveAt(i--); } continue; }
        if (b.Take("if") || b.Take("else") || b.Take("for") || b.Take("while") || b.Take("using") || b.Take("return")) continue;
        if (b.Take("var") || b.Take("try") || b.Take("catch") || b.Take("finally") || b.Take("throw") || b.Take("goto")) continue;
        if (b.Take("switch") || b.Take("case") || b.Take("default") || b.Take("lock") || b.Take("this") || b.Take("base")) continue;
        if (b.Take("foreach") || b.Take("++") || b.Take("--") || b.Take("checked") || b.Take("unchecked")) continue;
        attris.Clear();
        while (b.StartsWith('['))
        {
          var l = b.Next(); l = l.Trim();
          for (int t = 0, nt = l.ParamCount(); t < nt; t++)
          {
            var c = l.Param(); var cp = c; if (c.Length == 0) { Error(1001, "Identifier expected", c); continue; }
            var s = c[0]; var at = ParseType(ref c, 1); if (at == null) { Error(1031, "Type expected", c[0].Start()); continue; }
            if (!at.IsSubclassOf(typeof(Attribute))) { Error(0616, "'{1}' is not an attribute class", s.SubToken(0, c[-1].End().Position - s.Position), at.FullName); continue; }
            attris.Add(cp);
          }
        }

        bool ispublic = (modifier(ref b) & 1) != 0;
        if (b.Length == 0) { Error(1001, "Identifier expected", b[0].Start()); continue; }
        if (b.Take("class") || b.Take("struct")) { Error(0000, "Unsupported", b[-1]); continue; }
        if (b.Take("const")) { __const(b, 0); blocks.RemoveAt(i--); continue; }
        var a = b.TakeType(); if (a.Length == 0) continue; if (b.Length < 1) continue; var tok = b[1][0];
        var isfunc = tok.Equals('(') || tok.Equals('{'); //if (!isfunc && !isstatic) continue;
        var type = ParseType(a); if (type == null) continue;
        var rem = true;
        if (!isfunc)
        {
          for (int t = 0, np = b.ParamCount(); t < np; t++)
          {
            var para = b.Param(); var name = para.Take(); if (para.Length != 0) rem = false; if (!checkname(name, 0, 1)) continue;
            stack.Add(Map(name, type, 0x86, 0));
          }
        }
        else
        {
          var name = b.Take();
          if (iskeyword(name)) { Error(1041, "Identifier expected; '{0}' is a keyword", name); }
          if (isonstack(name)) { Error(0102, "The type '{1}' already contains a definition for '{0}'", name, @this); }
          if (tok.Equals('{'))
          {
            b = b.Trim(); var t1 = blocks.Count; MakeBlocks(b, true); var bod = false; int box = -1; var acc = new DynamicMethod[2]; var id = nextid++;
            for (int t = t1; t < blocks.Count; t++)
            {
              b = blocks[t];
              if (b.Length == 1)
              {
                if (box == -1) { box = stack.Count; stack.Add(Map(b[-1], type, 0x86, 0)); }
                if (bod) Error(0501, "'{0}' must declare a body because it is not marked abstract, extern, or partial", name);
              }
              else if (box != -1) Error(0501, "'{0}' must declare a body because it is not marked abstract, extern, or partial", name);

              if (b.Take("get"))
              {
                if (acc[0] != null) Error(1007, "Property accessor already defined", blocks[t][0]);
                var dm = new DynamicMethod(ispublic || trace != null ? name.ToString() : string.Empty, type, new Type[] { typeof(Neuron) }, typeof(object), true); if (ispublic) dms.Insert(lastprop++, dm);
                if (b.Length == 0) funcs.Add(Tuple.Create(box, dm, (ab[])null, b));
                else { funcs.Add(Tuple.Create(id, dm, new ab[0], b)); bod = true; }
                acc[0] = dm; continue;
              }
              if (b.Take("set"))
              {
                if (acc[1] != null) Error(1007, "Property accessor already defined", blocks[t][0]);
                var dm = new DynamicMethod(ispublic || trace != null ? name.ToString() : string.Empty, typeof(void), new Type[] { typeof(Neuron), type }, typeof(object), true); if (ispublic) dms.Insert(acc[0] != null ? lastprop++ - 1 : lastprop++, dm);
                if (b.Length == 0) funcs.Add(Tuple.Create(box, dm, (ab[])null, b));
                else { funcs.Add(Tuple.Create(id, dm, new ab[] { Map(b[0], type, 5 | /*0x40 |*/ (nextid << 8), 0) }, b)); bod = true; nextid++; }
                acc[1] = dm; continue;
              }
              Error(1014, "A get or set accessor expected", b[0].Start());
            }
            stack.Add(Map(name, acc, 0xc3 | (id << 8), 0));
            blocks.RemoveRange(t1, blocks.Count - t1); if (acc[0] == null && acc[1] == null) Error(0548, "'{0}': property or indexer must have at least one accessor", name);
            if (box != -1 && (acc[0] == null || acc[1] == null)) Error(0840, "'{0}' must declare a body because it is not marked abstract or extern. Automatically implemented properties must define both get and set accessors.", name);
            if (ispublic && attris.Count != 0)
            {
              var dm = new DynamicMethod(string.Empty, typeof(Attribute[]), new Type[] { typeof(Neuron) }, typeof(object), true);
              dms.Insert(lastprop++, dm); funsat.Add(Tuple.Create(dm, attris.ToArray()));
            }
          }
          else
          {
            var pp = b.Next(); pp = pp.Trim(); var np = pp.ParamCount(',', 0xf); var tt = new Type[np + 1]; tt[0] = typeof(Neuron); var t1 = stack.Count;
            for (int t = 1; t <= np; t++)
            {
              var pa = pp.Param(',', 0xf); var sn = pa.ParamName(); checkname(sn, t1, 2);
              var fa = pa.Take("ref") ? 1 : pa.Take("out") ? 2 : 0;
              var pt = ParseType(pa); //if (pt == null) continue;
              if (pt == typeof(void)) { Error(1536, "Invalid parameter type '{0}'", pa, pt); pt = null; }
              if (fa != 0 && pt != null) pt = pt.MakeByRefType();
              tt[t] = filterdyn(pt) ?? typeof(object); if (pt == null || sn.Length == 0) continue;
              stack.Add(Map(sn, pt, 5 | (nextid << 8), 0)); nextid++;
            }
            var dm = new DynamicMethod(ispublic || trace != null ? name.ToString() : string.Empty, type, tt, typeof(object), true); if (ispublic) dms.Add(dm); //var s = dm.Name;
            funcs.Add(Tuple.Create(nextid, dm, stack.Skip(t1).ToArray(), b));
            if (dbgstp != null || trace != null) for (int t = t1; t < stack.Count; t++) dm.DefineParameter(2 + t - t1, ParameterAttributes.None, tokens[stack[t].a].ToString()); //as info for RtExplorer?
            unstack(t1); stack.Add(Map(name, dm, 0x83 | (nextid << 8), 0)); nextid++;
          }
        }
        if (rem) blocks.RemoveAt(i--);
      }

      nglob = stack.Count; @continue = @break = @throw = @try = -1;

      boxopt();

      if (blocks.Count != 0)
      {
        var dm = new DynamicMethod(".", typeof(void), new Type[] { typeof(Neuron) }, typeof(object), true); var id = nextid++;
        Lambda(dm, id, null, new Block()); dms.Insert(lastprop, dm);
      }
      for (int i = 0; i < funcs.Count; i++)
      {
        var p = funcs[i];
        if (p.Item3 == null)
        {
          var dm = p.Item2; var a = stack[p.Item1].a; var box = vmap[a] >> 8; var type = (Type)pmap[a];
          dm.InitLocals = false; mb.Begin(dm); mb.Ldthis(); mb.Ldfld(fidata);
          mb.Ldc_I4(box >> 12); if ((box & 0x0fff) != 0x0fff) { mb.Ldelem(type.MakeArrayType()); mb.Ldc_I4(box & 0x0fff); }
          if (dm.ReturnType != typeof(void)) mb.Ldelem(type); else { mb.Ldarg(1); mb.Stelem(type); }
          mb.Ret(); mbend(null); continue;
        }
        Lambda(p.Item2, p.Item1, p.Item3, p.Item4);
      }
      for (int i = 0; i < funsat.Count; i++)
      {
        var p = funsat[i]; var ats = p.Item2; p.Item1.InitLocals = false;
        mb.Begin(p.Item1); mb.Ldc_I4(ats.Length); mb.Newarr(typeof(Attribute));
        for (int j = 0; j < ats.Length; j++)
        {
          var c = ats[j]; var s = c[0]; var t = ParseType(ref c, 1); s = s.SubToken(0, c[-1].End().Position - s.Position);
          if (c.Length != 0) c = c.Trim(); mb.Dup(); mb.Ldc_I4(j); newcall(s, t, c); mb.Stelem(typeof(Attribute));
        }
        mb.Ret(); mbend(null);
      }
      if (maxerror > 1) return null;
      for (int i = 0; i < tokens.Count; i++)
      {
        var v = vmap[i]; if (!((v & 0x8f) == 0x84 || (v & 0x8f) == 0x86 || ((v & 0x8f) == 0x83 && pmap[i] is FieldInfo))) continue; if (tokens[i].Equals('{')) continue;
        if ((v & 0x30) == 0) { Warning(0168, "The variable '{0}' is declared but never used", tokens[i]); continue; }
        if ((v & 0x20) == 0) { Warning(0219, "The variable '{0}' is assigned but its value is never used", tokens[i]); continue; }
      }

      var data = new object[boxc2];
      for (int i = head; i < boxc1; i++)
      {
        var c = 0; var t = (Type)null;
        for (int y = nglob - 1; y >= 0; y--)
        {
          var a = stack[y].a; var v = vmap[a]; if ((v & 0x8f) != 0x86 || (v >> 20) != i) continue;
          t = (Type)pmap[a]; c = Math.Max(c, (v >> 8) & 0x0fff); if (i > head) break;
        }
        data[i] = Array.CreateInstance(__typeopt(t), c + 1);
      }
      dms.Insert(lastprop, null); dms.Insert(0, null); dms.Insert(1, new byte[] { (byte)(dbgstp != null ? 1 : 0), checked((byte)(boxc1 - head)) });
      data[0] = dms.ToArray(); return data;
    }
    void Lambda(DynamicMethod dm, int id, ab[] pp, Block b)
    {
      var t2 = nextid; var t3 = spots.Count; var t4 = trace != null ? trace.Length : 0; dm.InitLocals = dbgstp != null;
      for (int[] replys = null; ;)
      {
        var t1 = stack.Count; if (pp != null) for (int x = 0; x < pp.Length; x++) stack.Add(new ab { a = pp[x].a, b = x + 1 }); retok = end = false;
        rettype = dm.ReturnType; var inl = !b.StartsWith('{'); var lrt = rettype != typeof(void) && (!inl || dbgstk != null);
        mb.Begin(dm);
        @return = mb.DefineLabel() << 1; if (lrt) mb.DeclareLocal(rettype); int iframe = 0;
        if (dbgstk != null)
        {
          mb.Ldc_I4(0); mb.Ldloca(iframe = mb.DeclareLocal(typeof(long))); mb.Ldarga(0); mb.Call(dbgstk);
          if (mb.parent != null && mb.parent.reptype != null) { mb.Ldc_I4(mb.parent.repid); mb.Ldloca(mb.DeclareLocal(typeof(long))); mb.Ldarga(0); mb.Call(dbgstk); }
          for (int t = t1, k = 0; t < stack.Count; t++)
          {
            var x = stack[t].a; var type = (Type)pmap[x]; mb.Ldc_I4(vmap[x] >> 8); mb.Ldloca(mb.DeclareLocal(typeof(long)));
            if (type.IsByRef) { mb.Ldarg(++k); var ü = mb.DeclareLocal(type, true); mb.Stloc(ü); mb.Ldloca(ü); } else mb.Ldarga(++k); mb.Call(dbgstk);
          }
        }
        if (replys != null)
        {
#if(true) // box optimization
          mb.repid = nextid++; var ttt = new List<Type>(replys.Length); var iii = new int[replys.Length];
          for (int t = 0; t < replys.Length; t++)
          {
            var type = (Type)pmap[replys[t]]; if (!type.IsValueType) { ttt.Add(null); iii[t] = ttt.Count; continue; }
            type = __typeopt(type); var x = ttt.IndexOf(type); if (x == -1) { ttt.Add(type); iii[t] = ttt.Count; continue; }
            var c = 0; for (int k = 0; k < t; k++) if ((iii[k] & 0xffff) == x + 1) c++; iii[t] = (x + 1) | (c << 16);
          }
          mb.Ldc_I4(1 + ttt.Count); mb.Newarr(typeof(object)); mb.Dup(); mb.Ldc_I4(0); mb.Ldarg(0); mb.Stelem(typeof(object));
          for (int t = 0; t < ttt.Count; t++)
          {
            if (ttt[t] == null) continue; var c = 0; for (int k = 0; k < iii.Length; k++) if ((iii[k] & 0xffff) == 1 + t) c++;
            mb.Dup(); mb.Ldc_I4(1 + t); mb.Ldc_I4(c); mb.Newarr(ttt[t]); mb.Stelem(typeof(object));
          }
          for (int t = 0; t < replys.Length; t++)
          {
            var x = replys[t]; var type = (Type)pmap[x];
            vmap[x] = (vmap[x] & 0xff) | 0x40 | (nextid++ << 8); //stack.Add(new ab { a = x, b = iii[t] });
            pmap[x] = new RepInfo { type = type, index = iii[t], id = mb.repid }; if ((vmap[x] & 0xf) != 5) continue;
            int k = t1; for (; stack[k].a != x; k++) ;
            mb.Dup(); mb.Ldc_I4(iii[t] & 0xffff); if (type.IsValueType) { mb.Ldelem(typeof(object)); mb.Ldc_I4(iii[t] >> 16); }
            mb.Ldarg(stack[k].b); mb.Stelem(type);
          }
          mb.Stloc(mb.reploc = mb.DeclareLocal(typeof(object[]))); mb.reptype = typeof(object[]);
          if (dbgstk != null) { mb.Ldc_I4(mb.repid); mb.Ldloca(mb.DeclareLocal(typeof(long))); mb.Ldloca(mb.reploc); mb.Call(dbgstk); }
#else
          mb.repid = nextid++; mb.Ldc_I4(1 + replys.Length); mb.Newarr(typeof(object)); mb.Dup(); mb.Ldc_I4(0); mb.Ldarg(0); mb.Stelem(typeof(object));
          for (int t = 1; t <= replys.Length; t++)
          {
            var type = (Type)pmap[replys[t - 1]];
            if (type.IsValueType) { mb.Dup(); mb.Ldc_I4(t); mb.Ldc_I4(1); mb.Newarr(type); mb.Stelem(typeof(object)); }
            var x = replys[t - 1]; vmap[x] = (vmap[x] & 0xff) | 0x40 | (nextid++ << 8); stack.Add(new ab { a = x, b = t });
            pmap[x] = new RepInfo { type = type, index = t, id = mb.repid }; if ((vmap[x] & 0xf) != 5) continue;
            int k = t1; for (; stack[k].a != x; k++) ;
            mb.Dup(); mb.Ldc_I4(t); if (type.IsValueType) { mb.Ldelem(typeof(object)); mb.Ldc_I4(0); }
            mb.Ldarg(stack[k].b); mb.Stelem(type);
          }
          mb.Stloc(mb.reploc = mb.DeclareLocal(typeof(object[]))); mb.reptype = typeof(object[]);
          if (dbgstk != null) { mb.Ldc_I4(mb.repid); mb.Ldloca(mb.DeclareLocal(typeof(long))); mb.Ldloca(mb.reploc); mb.Call(dbgstk); }  
#endif
        }

        if (inl) { if (pp != null) { Break(b); rettype = ParseStrong(b, rettype); if (lrt) mb.Stloc(0); } else { ParseBlock(0, true); mb.MarkLabel(@return >> 1); } }
        else
        {
          Break(b.SubBlock(0, 1)); var c = b.SubBlock(b.Length - 1, 1);
          ParseBlock(b.Trim(), true); mb.MarkLabel(@return >> 1); Break(c);
          if (!retok && lrt) Error(0161, "'{1}': not all code paths return a value", c, dm);
        }
        if (__isgen(dm.ReturnType)) { mb.Reset(); unstack(t1); break; }
        if (dbgstk != null) { mb.Ldc_I4(0); mb.Ldloca(iframe); mb.Ldnull(); mb.Call(dbgstk); }
        if (lrt) mb.Ldloc(0); mb.Ret(); endlabels();
        if (maxerror < 4 && mb.replys.Count != 0)
        {
          replys = mb.replys.ToArray(); mb.Reset(); unstack(t1); nextid = t2; spots.RemoveRange(t3, spots.Count - t3);
          if (trace != null) trace.Remove(t4, trace.Length - t4); continue;
        }
        mbend(trace); unstack(t1); break;
      }
    }
    void mbend(StringBuilder trace) { if (maxerror < 4) mb.End(trace); else mb.Reset(); }
    Type Lambda(DynamicMethod dm, ab[] pp, Block b)
    {
      var t2 = retok; var t3 = end; var t4 = @return; var t5 = @continue; var t6 = @break; var t7 = @try; var t8 = @throw; var t9 = rettype;
      mb = mb.child ?? (mb.child = new StateMachine { parent = mb }); mb.scope = stack.Count;
      Lambda(dm, nextid++, pp, b); var rt = rettype;
      mb = mb.parent; retok = t2; end = t3; @return = t4; @continue = t5; @break = t6; @try = t7; @throw = t8; rettype = t9; return rt;
    }
    internal void Reset()
    {
      nextid = maxerror = nglob = lastprop = 0; types = null; code = null;
      for (int i = 0, n = tokens.Count; i < n; i++) { pmap[i] = null; vmap[i] = 0; }
      tokens.Clear(); blocks.Clear(); usings.Clear(); usingst.Clear(); usingsuse.Clear(); errors.Clear(); funcs.Clear(); funsat.Clear(); attris.Clear(); elsestack.Clear(); //constids.Clear();
      stack.Clear(); marks.Clear(); spots.Clear(); dms.Clear(); compiler = null; accessor = null; chkdm = null;
    }
    int __tokenmap(int i)
    {
      //int x = 0; for (; x < tokens.Count; x++) if (tokens[x].Position == s.Position) break;
      for (int x = 0, i1 = 0, i2 = tokens.Count - 1; ;)
      {
        var t1 = tokens[i1].Position; var t2 = tokens[i2].Position;
        x = i1 + checked((i2 - i1) * (i - t1)) / (t2 - t1); //x = i1 + ((i2 - i1) >> 1);
        var k = tokens[x].Position; if (k == i) return x;
        if (k > i) i2 = x - 1; else i1 = x + 1;
      }
    }
    ab Map(Token s, object t, int v, int b)
    {
      var x = __tokenmap(s.Position);
      pmap[x] = t; vmap[x] = v; Debug.Assert(x < tokens.Count);
      return new ab { a = x, b = b };
    }
    void Break(Block b)
    {
      if (dbgstp == null) return; //mb.Break();
      mb.Ldthis(); mb.Ldc_I4(spots.Count); mb.Call(dbgstp);
      spots.Add(new map { i = b[0].Position, n = b[b.Length - 1].Position + b[b.Length - 1].Length - b[0].Position });
    }
    void ParseUsing(Block b)
    {
      //for compatibility ??? 
      //if (b[1].Equals('=')) { var s = b.Take(); checkname(s, 0, 3); b.Take(); var t = ParseType(b); if (t == null) return; stack.Add(Map(s, t, 0, 0)); return; }
      if (stack.Count != 0) Error(1529, "A using clause must precede all other elements defined in the namespace except extern alias declarations", b[-1]);
      for (string ns = null; ;)
      {
        if (b.Length == 0) { Error(1001, "Identifier expected", b[0].Start()); return; }
        var t = b.Take(); if (!t.IsWord) { Error(1001, "Identifier expected", t.Start()); return; }
        if (t.Equals("static"))
        {
          var st = ParseType(b); if (st == null) return;
          if (usingst.Contains(st)) { Warning(0105, "The using directive for '{1}' appeared previously in this namespace", b, ns); return; }
          usingst.Add(st); return;
        }
        ns = GetNameSpace(ns, t, true); if (ns == null) { Error(0246, "The type or namespace name '{0}' could not be found", t); return; }
        Map(t, ns, 0x88, 0);
        if (b.Length == 0) { if (!usings.Contains(ns)) usings.Add(ns); else Warning(0105, "The using directive for '{1}' appeared previously in this namespace", t, ns); return; }
        t = b.Take(); if (!t.Equals('.')) { Error(1002, "; expected", t.Start()); return; }
      }
    }
    Type ParseType(Block b)
    {
      var t = ParseType(ref b); if (t != null && b.Length == 0) return t;
      if (b.Length == 1) { Error(0246, "The type or namespace name '{0}' could not be found", b); return null; }// Error(1031, "Type expected", b); return null; }
      Error(1525, "Invalid expression term '{0}'", b); return null;
    }
    Type[] ParseTypes(ref Block b)
    {
      if (!b.StartsWith("<")) return null;
      var pp = b.Next(0xf).Trim(); var np = pp.ParamCount(',', 0xf); if (np == 0) { Error(1031, "Type expected", pp); }
      var tt = new Type[np]; for (int x = 0; x < np; x++) tt[x] = ParseType(pp.Param(',', 0xf)) ?? typeof(object);
      return tt;
    }
    Type ParseType(ref Block block, int fl = 0)
    {
      var x = pmap[block.Position];
      if (x != null)
      {
        if (!(x is string || x is Type)) return null;
        while (x is string) { block.Take(); block.Take(); x = pmap[block.Position]; }
        block.Take();
        for (; ; )
        {
          if (block.StartsWith('<')) { block.Next(0xf); continue; }
          if (block.StartsWith('.')) { var t = pmap[block.Position + 1]; if (t is Type) { block.Take(); block.Take(); x = t; continue; } }
          if (block.StartsWith('[')) if (block[1].Equals(']')) { block.Take(); block.Take(); x = ((Type)x).MakeArrayType(); continue; }
          if (block.StartsWith('*')) { block.Take(); return ((Type)x).MakePointerType(); }
          break;
        }
        return (Type)x;
      }
      var line = block; var token = line.Take();
      var type = token.DefType(); var typeid = 0;
      //if (type == null)
      //  for (int i = 0, j; i < stack.Count; i++)
      //    if (tokens[j = stack[i].a].Equals(token) && (vmap[j] & 0xff) == 0x80)
      //    {
      //      type = (Type)pmap[j]; typeid = vmap[j] & ~0x80; break;
      //    }

      if (type == null)
      {
        var ss = token.ToString();
        if ((type = @this.GetNestedType(ss)) == null)
          for (int i = usingst.Count - 1; i >= 0; i--)
            if ((type = usingst[i].GetNestedType(ss)) != null)
              break;
      }
      if (type == null)
      {
        string ns = GetNameSpace(null, token, false);
        if (ns != null)
          for (; ; )
          {
            Map(token, ns, 0x08, 0);
            if (line.Length == 0) { Error(1001, "Identifier expected", line[0].Start()); return null; }
            token = line.Take(); if (!token.Equals('.')) { Error(1002, "; expected", token.Start()); return null; }
            if (line.Length == 0) { Error(1001, "Identifier expected", token.End()); return null; }
            token = line.Take(); if (!token.IsWord) { Error(1001, "Identifier expected", token.Start()); return null; }
            var s = GetNameSpace(ns, token, false); if (s == null) break; ns = s;
          }
        type = GetType(ns, token, ParseTypes(ref line), (fl & 1) != 0);
      }
      if (type == null) return null;
      Map(token, type, typeid, 0);
      for (; line.Length != 0;)
      {
        if (line.StartsWith('['))
        {
          var s = line; s.Take(); if (!s.Take(']')) break;
          type = type.MakeArrayType(); line = s; continue;
        }
        if (line.StartsWith('.') || line.StartsWith('#'))
        {
          Map(line[0], type, 0x41, 0);
          var s = line; s.Take(); var name = s.Take(); if (!name.IsWord || ((fl & 2) == 0 && s.StartsWith('('))) break;
          var sn = name.ToString(); var tt = ParseTypes(ref s);
          if (tt != null) sn = string.Concat(sn, "`", tt.Length.ToString());
          var nt = type.GetNestedType(sn); if (nt == null) break;
          if (tt != null) nt = nt.MakeGenericType(tt); if (nt == null) break;
          line = s; Map(name, type = nt, 0, 0); continue;
        }
        if (line.StartsWith('*'))
        {
          if (!__isprimitiv(type)) { Error(0208, "Cannot take the address of, get the size of, or declare a pointer to a managed type ('{1}')", token, type); return null; }
          line.Take(); type = type.MakePointerType(); continue;
        }
        if (line.Take('?'))
        {
          if (!type.IsValueType) { Error(0453, "The type '{1}' must be a non-nullable value type in order to use it as parameter 'T' in the generic type or method 'System.Nullable<T>", token, type); return null; }
          type = typeof(Nullable<>).MakeGenericType(type);
        }
        break;
      }
      block = line; return type;
    }
    Type GetType(string ns, Token s, Type[] pp, bool at)
    {
      var tt = GetTypes(); var sl = s.Length; Type type = null; int last = 0;
      for (int i = 0; i < tt.Length; i++)
      {
        var t = tt[i]; var name = t.Name; if (!s.StartsWith(name)) continue;
        if (ns != null && t.Namespace != ns) continue;
        if (t.IsGenericType ^ (pp != null)) continue;
        if (at)
        {
          if (!t.IsSubclassOf(typeof(Attribute))) continue;
          if (!(name.Length == sl || (name.Length == sl + 9 && name.EndsWith("Attribute")))) continue;
        }
        else
        {
          if (pp == null)
          {
            if (name.Length != sl) continue;
          }
          else
          {
            if (name.Length != sl + 2) continue;
            if (name[sl] != '`') continue;
            if (name[sl + 1] != '0' + pp.Length) continue;
          }
        }
        if (ns != null) { type = t; break; }
        var x = usings.IndexOf(t.Namespace); if (x < last) continue; last = x; type = t; reguse(t.Namespace);
        //if (ns == null ? !usings.Contains(t.Namespace) : t.Namespace != ns) continue;
        //if (type != null) { Error(0104, "'{0}' is an ambiguous reference between '{1}' and '{2}'", s, type.FullName, t.FullName); break; }
        //type = t; if (ns != null) break;
      }
      if (type != null && pp != null) type = type.MakeGenericType(pp); return type;
    }
    Type ParseNumber(Token t, Type wt)
    {
      var hex = t.Length > 1 && t[0] == '0' && (t[1] | 0x20) == 'x'; var a = t; if (hex) t = t.SubToken(2, t.Length - 2);
      var la = t[t.Length - 1] | 0x20; var wtnum = wt != null && __isnumeric(wt);
      var t1 = hex ? TypeCode.Int32 : la == 'f' ? TypeCode.Single : la == 'd' ? TypeCode.Double : la == 'm' ? TypeCode.Decimal : TypeCode.Int32;
      var ts = t1 != TypeCode.Int32 ? t.SubToken(0, t.Length - 1) : t;
      if (t1 == TypeCode.Int32 && !hex && (t.Contains('.') || t.Contains('e') || t.Contains('E'))) t1 = TypeCode.Double;
      if (t1 == TypeCode.Int32 && wtnum) t1 = Type.GetTypeCode(wt);
      var ss = ts.ToString(); var ci = CultureInfo.InvariantCulture; var ns = hex ? NumberStyles.AllowHexSpecifier : NumberStyles.AllowLeadingSign;
      switch (t1)
      {
        case TypeCode.SByte:
          {
            sbyte v; if (!sbyte.TryParse(ss, ns, ci, out v)) goto m_byte;
            if (hex && v < 0 && wtnum && mb.Check != 1) Error(0221, "Constant value '{0}' cannot be converted to a '{1}' (use 'unchecked' syntax to override)", a, wt);
            if (wt != null) mb.Ldc_I4(v); return typeof(sbyte);
          }
        case TypeCode.Byte:
        m_byte:
          {
            byte v; if (!byte.TryParse(ss, ns, ci, out v)) goto m_short;
            if (wt != null) mb.Ldc_I4(v); return typeof(byte);
          }
        case TypeCode.Int16:
        m_short:
          {
            short v; if (!short.TryParse(ss, ns, ci, out v)) goto m_ushort;
            if (hex && v < 0 && wtnum && mb.Check != 1) Error(0221, "Constant value '{0}' cannot be converted to a '{1}' (use 'unchecked' syntax to override)", a, wt);
            if (wt != null) mb.Ldc_I4(v); return typeof(short);
          }
        case TypeCode.UInt16:
        m_ushort:
          {
            ushort v; if (!ushort.TryParse(ss, ns, ci, out v)) goto m_int;
            if (wt != null) mb.Ldc_I4(v); return typeof(ushort);
          }
        case TypeCode.Int32:
        m_int:
          {
            int v; if (!int.TryParse(ss, ns, ci, out v)) goto m_uint;
            if (hex && v < 0) { if (!wtnum) goto m_uint; if (mb.Check != 1) Error(0221, "Constant value '{0}' cannot be converted to a '{1}' (use 'unchecked' syntax to override)", a, wt); }
            if (wt != null) { mb.Ldc_I4(v); if (wt.IsEnum && v == 0) return wt; }
            return typeof(int);
          }
        case TypeCode.UInt32:
        m_uint:
          {
            uint v; if (!uint.TryParse(ss, ns, ci, out v)) goto m_long;
            if (wt != null) mb.Ldc_I4(unchecked((int)v)); return typeof(uint);
          }
        case TypeCode.Int64:
        m_long:
          {
            long v; if (!long.TryParse(ss, ns, ci, out v)) goto m_ulong;
            if (hex && v < 0) { if (!wtnum) goto m_ulong; if (mb.Check != 1) Error(0221, "Constant value '{0}' cannot be converted to a '{1}' (use 'unchecked' syntax to override)", a, wt); }
            if (wt != null) mb.Ldc_I8(v); return typeof(long);
          }
        case TypeCode.UInt64:
        m_ulong:
          {
            ulong v; if (!ulong.TryParse(ss, ns, ci, out v)) { if (a.Length == 1) Error(1056, "Unexpected character '{0}'", a); else Error(1021, "Integral constant is too large", a); return null; }
            if (wt != null) mb.Ldc_I8(unchecked((long)v)); return typeof(ulong);
          }
        case TypeCode.Single:
          {
            float v; if (!float.TryParse(ss, ns | NumberStyles.Float, ci, out v)) { Error(0594, "Floating-point constant is outside the range of type '{1}'", a, typeof(float)); return null; }
            if (wt != null) mb.Ldc_R4(v); return typeof(float);
          }
        case TypeCode.Double:
          {
            double v; if (!double.TryParse(ss, ns | NumberStyles.Float, ci, out v)) { Error(0594, "Floating-point constant is outside the range of type '{1}'", a, typeof(double)); return null; }
            if (wt != null) mb.Ldc_R8(v); return typeof(double);
          }
        case TypeCode.Decimal:
          {
            decimal v; if (!decimal.TryParse(ss, ns | NumberStyles.Float, ci, out v)) { Error(0594, "Floating-point constant is outside the range of type '{1}'", a, typeof(decimal)); return null; }
            if (wt != null)
            {
              var l = decimal.GetBits(v);
              mb.Ldc_I4(l[0]); mb.Ldc_I4(l[1]); mb.Ldc_I4(l[2]); mb.Ldc_I4((l[3] & 0x80000000) != 0 ? 1 : 0); mb.Ldc_I4(l[3] >> 16);
              mb.Newobj(typeof(decimal).GetConstructor(new Type[] { typeof(int), typeof(int), typeof(int), typeof(bool), typeof(byte) }));
            }
            return typeof(decimal);
          }
      }
      Error(0031, "Constant value '{0}' cannot be converted to a '{1}'", a, wt); return null;
    }
    void ParseBlock(Block b, bool outer)
    {
      var t2 = blocks.Count;
      MakeBlocks(b, outer); ParseBlock(t2);
      blocks.RemoveRange(t2, blocks.Count - t2);
    }
    bool iskeyword(Token t)
    {
      for (int i = 0; i < keywords.Length; i++) if (t.Equals(keywords[i])) return true; return false;
    }
    bool isonstack(Token t, int x = 0)
    {
      for (int i = stack.Count - 1; i >= x; i--)
        if (t.Equals(tokens[stack[i].a])) return true;
      return false;
    }
    void label(Token s, int fl)
    {
      int t = marks.Count - 1, k = 0; for (; t >= 0 && !tokens[k = marks[t].a].Equals(s); t--) ;
      var l = t >= 0 ? marks[t].b : mb.DefineLabel(); var id = t >= 0 ? vmap[k] >> 8 : nextid++;
      var x = Map(s, "label", fl | (id << 8), l); if (t == -1) { marks.Add(x); k = x.a; }
      if ((fl & 0x80) == 0) { vmap[k] |= 0x10; mb.Br(l); return; }
      if ((vmap[k] & 0x20) != 0) { Error(140, "The label '{0}' is a duplicate", s); return; }
      vmap[k] |= 0x20; mb.MarkLabel(l);
    }
    void endlabels()
    {
      for (int i = 0; i < marks.Count; i++)
      {
        var k = marks[i].a;
        if ((vmap[k] & 0x20) == 0) { Error(0159, "No such label '{0}' within the scope of the goto statement", tokens[k]); continue; }
        if ((vmap[k] & 0x10) == 0) { Warning(0164, "This label has not been referenced", tokens[k]); continue; }
      }
      marks.Clear();
    }
    void ParseBlock(int i, bool stat = false)
    {
      var t1 = stack.Count; var t2 = elsestack.Count;
      for (; i < blocks.Count; i++)
      {
        var b = blocks[i];
        if (b.Take("else")) { if (t2 == elsestack.Count) Error(1003, "Syntax error, '{1}' expected", b[-1].Start(), "if"); }
        else while (elsestack.Count > t2) mb.MarkLabel(elsestack.Pop());
        if (b.Take("catch") || b.Take("finally")) { Error(1003, "Syntax error, '{1}' expected", b[-1].Start(), "try"); continue; }
        if (b.Take("case") || b.Take("default")) { Error(1003, "Syntax error, '{1}' expected", b[-1].Start(), "switch"); continue; }
        if (b.Length >= 1 && b[1].Equals(':'))
        {
          while (b.Length >= 1 && b[1].Equals(':')) { var s = b.Take(); b.Take(); if (checkname(s, nglob, 3)) label(s, 0x82); }
          retok = end = false; if (b.Length == 0) continue;
        }
        if (end) { end = false; Warning(0162, "Unreachable code detected", b[0]); }
        if (b.StartsWith("goto"))
        {
          Break(b); b.Take(); var s = b.Take(); if (b.Length != 0) Error(1002, "; expected", b[0].Start());
          if (checkname(s, nglob, 3)) label(s, 0x02); end = true; continue;
        }
        retok = false;
        if (b[0].Equals('{')) { ParseBlockOut(b); continue; }
        if (b.Take("checked") || b.Take("unchecked"))
        {
          if (!b.StartsWith('{')) { Error(1514, "{1} expected", b[-1].End(), '{'); continue; }
          var o = mb.Check; mb.Check = b[-1].Equals("checked") ? 2 : 1; ParseBlockOut(b); mb.Check = o; continue;
        }
        if (b.Take("try"))
        {
          if (!b.StartsWith('{')) { Error(1514, "{1} expected", b[-1].End(), '{'); continue; }
          var t5 = @try; @try = mb.BeginTry(); var t10 = @return; var t11 = @break; var t12 = @continue; @return |= 1; @break |= 1; @continue |= 1;
          ParseBlockOut(b); end = false; int ff = 0; List<Type> excepts = new List<Type>();
          for (; i + 1 < blocks.Count; i++)
          {
            b = blocks[i + 1];
            if (b.Take("catch"))
            {
              ff |= 2; var t3 = stack.Count; var et = typeof(Exception); var sn = b[0].Start(); var use = 0x20;
              if (b.StartsWith('('))
              {
                var c = b.Next().Trim(); var pa = c.Param(); if (c.Length != 0) Error(1514, "{1} expected", c[-1].Start(), ')'); use = 0;
                var s = pa[0]; et = ParseType(ref pa); if (et == null) continue; sn = pa; s = s.SubToken(0, pa[-1].End().Position - s.Position);
                if (!typeof(Exception).IsAssignableFrom(et)) Error(00155, "The type caught or thrown must be derived from System.Exception", s);
                for (int x = 0; x < excepts.Count; x++) if (excepts[x].IsAssignableFrom(et)) { Error(0160, "A previous catch clause already catches all exceptions of this or of a super type ('{1}')", s, typeof(Exception)); break; }
              }
              if (sn.Length != 0) checkname(sn, t1, 2);
              if (!b.StartsWith('{')) { Error(1514, "{1} expected", b[-1].End(), '{'); continue; }
              excepts.Add(et);
              var t4 = @throw; @throw = mb.GetLocal(et); var id = nextid++; stack.Add(Map(sn, et, 0x84 | use | (id << 8), @throw));
              mb.Catch(@try, et); mb.Stloc(@throw); assig(id, @throw); ParseBlockOut(b); end = false;
              unstack(t3); @throw = t4; continue;
            }
            if (b.Take("finally"))
            {
              ff |= 1; if (!b.StartsWith('{')) { Error(1514, "{1} expected", b[-1].End(), '{'); continue; }
              @return = @break = @continue = -1;
              mb.Finally(@try); ParseBlockOut(b); i++; break;
            }
            break;
          }
          mb.EndTry(@try); @try = t5; @return = t10; @break = t11; @continue = t12;
          if (ff == 0) Error(1524, "Expected catch or finally", b.Last().End());
          continue;
        }
        if (b.StartsWith("throw"))
        {
          Break(b); var c = b.Take();
          if (b.Length == 0)
          {
            if (@throw == -1) { Error(0156, "A throw statement with no arguments is not allowed outside of a catch clause", c); continue; }
            mb.Ldloc(@throw); mb.Throw(); continue;
          }
          var et = Parse(b, typeof(Exception)); if (et == null) continue;
          if (!typeof(Exception).IsAssignableFrom(et)) { Error(0155, "The type caught or thrown must be derived from System.Exception", b); continue; }
          mb.Throw(); continue;
        }
        if (b.Take("if"))
        {
          var bc = b.Next(); Break(bc.SubBlock(-1, bc.Length + 1)); if (!bc[0].Equals('(')) { Error(1003, "Syntax error, '{1}' expected", bc[0].Start(), '('); continue; }
          bc = bc.Trim();
          var l1 = mb.DefineLabel(); ParseStrong(bc, typeof(bool)); mb.Brfalse(l1);
          ParseBlock(b, false); end = false;
          if (i + 1 < blocks.Count && blocks[i + 1].StartsWith("else")) { var l2 = mb.DefineLabel(); mb.Br(l2); elsestack.Push(l2); }
          mb.MarkLabel(l1);
          continue;
        }
        if (b.Take("while"))
        {
          var bc = b.Next(); var bb = bc.SubBlock(-1, bc.Length + 1); if (!bc[0].Equals('(')) { Error(1003, "Syntax error, '{1}' expected", bc[0].Start(), '('); continue; }
          bc = bc.Trim();
          var t3 = @continue; var t4 = @break; @continue = mb.DefineLabel() << 1; @break = mb.DefineLabel() << 1;
          mb.MarkLabel(@continue >> 1); Break(bb); ParseStrong(bc, typeof(bool)); mb.Brfalse(@break >> 1);
          ParseBlock(b, false); mb.Br(@continue >> 1); mb.MarkLabel(@break >> 1); @continue = t3; @break = t4; continue;
        }
        if (b.Take("for"))
        {
          var bc = b.Next(); if (!bc[0].Equals('(')) { Error(1003, "Syntax error, '{1}' expected", bc[0].Start(), '('); continue; }
          bc = bc.Trim(); var nc = bc.ParamCount(';');
          var ba = bc.Param(';'); if (nc <= 1) { Error(1002, "; expected", bc[0].Start()); }
          var bb = bc.Param(';'); if (nc <= 2) { Error(1002, "; expected", bc[0].Start()); }
          if (nc > 3) { Error(1026, "{1} expected", bc.Param(';').Last().End(), ')'); }
          var t5 = stack.Count;
          if (ba.Length != 0) if (!ParseVars(ba, false)) for (int nx = ba.ParamCount(), x = 0; x < nx; x++) ParseStat(ba.Param());
          var t3 = @continue; var t4 = @break; @continue = mb.DefineLabel() << 1; @break = mb.DefineLabel() << 1; var next = mb.DefineLabel();
          mb.MarkLabel(next); if (bb.Length != 0) { Break(bb); ParseStrong(bb, typeof(bool)); mb.Brfalse(@break >> 1); }
          ParseBlock(b, false);
          mb.MarkLabel(@continue >> 1);
          if (bc.Length != 0) for (int nx = bc.ParamCount(), x = 0; x < nx; x++) ParseStat(bc.Param());
          mb.Br(next); mb.MarkLabel(@break >> 1);
          unstack(t5); @continue = t3; @break = t4; continue;
        }
        if (b.Take("using"))
        {
          var bc = b.Next(); if (!bc[0].Equals('(')) { Error(1003, "Syntax error, '{1}' expected", bc[0].Start(), '('); continue; }
          bc = bc.Trim();
          int np = bc.ParamCount(); if (np == 0) { Error(1001, "Identifier expected", bc); continue; }
          var t3 = stack.Count; if (!ParseVars(bc, false)) for (int x = 0; x < np; x++) ParseStat(bc.Param()); //if (stack.Count - t2 != np) ... 
          var t4 = @try; @try = mb.BeginTry(); var t10 = @return; var t11 = @break; var t12 = @continue; @return |= 1; @break |= 1; @continue |= 1;
          ParseBlock(b, false);
          mb.Finally(@try); //mb.Ldstr("finally"); mb.Call(typeof(Console).GetMethod("WriteLine", new Type[] { typeof(object) }));
          for (int x = t3; x < stack.Count; x++)
          {
            var y = stack[x].b; var ty = (Type)pmap[stack[x].a]; vmap[stack[x].a] |= 0x20; //used
            if (!GetInterface(ty, typeof(IDisposable))) { Error(1674, "'{1}': type used in a using statement must be implicitly convertible to 'System.IDisposable'", blocks[i][0], ty); continue; }
            var im = ty.GetInterfaceMap(typeof(IDisposable));
            if (ty.IsValueType) { mb.Ldloca(y); /*mb.Constrained(ty);*/ mb.Callvirt(im.TargetMethods[0]); }
            else
            {
              var l1 = mb.DefineLabel(); mb.Ldloc(y); mb.Brfalse(l1); //var l1 = mb.DefineLabel(); mb.Ldloc(y); mb.Ldnull(); mb.Ceq(); mb.Brtrue(l1);
              mb.Ldloc(y); mb.Callvirt(im.TargetMethods[0]); mb.MarkLabel(l1);
            }
          }
          mb.EndTry(@try); @try = t4; @return = t10; @break = t11; @continue = t12; unstack(t3); continue;
        }
        if (b.Take("lock"))
        {
          var bc = b.Next(); if (!bc[0].Equals('(')) { Error(1003, "Syntax error, '{1}' expected", bc[0].Start(), '('); continue; }
          bc = bc.Trim();
          int np = bc.ParamCount(); if (np == 0) { Error(1001, "Identifier expected", bc); continue; }
          if (np != 1) { Error(1003, "Syntax error, '{1}' expected", bc.Param().Last().End(), ')'); continue; }
          Break(bc); var tv = Parse(bc, typeof(object)); if (tv == null) continue;
          if (tv.IsValueType) Error(0185, "'{1}' is not a reference type as required by the lock statement", bc, tv);
          tv = Cast(bc, tv, typeof(object)); if (tv == null) continue;
          var l1 = mb.GetLocal(tv); var l2 = mb.GetLocal(typeof(bool)); mb.Stloc(l1); mb.Ldc_I4(0); mb.Stloc(l2);
          var t4 = @try; @try = mb.BeginTry(); var t10 = @return; var t11 = @break; var t12 = @continue; @return |= 1; @break |= 1; @continue |= 1;
          mb.Ldloc(l1); mb.Ldloca(l2);
          mb.Callx(typeof(System.Threading.Monitor).GetMethod("Enter", new Type[] { typeof(object), typeof(bool).MakeByRefType() }));
          ParseBlock(b, false);
          mb.Finally(@try);
          mb.Ldloc(l2); var m1 = mb.DefineLabel(); mb.Brfalse(m1); //mb.Ldloc(l2); mb.Ldc_I4(0); mb.Ceq(); var m1 = mb.DefineLabel(); mb.Brtrue(m1);
          mb.Ldloc(l1); mb.Callx(typeof(System.Threading.Monitor).GetMethod("Exit"));
          mb.MarkLabel(m1);
          mb.EndTry(@try); @try = t4; @return = t10; @break = t11; @continue = t12; mb.ReleaseLocal(l1); mb.ReleaseLocal(l2); continue;
        }
        if (b.Take("foreach"))
        {
          var bc = b.Next(); if (!bc[0].Equals('(')) { Error(1003, "Syntax error, '{1}' expected", bc[0].Start(), '('); continue; }
          bc = bc.Trim();
          var nc = bc.ParamCount(); if (nc == 0) { Error(1001, "Identifier expected", bc); continue; }
          if (nc != 1) { Error(1003, "Syntax error, '{1}' expected", bc.Param().Last().End(), ')'); continue; }
          int x = 0; for (; x < bc.Length && !bc[x].Equals("in"); x++) ; if (x == bc.Length) { Error(1515, "'{1}' expected", bc, "in"); continue; }
          var ba = bc.SubBlock(0, x); var bin = bc.SubBlock(x++, 1); bc = bc.SubBlock(x, bc.Length - x);
          var rt = Parse(bc, null); if (rt == null) continue;
          var en = rt.GetInterface("IEnumerable`1") ?? rt; var ge = en.GetMethod("GetEnumerator", Type.EmptyTypes);
          if (ge == null) { Error(1579, "foreach statement cannot operate on variables of type '{1}' because '{1}' does not contain a public definition for '{2}'", bc, rt, "GetEnumerator"); continue; }
          if (en.IsValueType) Error(0000, "todo", bc); //todo: en.IsValueType ? mb.Ldloca : mb.Ldloc;
          var et = ge.ReturnType; var cu = et.GetProperty("Current").GetGetMethod();
          var mn = (et.GetInterface("IEnumerator") ?? et).GetMethod("MoveNext");
          var di = (et.GetInterface("IDisposable") ?? et).GetMethod("Dispose");
          Break(bc); Parse(bc, rt); mb.Callx(ge); var l1 = mb.GetLocal(rt); mb.Stloc(l1);
          var t4 = @try; var t10 = @return; if (di != null) { @try = mb.BeginTry(); @return |= 1; }
          var t6 = @break; @break = mb.DefineLabel() << 1; var t7 = @continue; @continue = mb.DefineLabel() << 1;
          mb.MarkLabel(@continue >> 1); Break(bin);
          mb.Ldloc(l1); mb.Callx(mn); mb.Brfalse(@break >> 1); mb.Ldloc(l1); mb.Callx(cu);
          var t3 = stack.Count; ParseVars(ba, false, cu.ReturnType); if (t3 + 1 != stack.Count) { Error(0000, "todo", ba); continue; }
          var lo = stack[t3].b; var tp = stack[t3].a; assig(vmap[tp] >> 8, lo);
          Cast(ba, cu.ReturnType, (Type)pmap[tp]); mb.Stloc(lo);
          ParseBlock(b, false); end = false; mb.Br(@continue >> 1); mb.MarkLabel(@break >> 1);
          if (di != null) { mb.Finally(@try); mb.Ldloc(l1); mb.Callx(di); mb.EndTry(@try); }
          mb.ReleaseLocal(l1); @try = t4; @return = t10; @break = t6; @continue = t7; unstack(t3); continue;
        }
        if (b.Take("switch")) //todo: check switch optimization codesize?
        {
          var bc = b.Next(); if (!bc[0].Equals('(')) { Error(1003, "Syntax error, '{1}' expected", bc[-1].End(), '('); continue; }
          Break(bc.SubBlock(-1, bc.Length + 1)); bc = bc.Trim();
          var nc = bc.ParamCount(); if (nc == 0) { Error(1001, "Identifier expected", bc); continue; }
          if (nc != 1) { Error(1003, "Syntax error, '{1}' expected", bc.Param().Last().End(), ')'); continue; }
          var vt = Parse(bc, typeof(int)); if (vt == typeof(void)) { Error(1536, "Invalid parameter type '{0}'", bc[-1], vt); continue; }
          var tc = Type.GetTypeCode(vt); if (!((tc >= TypeCode.Boolean && tc <= TypeCode.UInt32) || tc == TypeCode.String)) { Error(0151, "A switch expression or case label must be a bool, char, string, integral, enum, or corresponding nullable type", bc[-1]); continue; }
          var cp = tc == TypeCode.String ? vt.GetMethod("op_Equality") : null;
          var l1 = mb.GetLocal(vt); mb.Stloc(l1);
          if (!b.StartsWith('{')) { Error(1003, "Syntax error, '{1}' expected", b[-1].End(), '{'); continue; }
          b = b.Trim();
          if (b.Length == 0) Warning(1522, "Empty switch block", b);
          var t6 = @break; @break = mb.DefineLabel() << 1; int next = mb.DefineLabel(), los = -1; var def = new Block();
          for (; b.Length != 0;)
          {
            var a = b; var u = b.Take("case") ? 1 : b.Take("default") ? 2 : 0; if (u == 0) { Error(1525, "Invalid expression term '{0}'", b[0]); break; }
            if (u == 1) { mb.Ldloc(l1); ParseStrong(b.Param(':'), vt); if (cp != null) mb.Callx(cp); else mb.Ceq(); }
            if (u == 2) { if (!b.Take(':')) Error(0000, "todo", b[0]); }
            var c = b; for (; c.Length != 0 && !(c.StartsWith("case") || c.StartsWith("default")); c.Next()) ;
            var bl = b.SubBlock(0, c.Position - b.Position); b = c;
            if (u == 2) { if (def.Length != 0) { Error(0152, "The label '{1}:' already occurs in this switch statement", a[0]); } def = bl; if (def.Length == 0) Error(0163, "Control cannot fall through from one case label ('{0}:') to another", a[0]); continue; }
            if (bl.Length == 0 && b.Length == 0) Error(0163, "Control cannot fall through from one case label ('{0}:') to another", a[0]);
            if (bl.Length == 0) { if (los == -1) los = mb.DefineLabel(); mb.Brtrue(los); continue; }
            mb.Brfalse(next); if (los != -1) { mb.MarkLabel(los); los = -1; }
            ParseBlock(bl, false); if (!end) Error(0163, "Control cannot fall through from one case label ('{0}:') to another", a[0]); end = false;
            //if (mb.reachable()) Error(0163, "Control cannot fall through from one case label ('{0}:') to another", a[0]);
            mb.MarkLabel(next); next = mb.DefineLabel();
          }
          if (los != -1) mb.MarkLabel(los);
          if (def.Length != 0) { ParseBlock(def, false); if (!end) Error(0163, "Control cannot fall through from one case label ('{0}:') to another", def[-2]); end = false; }
          mb.MarkLabel(@break >> 1); mb.MarkLabel(next);
          mb.ReleaseLocal(l1); @break = t6;
          continue;
        }
        if (b.StartsWith("continue"))
        {
          if (b.Length != 1) Error(1002, "; expected", b[1].Start());
          if (@return == -1) { Error(0157, "Control cannot leave the body of a finally clause", b); continue; }
          if (@continue == -1) { Error(0139, "No enclosing loop out of which to break or continue;", b); continue; }
          Break(b); if ((@continue & 1) == 1) mb.Leave(@continue >> 1); else mb.Br(@continue >> 1); end = true; continue;
        }
        if (b.StartsWith("break"))
        {
          if (b.Length != 1) Error(1002, "; expected", b[1].Start());
          if (@return == -1) { Error(0157, "Control cannot leave the body of a finally clause", b); continue; }
          if (@break == -1) { Error(0139, "No enclosing loop out of which to break or continue;", b); continue; }
          Break(b); if ((@break & 1) == 1) mb.Leave(@break >> 1); else mb.Br(@break >> 1); end = true; continue;
        }
        if (b.StartsWith("return"))
        {
          if (@return == -1) { Error(0157, "Control cannot leave the body of a finally clause", b); continue; }
          Break(b); b.Take();
          //if (rettype == null || rettype == typeof(__null)) { if (b.Length != 0) { rettype = Parse(b, null); retok = true; } else rettype = typeof(void); end = true; continue; }
          if (__isgen(rettype) || rettype == typeof(__null)) rettype = b.Length != 0 ? Parse(b, null) : typeof(void);
          if (rettype == typeof(void))
          {
            if (b.Length != 0) Error(0127, "Since '{1}' returns void, a return keyword must not be followed by an object expression", b, mb.Method);
            if ((@return & 1) == 1) mb.Leave(@return >> 1); else mb.Br(@return >> 1); end = true; continue;
          }
          if (b.Length == 0 || ParseStrong(b, rettype) == null) Error(0126, "An object of a type convertible to '{1}' is required", blocks[i][0], rettype);
          mb.Stloc(0); if ((@return & 1) == 1) mb.Leave(@return >> 1); else mb.Br(@return >> 1); retok = end = true; continue;
        }
        if (b.Take("const")) { __const(b, nglob); continue; }
        if (b.StartsWith("this") || b.StartsWith("base")) { ParseStat(b); continue; }
        var ä = stack.Count - 1; for (var s = b[0]; ä >= 0; ä--) if (s.Equals(tokens[stack[ä].a]) && (vmap[stack[ä].a] & 0xff) != 0x80) break;
        if (ä == -1) { if (ParseVars(b, stat)) { if (elsestack.Count > t2) Error(1023, "Embedded statement cannot be a declaration or labeled statement", b); continue; } }
        ParseStat(b);
      }
      while (elsestack.Count > t2) mb.MarkLabel(elsestack.Pop()); unstack(t1);
    }
    void __const(Block b, int v)
    {
      var t = ParseType(ref b); if (t == null) { Error(1031, "Type expected", b[0]); return; }
      if (b.Length == 0) { Error(1001, "Identifier expected", b[0].Start()); return; }
      for (int x = 0, n = b.ParamCount(); x < n; x++)
      {
        var c = b.Param(); if (c.Length == 0) { Error(1001, "Identifier expected", c[0].Start()); continue; }
        var name = c.Next(); if (!checkname(name, v, 0)) continue;
        if (!c.Take('=') || c.Length == 0) { Error(0145, "A const field requires a value to be provided", name); continue; }
        var id = nextid++; stack.Add(Map(name, t, 0x82 | (id << 8), c.Length));
        mb = mb.child ?? (mb.child = new StateMachine { parent = mb });
        mb.Begin(chkdm ?? (chkdm = new DynamicMethod(string.Empty, typeof(void), Type.EmptyTypes, typeof(object), true)));
        var oc = mb.OpCount; if (ParseStrong(c, t) == t && mb.OpCount != oc + 1) Error(0133, "The expression being assigned to '{1}' must be constant", c, name.ToString());
        mb.Reset(); mb = mb.parent;
      }
    }
    void unstack(int x)
    {
      for (int i = stack.Count - 1; i >= x; i--)
      {
        if ((vmap[stack[i].a] & 0xcf) != 0x84) continue;
        mb.ReleaseLocal(stack[i].b);
      }
      stack.RemoveRange(x, stack.Count - x);
    }
    int modifier(Token b)
    {
      if (b.Equals("public")) return 0x01;
      if (b.Equals("protected")) return 0x02;
      if (b.Equals("private")) return 0x04;
      if (b.Equals("internal")) return 0x08;
      if (b.Equals("readonly")) return 0x10;
      if (b.Equals("static")) return 0x20;
      //if (b.Equals("const")) return 0x40;
      return 0;
    }
    int modifier(ref Block b)
    {
      int f = 0;
      for (; b.Length != 0; b.Take())
      {
        var t = modifier(b[0]); if (t == 0) break;
        if ((f & t) != 0) Error(1004, "Duplicate '{0}' modifier", b[0]); f |= t;
        if ((t & ~(0x01)) != 0) Warning(0000, "Currently unsupported", b[0]);
      }
      return f;
    }
    void ParseBlockOut(Block b)
    {
      Break(b.SubBlock(0, 1)); var e = b.SubBlock(b.Length - 1, 1);
      var c = b.Next().Trim(); ParseBlock(c, true); if (!end) Break(e);
    }
    static bool GetInterface(Type t, Type x)
    {
      var a = t.GetInterfaces(); for (int i = 0; i < a.Length; i++) if (a[i] == x) return true; return false;
    }
    void ParseStat(Block b)
    {
      Break(b); var t = Parse(b, typeof(void)); if (t == null || t == typeof(void)) return;
      mb.Pop(); if (b.StartsWith("new") || b.StartsWith("stackalloc")) return;
      Error(0201, "Only assignment, call, increment, decrement, and new object expressions can be used as a statement", b);
    }
    bool ParseVars(Block b, bool isstatic, Type rt = null)
    {
      if (b.Take("var"))
      {
        if (__mask(b)) return true; Break(b); var name = b.Take(); checkname(name, nglob, 0); //if (!checkname(name, nglob, 0)) return true;
        var t = rt; if (t == null && !b.Take('=')) { Error(1003, "Syntax error, '{1}' expected", b[0].Start(), '='); return true; }
        if (t == null) { t = Parse(b, null); if (t == null) return true; if (t == typeof(void)) { Error(0815, "Cannot assign void to an implicitly-typed local variable", name); return true; } }
        int v = mb.GetLocal(t); var id = nextid++; stack.Add(Map(name, t, 0x84 | (id << 8), v)); if (rt != null) return true;
        ParseStrong(b, t); mb.Stloc(v); assig(id, v); return true;
      }
      var a = b.TakeType(); if (a.Length == 0) return false;
      var type = ParseType(a); if (type == null) return true; //todo: check, true ? 
      for (int k = 0, np = b.ParamCount(); k < np; k++)
      {
        a = b.Param(); if (isstatic) { if (a.Length > 1) ParseStat(a); continue; }
        if (__mask(a)) continue; var d = a; var name = a.Take();
        if (!checkname(name, nglob, 0)) continue;
        int v = mb.GetLocal(type); var id = nextid++; stack.Add(Map(name, type, 0x84 | (id << 8), v));// if (c) constids.Add(id); 
        if (rt != null) { Break(d); if (a.Length != 0) Error(1525, "Invalid expression term '{0}'", a); return true; }
        if (a.Length == 0) { /*if (c) Error(0145, "A const field requires a value to be provided", d);*/ continue; }
        Break(d);
        var g = a.Take(); if (!g.Equals('=')) { Error(1525, "Invalid expression term '{0}'", g); continue; }
        ParseStrong(a, type); mb.Stloc(v); assig(id, v);
      }
      return true;
    }
    internal class RepInfo { internal Type type; internal int index, id; }
    bool __mask(Block b)
    {
      if (mb.reptype == null) return false; var a = b; var s = b.Take();

      var x = __tokenmap(s.Position);
      var v = vmap[x]; if ((v & 0x40) == 0) return false;
      var r = (RepInfo)pmap[x]; stack.Add(new ab { a = x, b = r.index });
      if (b.Length == 0) return true; Break(a); b.Take();
      mb.Ldloc(mb.reploc); mb.Ldc_I4(r.index & 0xffff);
      if (r.type.IsValueType) { mb.Ldelem(typeof(object)); mb.Ldc_I4(r.index >> 16); }
      ParseStrong(b, r.type); mb.Stelem(r.type); return true;
    }
    Type ParseLeft(ref Block b, Type wt, int fl)
    {
      var tok = b[0]; var org = b;
      if (tok.Equals('('))
      {
        var a = b.Next().Trim(); if (b.Length == 0) return Parse(a, wt);
        if (b.StartsWith('.') || b.StartsWith('[')) return Parse(a, wt != null ? Parse(a, null) : null);
        var c = b; b = b.SubBlock(b.Length, 0);
        var tb = ParseType(a); if (tb == null) return null; if (wt == null) return tb;
        var ta = Parse(c, tb); if (ta == null) return null; if (ta == tb) return tb;
        return Cast(c, ta, tb, true);
      }
      if (tok.IsString) { b.Take(); if (wt != null) mb.Ldstr(tok.GetString()); return typeof(string); }
      if (tok.IsChar) { b.Take(); if (wt != null) mb.Ldc_I4(tok.GetChar()); return typeof(char); }
      if (!tok.IsWord)
      {
        b.Take(); if (tok[0] == '<') { if (wt != null) { var s = tok.ToString(); mb.Ldstr(s); Map(tok, s, 0, 0); } return typeof(string); }; //inline xml
        return ParseNumber(tok, wt);
      }
      if (b.Take("true")) { if (wt != null) mb.Ldc_I4(1); return typeof(bool); }
      if (b.Take("false")) { if (wt != null) mb.Ldc_I4(0); return typeof(bool); }
      if (b.Take("new"))
      {
        var s = b[0]; var t = ParseType(ref b, 2);
        if (t == null) { Error(1031, "Type expected", b[0].Start()); return null; }
        s = s.SubToken(0, b[-1].End().Position - s.Position);
        if (t.IsArray && !b.StartsWith('{')) { Error(1586, "Array creation must have array size or array initializer", org); return null; }
        if (b.StartsWith('['))
        {
          t = t.MakeArrayType(); var a = b.Next();
          while (b.StartsWith('['))
          {
            var u = b.Next().Trim(); if (u.Length != 0) { Error(0178, "Invalid rank specifier: expected ',' or ']'", u); return null; }
            t = t.MakeArrayType();
          }
          if (wt == null) return t;
          a = a.Trim(); var np = a.ParamCount(); if (np != 1) { Error(0000, "todo", a); return null; }
          if (b.Length != 0) Error(1002, "; expected", b[0].Start());
          ParseStrong(a, typeof(int)); mb.Newarr(t.GetElementType()); return t;
        }
        if (b.StartsWith('('))
        {
          var a = b.Next(); var c = b.StartsWith('{') ? b.Next() : new Block(); if (wt == null) return t;
          a = a.Trim(); t = newcall(s, t, a); if (t == null || c.Length == 0) return t;
          var ic = t.GetInterface("ICollection`1"); if (ic == null) { Error(1922, "Cannot initialize type '{1}' with a collection initializer because it does not implement '{2}'", c, t, typeof(System.Collections.ICollection)); return null; }
          c = c.Trim(); var nc = c.ParamCount(); if (nc != 0 && c[c.Length - 1].Equals(',')) nc--; //MS compatibility
          var xt = ic.GetGenericArguments()[0]; var me = t.GetMethod("Add", new Type[] { xt });
          for (int i = 0; i < nc; i++) { mb.Dup(); ParseStrong(c.Param(), xt); mb.Callx(me); }
          return t;
        }
        if (b.StartsWith('{'))
        {
          var a = b.Next().Trim(); if (wt == null) return t; var np = a.ParamCount();
          if (np != 0 && a[a.Length - 1].Equals(',')) np--; //MS compatibility
          if (t.IsArray)
          {
            var et = t.GetElementType(); mb.Ldc_I4(np); mb.Newarr(et);
            for (int i = 0; i < np; i++) { mb.Dup(); mb.Ldc_I4(i); ParseStrong(a.Param(), et); mb.Stelem(et); }
            return t; //todo: meta?
          }
          var v = -1;
          if (t.IsValueType) { v = mb.GetLocal(t); mb.Ldloca(v); mb.Dup(); mb.Initobj(t); }// mb.Ldloc(v);  }  
          else
          {
            var ci = t.GetConstructor(Type.EmptyTypes); if (ci == null) { Error(0122, "'{1}()' is inaccessible due to its protection level", s, t); return t; }
            mb.Newobj(ci);
          }
          var ic = t.GetInterface("ICollection`1");
          if (ic != null)
          {
            var xt = ic.GetGenericArguments()[0]; var me = t.GetMethod("Add", new Type[] { xt });
            for (int i = 0; i < np; i++) { mb.Dup(); ParseStrong(a.Param(), xt); mb.Callx(me); }
            return t;
          }
          for (int i = 0; i < np; i++)
          {
            var p = a.Param(); var sn = p.Next(); if (!p.Take('=')) { Error(1003, "Syntax error, '{1}' expected", p[0].Start(), '='); continue; }
            var ss = sn.ToString(); var bind = BindingFlags.Public | BindingFlags.Instance; if (@this.IsOrIsSubclassOf(t)) bind |= BindingFlags.NonPublic;
            var pi = t.GetProperty(ss, bind); if (pi != null && pi.CanWrite) { Map(sn, pi, shareid(pi, 3), 0); mb.Dup(); ParseStrong(p, pi.PropertyType); mb.Callx(pi.GetSetMethod(true)); continue; }
            var fi = t.GetField(ss, bind); if (fi != null) { Map(sn, fi, shareid(fi, 3) & ~0xf0, 0); mb.Dup(); ParseStrong(p, fi.FieldType); mb.Stfld(fi); continue; }
            Error(0117, "'{1}' does not contain a definition for '{0}'", sn, t);
          }
          if (v != -1) { mb.Ldobj(t); mb.ReleaseLocal(v); }
          return t;
        }
        Error(1526, "A new expression requires (), [], or {{}} after type", b[-1].End()); return null;
      }
      if (b.Take("typeof"))
      {
        if (!b.StartsWith('(')) { Error(1003, "Syntax error, '{1}' expected", b[0].Start(), '('); return null; }
        var c = b.Next(); if (wt != null) { c = c.Trim(); var t = ParseType(c); if (t == null) return null; mb.Ldtoken(t); mb.Call(typeof(Type).GetMethod("GetTypeFromHandle")); }
        return typeof(Type);
      }
      if (b.Take("sizeof"))
      {
        if (!b.StartsWith('(')) { Error(1003, "Syntax error, '{1}' expected", b[0].Start(), '('); return null; }
        var c = b.Next();
        if (wt == null) return typeof(int); c = c.Trim(); var t = ParseType(c); if (t == null) return null;
        if (!(t.IsValueType || t.IsPointer)) { Error(0208, "Cannot take the address of, get the size of, or declare a pointer to a managed type ('{1}')", c, t); return null; }
        //Error(0208, "Cannot take the address of, get the size of, or declare a pointer to a managed type ('{1}')", c, t); 
        //var tc = Type.GetTypeCode(t); if (tc < TypeCode.Boolean || tc > TypeCode.Decimal) { Error(0233, "'{1}' does not have a predefined size, therefore sizeof can only be used in an unsafe context (consider using System.Runtime.InteropServices.Marshal.SizeOf)", c, t); }
        mb.Sizeof(t); return typeof(int);
      }
      if (b.Take("default"))
      {
        if (!b.StartsWith('(')) { Error(1003, "Syntax error, '{1}' expected", b[0].Start(), '('); return null; }
        var c = b.Next().Trim(); var t = ParseType(c); if (t == null) return null;
        if (wt != null) ldconst(null, t, c); return t;
      }
      if (b.Take("stackalloc"))
      {
        var t = ParseType(ref b); if (t == null) { Error(1031, "Type expected", b[0].Start()); return null; }
        if (!__isprimitiv(t)) { Error(0208, "Cannot take the address of, get the size of, or declare a pointer to a managed type ('{1}')", b[-1], t); return null; }
        if (!b.StartsWith('[')) { Error(1575, "A stackalloc expression requires [] after type", b[0].Start()); return null; }
        var a = b.Next(); a = a.Trim(); var np = a.ParamCount(); if (np != 1) { Error(0000, "todo", a); return null; }
        if (b.Length != 0) Error(1002, "; expected", b[0].Start());
        var pt = t.MakePointerType(); if (wt == null) return pt; if (mb.Check != 1) mb.Method.InitLocals = true; //the MS way to make sure... own solution for fast realtime: unchecked(stackalloc ...)
        ParseStrong(a, typeof(int)); mb.Sizeof(t); mb.Mul(); mb.Localloc(); return pt;
      }
      if (b.Take("checked") || b.Take("unchecked"))
      {
        if (!b.StartsWith('(')) { Error(1003, "Syntax error, '{1}' expected", b[0].Start(), '('); return null; }
        var o = mb.Check; mb.Check = b[-1].Equals("checked") ? 2 : 1; var t = Parse(b.Next(), wt, fl); mb.Check = o; return t;
      }
      if (b.Take("base")) { Map(tok, @this, 3, 0); if (wt != null) mb.Ldthis(); return @this; }

      var nsa = stack.Count;
      if (b.Take("this"))
      {
        Map(tok, @this, 3, 0);
        if (b[0].Equals('.'))
        {
          var nt = b[1];
          for (int i = nglob - 1; i >= 0; i--)
          {
            if (!nt.Equals(tokens[stack[i].a])) continue;
            Map(b[0], @this, 0x01, 0); tok = nt; nsa = i + 1; b.Take(); break;
          }
        }
        if (nsa == stack.Count) { if (wt != null) mb.Ldthis(); return @this; }
      }

      for (int i = nsa - 1; i >= 0; i--)
      {
        var ind = stack[i].a; var tps = tokens[ind];
        if (!tok.Equals(tps) && !(tps.Equals('{') && tok.Equals("value"))) continue;
        var tpv = vmap[ind]; if ((tpv & 0xff) == 0x80) break; var tpp = pmap[ind];
        var im = Map(tok, tpp, tok.Position != tps.Position ? tpv & ~0x80 : tpv, 0); b.Take(); var ke = tpv & 0x0f;
        if (ke == 3)
        {
          if ((tpv & 0xff) == 0xc3)
          {
            var acc = (DynamicMethod[])tpp;
            if ((fl & 1) != 0 && b.Length == 0)
            {
              if (acc[1] == null) { Error(0200, "Property or indexer '{0}' cannot be assigned to -- it is read only", tok); return null; }
              if ((fl & 2) != 0 && acc[0] == null) { Error(0154, "The property or indexer '{0}' cannot be used in this context because it lacks the get accessor", tok); return null; }
              mb.Ldthis(); if ((fl & 2) != 0) { mb.Dup(); mb.Call(acc[0]); }
              accessor = __Call(acc[1]); return TypeHelper.GetParametersNoCopy(acc[1])[1].ParameterType;
            }
            if (acc[0] == null) { Error(0154, "The property or indexer '{0}' cannot be used in this context because it lacks the get accessor", tok); return null; }
            if (wt != null) { mb.Ldthis(); mb.Call(acc[0]); }
            return acc[0].ReturnType;
          }
          var dm = (DynamicMethod)tpp;
          if (b.Length == 0)
          {
            if (wt == null) return GetDelegateType(dm); //Dynamic.GetDelegateType(dm);
            if (!wt.IsSubclassOf(typeof(Delegate))) { Error(1503, "cannot convert from 'method group' to '{1}'", tok, wt); return null; }
            var mi = wt.GetMethod("Invoke"); var aa = TypeHelper.GetParametersNoCopy(dm); var bb = TypeHelper.GetParametersNoCopy(mi);
            int x = -1; if (aa.Length - 1 == bb.Length) for (x = 0; x < bb.Length; x++)
              {
                var t1 = aa[1 + x].ParameterType; var t2 = bb[x].ParameterType;
                if (!t2.IsOrIsSubclassOf(t1)) break;
              }
            if (x != bb.Length) { Error(1503, "cannot convert from 'method group' to '{1}'", tok, wt); return null; }
            mb.Ldthis(); mb.Ldftn(dm); mb.Newobj(wt.GetConstructors()[0]); return wt;
          }
          if (!b.StartsWith('(')) { Error(1003, "Syntax error, '{1}' expected", b[0].Start(), '('); return null; }
          var c = b.Next(); if (wt == null) return dm.ReturnType; c = c.Trim();
          var np = c.ParamCount(); var tt = TypeHelper.GetParametersNoCopy(dm);
          if (1 + np != tt.Length) { if (nsa == stack.Count) { b = org; pmap[org.Position] = null; continue; } Error(1501, "No overload for method '{0}' takes {1} arguments", tok, np); return null; }
          mb.Ldthis();
          for (int t = 1; t < tt.Length; t++)
          {
            var pa = c.Param(); var pt = tt[t].ParameterType;
            if (pt.IsByRef)
            {
              var re = pa.Take("ref") ? 4 | 2 | 1 : pa.Take("out") ? 8 | 4 | 1 : 0;
              var px = Parse(pa, pt.GetElementType(), re); if (pt != px) Error(0206, "A property, indexer or dynamic member access may not be passed as an out or ref parameter", pa);
              continue;
            }
            ParseStrong(pa, pt);
          }
          mb.Call(dm); if (b.Length == 0 && wt == typeof(void) && dm.ReturnType != typeof(void)) { mb.Pop(); return typeof(void); }
          return dm.ReturnType;
        }
        var type = tpp as Type; if (type == null) type = ((RepInfo)tpp).type; if (wt == null) return type.IsByRef ? type.GetElementType() : type;
        if (ke == 2) // "const"
        {
          if ((fl & 1) != 0 && b.Length == 0) { Error(0131, "The left-hand side of an assignment must be a variable, property or indexer", tok); return wt; }
          return Parse(new Block(ind + 2, stack[i].b), wt);
        }
        if (ke == 4)
        {
          var x = stack[i].b; var id = tpv >> 8;
          if ((fl == 0) || !((fl & 3) == 1 && b.Length == 0)) if ((tpv & 0x10) == 0) Error(0165, "Use of unassigned local variable '{0}'", tok);
          vmap[ind] |= 0x20;
          if ((tpv & 0x40) != 0) return __repaccess(b, fl, tpp, i);
          if (i < mb.scope) { var mo = mb; while (i < mo.scope) mo = mo.parent; if (!mo.replys.Contains(ind)) mo.replys.Add(ind); x = mb.GetLocal(type); mb.ReleaseLocal(x); } //dummy 
          if ((fl & 1) != 0 && b.Length == 0)
          {
            if ((fl & 0x10) != 0)
            {
              if (!__isprimitiv(type)) { Error(0208, "Cannot take the address of, get the size of, or declare a pointer to a managed type ('{1}')", tok, type); return null; }
              mb.Ldloca(x); return type.MakePointerType();
            }
            if ((fl & 4) != 0) { mb.Ldloca(x); if ((fl & 8) != 0) assig(id, x); return type.MakeByRefType(); }
            if ((fl & 2) != 0) mb.Ldloc(x); accessor = __Stloc(x, id); return type;
          }
          if (b.Length > 1 && type.IsValueType) { mb.Ldloca(x); return type.MakeByRefType(); }
          mb.Ldloc(x); return type;
        }
        if (ke == 5)
        {
          var x = stack[i].b;
          if ((tpv & 0x40) != 0) return __repaccess(b, fl, tpp, i);
          if (i < mb.scope) { var mo = mb; while (i < mo.scope) mo = mo.parent; if (mo.reptype == null) if (!mo.replys.Contains(ind)) mo.replys.Add(ind); x = 0; }
          if (type.IsByRef)
          {
            var et = type.GetElementType();
            if ((fl & 1) != 0 && b.Length == 0)
            {
              mb.Ldarg(x);
              if ((fl & 4) != 0) { return type; }
              if ((fl & 2) != 0) { mb.Dup(); mb.Ldobj(et); }
              accessor = __Stobj(et); return et;
            }
            mb.Ldarg(x); if (b.Length > 1 && et.IsValueType) return type;
            mb.Ldobj(et); return et; //todo: check, ldind.ref ? seems to be identical
          }
          if ((fl & 1) != 0 && b.Length == 0)
          {
            if ((fl & 0x10) != 0)
            {
              if (!__isprimitiv(type)) { Error(0208, "Cannot take the address of, get the size of, or declare a pointer to a managed type ('{1}')", tok, type); return null; }
              mb.Ldarga(x); return type.MakePointerType();
            }
            if ((fl & 4) != 0) { mb.Ldarga(x); return type.MakeByRefType(); }
            if ((fl & 2) != 0) mb.Ldarg(x); accessor = __Starg(x); return type;
          }
          if (b.Length > 1 && type.IsValueType) { mb.Ldarga(x); return type.MakeByRefType(); }
          mb.Ldarg(x); return type;
        }
        if (ke == 6)
        {
          vmap[ind] |= 0x20; var id = tpv >> 8;
          mb.Ldthis(); mb.Ldfld(fidata); mb.Ldc_I4(id >> 12); id &= 0x0fff;
          if (id != 0x0fff) { mb.Ldelem(type.MakeArrayType()); mb.Ldc_I4(id); }
          if ((fl & 1) != 0 && b.Length == 0)
          {
            if ((fl & 4) != 0) { mb.Ldelema(type.IsValueType ? type : typeof(object)); return type.MakeByRefType(); }
            if ((fl & 2) != 0) { mb.Ldelema(type); mb.Dup(); mb.Ldobj(type); accessor = __Stobj(type); return type.MakeByRefType(); }
            accessor = __Stelem(type); return type.MakeByRefType();
          }
          if (b.Length > 1 && type.IsValueType) { mb.Ldelema(type); return type.MakeByRefType(); }
          mb.Ldelem(type); return type;
        }
      }

      //if (usingst.Count != 0)
      //{
      //  var ss = b[0].ToString();
      //  for (int i = usingst.Count - 1; i >= 0; i--)
      //  {
      //    var t = usingst[i].GetNestedType(ss);
      //  }
      //}
      return null;
    }
    Type newcall(Token s, Type t, Block a)
    {
      var np = a.ParamCount();
      if (np == 0)
      {
        if (t.IsPrimitive) { ldconst(null, t, a); return t; }
        if (t.IsValueType) { var v = mb.GetLocal(t); mb.Ldloca(v); mb.Initobj(t); mb.Ldloc(v); mb.ReleaseLocal(v); return t; }
      }
      var tt = np != 0 ? new Type[np] : Type.EmptyTypes; var aa = a;
      for (int i = 0; i < np; i++) tt[i] = Parse(a.Param(), null) ?? typeof(object);
      var ci = t.GetConstructor(tt);
      if (ci == null)
      {
        ci = GetMethodBase(t, null, null, tt, 0, s, aa) as ConstructorInfo;
        if (ci == null) return t;
      }
      ParseParams(t, 0, np, aa, tt, ci);
      mb.Newobj(ci); return t;
    }
    Type __repaccess(Block b, int fl, object p, int i)
    {
      var fi = (RepInfo)p; var type = fi.type;
      if (i < mb.scope)
      {
        //mb.Ldarg(0); var d = 0; for (var l = mb.parent; i < l.scope; l = l.parent) if (l.reptype != null) { d++; mb.Ldc_I4(0); mb.Ldelem(typeof(object)); }
        //if (d != 0) { var x = b.Position - 1; var v = (RepInfo)pmap[x]; pmap[x] = new RepInfo { type = v.type, index = v.index, id = mb.parent.repid | (d << 16) }; }
        mb.Ldarg(0); int d = 0, d2 = 0; for (var l = mb.parent; i < l.scope; l = l.parent, d++) if (l.reptype != null) { mb.Ldc_I4(0); mb.Ldelem(typeof(object)); d2++; }
        if (d != 0) { var x = b.Position - 1; var v = (RepInfo)pmap[x]; pmap[x] = new RepInfo { type = v.type, index = v.index, id = mb.parent.repid | (d2 << 16) }; }
      }
      else
      {
        mb.Ldloc(mb.reploc);
      }
      mb.Ldc_I4(fi.index & 0xffff); var at = typeof(object); if (type.IsValueType) { mb.Ldelem(at); mb.Ldc_I4(fi.index >> 16); at = type; }
      if ((fl & 1) != 0 && b.Length == 0)
      {
        if ((fl & 4) != 0) { mb.Ldelema(type.IsValueType ? type : typeof(object)); return type.MakeByRefType(); } //todo: check !!!
        if ((fl & 2) != 0) { mb.Ldelema(at); mb.Dup(); mb.Ldobj(type); accessor = __Stobj(at); return type.MakeByRefType(); }
        accessor = __Stelem(type); return type.MakeByRefType();
      }
      if (b.Length > 1 && type.IsValueType) { mb.Ldelema(type); type = type.MakeByRefType(); return type; }
      mb.Ldelem(type); return type;
    }
    Func<int, int> __Starg(int x) { return i => { if (i != 0) return 0; mb.Starg(x); return 0; }; }
    Func<int, int> __Stloc(int x, int l) { return i => { if (i != 0) return 0; mb.Stloc(x); assig(l, x); return 0; }; }
    Func<int, int> __Stfld(FieldInfo fi) { return i => { if (i != 0) return 1; if (fi.IsStatic) mb.Stsfld(fi); else mb.Stfld(fi); return 0; }; }
    Func<int, int> __Call(MethodInfo mi, int f = 1) { return i => { if (i != 0) return f; mb.Callx(mi); return 0; }; }
    Func<int, int> __Stobj(Type et) { return i => { if (i != 0) return 1; mb.Stobj(et); return 0; }; }
    Func<int, int> __Stelem(Type et) { return i => { if (i != 0) return 1; mb.Stelem(et); return 0; }; }
    Func<int, int> __Stelem_Ref(Type et) { return i => { if (i != 0) return 1; mb.Stelem_Ref(); return 0; }; }
    void ldconst(object p, Type t, Token a)
    {
      switch (Type.GetTypeCode(t))
      {
        case TypeCode.Boolean: mb.Ldc_I4(p != null && (bool)p ? 1 : 0); return;
        case TypeCode.Char: mb.Ldc_I4(p != null ? (char)p : 0); return;
        case TypeCode.SByte: mb.Ldc_I4(p != null ? (sbyte)p : 0); return;
        case TypeCode.Byte: mb.Ldc_I4(p != null ? (byte)p : 0); return;
        case TypeCode.Int16: mb.Ldc_I4(p != null ? (short)p : 0); return;
        case TypeCode.UInt16: mb.Ldc_I4(p != null ? (ushort)p : 0); return;
        case TypeCode.Int32: mb.Ldc_I4(p != null ? (int)p : 0); return;
        case TypeCode.UInt32: mb.Ldc_I4(p != null ? unchecked((int)(uint)p) : 0); return;
        case TypeCode.Int64: mb.Ldc_I8(p != null ? (long)p : 0); return;
        case TypeCode.UInt64: mb.Ldc_I8(p != null ? unchecked((long)(ulong)p) : 0); return;
        case TypeCode.Single: mb.Ldc_R4(p != null ? (float)p : 0); return;
        case TypeCode.Double: mb.Ldc_R8(p != null ? (double)p : 0); return;
        case TypeCode.Object: if (!t.IsValueType && p == null) { mb.Ldnull(); return; } break;
      }
      if (!t.IsValueType) { Error(0000, "todo", a); return; }//todo: check, is there a case?
      var v = mb.GetLocal(t); mb.Ldloca(v); mb.Initobj(t); mb.Ldloc(v);
    }
    static bool __isint(Type t)
    {
      var tc = Type.GetTypeCode(t); return tc >= TypeCode.Char && tc <= TypeCode.UInt64;
    }
    static bool __isnumeric(Type t)
    {
      var tc = Type.GetTypeCode(t); return tc >= TypeCode.Char && tc < TypeCode.Decimal;
    }
    static Type __numeric(Type a, Type b)
    {
      if (!__isnumeric(a) || !__isnumeric(b)) return null;
      if (a.IsEnum) return a; if (b.IsEnum) return b;
      return __implicitnum(a, b) ? b : a;
    }
    static bool __implicitnum(Type ta, Type tb)
    {
      var a = Type.GetTypeCode(ta); var b = Type.GetTypeCode(tb);
      if (a == b) return true;
      if (a == TypeCode.Char) switch (b) { case TypeCode.UInt16: case TypeCode.UInt32: case TypeCode.Int32: case TypeCode.UInt64: case TypeCode.Int64: case TypeCode.Single: case TypeCode.Double: case TypeCode.Decimal: return true; default: return false; }
      if (a == TypeCode.Byte) switch (b) { case TypeCode.Char: case TypeCode.UInt16: case TypeCode.Int16: case TypeCode.UInt32: case TypeCode.Int32: case TypeCode.UInt64: case TypeCode.Int64: case TypeCode.Single: case TypeCode.Double: case TypeCode.Decimal: return true; default: return false; }
      if (a == TypeCode.SByte) switch (b) { case TypeCode.Int16: case TypeCode.Int32: case TypeCode.Int64: case TypeCode.Single: case TypeCode.Double: case TypeCode.Decimal: return true; default: return false; }
      if (a == TypeCode.UInt16) switch (b) { case TypeCode.UInt32: case TypeCode.Int32: case TypeCode.UInt64: case TypeCode.Int64: case TypeCode.Single: case TypeCode.Double: case TypeCode.Decimal: return true; default: return false; }
      if (a == TypeCode.Int16) switch (b) { case TypeCode.Int32: case TypeCode.Int64: case TypeCode.Single: case TypeCode.Double: case TypeCode.Decimal: return true; default: return false; }
      if (a == TypeCode.UInt32) switch (b) { case TypeCode.UInt64: case TypeCode.Int64: case TypeCode.Single: case TypeCode.Double: case TypeCode.Decimal: return true; default: return false; }
      if (a == TypeCode.Int32) switch (b) { case TypeCode.Int64: case TypeCode.Single: case TypeCode.Double: case TypeCode.Decimal: return true; default: return false; }
      if (a == TypeCode.UInt64) switch (b) { case TypeCode.Single: case TypeCode.Double: case TypeCode.Decimal: return true; default: return false; }
      if (a == TypeCode.Int64) switch (b) { case TypeCode.Single: case TypeCode.Double: case TypeCode.Decimal: return true; default: return false; }
      if (a == TypeCode.Single) switch (b) { case TypeCode.Double: return true; default: return false; }
      return false;
    }
    static MethodInfo opmeth(Type p, Type r, bool expl)
    {
      MethodInfo match = null; var s = expl ? "op_Explicit" : "op_Implicit";
      for (int k = 0; k < 2; k++)
      {
        var ma = (k == 0 ? p : r).GetMember(s, MemberTypes.Method, BindingFlags.Static | BindingFlags.Public);
        for (int i = 0; i < ma.Length; i++)
        {
          var mi = (MethodInfo)ma[i];
          if (mi.ReturnType != r && !expl) continue;
          var pp = TypeHelper.GetParametersNoCopy(mi);
          if (pp.Length != 1) continue; var pa = pp[0].ParameterType;
          if (pa == p && mi.ReturnType == r) return mi;
          if (!__canconv(p, pa)) continue;
          if (!__implicitnum(mi.ReturnType, r)) continue;
          if (match != null && Type.GetTypeCode(match.ReturnType) > Type.GetTypeCode(mi.ReturnType)) continue;
          match = mi;
        }
      }
      return match;
    }
    Type opmethcall(Block e, Type a, MethodInfo me, Type r)
    {
      var b = TypeHelper.GetParametersNoCopy(me)[0].ParameterType;
      if (a != b) Cast(e, a, b, false); mb.Callx(me);
      if (me.ReturnType != r) return Cast(e, me.ReturnType, r, true); return r;
    }
    Type Cast(Block e, Type a, Type b, bool expl = false)
    {
      a = filterdyn(a); b = filterdyn(b);
      if (a == typeof(void) || b == typeof(void)) { Error(0030, "Cannot convert type '{1}' to '{2}'", e, a, b); return null; }
      if (a == b) return b;
      //if (b == typeof(object)) { if (a.IsValueType) mb.Box(a); return b; }
      if (expl && a == typeof(object)) { if (b.IsValueType) mb.Unbox_Any(b); else mb.Castclass(b); return b; }
      if (expl && __isnumeric(a) && __isnumeric(b)) return __cast(b);
      var u = __numeric(a, b); if (u == b) { if (u.IsEnum) { Error(0266, "Cannot implicitly convert type '{1}' to '{2}'. An explicit conversion exists (are you missing a cast?)", e, a, b); return null; } return __cast(b); }
      if (b.IsAssignableFrom(a) && !isnullable(b))
      {
        if (a.IsValueType && !b.IsValueType) mb.Box(a); return b;
      }
      if (a.IsAssignableFrom(b) && !isnullable(a)) // after op_Implicit ?
      {
        if (!expl) { Error(0266, "Cannot implicitly convert type '{1}' to '{2}'. An explicit conversion exists (are you missing a cast?)", e, a, b); return null; }
        mb.Castclass(b); return b;
      }
      var me = opmeth(a, b, false);
      if (me != null) return opmethcall(e, a, me, b); // mb.Callx(me); return me.ReturnType; }
      me = opmeth(a, b, true);// ?? __opmeth("op_Explicit", a, b);
      if (me != null && expl) return opmethcall(e, a, me, b); // mb.Callx(me); return me.ReturnType; }
      if (me != null) { Error(0266, "Cannot implicitly convert type '{1}' to '{2}'. An explicit conversion exists (are you missing a cast?)", e, a, b); return null; }
      if (a.IsArray && b.IsArray && a.IsClass & b.IsClass) return Cast(e, a.GetElementType(), b.GetElementType());
      if (expl && a.IsInterface && b.IsClass) { mb.Castclass(b); return b; }
      if (expl && a.IsPointer && b.IsPointer) return b;
      if (a.IsPointer && b == typeof(void*)) return b;
      if (b == typeof(bool)) // c++ like... != default(
      {
        ldconst(null, a, e); mb.Ceq(); mb.Ldc_I4(1); mb.Xor(); return typeof(bool);
      }
      Error(0030, "Cannot convert type '{1}' to '{2}'", e, a, b); return null;
    }
    static bool __implicit(Type a, Type b)
    {
      if (b == typeof(__null)) return false;
      if (b == typeof(void)) return false;
      if (a == typeof(__null)) return !b.IsValueType;
      if (a == b) return true;
      if (b == typeof(object)) return true;
      if (a.IsSubclassOf(b)) return true;
      var c = __numeric(a, b); if (c == b) return true;
      return false;
    }
    bool checkname(Token s, int x, int fl)
    {
      if (!s.IsWord) { Error(1001, "Identifier expected", s); return false; }
      if (iskeyword(s)) { Error(1041, "Identifier expected; '{0}' is a keyword", s); return false; }
      if (fl == 3) return true;
      if (!isonstack(s, x)) return true;
      if (fl == 0) Error(0128, "A local variable named '{0}' is already defined in this scope", s);
      if (fl == 1) Error(0102, "The type '{1}' already contains a definition for '{0}'", s, @this);
      if (fl == 2) Error(0100, "The parameter name '{0}' is a duplicate", s);
      return false;
    }
    static bool isnullable(Type t)
    {
      return t.IsValueType && t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>);
    }
    static string __operator(Token op)
    {
      var c = op[0];
      if (c == '+') return "op_Addition";
      if (c == '-') return "op_Subtraction";
      if (c == '*') return "op_Multiply";
      if (c == '/') return "op_Division";
      if (c == '&') return "op_BitwiseAnd";
      if (c == '|') return "op_BitwiseOr";
      if (c == '^') return "op_ExclusiveOr";
      if (op.Equals("==")) return "op_Equality";
      if (op.Equals("!=")) return "op_Inequality";
      if (op.Equals("<")) return "op_LessThan";
      if (op.Equals(">")) return "op_GreaterThan";
      if (op.Equals("<=")) return "op_LessThanOrEqual";
      if (op.Equals(">=")) return "op_GreaterThanOrEqual";
      return null;
    }
    static MethodInfo __operator(string s, Type[] tt)
    {
      var bin = BindingFlags.Static | BindingFlags.Public;
      return tt[0].GetMethod(s, bin, null, tt, null) ?? tt[1].GetMethod(s, bin, null, tt, null);
    }
    static MethodInfo __operator(Token op, Type t1, Type t2)
    {
      var s = __operator(op); if (s == null) return null;
      var tt = new Type[] { t1, t2 };
      if (op[0] == '+') return __operator(s, tt) ?? __operator("Concat", tt);
      return __operator(s, tt);
    }
    void __operator(Token op, Type t1, Type t2, ref MethodInfo best)
    {
      var a = t1.GetMember(__operator(op), BindingFlags.Static | BindingFlags.Public | BindingFlags.InvokeMethod);
      for (int i = 0; i < a.Length; i++)
      {
        var m = (MethodInfo)a[0];
        if (best != null) Error(0, "todo: bestmatch '{0}'", op);
        best = m;
      }
    }
    Func<int, int> StartAccess(Block b, bool writeonly, out Type type)
    {
      var t1 = accessor; accessor = null; type = Parse(b, typeof(map), writeonly ? 1 : 3); var p = accessor; accessor = t1;
      if (type == null) return null; if (p == null) { Error(0131, "The left-hand side of an assignment must be a variable, property or indexer", b); return null; }
      return p;
    }
    Type Parse(Block b, Type wt, int fl = 0)
    {
      if (b.Length == 0) { Error(1525, "Invalid expression term '{0}'", b[0]); return null; }
      if (b.Equals("null"))
      {
        if (wt == null) return typeof(__null);
        if (wt.IsValueType)
        {
          if (isnullable(wt)) { var v = mb.GetLocal(wt); mb.Ldloca(v); mb.Initobj(wt); mb.Ldloc(v); mb.ReleaseLocal(v); return wt; }
          Error(0037, "Cannot convert null to '{1}' because it is a non-nullable value type", b, wt); return null;
        }
        mb.Ldnull(); return wt;
      }

      var a = b; var pos = -1; int l = 0;
      for (char ü; a.Length != 0;)
      {
        if (a[0].Equals('<')) { var x = a; x.Next(0xf); if (x.StartsWith('(')) { a = x; continue; } }
        var c = a.Next(); if (c.Length != 1) continue; if (c.Position == b.Position) continue; var t = c[0];
        if (c.Equals("=>")) { pos = t.Position; l = 1; break; }
        if (t.Equals('=') || (t.Length == 2 && t[1] == '=' && ((ü = t[0]) == '+' || ü == '-' || ü == '*' || ü == '/' || ü == '%' || ü == '|' || ü == '&' || ü == '^')) || t.Equals("<<=") || t.Equals(">>=")) { pos = t.Position; l = 1; break; }
        if (t.Equals('?') || t.Equals("??")) { if (l != 2) pos = t.Position; l = 2; }
        if (l == 2) continue;
        if (t.Equals("||")) { pos = t.Position; l = 3; }
        if (l == 3) continue;
        if (t.Equals("&&")) { pos = t.Position; l = 4; }
        if (l == 4) continue;
        if (t.Equals('|')) { pos = t.Position; l = 5; }
        if (l == 5) continue;
        if (t.Equals('^')) { pos = t.Position; l = 6; }
        if (l == 6) continue;
        if (t.Equals('&')) { if (c.Position > 2 && c[-1].Equals(')') && c[-2].Equals('*')) continue; pos = t.Position; l = 7; }
        if (l == 7) continue;
        if (t.Equals("==") || t.Equals("!=")) { pos = t.Position; l = 8; }
        if (l == 8) continue;
        if (t.Equals('>') && a[0].Equals('>') && t.Position + 1 == a[0].Position) { t = new Token(t.Position, 2); a.Next(); }
        if (t.Equals('<') || t.Equals('>') || t.Equals("<=") || t.Equals(">=") || t.Equals("as") || t.Equals("is")) { if (char.IsLetter(t[0])) ParseType(ref a); pos = t.Position; l = 9; }
        if (l == 9) continue;
        if (t.Equals("<<") || t.Equals(">>")) { pos = t.Position; l = 10; continue; }
        if (l == 10) continue;
        if (t.Equals('+') || t.Equals('-')) { if (t.Position == b[0].Position) continue; pos = t.Position; l = 11; while (a.Take('-') || a.Take('+')) { } }
        if (l == 11) continue;
        if (t.Equals('*') || t.Equals('/') || t.Equals('%')) { pos = t.Position; l = 12; while (a.Take('-') || a.Take('+')) { } }
        if (l == 12) continue;
        if (t.Equals(',')) { Error(1525, "Invalid expression term '{0}'", t); break; }// return null; }
      }
      if (l != 0)
      {
        var x = 0; for (; b[x].Position != pos; x++) ;
        a = b.SubBlock(0, x); var op = b[x++]; var c = b.SubBlock(x, b.Length - x); if (l == 10 && op.Equals('>')) { op = new Token(op.Position, 2); c.Next(); }
        if (l == 1)
        {
          if (op.Equals("=>"))
          {
            if (wt == null) return typeof(Delegate);
            if (!wt.IsSubclassOf(typeof(Delegate))) { Error(1525, "Invalid expression term '{0}'", a); return null; }
            var mi = wt.GetMethod("Invoke"); var bb = TypeHelper.GetParametersNoCopy(mi);
            if (a.StartsWith('(')) a = a.Trim(); var np = a.ParamCount();
            if (np != bb.Length) { Error(1593, "Delegate '{1}' does not take {2} arguments", a, wt, np); return null; }
            var mo = mb; for (; mo != null && mo.reptype == null; mo = mo.parent) ; var rept = mo != null ? mo.reptype : null; //if (rept != null) { }
            var vv = new ab[np]; var tt = new Type[np + 1]; tt[0] = rept ?? typeof(Neuron); //mb.reptype ?? @this;
            for (int t = 0; t < np; t++) { var sn = a.Param(); checkname(sn, nglob, 2); vv[t] = Map(sn, tt[t + 1] = bb[t].ParameterType, 5 | (nextid << 8), t + 1); nextid++; }
            var dm = new DynamicMethod(trace != null ? "__anonymous" + b.Position : string.Empty, mi.ReturnType, tt, typeof(object), true);
            var rt = Lambda(dm, vv, c); if (__isgen(mi.ReturnType)) return rt;
            if (mb.reptype != null) mb.Ldloc(mb.reploc); else mb.Ldarg(0);
            mb.Ldftn(dm); mb.Newobj(wt.GetConstructors()[0]); return wt;
          }
          if (wt == null) return Parse(a, null); var oc = op[0];
          Type vt; var pt = StartAccess(a, oc == '=', out vt); if (pt == null) return null; var u = -1;
          if (vt.IsByRef) vt = vt.GetElementType();
          if (oc == '=')
          {
            ParseStrong(c, vt);
          }
          else
          {
            var bo = vt == typeof(bool) && (oc == '|' || oc == '&' || oc == '^');
            if (bo || __isnumeric(vt))
            {
              ParseStrong(c, oc == '<' || oc == '>' ? typeof(int) : vt); bool bin = false;
              switch (oc)
              {
                case '+': mb.Add(); break;
                case '-': mb.Sub(); break;
                case '*': mb.Mul(); break;
                case '/': mb.Div(); break;
                case '%': mb.Rem(); break;
                case '|': mb.Or(); bin = true; break;
                case '&': mb.And(); bin = true; break;
                case '^': mb.Xor(); bin = true; break;
                case '<': mb.Shl(); bin = true; break;
                case '>': mb.Shr(); bin = true; break;
                default: Error(0000, "todo", op); break;
              }
              if (bin && !bo && !__isint(vt)) Error(0019, "Operator '{0}' cannot be applied to operands of type '{1}' and '{2}'", op, vt, vt);
            }
            else
            {
              if ((oc == '+' || oc == '-') && vt.IsSubclassOf(typeof(Delegate)))
              {
                ParseStrong(c, vt); if ((pt(1) & 2) == 0) mb.Call(typeof(Delegate).GetMethod(oc == '+' ? "Combine" : "Remove", new Type[] { typeof(Delegate), typeof(Delegate) }));
              }
              else
              {
                var rt = Parse(c, null); if (rt == null) return null;
                if (vt.IsPointer && __isint(rt) && (oc == '+' || oc == '-'))
                {
                  ParseStrong(c, rt); mb.Sizeof(vt.GetElementType()); mb.Mul(); if (oc == '+') mb.Add(); else mb.Sub();
                }
                else
                {
                  var mi = __operator(op, vt, rt);
                  if (mi == null) mi = __operator(op, vt, vt); //dec += 3
                  if (mi == null /*|| mi.ReturnType != vt*/) { Error(0019, "Operator '{0}' cannot be applied to operands of type '{1}' and '{2}'", op, vt, rt); return null; }
                  var pp = TypeHelper.GetParametersNoCopy(mi); ParseStrong(c, pp[1].ParameterType); mb.Call(mi);
                  if (mi.ReturnType != vt) Cast(c, mi.ReturnType, vt);
                }
              }
            }
          }
          if (wt != typeof(void)) { mb.Dup(); if ((pt(1) & 1) != 0) mb.Stloc(u = mb.GetLocal(vt)); } else vt = typeof(void);
          pt(0); if (u != -1) { mb.Ldloc(u); mb.ReleaseLocal(u); }
          return vt;
        }

        //if (op.Equals("is")) { var t = ParseType(c); if (t != null && wt != null) { Parse(a, wt); mb.Isinst(t); mb.Ldnull(); mb.Cgt(); } return typeof(bool); }
        if (op.Equals("is")) { var t = ParseType(c); if (t != null && wt != null) { Parse(a, wt); mb.Isinst(t); mb.Ldnull(); mb.Ceq(); mb.Ldc_I4(1); mb.Xor(); } return typeof(bool); } //better to optimize
        if (op.Equals("as")) { var t = ParseType(c); if (t != null && wt != null) { Parse(a, wt); mb.Isinst(t); } return t; }
        if (op.Equals('?'))
        {
          var cc = c; var d = c.Param(':'); if (c.Length == 0) { Error(1515, "'{1}' expected", c, ':'); return null; }
          if (wt == null)
          {
            var ta = Parse(d, null); var tb = Parse(c, null); if (ta == null || tb == null) return null;
            var vt = __implicit(ta, tb) ? tb : __implicit(tb, ta) ? ta : null;
            if (vt == null) { var mc = opmeth(ta, tb, false) ?? opmeth(tb, ta, false); if (mc != null) vt = mc.ReturnType; }
            if (vt == null) { Error(0173, "Type of conditional expression cannot be determined because there is no implicit conversion between '{1}' and '{2}'", cc, ta, tb); return null; }
            return vt;
          }
          var l1 = mb.DefineLabel(); var l2 = mb.DefineLabel();
          ParseStrong(a, typeof(bool)); mb.Brfalse(l1); ParseStrong(d, wt); mb.Br(l2); mb.DecStack(); mb.MarkLabel(l1); ParseStrong(c, wt); mb.MarkLabel(l2); return wt;
        }
        if (op.Equals("??"))
        {
          var ta = Parse(a, null); var tb = Parse(c, null); if (ta == null || tb == null) return null;
          var vt = __implicit(ta, tb) ? tb : __implicit(tb, ta) ? ta : null;
          if (vt == null || vt.IsValueType) { Error(0019, "Operator '{0}' cannot be applied to operands of type '{1}' and '{2}'", op, ta, tb); return null; }
          if (wt == null) return vt; var l1 = mb.DefineLabel();
          ParseStrong(a, vt); mb.Dup(); mb.Brtrue(l1); mb.Pop(); ParseStrong(c, vt); mb.MarkLabel(l1); return vt;
        }
        if (op.Equals("&&") || op.Equals("||"))
        {
          if (wt == null) return typeof(bool); var l1 = mb.DefineLabel(); var l2 = mb.DefineLabel(); var oc = op[0];
          ParseStrong(a, typeof(bool)); if (oc == '&') mb.Brfalse(l1); else mb.Brtrue(l1);
          ParseStrong(c, typeof(bool)); if (oc == '&') mb.Brfalse(l1); else mb.Brtrue(l1);
          mb.Ldc_I4(oc == '&' ? 1 : 0); mb.Br(l2); mb.DecStack(); mb.MarkLabel(l1); mb.Ldc_I4(oc == '&' ? 0 : 1); mb.MarkLabel(l2); return typeof(bool);
        }

        var t1 = Parse(a, null); var t2 = Parse(c, null); if (t1 == null || t2 == null) return null;

        var t3 = __numeric(t1, t2);
        if (t3 == null)
        {
          if (t1 != typeof(__null) && t2 != typeof(__null))
          {
            if (op.Equals('+') || op.Equals('-')) // pointer arithmetic
            {
              if (t1.IsPointer || t2.IsPointer)
              {
                if (__isint(t1) || __isint(t2))
                {
                  if (wt != null)
                  {
                    ParseStrong(a, t1); if (t2.IsPointer) { mb.Sizeof(t2.GetElementType()); mb.Mul(); }
                    ParseStrong(c, t2); if (t1.IsPointer) { mb.Sizeof(t1.GetElementType()); mb.Mul(); }
                    if (op.Equals('+')) mb.Add(); else mb.Sub();
                  }
                  return t1.IsPointer ? t1 : t2;
                }
                if (t1 == t2 && op.Equals('-'))
                {
                  if (wt != null) { ParseStrong(a, t1); ParseStrong(c, t2); mb.Sub(); mb.Sizeof(t1.GetElementType()); mb.Div(); mb.Conv_I8(); }
                  return typeof(long);
                }
              }
            }

            var mi = __operator(op, t1, t2);
            if (mi == null)
            {
              var mc = opmeth(t1, t2, false) ?? opmeth(t2, t1, false);
              if (mc != null) mi = __operator(op, mc.ReturnType, mc.ReturnType);
              if (mi == null) { __operator(op, t1, t2, ref mi); __operator(op, t2, t1, ref mi); }
            }
            if (mi != null)
            {
              if (wt != null) { var pp = TypeHelper.GetParametersNoCopy(mi); ParseStrong(a, pp[0].ParameterType); ParseStrong(c, pp[1].ParameterType); mb.Call(mi); }
              return mi.ReturnType;
            }
          }
          if (op.Equals("==") || op.Equals("!=")) t3 = __implicit(t1, t2) ? t2 : __implicit(t2, t1) ? t1 : null;
          if (t3 != null && t3.IsValueType && !t3.IsPrimitive) t3 = null;
          if (t3 == null) { Error(0019, "Operator '{0}' cannot be applied to operands of type '{1}' and '{2}'", op, t1, t2); return null; }
        }
        if (op.Equals("<<") || op.Equals(">>"))
        {
          if (wt == null) return t1; if (!__isint(t1) || t2 != typeof(int)) Error(0019, "Operator '{0}' cannot be applied to operands of type '{1}' and '{2}'", op, t1, t2);
          ParseStrong(a, t1); ParseStrong(c, typeof(int)); if (op[0] == '<') mb.Shl(); else mb.Shr(); return t1;
        }
        if (wt != null) { ParseStrong(a, t3); ParseStrong(c, t3); }
        if (op.Equals("==")) { if (wt != null) { mb.Ceq(); } return typeof(bool); }
        if (op.Equals("!=")) { if (wt != null) { mb.Ceq(); mb.Ldc_I4(1); mb.Xor(); } return typeof(bool); }
        if (op.Equals('+')) { if (wt != null) mb.Add(); return t3; }
        if (op.Equals('-')) { if (wt != null) mb.Sub(); return t3; }
        if (op.Equals('*')) { if (wt != null) mb.Mul(); return t3; }
        if (op.Equals('/')) { if (wt != null) mb.Div(); return t3; }
        if (op.Equals('%')) { if (wt != null) mb.Rem(); return t3; }
        if (op.Equals('<')) { if (wt != null) mb.Clt(); return typeof(bool); }
        if (op.Equals('>')) { if (wt != null) mb.Cgt(); return typeof(bool); }
        if (op.Equals("<=")) { if (wt != null) { mb.Cgt(); mb.Ldc_I4(1); mb.Xor(); } return typeof(bool); }
        if (op.Equals(">=")) { if (wt != null) { mb.Clt(); mb.Ldc_I4(1); mb.Xor(); } return typeof(bool); }
        if (!__isint(t3)) { Error(0019, "Operator '{0}' cannot be applied to operands of type '{1}' and '{2}'", op, t1, t2); return null; }
        if (op.Equals('|')) { if (wt != null) mb.Or(); return t3; }
        if (op.Equals('&')) { if (wt != null) mb.And(); return t3; }
        if (op.Equals('^')) { if (wt != null) mb.Xor(); return t3; }
        Error(0000, "todo", b); return t3;
      }

      //if (b.StartsWith('(')) { b.Trim(); }
      if (b.Take('!')) { if (wt != null) { ParseStrong(b, typeof(bool)); mb.Ldc_I4(0); mb.Ceq(); } return typeof(bool); }
      if (b.Take('~'))
      {
        var vt = Parse(b, null); if (vt == null) return null; if (wt == null) return vt;
        if (!__isint(vt))
        {
          var mi = vt.GetMethod("op_OnesComplement", BindingFlags.Static | BindingFlags.Public, null, new Type[] { vt }, null);
          if (mi == null || mi.ReturnType != vt) { Error(0023, "Operator '{0}' cannot be applied to operand of type '{1}'", b[-1], vt); return null; }
          ParseStrong(b, vt); mb.Call(mi); return vt;
        }
        ParseStrong(b, vt); mb.Not(); return vt;
      }
      if (b.Take('+')) return Parse(b, wt);
      if (b.Take('-'))
      {
        var vt = Parse(b, null); if (vt == null) return null; if (wt == null) return vt;
        if (!__isnumeric(vt))
        {
          var mi = vt.GetMethod("op_UnaryNegation", BindingFlags.Static | BindingFlags.Public, null, new Type[] { vt }, null);
          if (mi == null || mi.ReturnType != vt) { Error(0023, "Operator '{0}' cannot be applied to operand of type '{1}'", b[-1], vt); return null; }
          ParseStrong(b, vt); mb.Call(mi); return vt;
        }
        ParseStrong(b, vt); mb.Neg(); return vt;
      }

      var h = b[0].Equals("++") ? 2 | 1 : b[0].Equals("--") ? 4 | 1 : b.Last().Equals("++") ? 2 : b.Last().Equals("--") ? 4 : 0;
      if (h != 0)
      {
        b = b.SubBlock(h & 1, b.Length - 1); if (wt == null) return Parse(b, null);
        Type vt; var pt = StartAccess(b, false, out vt); if (pt == null) return null; var u = -1;
        if (vt.IsByRef) vt = vt.GetElementType(); var ot = vt;
        if (!(__isnumeric(vt) || ot.IsPointer)) { Error(0023, "Operator '{0}' cannot be applied to operand of type '{1}'", b[(h & 1) != 0 ? -1 : +1], vt); return null; }
        if ((h & 1) == 0) if (wt != typeof(void)) { mb.Dup(); if (pt(1) != 0) mb.Stloc(u = mb.GetLocal(vt)); } else vt = typeof(void);
        if (ot.IsPointer) mb.Sizeof(ot.GetElementType()); else { mb.Ldc_I4(1); if (!__isint(ot)) __cast(ot); }
        if ((h & 2) != 0) mb.Add(); else mb.Sub();
        if ((h & 1) != 0) if (wt != typeof(void)) { mb.Dup(); if (pt(1) != 0) mb.Stloc(u = mb.GetLocal(vt)); } else vt = typeof(void);
        pt(0); if (u != -1) { mb.Ldloc(u); mb.ReleaseLocal(u); }
        return vt;
      }
      if (b.Take('&'))
      {
        var tv = Parse(b, wt, 0x11); if (tv == null) return null;
        if (wt == null && !__isprimitiv(tv)) { Error(0208, "Cannot take the address of, get the size of, or declare a pointer to a managed type ('{1}')", b, tv); return null; }
        return wt == null ? tv.MakePointerType() : tv;
      }
      if (b.Take('*'))
      {
        var tv = Parse(b, wt); if (tv == null) return null;
        if (!tv.IsPointer) { Error(0193, "The * or -> operator must be applied to a pointer", b); return null; }
        tv = tv.GetElementType();
        if (wt != null)
        {
          if ((fl & 1) != 0)
          {
            if ((fl & 4) != 0) { Error(0000, "todo", b); } //return et.MakeByRefType(); } //todo: checked
            if ((fl & 2) != 0) { mb.Dup(); mb.Ldobj(tv); accessor = __Stobj(tv); return tv; }
            accessor = __Stobj(tv); return tv;
          }
          mb.Ldobj(tv);
        }
        return tv;
      }

      var ec = errors.Count; var type = ParseLeft(ref b, wt, fl); if (ec != errors.Count) return null;
      var basetype = type; var atbase = false;
      if (type == null)
      {
        var x = pmap[b.Position + 0];
        if (!(x is string || x is Type || b[0].DefType() != null))
        {
          var ss = b[0].ToString();
          if (basetype == null)
            for (int i = usingst.Count - 1; i >= 0; i--)
            {
              var xx = usingst[i].GetMember(ss, BindingFlags.Static | BindingFlags.Public);
              if (xx.Length != 0)
              {
                //if (xx[0] is Type) { type = usingst[i]; }
                atbase = true; basetype = usingst[i]; break;
              }
            }
          if (!atbase)
          {
            var mm = x == null ? @this.GetMember(ss, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy) : null;
            if (x != null || mm.Length != 0) { atbase = true; basetype = @this; }
          }
        }
        if (basetype == null)
        {
          basetype = ParseType(ref b); if (basetype == null) { Error(0103, "The name '{0}' does not exist in the current context", b[0]); return null; }
          if (b.Length == 0) { Error(1515, "'{1}' expected", b[0].Start(), '.'); return null; }
        }
      }
      for (; b.Length != 0; basetype = null, atbase = false)
      {
      m1: bool priv = false;
        if (!atbase)
        {
          if (b.StartsWith('('))
          {
            if (type == null) { Error(1001, "Identifier expected", b[0]); return null; }
            if (!type.IsSubclassOf(typeof(Delegate))) { Error(1001, "Identifier expected", b[0]); return null; }
            var mi = type.GetMethod("Invoke"); if (wt == null) return mi.ReturnType;
            var bb = b; var c = b.Next().Trim(); var np = c.ParamCount();
            var pp = TypeHelper.GetParametersNoCopy(mi); if (np != pp.Length) { Error(1593, "Delegate '{1}' does not take {2} arguments", bb[-1], type, np); return null; }
            for (int i = 0; i < np; i++) ParseStrong(c.Param(), pp[i].ParameterType); mb.Callx(mi);
            type = mi.ReturnType; if (b.Length == 0 && wt == typeof(void) && type != typeof(void)) { mb.Pop(); return typeof(void); }
            continue;
          }
          if (b.StartsWith('['))
          {
            if (type == null) { Error(1001, "Identifier expected", b[0]); return null; }
            var c = b.Next().Trim(); var cc = c; var np = c.ParamCount(); if (np == 0) { Error(0000, "todo", c); return null; }
            var tt = np != 0 ? new Type[np] : Type.EmptyTypes;
            for (int i = 0; i < np; i++) tt[i] = Parse(c.Param(), null) ?? typeof(int);
            if (type.IsArray)
            {
              if (np != 1) { Error(0000, "todo", c); return null; }
              var et = type.GetElementType();
              if (wt != null)
              {
                ParseStrong(cc, typeof(int));
                if ((fl & 1) != 0 && b.Length == 0)
                {
                  if ((fl & 4) != 0) { mb.Ldelema(et); return et.MakeByRefType(); }
                  if ((fl & 2) != 0) { mb.Ldelema(et); mb.Dup(); mb.Ldobj(et); accessor = __Stobj(et); return et.MakeByRefType(); }
                  accessor = __Stelem(et); return et;
                }
                //if ((fl & 1) != 0 && et.IsValueType) { mb.Ldelema(et); et = et.MakeByRefType(); } else mb.Ldelem(et);
                if (((fl & 1) != 0 || b.Length != 0) && et.IsValueType) { mb.Ldelema(et); et = et.MakeByRefType(); } else mb.Ldelem(et);
              }
              type = et; continue;
            }
            if (type.IsPointer)
            {
              if (np != 1) { Error(0000, "todo", c); return null; }
              var et = type.GetElementType();
              if (wt != null)
              {
                ParseStrong(cc, typeof(int)); mb.Sizeof(et); mb.Mul(); mb.Add(); //ParseStrong(cc, typeof(int)); mb.Ldc_I4(Marshal.SizeOf(et)); mb.Mul(); mb.Add();
                if ((fl & 1) != 0 && b.Length == 0)
                {
                  if ((fl & 0x10) != 0)
                  {
                    if (!__isprimitiv(et)) { Error(0208, "Cannot take the address of, get the size of, or declare a pointer to a managed type ('{1}')", c, et); return null; }
                    type = et.MakePointerType(); continue;
                  }
                  if ((fl & 4) != 0) { Error(0000, "todo", c); } //{ return et.MakeByRefType(); } //todo: checked
                  if ((fl & 2) != 0) { mb.Dup(); mb.Ldobj(et); accessor = __Stobj(et); return et.MakeByRefType(); } //todo: checked
                  accessor = __Stobj(et); return et;
                }
                if ((fl & 1) != 0) { et = et.MakeByRefType(); } else mb.Ldobj(et);
              }
              type = et; continue;
            }

            if (type.IsByRef) type = type.GetElementType();
            var ii = type.GetProperty(type == typeof(string) ? "Chars" : "Item", tt); if (ii == null) { Error(0000, "todo", c); return null; }
            if (wt != null)
            {
              var get = ii.GetGetMethod(); if (get == null) { Error(0000, "todo", c); return null; }
              var gpp = TypeHelper.GetParametersNoCopy(get); if (gpp.Length != 1) { Error(0000, "todo", c); return null; }
              var tit = gpp[0].ParameterType;
              if (get.ReturnType.IsValueType && (fl & 3) != 0 && b.Length != 0) { Error(1612, "Cannot modify the return value of '{1}' because it is not a variable", b, ii); return null; }
              if ((fl & 1) != 0 && b.Length == 0)
              {
                if ((fl & 2) != 0)
                {
                  mb.Dup(); var l1 = mb.GetLocal(type); mb.Stloc(l1); ParseStrong(cc, tit); if (cc.Length != 1) { Error(0000, "todo", c); return null; } //todo: complex case
                  mb.Ldloc(l1); mb.ReleaseLocal(l1); ParseStrong(cc, tit); mb.Callx(get);
                }
                else ParseStrong(cc, tit);
                var set = ii.GetSetMethod(); if (set == null) { Error(0200, "Property or indexer '{0}' cannot be assigned to -- it is read only", cc[-2]); return null; }
                accessor = __Call(set); return ii.PropertyType;
              }
              var u = -1; if (type.IsValueType) { u = mb.GetLocal(type); mb.Stloc(u); mb.Ldloca(u); }
              ParseStrong(cc, tit); mb.Callx(get);
              if (u != -1) mb.ReleaseLocal(u);
            }
            type = ii.PropertyType; continue;
          }
          var tp = type ?? basetype;
          var t = b.Take(); if (!(tp.IsPointer ? t.Equals("->") : (t.Equals('.') || (priv = t.Equals('#'))))) { Error(1515, "'{1}' expected", b[-2].End(), ';'); return null; }
          if (tp == typeof(void)) { Error(0023, "Operator '{0}' cannot be applied to operand of type '{1}'", t, tp); return null; }
          basetype = tp.IsPointer || tp.IsByRef ? tp.GetElementType() : tp; //if (priv) { }
          if (type != null) Map(t, basetype, 0x01, 0); if (b.Length == 0) { Error(1001, "Identifier expected", t.End()); return null; }
        }

        a = b.Next();
        var bind = (atbase || type != null ? BindingFlags.Instance : 0) | (atbase || type == null ? BindingFlags.Static : 0) | BindingFlags.Public | BindingFlags.FlattenHierarchy;
        if (priv || atbase || (basetype != null && @this.IsOrIsSubclassOf(basetype))) bind |= BindingFlags.NonPublic;
        var aobj = pmap[a.Position]; var s = aobj == null ? a.ToString() : null;
        //if (aobj == typeof(dynamic)) { }
        var fi = aobj as FieldInfo;
        if (fi == null && s != null) { fi = __filter(basetype.GetField(s, bind), priv); if (fi != null) Map(a, fi, shareid(fi, 3) & ~0xf0, 0); }
        if (fi != null)
        {
          if (fi.IsLiteral)
          {
            if (type != null) { Error(0000, "Unexpected", a); return null; }
            type = fi.FieldType;
            if (wt != null) ldconst(fi.GetRawConstantValue(), type.IsEnum ? type.GetEnumUnderlyingType() : type, a);
            continue;
          }
          if (wt != null && type == null && !fi.IsStatic) mb.Ldthis(); type = fi.FieldType;
          if ((fl & 1) != 0 && b.Length == 0)
          {
            if ((fl & 4) != 0) { if (!fi.IsStatic) mb.Ldflda(fi); else mb.Ldsflda(fi); return type.MakeByRefType(); } //ref, out, todo: check Ldsflda
            if ((fl & 2) != 0) { if (!fi.IsStatic) { mb.Dup(); mb.Ldfld(fi); } else mb.Ldsfld(fi); }
            accessor = __Stfld(fi); return type;
          }
          if (wt != null) { if (fi.IsStatic) mb.Ldsfld(fi); else if ((fl & 1) != 0 && type.IsValueType) mb.Ldflda(fi); else mb.Ldfld(fi); }
          continue;
        }

        var pi = aobj as PropertyInfo;
        if (pi == null && s != null)
        {
          pi = __filter(basetype.GetProperty(s, bind, null, null, Type.EmptyTypes, null), priv);
          if (pi != null && b.StartsWith('(')) if (!pi.PropertyType.IsSubclassOf(typeof(Delegate))) pi = null;
          if (pi != null) Map(a, pi, shareid(pi, 3), 0);
        }
        if (pi != null)
        {
          if (wt != null)
          {
            var mi = pi.GetGetMethod(true); if (mi == null && !((fl & 3) == 1 && b.Length == 0)) { Error(0154, "The property or indexer '{0}' cannot be used in this context because it lacks the get accessor", a); return null; }
            if (type == null && !mi.IsStatic) mb.Ldthis();
            if ((fl & 1) != 0 && b.Length == 0)
            {
              if ((fl & 2) != 0) { if (!mi.IsStatic) mb.Dup(); mb.Callx(mi); }
              var set = pi.GetSetMethod(true); if (set == null) { Error(0200, "Property or indexer '{0}' cannot be assigned to -- it is read only", a); return null; }
              accessor = __Call(set); return pi.PropertyType;
            }
            if (type != null && type.IsArray && pi.Name == "Length") { mb.Ldlen(); type = pi.PropertyType; continue; }
            if (type != null && type.IsValueType) { int u = mb.GetLocal(type); mb.Stloc(u); mb.Ldloca(u); mb.ReleaseLocal(u); }
            mb.Callx(mi);
          }
          type = pi.PropertyType; continue;
        }

        if (type == typeof(dynamic))
        {
          if (aobj == null) Map(a, aobj = s, 0x41, 0);
          if (wt != null)
          {
            mb.Ldstr((string)aobj);
            if ((fl & 1) != 0 && b.Length == 0) { accessor = __Call(typeof(Neuron).GetMethod("put", BindingFlags.Static | BindingFlags.NonPublic)); return type; }
            mb.Callx(typeof(Neuron).GetMethod("get", BindingFlags.Static | BindingFlags.NonPublic));
          }
          continue;
        }

        if (b.StartsWith('(') || b.StartsWith('<'))
        {
          var g = ParseTypes(ref b); if (!b.StartsWith('(')) { Error(1002, "; expected", b[0].Start()); return null; }
          var c = b.Next().Trim(); var m = c.EndsWith('>') ? 0xf : 0x7; //if (c.EndsWith('>')) { }
          var np = c.ParamCount(',', m); var cc = c;
          var tt = np != 0 ? new Type[np] : Type.EmptyTypes;
          for (int i = 0; i < np; i++)
          {
            var pa = c.Param(',', m); var re = pa.Take("ref") ? 1 : pa.Take("out") ? 1 : 0;
            var pt = Parse(pa, null) ?? typeof(object); if (re != 0) pt = pt.MakeByRefType(); tt[i] = pt;
          }
          var mi = aobj as MethodInfo;
          if (mi == null)
          {
            //if ((mi = __filter(GetMethod(basetype, s, g, tt, bind, a, cc), priv)) == null) return null;
            if ((mi = GetMethodBase(basetype, s, g, tt, bind, a, cc) as MethodInfo) == null) return null;
            Map(a, mi, shareid(mi, 3), 0);
          }
          if (wt != null)
          {
            if (type != null && type.IsValueType) { }
            if (type == null && !mi.IsStatic) mb.Ldthis();
            int u = -1; if (type != null && type.IsValueType)// && !mi.IsStatic)
            {
              if (!mi.DeclaringType.IsValueType) mb.Box(type); //if (!type.IsClass && mi.DeclaringType.IsClass) mb.Box(type);
              else { mb.Stloc(u = mb.GetLocal(type)); mb.Ldloca(u); }
            }
            ParseParams(type, bind, np, cc, tt, mi);
            mb.Callx(mi);
            if (u != -1) mb.ReleaseLocal(u);
          }
          type = mi.ReturnType;
          if (b.Length == 0 && wt == typeof(void) && type != typeof(void)) { mb.Pop(); return typeof(void); }
          continue;
        }
        var ev = aobj as EventInfo;
        if (ev == null && s != null) { ev = basetype.GetEvent(s, bind); if (ev != null) Map(a, ev, shareid(ev, 3), 0); }
        if (ev != null)
        {
          var tk = a[a.Length];
          if (!(tk.Equals("+=") || tk.Equals("-="))) { Error(0079, "The event '{1}' can only appear on the left hand side of += or -=", a, ev); return null; }
          accessor = __Call(tk.Equals("+=") ? ev.GetAddMethod() : ev.GetRemoveMethod(), 1 | 2); return ev.EventHandlerType;
        }

        if (wt != null && wt.IsSubclassOf(typeof(Delegate))) // menu.Closed += Node.IdleDispose;
        {
          var mi = aobj as MethodInfo;
          if (mi == null)
          {
            var mv = wt.GetMethod("Invoke"); var pa = TypeHelper.GetParametersNoCopy(mv);
            var tt = new Type[pa.Length]; for (int t = 0; t < pa.Length; t++) tt[t] = pa[t].ParameterType;
            bind = (type != null ? BindingFlags.Instance : BindingFlags.Static) | BindingFlags.Public | BindingFlags.FlattenHierarchy;
            if ((mi = GetMethodBase(basetype, s, null, tt, bind, a, b) as MethodInfo) == null) return null;
            Map(a, mi, shareid(mi, 3), 0);
          }
          //todo: strong param type check ?
          if (mi.IsStatic) mb.Ldnull(); mb.Ldftn(mi); mb.Newobj(wt.GetConstructors()[0]); return wt;
          //mb.Ldtoken(mi); mb.Ldtoken(wt); mb.Call(typeof(Dynamic).GetMethod("mkd", BindingFlags.Static | BindingFlags.NonPublic)); return wt;
        }
        if (atbase)
        {
          var t = basetype.GetNestedType(s);
          if (t != null) { Map(a, t, 0, 0); var x = b.Take(); if (x.Equals('.') || x.Equals('#')) Map(x, t, 0x41, 0); basetype = t; goto m1; }
        }
        if (iskeyword(a[0])) { Error(1002, "; expected", a[-1].End()); return null; }
        Error(0117, "'{1}' does not contain a definition for '{0}'", a, basetype); return null;
      }
      return type;
    }
    void ParseParams(Type type, BindingFlags bind, int np, Block cc, Type[] tt, MethodBase mi)
    {
      var pp = TypeHelper.GetParametersNoCopy(mi);
      var na = mi.IsStatic && (bind & (BindingFlags.Instance | BindingFlags.Static)) == BindingFlags.Instance ? 1 : 0;
      if (na == 1 && type.IsByRef && !pp[0].ParameterType.IsByRef) mb.Ldobj(pp[0].ParameterType); //value extensions
      for (int i = na; i < pp.Length; i++)
      {
        var pt = pp[i].ParameterType;
        if (i - na >= np)
        {
          if (pt.IsArray && pp[i].IsDefined(typeof(ParamArrayAttribute), false)) { mb.Ldc_I4(0); mb.Newarr(pt.GetElementType()); break; }
          ldconst(pp[i].DefaultValue, pt, cc); continue;
        }
        var pa = cc.Param();
        if (pt.IsByRef)
        {
          var re = pa.Take("ref") ? 4 | 2 | 1 : pa.Take("out") ? 8 | 4 | 1 : 0;
          var px = Parse(pa, pt.GetElementType(), re); if (pt != px) Error(0206, "A property, indexer or dynamic member access may not be passed as an out or ref parameter", pa);
          continue;
        }
        if (pt.IsArray && i == pp.Length - 1 && pp[i].IsDefined(typeof(ParamArrayAttribute), false))
          if (!(tt.Length == pp.Length && (tt[i] == pt || tt[i] == typeof(__null))))
          {
            pt = pt.GetElementType(); mb.Ldc_I4(tt.Length - i); mb.Newarr(pt);
            for (int k = 0; i < tt.Length; i++) { mb.Dup(); mb.Ldc_I4(k++); ParseStrong(pa, pt); mb.Stelem(pt); pa = cc.Param(); }
            break;
          }
        ParseStrong(pa, pt);
      }
    }
    static bool __isstat(MemberInfo t)
    {
      var fi = t as FieldInfo; if (fi != null) return fi.IsStatic;
      var mi = t as MethodInfo; if (mi != null) return mi.IsStatic;
      var pi = t as PropertyInfo; if (pi != null) return pi.CanRead ? pi.GetGetMethod(true).IsStatic : pi.GetSetMethod(true).IsStatic;
      return false;
    }
    static bool __canconv(Type a, Type b)
    {
      if (a == b) return true;
      if (a == typeof(__null) && !b.IsPrimitive) return true;
      if (a.IsSubclassOf(b)) return true;
      if (b == typeof(object)) return true;
      if (!a.IsPrimitive || !b.IsPrimitive) return false;
      return __implicitnum(a, b);
    }
    //int __match(ParameterInfo[] pp, Type[] tt, ref int maxperfect) //todo: remove, check get constructor
    //{
    //  int np = tt.Length;
    //  if (pp.Length < np) return 0;
    //  if (pp.Length > np && (pp[np].Attributes & ParameterAttributes.HasDefault) == 0) return 0;
    //  int perfect = 0, needconv = 0;
    //  for (int j = 0; j < np; j++)
    //  {
    //    var ta = tt[j]; var tb = pp[j].ParameterType;
    //    if (ta == tb) { perfect++; continue; }
    //    if (ta == typeof(__null) && !tb.IsPrimitive) { perfect++; continue; }
    //    if (typeof(Delegate).IsAssignableFrom(ta) && tb.IsSubclassOf(typeof(Delegate))) { needconv++; continue; }
    //    if (__canconv(ta, tb)) { needconv++; continue; }
    //    var me = opmeth(ta, tb, false);
    //    if (me != null) { needconv++; continue; }
    //    break;
    //  }
    //  if (perfect + needconv < np) return 0; if (perfect < maxperfect) return 0;
    //  maxperfect = perfect; return maxperfect == np ? 2 : 1;
    //}
    MethodBase GetMethodBase(Type type, string name, Type[] gen, Type[] tt, BindingFlags bind, Token tn, Block cc)
    {
      MethodBase meb = null, mef = null; int mefc = -1; int ncb = tt.Length + 2;
      var mm = name != null ? type.GetMember(name, MemberTypes.Method, bind) : type.GetConstructors();
      for (int j = 0; ;)
      {
        for (int i = 0; i < mm.Length; i++)
        {
          var me = (MethodBase)mm[i]; if (mef == null) mef = me;
          if (gen != null && !me.IsGenericMethod) continue;
          var pa = TypeHelper.GetParametersNoCopy(me); int nc = 0;
          if (pa.Length < tt.Length)
          {
            if (pa.Length == 0) continue; var pl = pa[pa.Length - 1];
            if (!pl.ParameterType.IsArray || !pl.IsDefined(typeof(ParamArrayAttribute), false)) continue; nc++;
          }
          if (pa.Length > tt.Length && (pa[tt.Length].Attributes & ParameterAttributes.HasDefault) == 0)
          {
            if (pa.Length != tt.Length + 1) continue; var pl = pa[pa.Length - 1];
            if (!pl.ParameterType.IsArray || !pl.IsDefined(typeof(ParamArrayAttribute), false)) continue; nc++;
          }
          if (mef == me) mefc = pa.Length; else if (mefc != pa.Length) mef = me;
          var ga = me.IsGenericMethod ? me.GetGenericArguments() : null;
          if (gen != null)
          {
            if (!__chkconst(ga, gen)) continue;
            me = ((MethodInfo)me).MakeGenericMethod(gen); pa = TypeHelper.GetParametersNoCopy(me);
          }
          else if (ga != null) nc++;
          int k = 0; var gg = ga != null && gen == null ? new Type[ga.Length] : null;
          for (; k < tt.Length; k++)
          {
            var a = tt[k]; var b = pa[Math.Min(k, pa.Length - 1)].ParameterType;

            if (a.IsByRef != b.IsByRef) break;
            if (a.IsByRef)
            {
              if (a == b) continue;
              if (b.ContainsGenericParameters && !b.IsGenericTypeDefinition)
              {
                a = a.GetElementType(); b = b.GetElementType();
                //if (a.IsArray != b.IsArray) { } //if (a.IsArray != b.IsArray) if(!b.IsGenericParameter) break;
                while (a.IsArray && b.IsArray) { a = a.GetElementType(); b = b.GetElementType(); }
                if (!__chkconst(b, a)) break;
                for (int v = 0; v < ga.Length; v++) if (ga[v].Name == b.Name) { gg[v] = a; break; }
                continue; //Array.Resize(ref T[],...
              }
              break;
            }

            if (pa.Length < tt.Length && k >= pa.Length - 1) b = b.GetElementType();
            if (pa.Length == tt.Length && k == pa.Length - 1 && !a.IsArray && b.IsArray && pa[k].IsDefined(typeof(ParamArrayAttribute), false))
            {
              nc++; if (a == typeof(__null)) continue; b = b.GetElementType();
            }

            if (b.IsGenericParameter)
            {
              if (a == typeof(__null)) break;
              if (!__chkconst(b, a)) break;
              if (gg != null) gg[b.GenericParameterPosition] = a; continue;
            }
            if (a == b) continue;
            //if (a == typeof(__null) && !b.IsPrimitive) continue;
            if (a == typeof(__null)) { if (b.IsValueType) break; continue; }
            if (b == typeof(object)) { nc++; continue; }
            if (a.IsPrimitive && b.IsPrimitive)
            {
              if (__implicitnum(a, b)) { nc++; continue; }
              if (__isnumeric(b)) { var pc = cc.GetParam(k - j); if (pc.Length == 1 && char.IsNumber(pc[0][0])) { nc++; continue; } }
            }
            if (a.IsPointer && b == typeof(void*)) { nc++; continue; }

            if (b.IsEnum && a == typeof(int)) { nc++; continue; } //just to keep it MS compatible, the error comes later if v != 0
            if (b.IsAssignableFrom(a)) { nc++; continue; }
            if (b.IsSubclassOf(typeof(Delegate)))
            {
              if (a == typeof(Delegate))
              {
                var pc = cc.GetParam(k - j); var bc = pc; var tc = pc.Next();
                //var tc = cc; var pc = tc; for (int t = 0; t <= k - j; t++) pc = tc.Param(); tc = pc.Next();
                if (!pc.Take("=>")) break; if (tc.StartsWith('(')) tc = tc.Trim();
                var iv = b.GetMethod("Invoke"); if (TypeHelper.GetParametersNoCopy(iv).Length != tc.ParamCount()) break;
                if (__isgen(iv.ReturnType))
                {
                  var ab = b.GetGenericArguments();
                  for (int v = 0; v < ga.Length; v++) if (gg[v] != null) for (int u = 0; u < ab.Length; u++) if (ga[v].Name == ab[u].Name) ab[u] = gg[v];
                  var bb = b.GetGenericTypeDefinition().MakeGenericType(ab);
                  var t3 = dbgstp; dbgstp = null; var t4 = dbgstk; dbgstk = null; var t5 = trace; trace = null; var t6 = nextid;
                  var rt = Parse(bc, bb); dbgstp = t3; dbgstk = t4; trace = t5; nextid = t6; //cc -> pc
                  if (rt == null) break;
                  var bp = bb.GetMethod("Invoke").ReturnType;
                  if (bp.IsGenericType)//bp.ContainsGenericParameters)
                  {
                    var aa = bp.GetGenericArguments();
                    var ra = rt.GetGenericArguments();
                    for (int u = 0; u < aa.Length; u++)
                      for (int v = 0; v < ga.Length; v++)
                        if (ga[v].Name == aa[u].Name) { gg[v] = ra[u]; break; }
                  }
                  else
                  {
                    for (int v = 0; v < ga.Length; v++) if (ga[v].Name == bp.Name) { gg[v] = rt; break; }
                  }
                }
                continue;
              }
              if (a.IsSubclassOf(typeof(Delegate)))
              {
                var tb = b.IsGenericType ? b.GetGenericTypeDefinition() : b;
                var ta = a.IsGenericType ? a.GetGenericTypeDefinition() : a;
                if (ta == tb) continue;

                var ia = a.GetMethod("Invoke"); var aa = TypeHelper.GetParametersNoCopy(ia);
                var ib = b.GetMethod("Invoke"); var bb = TypeHelper.GetParametersNoCopy(ib);
                if (aa.Length == bb.Length)
                {
                  int x = 0; for (; x < bb.Length && bb[x].ParameterType.IsOrIsSubclassOf(aa[x].ParameterType); x++) ;
                  if (x == bb.Length) continue;
                }
              }
            }
            if (b.IsInterface)
            {
              var tb = b.IsGenericType ? b.GetGenericTypeDefinition() : b;
              var ta = a.IsGenericType ? a.GetGenericTypeDefinition() : a; var c = a;
              if (ta != tb)
                foreach (var p in a.GetInterfaces())
                  if ((ta = (c = p).IsGenericType ? p.GetGenericTypeDefinition() : p) == tb)
                    break;
              if (ta == tb)
              {
                if (b.IsGenericType)
                {
                  if (ga == null) break;
                  var aa = c.GetGenericArguments();
                  var ab = b.GetGenericArguments();
                  for (int u = 0; u < aa.Length; u++)
                    for (int v = 0; v < ga.Length; v++)
                      if (ga[v].Name == ab[u].Name) { gg[v] = aa[u]; break; }
                }
                nc++;
                continue;
              }
            }
            if (opmeth(a, b, false) != null) { nc++; continue; }
            break;
          }
          if (k < tt.Length) continue;
          if (nc >= ncb) continue;
          if (gg != null) { if (Array.IndexOf(gg, null) != -1) continue; me = ((MethodInfo)me).MakeGenericMethod(gg); }
          meb = me; if (nc == 0) break; ncb = nc;
        }
        if (meb != null) break; if (j++ == 1) break;
        if ((bind & BindingFlags.Instance) == 0) break;
        if (name == null) break; mm = Extensions(name).ToArray(); if (mm.Length == 0) break;
        tt = Enumerable.Repeat(type, 1).Concat(tt).ToArray(); ncb = tt.Length + 2; //ncb++;
      }
      if (meb != null && meb.IsDefined(typeof(ExtensionAttribute), false)) reguse(meb.DeclaringType.Namespace);
      if (meb != null) return meb;
      if (mef != null) { Error(1502, "The best overloaded method match for '{1}' has some invalid arguments", tn, mef); return null; }
      if (name == null) { Error(1729, "'{1}' does not contain a constructor that takes {2} arguments", tn, type, tt.Length); return null; }
      Error(0117, "'{1}' does not contain a definition for '{0}'", tn, type); return null;
    }
    void reguse(string s)
    {
      if (!usingsuse.Contains(s)) usingsuse.Add(s);
    }
    static bool __isgen(Type t)
    {
      return t.ContainsGenericParameters && !t.IsGenericTypeDefinition; //return t.FullName == null; //if ((t.FullName == null) != (t.ContainsGenericParameters && !t.IsGenericTypeDefinition)) { }
    }
    //todo: complete... http://msdn.microsoft.com/query/dev10.query?appId=Dev10IDEF1&l=EN-US&k=k(SYSTEM.REFLECTION.GENERICPARAMETERATTRIBUTES);k(GENERICPARAMETERATTRIBUTES);k(DevLang-CSHARP)&rd=true
    static bool __chkconst(Type a, Type b)
    {
      if (b.IsByRef) return false;
      if (!a.IsGenericParameter) return false;
      var at = a.GenericParameterAttributes;
      if ((at & GenericParameterAttributes.ReferenceTypeConstraint) != 0) if (b.IsValueType) return false;  //where T : class
      if ((at & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0) if (!b.IsValueType) return false; //where T : struct
      //var xx = a.GetGenericParameterConstraints();
      return true;
    }
    static bool __chkconst(Type[] a, Type[] b)
    {
      if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (!__chkconst(a[i], b[i])) return false; return true;
    }
    IEnumerable<MethodInfo> Extensions(string name)
    {
      var tt = GetTypes();
      for (int i = 0; i < tt.Length; i++)
      {
        var t = tt[i]; if (!t.IsAbstract || !t.IsSealed || t.IsGenericType) continue;
        if (!t.IsDefined(typeof(ExtensionAttribute), false)) continue;
        if (!usings.Contains(t.Namespace)) continue;
        var mm = t.GetMember(name, MemberTypes.Method, BindingFlags.Static | BindingFlags.Public);
        for (int k = 0; k < mm.Length; k++)
        {
          var m = (MethodInfo)mm[k];
          if (!m.IsDefined(typeof(ExtensionAttribute), false)) continue;
          yield return m;
        }
      }
    }
    Type __cast(Type t)
    {
      switch (Type.GetTypeCode(t))
      {
        case TypeCode.Char: mb.Conv_U2(); break;
        case TypeCode.SByte: mb.Conv_I1(); break;
        case TypeCode.Byte: mb.Conv_U1(); break;
        case TypeCode.Int16: mb.Conv_I2(); break;
        case TypeCode.UInt16: mb.Conv_U2(); break;
        case TypeCode.Int32: mb.Conv_I4(); break;
        case TypeCode.UInt32: mb.Conv_U4(); break;
        case TypeCode.Int64: mb.Conv_I8(); break;
        case TypeCode.UInt64: mb.Conv_U8(); break;
        case TypeCode.Single: mb.Conv_R4(); break;
        case TypeCode.Double: mb.Conv_R8(); break;
      }
      return t;
    }
    Type ParseStrong(Block b, Type wt)
    {
      var t = Parse(b, wt); if (t == null || t == wt) return wt;
      if (__isgen(wt)) return t; return Cast(b, t, wt);
    }
    int shareid(object p, int id)
    {
      for (int i = 0; i < tokens.Count; i++) if (pmap[i] == p) return vmap[i];
      id = id | unchecked((int)0x80000000) | (nextid << 8); nextid++;
      return id;
    }
    void assig(int id, int loc)
    {
      int i = 0; for (int v; i < tokens.Count && !(((v = vmap[i]) >> 8) == id && (v & 0x8f) == 0x84); i++) ; //var u = vmap[i]; 
      if ((vmap[i] & 0x10) != 0) return; vmap[i] |= 0x10;
      if (dbgstk != null) { mb.Ldc_I4(id); mb.Ldloca(mb.DeclareLocal(typeof(long))); mb.Ldloca(loc); mb.Call(dbgstk); }
    }
    static bool __isprimitiv(Type t)
    {
      if (t.IsPrimitive || t.IsPointer) return true; if (!t.IsValueType) return false;
      var a = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      for (int i = 0; i < a.Length; i++) if (!__isprimitiv(a[i].FieldType)) return false;
      return true;
    }
    void boxopt()
    {
      int x = head, y = 0;
      for (int i = 0, d = 0, i1 = 0, i2 = 0; i < nglob; i++)
      {
        var ab = stack[i]; if (vmap[ab.a] != 0x86) continue;
        var t1 = (Type)pmap[ab.a]; if (t1 != typeof(float) ^ d == 0) continue;
        var t2 = __typeopt(t1); if (t2 != typeof(int)) continue;
        vmap[ab.a] = 0x86 | (((x << 12) | y++) << 8);
        if (d == 0) { i1 = i; i = i2; } else { i2 = i; i = i1; }
        d ^= 1;
      }
      for (int i = 0; i < nglob; i++)
      {
        var ab = stack[i]; if (vmap[ab.a] != 0x86) continue;
        var t2 = __typeopt((Type)pmap[ab.a]); if (t2 != typeof(int)) continue;
        vmap[ab.a] = 0x86 | (((x << 12) | y++) << 8);
      }
      if (y != 0) { x++; y = 0; }
      for (int i = 0; i < nglob; i++)
      {
        var ab = stack[i]; if (vmap[ab.a] != 0x86) continue;
        var t1 = (Type)pmap[ab.a]; if (!t1.IsValueType) continue;
        var t2 = __typeopt(t1);
        for (int k = i; k < nglob; k++)
        {
          ab = stack[k]; if (vmap[ab.a] != 0x86) continue;
          if (__typeopt((Type)pmap[ab.a]) != t2) continue;
          vmap[ab.a] = 0x86 | (((x << 12) | y++) << 8);
        }
        x++; y = 0;
      }
      boxc1 = x;
      for (int i = 0; i < nglob; i++)
      {
        var ab = stack[i]; if (vmap[ab.a] != 0x86) continue;
        vmap[ab.a] = 0x86 | (((x++ << 12) | 0x0fff) << 8);
      }
      boxc2 = x;
    }
    static Type __typeopt(Type type)
    {
      switch (Type.GetTypeCode(type))
      {
        case TypeCode.Boolean:
        case TypeCode.SByte:
        case TypeCode.Byte: return typeof(byte);
        case TypeCode.Char:
        case TypeCode.Int16:
        case TypeCode.UInt16: return typeof(short);
        case TypeCode.Int32:
        case TypeCode.UInt32:
        case TypeCode.Single: return typeof(int);
        case TypeCode.Int64:
        case TypeCode.UInt64:
        case TypeCode.Double: return typeof(long);
      }
      return type;
    }
    struct Token
    {
      int i, n;
      public Token(int i, int n) { this.i = i; this.n = n; }
      public override string ToString() { return compiler.code.Substring(i, n); }
      public char this[int x] { get { return (uint)x < (uint)n ? compiler.code[i + x] : '\0'; } }
      public bool Equals(char c) { return n == 1 && compiler.code[i] == c; }
      public bool Equals(string s) { return s.Length == n && string.CompareOrdinal(s, 0, compiler.code, i, n) == 0; }
      public bool Equals(Token m) { return m.n == n && string.CompareOrdinal(compiler.code, m.i, compiler.code, i, n) == 0; }
      public bool StartsWith(string s, int x = 0) { return s.Length - x >= n && string.CompareOrdinal(s, x, compiler.code, i, n) == 0; }
      public Token SubToken(int i, int n) { return new Token(this.i + i, n); }
      public int IndexOf(char c) { var x = compiler.code.IndexOf(c, i, n); return x >= 0 ? x - i : x; }
      public bool Contains(char c) { return IndexOf(c) != -1; }
      public int Position { get { return i; } }
      public int Length { get { return n; } }
      public Token End() { return new Token(i + n, 0); }
      public Token Start() { return new Token(i, 0); }
      internal bool IsWord { get { var c = this[0]; return c == '_' || char.IsLetter(c); } }
      internal bool IsString { get { return this[0] == '"'; } }
      internal bool IsChar { get { return this[0] == '\''; } }
      internal string GetString()
      {
        var t = SubToken(1, n - 2);
        if (i != 0 && compiler.code[i - 1] == '@')
          return t.ToString().Replace("\"\"", "\"").Replace("\n", "\r\n");
        if (t.IndexOf('\\') == -1) return t.ToString();
        var sb = new StringBuilder(t.Length + 1);
        for (int k = 0; k < t.Length;)
        {
          var c = t[k++]; if (c != '\\') { sb.Append(c); continue; }
          switch (c = t[k++])
          {
            case '\'': c = '\''; break;
            case '\"': c = '\"'; break;
            case '\\': c = '\\'; break;
            case '0': c = '\0'; break;
            case 'a': c = '\a'; break;
            case 'b': c = '\b'; break;
            case 'f': c = '\f'; break;
            case 'n': c = '\n'; break;
            case 'r': c = '\r'; break;
            case 't': c = '\t'; break;
            case 'v': c = '\v'; break;
            case 'u':
              {
                var tt = t.SubToken(k, Math.Min(4, t.Length - k)); k += tt.Length;
                if (tt.Length == 4) { ushort u; if (ushort.TryParse(tt.ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out u)) { c = (char)u; break; } }
                compiler.Error(1009, "Unrecognized escape sequence", tt.SubToken(-1, tt.Length + 1));
              }
              return null;
            default: compiler.Error(1009, "Unrecognized escape sequence", t.SubToken(k - 1, 1)); return null;
          }
          sb.Append(c);
        }
        return sb.ToString();
      }
      internal char GetChar()
      {
        if (n == 3) return this[1]; var s = GetString(); if (s == null) return '\0'; if (s.Length == 1) return s[0];
        if (s.Length > 1) compiler.Error(1012, "Too many characters in character literal", this); else compiler.Error(1011, "Empty character literal", this); return '\0';
      }
      internal Type DefType()
      {
        if (Equals("void")) return typeof(void);
        if (Equals("bool")) return typeof(bool);
        if (Equals("char")) return typeof(char);
        if (Equals("sbyte")) return typeof(sbyte);
        if (Equals("byte")) return typeof(byte);
        if (Equals("int")) return typeof(int);
        if (Equals("uint")) return typeof(uint);
        if (Equals("short")) return typeof(short);
        if (Equals("ushort")) return typeof(ushort);
        if (Equals("long")) return typeof(long);
        if (Equals("ulong")) return typeof(ulong);
        if (Equals("decimal")) return typeof(decimal);
        if (Equals("float")) return typeof(float);
        if (Equals("double")) return typeof(double);
        if (Equals("string")) return typeof(string);
        if (Equals("object")) return typeof(object);
        if (Equals("dynamic")) return typeof(dynamic);
        return null;
      }
    }
    class dynamic { }
    internal static Type filterdyn(Type t) { return t == typeof(dynamic) ? typeof(object) : t; }
    struct Block
    {
      int i, n;
      public override string ToString() { return string.Join(" ", compiler.tokens.Skip(i).Take(n).Select(t => t.ToString())); }
      internal Block(int i, int n) { this.i = i; this.n = n; }
      internal Block SubBlock(int i, int n) { return new Block(this.i + i, n); }
      internal int Length { get { return n; } }
      internal int Position { get { return i; } }
      internal Token this[int x] { get { return compiler.tokens[i + x]; } }
      internal bool Take(char c)
      {
        if (n != 0 && this[0].Equals(c)) { i++; n--; return true; }
        return false;
      }
      internal bool Take(string s)
      {
        if (n != 0 && this[0].Equals(s)) { i++; n--; return true; }
        return false;
      }
      internal Token Take()
      {
        var t = this[0]; if (n == 0) return t.Start();
        i += 1; this.n -= 1; return t;
      }
      internal Block Next(int m = 0x7)
      {
        int i = 0;
        if (StartsWith("new") || StartsWith("stackalloc"))
        {
          for (; i < this.n; i++) { var c = this[i][0]; if (c == '(' || c == '[' || c == '{') { i--; break; } }
        }
        else
        {
          for (int k = 0; i < this.n; i++)
          {
            var t = this[i]; var c = t.Length == 1 ? t[0] : '\0';
            if (c == '{' || c == '(' || c == '[' || ((m & 8) != 0 && c == '<')) { k++; continue; }
            if (i == 0) break;
            if (c == '}' || c == ')' || c == ']' || ((m & 8) != 0 && c == '>')) if (--k == 0) break;
          }
        }
        i = Math.Min(i + 1, this.n); var b = this; this.i += i; this.n -= i; b.n = i; return b;
      }
      internal Block Trim()
      {
        if (n == 0) return this;
        if (n == 1) return SubBlock(1, n - 1);
        if (this[0].Equals('(') && !this[n - 1].Equals(')')) return SubBlock(1, n - 1);
        if (this[0].Equals('[') && !this[n - 1].Equals(']')) return SubBlock(1, n - 1);
        if (this[0].Equals('{') && !this[n - 1].Equals('}')) return SubBlock(1, n - 1);
        return SubBlock(1, n - 2);
      }
      internal int ParamCount(char x = ',', int f = 0x7)
      {
        int c = n != 0 ? 1 : 0; for (var t = this; t.n != 0;) if (t.Take(x)) c++; else t.Next(f); return c;
      }
      internal Block Param(char x = ',', int f = 0x7)
      {
        var a = this; for (; n != 0; Next(f)) if (Take(x)) return new Block(a.i, this.i - 1 - a.i); return a;
      }
      internal Block GetParam(int i)
      {
        var cc = this; var pc = cc; for (int t = 0; t <= i; t++) pc = cc.Param(); return pc;
      }
      internal Token ParamName()
      {
        return n > 1 ? this[--n] : n > 0 ? this[0].End() : this[0].Start();
      }
      internal Token Last()
      {
        return n != 0 ? this[n - 1] : this[0].End();
      }
      internal Block Last(char c)
      {
        int pos = -1; var a = this; // a = b = c => a = b | c
        for (; a.Length != 0;) { var b = a.Next(); if (b.StartsWith(c)) { pos = b[0].Position; } }
        if (pos != -1)
        {
          var x = 0; for (; this[x].Position != pos; x++) ;
          a = SubBlock(0, x++); var b = SubBlock(x, Length - x); this = a; return b;
        }
        a = this; this = SubBlock(Length, 0); return a;
      }
      internal bool StartsWith(string s) { return n != 0 && this[0].Equals(s); }
      internal bool StartsWith(char c) { return n != 0 && this[0].Equals(c); }
      internal bool EndsWith(char c) { return n != 0 && this[n - 1].Equals(c); }
      public static implicit operator Token(Block b)
      {
        return new Token(b[0].Position, b.n > 0 ? b[b.n - 1].Position + b[b.n - 1].Length - b[0].Position : 0);
      }
      public bool Equals(string s) { return n == 1 && this[0].Equals(s); }
      internal Block TakeType()
      {
        var b = this;
        for (; ; )
        {
          var a = b.Next();
          if (a.Length != 1 || !a[0].IsWord) break;
          if (b.Take('.')) continue;
          if (b.StartsWith('<')) b.Next(0xf);
          //if (b.StartsWith('[')) b.Next();
          while (b.StartsWith('[')) b.Next();
          while (b.Take('*')) { }
          if (b.Take('?')) { }
          if (!b[0].IsWord) break;
          var x = 0; for (var p = b[0].Position; this[x].Position != p; x++) ;
          a = new Block(i, x); this = b; return a;
        }
        return new Block(i, 0);
      }
    }
    internal struct map
    {
      internal int i, n, v; internal object p;
      internal string ToString(string fmt) { return compiler.code.Substring(i, n); }
#if(DEBUG)
      public override string ToString() { return ToString(null) + " 0x" + v.ToString("X8") + " " + p; }
#endif
    }
    void MakeTokens()
    {
      var sign = true;
      for (int i = 0, n = code.Length; i < n; i++)
      {
        var a = code[i]; if (a <= ' ') continue;
        if (char.IsLetter(a) || a == '_')
        {
          int k = i + 1; for (char c; k < n && (char.IsLetterOrDigit(c = code[k]) || c == '_'); k++) ;
          tokens.Add(new Token(i, k - i)); i = k - 1; sign = false; continue;
        }
        if (a == '"' || a == '\'')
        {
          int k = i + 1;
          for (; k < n; k++)
          {
            if (code[k] < ' ') { compiler.Error(1010, "Newline in constant", new Token(i, k - i)); break; }
            if (code[k] == '\\') { k++; continue; }
            if (code[k] == a) { tokens.Add(new Token(i, k - i + 1)); break; }
          }
          i = k; sign = false; continue;
        }
        var b = i + 1 < n ? code[i + 1] : '\0';
        if (a == '@' && b == '"')
        {
          int k = i + 2;
          for (; k < n; k++) { if (code[k] != '"') continue; if (k + 1 < n && code[k + 1] == '"') { k++; continue; } break; }
          if (k == n) { Error(1002, "; expected", new Token(i, k - i)); break; }
          tokens.Add(new Token(i + 1, k - i)); i = k; continue;
        }
        if (a == '<') //inline xml
        {
          char c = '='; if (tokens.Count != 0) { var e = tokens[tokens.Count - 1]; c = e[e.Length - 1]; }
          if ("=+(,;}?".Contains(c))
          {
            int k = i, j = i, toc = tokens.Count;
            for (int z = 0, lz; j < n; j++)
            {
              if (code[j] == '<')
              {
                for (k = j + 1; k < n && code[k] != '>'; k++) ;
                lz = z; if (k == n) { for (; code[k - 1] <= ' '; k--) ; Error(0000, "Expecting {1}", new Token(k - 1, 1), ">"); break; }
                /////////
                int u = j + 1, v = k - 1;
                if (code[u] == '!')
                {
                  if (code[u + 1] != '-' || code[u + 2] != '-') Error(0000, "Expecting {1}", new Token(j, 1), "<--");
                  for (; k < n && (code[k] != '>' || code[k - 1] != '-' || code[k - 2] != '-'); k++) ;
                  if (k == n) { Error(0000, "Expecting {1}", new Token(v, 1), "-->"); j = v + 2; continue; }
                  j = k++; continue;
                }
                if (code[u] == '/') { u++; z--; } else if (code[v] != '/') { z++; } else { v--; }
                int w = u; for (; u <= v && (char.IsLetterOrDigit(c = code[u]) || c == '_'); u++) ;
                if (u == w || char.IsDigit(code[w])) Error(0000, "Expecting valid start name character", new Token(w, 1));
                var tk = new Token(w, u - w); if (z > lz) tokens.Add(tk);
                else if (z < lz && tokens.Count > toc)
                {
                  var ak = tokens[tokens.Count - 1]; tokens.RemoveRange(tokens.Count - 1, 1);
                  if (!ak.Equals(tk)) Error(0000, "Expecting end tag </{1}>", tk, ak.ToString());
                }
                var nc = tokens.Count;
                for (; u <= v;)
                {
                  for (; u <= v && code[u] <= ' '; u++) ; if (u > v) break;
                  w = u; for (; u <= v && (char.IsLetterOrDigit(c = code[u]) || c == '_' || c == '-'); u++) ; tk = new Token(w, u - w);
                  if (z < lz) { Error(0000, "Expecting '>'", tk); break; }
                  if (u == w || char.IsDigit(code[w])) Error(0000, "Expecting valid start name character", new Token(w, 1));
                  for (var r = nc; r < tokens.Count; r++) if (tokens[r].Equals(tk)) Error(0000, "Duplicate attribute", tk); tokens.Add(tk);
                  for (; u <= v && code[u] <= ' '; u++) ;
                  if (code[u++] != '=') { Error(0000, "Missing attribute value", tk); break; }
                  for (; u <= v && code[u] <= ' '; u++) ;
                  if (code[u++] != '"') { Error(0000, "Missing attribute value", tk); break; }
                  for (; u <= v && code[u] != '"'; u++) __chkxml(ref u, v);
                  if (code[u++] != '"') { Error(0000, "Missing attribute value", tk); break; }
                  if (u <= v && code[u] > ' ') { Error(0000, "Missing required whitespace", new Token(u, 1)); break; }
                }
                tokens.RemoveRange(nc, tokens.Count - nc);
                ///////// 
                j = k++; continue;
              }
              if (code[j] <= ' ') continue; if (z <= 0) break; __chkxml(ref j, n);
            }
            if (tokens.Count != toc)
            {
              for (; code[k - 1] <= ' '; k--) ; Error(0000, "Expecting end tag </{1}>", new Token(k - 1, 1), tokens[tokens.Count - 1].ToString());
              tokens.RemoveRange(toc, tokens.Count - toc);
            }
            tokens.Add(new Token(i, k - i)); i = k - 1;
            continue;
          }
        }

        if (a == '/' && b == '/') { for (i += 2; i < n && !(code[i] == '\n'); i++) ; continue; }
        if (a == '/' && b == '*') { for (i += 3; i < n && !(code[i] == '/' && code[i - 1] == '*'); i++) ; continue; }
        if (char.IsDigit(a) || (a == '.' && char.IsDigit(b)) || (sign && (a == '+' || a == '-') && ((b == '.') || char.IsDigit(b)) && !(b == '0' && i + 2 < n && (code[i + 2] | 0x20) == 'x')))
        {
          var k = i + 1;
          for (char c; k < n && (char.IsLetterOrDigit(c = code[k]) || c == '.' || ((c == '+' || c == '-') && ((code[k - 1] | 0x20) == 'e'))); k++) ;
          tokens.Add(new Token(i, k - i)); i = k - 1; sign = false; continue;
        }
        if ((a == '=' || a == '-') && b == '>') { tokens.Add(new Token(i++, 2)); sign = false; continue; }
        if (b == '=' && (a == '=' || a == '+' || a == '-' || a == '*' || a == '/' || a == '!' || a == '^' || a == '|' || a == '&' || a == '<' || a == '>'))
        {
          tokens.Add(new Token(i++, 2)); sign = true; continue;
        }
        if (a == b && (a == '+' || a == '-' || a == '?' || a == '|' || a == '&' || a == '<' || a == '>'))
        {
          var d = (a == '<' || a == '>') && i + 2 < n && code[i + 2] == '=' ? 3 : 2; if (d == 2 && a == '>') d = 1;
          tokens.Add(new Token(i, d)); i += d - 1; sign = true; continue;
        }
        tokens.Add(new Token(i, 1)); if (a == ')') sign = false; else sign = true;
      }
      tokens.Add(new Token(code.Length, 0)); sign = false;
    }
    void MakeBlocks(Block block, bool outer)
    {
      for (int i = 0, k, l1, l2, bn; i < block.Length; i = k)
      {
        for (k = i, l1 = 0, l2 = 0, bn = 0; ; k++)
        {
          if (k == block.Length) { blocks.Add(block.SubBlock(i, k - i)); if (outer) Error(1002, "; expected", block[k - 1].End()); break; }
          var t = block[k];
          if (t.Equals('{')) { l1++; continue; }
          if (t.Equals('}'))
          {
            if (l1 == 0) { Error(1525, "Invalid expression term '{0}'", t); continue; }
            l1--;
            if (l1 == 0 && l2 == 0 && bn == 0) { if (__elseblock(block, i, k)) continue; blocks.Add(block.SubBlock(i, ++k - i)); break; }
            continue;
          }
          if (l1 != 0) continue;
          if (t.Equals('(')) { l2++; continue; }
          if (t.Equals(')')) { if (l2 == 0) { Error(1525, "Invalid expression term '{0}'", t); continue; } l2--; continue; }
          if (l2 != 0) continue;
          if (t.Equals(';')) { if (k > i) { if (__elseblock(block, i, k)) continue; blocks.Add(block.SubBlock(i, k++ - i)); } else k++; break; }
          if (t.Equals("new") || t.Equals("stackalloc")) bn = 1;
          if (i == k && t[0] == '<' && t[t.Length - 1] == '>') { k++; break; } //inline xml
        }
      }
    }
    static bool __elseblock(Block block, int i, int k)
    {
      return block[k + 1].Equals("else") && !(block[i].Equals("if") || block[i].Equals("else"));
    }
    internal Type[] GetTypes()
    {
      if (types != null) return types;
      types = TypeHelper.cache.Select(p => p.Target).OfType<Type[]>().FirstOrDefault();
      if (types == null) TypeHelper.cache.Add(new WeakReference(types = TypeHelper.Assemblys.SelectMany(a => a.GetTypes()).Where(t => t.IsPublic && !t.IsNested && t.Namespace != null).ToArray()));
      return types;
    }

    string GetNameSpace(string a, Token b, bool load)
    {
      var tt = GetTypes(); var t = -1;
      for (int i = 0, i1 = a != null ? a.Length + 1 : 0, i2 = i1 + b.Length; i < tt.Length; i++)
      {
        var s = tt[i].Namespace;
        if (s.Length < i2) continue;
        if (a != null && s[i1 - 1] != '.') continue;
        if (a != null && !s.StartsWith(a)) continue;
        if (!b.StartsWith(s, i1)) continue;
        if (s.Length > i2) { if (s[i2] == '.') t = i; continue; }
        return s;
      }
      if (t != -1) { if (a == null) return null; return tt[t].Namespace.Substring(0, a.Length + 1 + b.Length); }
      if (!load) return null;

      //foreach (var x in
      //  Assembly.GetEntryAssembly().GetReferencedAssemblies().Concat(Assembly.GetExecutingAssembly().GetReferencedAssemblies()).Distinct().
      //  Where(p => !Assemblys.Any(y => y.FullName == p.FullName))) { }

      //var ra = Assembly.GetEntryAssembly().GetReferencedAssemblies();
      var ra = Assembly.GetEntryAssembly().GetReferencedAssemblies().Concat(Assembly.GetExecutingAssembly().GetReferencedAssemblies());
      var an = ra.FirstOrDefault(p => !TypeHelper.Assemblys.Any(x => x.FullName == p.FullName));
      if (an != null) { Assembly.Load(an); TypeHelper.Assemblys = null; cacheremove(types); types = null; return GetNameSpace(a, b, load); }
      return null;
    }
    static void cacheremove(object p)
    {
      var cache = TypeHelper.cache;
      for (int i = cache.Count - 1; i >= 0; i--) if (cache[i].Target == p) { cache.RemoveAt(i); return; }
    }
    static Type GetDelegateType(DynamicMethod dm)
    {
      //var pp = dm.GetParameters(); var np = pp.Length; 
      //var tt = new Type[np]; for (int i = 1; i < np; i++) tt[i - 1] = pp[i].ParameterType; tt[np - 1] = dm.ReturnType;
      //var t = Expression.GetDelegateType(tt); return t;
      var pp = TypeHelper.GetParams(dm);
      var np = pp.Length; var tt = new Type[np];
      for (int i = 1; i < np; i++) tt[i - 1] = pp[i]; tt[np - 1] = dm.ReturnType;
      var t = Expression.GetDelegateType(tt); return t;
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
    void __chkxml(ref int i, int n)
    {
      var c = code[i];
      if (c == '<') { Error(0000, "Character '{0}' is illegal", new Token(i, 1)); return; }
      if (c != '&') return;
      int x = ++i; for (; i <= n && code[i] != ';'; i++) ;
      int d = i - x; if (code[i] != ';' || d > 5) { i = x; Error(0000, "Expecting '{1}'", new Token(x, 1), ";"); return; }
      if (code[x] == '#') { for (x++; x < i; x++) if (!char.IsNumber(code[x])) Error(0000, "Character '{0}' is illegal", new Token(x, 1)); return; }
      if (d == 2 && string.CompareOrdinal("lt", 0, code, x, d) == 0) return;
      if (d == 2 && string.CompareOrdinal("gt", 0, code, x, d) == 0) return;
      if (d == 3 && string.CompareOrdinal("amp", 0, code, x, d) == 0) return;
      if (d == 4 && string.CompareOrdinal("quot", 0, code, x, d) == 0) return;
      if (d == 4 && string.CompareOrdinal("apos", 0, code, x, d) == 0) return;
      Error(0000, "Entity '{0}' not defined", new Token(x, d));
    }
    void Warning(int id, string s, Token t, object p = null)
    {
      maxerror = Math.Max(maxerror, 1);
      errors.Add(new map { v = (id << 8) | 0, i = t.Position, n = t.Length, p = new object[] { s, p, null } });
    }
    void Error(int id, string s, Token t, object p1 = null, object p2 = null)
    {
      maxerror = Math.Max(maxerror, 4);
      errors.Add(new map { v = (id << 8) | 4, i = t.Position, n = t.Length, p = new object[] { s, p1, p2 } });
    }
  }

  unsafe class ILGenerator
  {
    DynamicILInfo il; List<object> tokens;
    int curstack, maxstack, maxlabel;
    List<byte> code = new List<byte>();
    List<int> labels = new List<int>(), excepts = new List<int>(), temps = new List<int>(), lines = new List<int>();
    List<Type> locals = new List<Type>(); List<int> pins = new List<int>();
    public string IlCode
    {
      get
      {
        var sb = new StringBuilder(); var nf = CultureInfo.InvariantCulture.NumberFormat;
        for (int i = 0; i < code.Count;)
        {
          for (int t = 1, k = 0; t < excepts.Count; t += 6)
          {
            if (excepts[t + 1] == i) { if (k++ == 0) sb.AppendLine(".try {"); continue; }
            if (excepts[t + 3] == i)
            {
              if (excepts[t] == 2) sb.Append(".finally {"); else sb.AppendFormat(".catch {0} {{", stoken(excepts[t + 5]));
              sb.AppendLine(); continue;
            }
            if (excepts[t + 3] + excepts[t + 4] == i) { sb.AppendLine(".}"); continue; }
          }

          var c = code[i]; sb.AppendFormat("{0,6:X6} ", i++);
          if (is_jmp1(c))
          {
            sb.AppendFormat("{0}.s {1,6:X6}", ctex(to_jmp4(c)), getjmp(i - 1));
            i += 1; sb.AppendLine(); continue;
          }
          if (is_jmp4(c))
          {
            var t = readi4(i); sb.AppendFormat("{0} {1,6:X6}", ctex(c), (t & 0xf0000000) != 0x40000000 ? getjmp(i - 1) : 0);
            i += 4; sb.AppendLine(); continue;
          }
          switch (c)
          {
            case 0x00: sb.Append("nop"); break;
            //case 0x01: sb.Append("break"); break;
            case 0x02:
            case 0x03:
            case 0x04:
            case 0x05: sb.AppendFormat(nf, "ldarg.{0}", c - 0x02); break;
            case 0x06:
            case 0x07:
            case 0x08:
            case 0x09: sb.AppendFormat(nf, "ldloc.{0}", c - 0x06); break;
            case 0x0A:
            case 0x0B:
            case 0x0C:
            case 0x0D: sb.AppendFormat(nf, "stloc.{0}", c - 0x0A); break;
            case 0x0F: sb.AppendFormat(nf, "ldarga.s {0}", code[i]); i += 1; break;
            case 0x10: sb.AppendFormat(nf, "starg.s {0}", code[i]); i += 1; break;
            case 0x11: sb.AppendFormat(nf, "ldloc.s {0}", code[i]); i += 1; break;
            case 0x12: sb.AppendFormat(nf, "ldloca.s {0}", code[i]); i += 1; break;
            case 0x13: sb.AppendFormat(nf, "stloc.s {0}", code[i]); i += 1; break;
            case 0x14: sb.Append("ldnull"); break;
            case 0x0E: sb.AppendFormat(nf, "ldarg.s {0}", code[i]); i += 1; break;
            case 0xD0: sb.AppendFormat("ldtoken {0}", stoken(readi4(i))); i += 4; break;
            case 0x15: sb.AppendFormat(nf, "ldc.i4.m1 {0}", -1); break;
            case 0x16:
            case 0x17:
            case 0x18:
            case 0x19:
            case 0x1A:
            case 0x1B:
            case 0x1C:
            case 0x1D:
            case 0x1E: sb.AppendFormat(nf, "ldc.i4.{0}", c - 0x16); break;
            case 0x1F: sb.AppendFormat(nf, "ldc.i4.s {0}", (sbyte)code[i]); i += 1; break;
            case 0x20: sb.AppendFormat(nf, "ldc.i4 {0}", readi4(i)); i += 4; break;
            case 0x21: sb.AppendFormat(nf, "ldc.i8 {0}", readi8(i)); i += 8; break;
            case 0x22: sb.AppendFormat(nf, "ldc.r4 {0}f", readr4(i)); i += 4; break;
            case 0x23: sb.AppendFormat(nf, "ldc.r8 {0}", readr8(i)); i += 8; break;
            case 0x25: sb.Append("dup"); break;
            case 0x26: sb.Append("pop"); break;
            case 0x28: sb.AppendFormat("call {0}", stoken(readi4(i))); i += 4; break;
            case 0x29: sb.Append("calli"); i += 4; break; //sb.AppendFormat("calli {0}", readi4(i)); i += 4; break;
            case 0x2A: sb.Append("ret"); break;
            case 0x4D: sb.Append("ldind.i"); break;
            case 0x46: sb.Append("ldind.i1"); break;
            case 0x47: sb.Append("ldind.u1"); break;
            case 0x48: sb.Append("ldind.i2"); break;
            case 0x49: sb.Append("ldind.u2"); break;
            case 0x4A: sb.Append("ldind.i4"); break;
            case 0x4B: sb.Append("ldind.u4"); break;
            case 0x4C: sb.Append("ldind.i8"); break;
            case 0x4E: sb.Append("ldind.r4"); break;
            case 0x4F: sb.Append("ldind.r8"); break;
            case 0x51: sb.Append("stind.ref"); break;
            case 0x52: sb.Append("stind.i1"); break;
            case 0x53: sb.Append("stind.i2"); break;
            case 0x54: sb.Append("stind.i4"); break;
            case 0x55: sb.Append("stind.i8"); break;
            case 0x56: sb.Append("stind.r4"); break;
            case 0x57: sb.Append("stind.r8"); break;
            case 0x58: sb.Append("add"); break;
            case 0x59: sb.Append("sub"); break;
            case 0x5A: sb.Append("mul"); break;
            case 0x5B: sb.Append("div"); break;
            case 0x5D: sb.Append("rem"); break;
            case 0x5F: sb.Append("and"); break;
            case 0x60: sb.Append("or"); break;
            case 0x61: sb.Append("xor"); break;
            case 0x62: sb.Append("shl"); break;
            case 0x63: sb.Append("shr"); break;
            case 0x65: sb.Append("neg"); break;
            case 0x66: sb.Append("not"); break;
            case 0x67: sb.Append("conv.i1"); break;
            case 0x68: sb.Append("conv.i2"); break;
            case 0x69: sb.Append("conv.i4"); break;
            case 0x6A: sb.Append("conv.i8"); break;
            case 0x6B: sb.Append("conv.r4"); break;
            case 0x6C: sb.Append("conv.r8"); break;
            case 0x6D: sb.Append("conv.u4"); break;
            case 0x6E: sb.Append("conv.u8"); break;
            case 0x6F: sb.AppendFormat("callvirt {0}", stoken(readi4(i))); i += 4; break;
            case 0x71: sb.AppendFormat("ldobj {0}", stoken(readi4(i))); i += 4; break;
            case 0x72: sb.AppendFormat("ldstr '{0}'", stoken(readi4(i))); i += 4; break;
            case 0x73: sb.AppendFormat("newobj {0}", stoken(readi4(i))); i += 4; break;
            case 0x74: sb.AppendFormat("castclass {0}", stoken(readi4(i))); i += 4; break;
            case 0x75: sb.AppendFormat("isinst {0}", stoken(readi4(i))); i += 4; break;
            case 0x76: sb.Append("conv.r.un"); break;
            case 0x79: sb.AppendFormat("unbox {0}", stoken(readi4(i))); i += 4; break;
            case 0x7A: sb.Append("throw"); break;
            case 0x7B: sb.AppendFormat("ldfld '{0}'", stoken(readi4(i))); i += 4; break;
            case 0x7C: sb.AppendFormat("ldflda '{0}'", stoken(readi4(i))); i += 4; break;
            case 0x7D: sb.AppendFormat("stfld '{0}'", stoken(readi4(i))); i += 4; break;
            case 0x7E: sb.AppendFormat("ldsfld '{0}'", stoken(readi4(i))); i += 4; break;
            case 0x7F: sb.AppendFormat("ldsflda '{0}'", stoken(readi4(i))); i += 4; break;
            case 0x80: sb.AppendFormat("stsfld '{0}'", stoken(readi4(i))); i += 4; break;
            case 0x81: sb.AppendFormat("stobj '{0}'", stoken(readi4(i))); i += 4; break;
            case 0x82: sb.Append("conv.ovf.i1.un "); break;
            case 0x83: sb.Append("conv.ovf.i2.un"); break;
            case 0x84: sb.Append("conv.ovf.i4.un"); break;
            case 0x85: sb.Append("conv.ovf.i8.un"); break;
            case 0x8A: sb.Append("conv.ovf.i.un"); break;
            case 0x8C: sb.AppendFormat("box {0}", stoken(readi4(i))); i += 4; break;
            case 0x8D: sb.AppendFormat("newarr {0}", stoken(readi4(i))); i += 4; break;
            case 0x8E: sb.Append("ldlen"); break;
            case 0x8F: sb.AppendFormat("ldelema '{0}'", stoken(readi4(i))); i += 4; break;
            case 0x90: sb.Append("ldelem.i1"); break;
            case 0x91: sb.Append("ldelem.u1"); break;
            case 0x92: sb.Append("ldelem.i2"); break;
            case 0x93: sb.Append("ldelem.u2"); break;
            case 0x94: sb.Append("ldelem.i4"); break;
            case 0x95: sb.Append("ldelem.u4"); break;
            case 0x96: sb.Append("ldelem.i8"); break;
            case 0x98: sb.Append("ldelem.r4"); break;
            case 0x99: sb.Append("ldelem.r8"); break;
            case 0x9A: sb.Append("ldelem.ref"); break;
            case 0x9C: sb.Append("stelem.i1"); break;
            case 0x9D: sb.Append("stelem.i2"); break;
            case 0x9E: sb.Append("stelem.i4"); break;
            case 0x9F: sb.Append("stelem.i8"); break;
            case 0xA0: sb.Append("stelem.r4"); break;
            case 0xA1: sb.Append("stelem.r8"); break;
            case 0xA2: sb.Append("stelem.ref"); break;
            case 0xA3: sb.AppendFormat("ldelem '{0}'", stoken(readi4(i))); i += 4; break;
            case 0xA4: sb.AppendFormat("stelem '{0}'", stoken(readi4(i))); i += 4; break;
            case 0xA5: sb.AppendFormat("unbox.any {0}", stoken(readi4(i))); i += 4; break;
            case 0xB3: sb.Append("conv.ovf.i1"); break;
            case 0xB5: sb.Append("conv.ovf.i2"); break;
            case 0xB7: sb.Append("conv.ovf.i4"); break;
            case 0xB9: sb.Append("conv.ovf.i8"); break;
            case 0xC6: sb.AppendFormat("mkrefany {0}", stoken(readi4(i))); i += 4; break;
            case 0xD1: sb.Append("conv.u2"); break;
            case 0xD2: sb.Append("conv.u1"); break;
            case 0xD3: sb.Append("conv.i"); break;
            case 0xD4: sb.Append("conv.ovf.i"); break;
            case 0xDC: sb.Append("endfinally"); break;
            case 0xDF: sb.Append("stind.i"); break;
            case 0xE0: sb.Append("conv.u"); break;
            case 0xFE:
              switch (code[i++])
              {
                case 0x01: sb.Append("ceq"); break;
                case 0x02: sb.Append("cgt"); break;
                case 0x04: sb.Append("clt"); break;
                case 0x06: sb.AppendFormat("ldftn {0}", stoken(readi4(i))); i += 4; break;
                case 0x07: sb.AppendFormat("ldvirtftn {0}", stoken(readi4(i))); i += 4; break;
                case 0x09: sb.AppendFormat(nf, "ldarg {0}", readu2(i)); i += 2; break;
                case 0x0A: sb.AppendFormat(nf, "ldarga {0}", readu2(i)); i += 2; break;
                case 0x0B: sb.AppendFormat(nf, "starg {0}", readu2(i)); i += 2; break;
                case 0x0C: sb.AppendFormat(nf, "ldloc {0}", readu2(i)); i += 2; break;
                case 0x0D: sb.AppendFormat(nf, "ldloca {0}", readu2(i)); i += 2; break;
                case 0x0E: sb.AppendFormat(nf, "stloc {0}", readu2(i)); i += 2; break;
                case 0x0F: sb.Append("localloc"); break;
                //case 0x19: sb.AppendFormat("no. {0}", code[i++]); break;
                case 0x15: sb.AppendFormat("initobj {0}", stoken(readi4(i))); i += 4; break;
                case 0x16: sb.AppendFormat("constrained {0}", stoken(readi4(i))); i += 4; break;
                case 0x17: sb.Append("cpblk"); break;
                case 0x1C: sb.AppendFormat("sizeof {0}", stoken(readi4(i))); i += 4; break;
                default: return null;
              }
              break;
            default: return null;
          }
          sb.AppendLine(string.Empty);
        }
        return sb.ToString();
      }
    }

    internal void Begin(DynamicMethod dm)
    {
      tokens = TypeHelper.GetTokens(il = dm.GetDynamicILInfo()); //Nop();
    }
    internal void End(StringBuilder trace)
    {
      if (labels.Count <= 1) { maxlabel = 0; if (remlast(0x0A062A)) emitop(0x2A); } //stloc.0, ldloc.0, ret -> ret
      if (curstack != (il.DynamicMethod.ReturnType != typeof(void) ? 1 : 0)) Debug.WriteLine("invalid stack {0} {1}", curstack, il.DynamicMethod);
      Optimize();
      var varsig = SignatureHelper.GetLocalVarSigHelper();
      if (pins.Count != 0)
        for (int t = 0, nt = locals.Count; t < nt; t++) varsig.AddArgument(locals[t], pins.Contains(t));
      else
        varsig.AddArguments(locals.ToArray(), null, null);
      il.SetLocalSignature(varsig.GetSignature());
      il.SetCode(code.ToArray(), maxstack);
      if (trace != null)
      {
        trace.AppendLine(string.Format("{0} maxstack({1})", TypeHelper.shortname(Method), maxstack));
        //trace.AppendLine(TypeHelper.shortname(Method));
        //trace.AppendLine(".maxstack " + maxstack);
        //trace.AppendLine(".initlocals " + Method.InitLocals); 
        //for (int t = 0; t < locals.Count; t++) trace.AppendLine(string.Format(".locals[{0}] {1}", t, locals[t]));
        trace.AppendLine(IlCode);
      }
      if (excepts.Count <= 1) { Reset(); return; }
      code.Clear();
      var n = (excepts.Count - 1) / 6; var nd = n * 12 + 4;
      int i = 0; if (nd <= 0xff) for (i = 1; i < excepts.Count && excepts[i + 0] <= 0xffff && excepts[i + 1] <= 0xffff && excepts[i + 2] <= 0xff && excepts[i + 3] <= 0xffff && excepts[i + 4] <= 0xff; i += 6) ;
      if (i == excepts.Count)//!CorILMethod_Sect_FatFormat
      {
        emit(0x01 | (nd << 8), 4);
        for (i = 1; i < excepts.Count; i += 6) { emit(excepts[i + 0], 2); emit(excepts[i + 1], 2); emit(excepts[i + 2]); emit(excepts[i + 3], 2); emit(excepts[i + 4]); emit(excepts[i + 5], 4); }
      }
      else
      {
        for (i = 0; i < excepts.Count; i++) emit(excepts[i], 4);
      }
      il.SetExceptions(code.ToArray()); Reset();
    }
    internal virtual void Reset()
    {
      il = null; tokens = null;
      curstack = maxstack = maxlabel = 0; Check = 0;
      code.Clear(); labels.Clear(); excepts.Clear(); locals.Clear(); temps.Clear(); lines.Clear(); pins.Clear();
    }
    internal void DecStack() { curstack--; }
    internal DynamicMethod Method { get { return il.DynamicMethod; } }
    internal int OpCount
    {
      get { return lines.Count; }
    }
    internal int Check;

    internal void Nop()
    {
      emitop(0x00);
    }
    //internal void Break()
    //{
    //  emitop(0x01);
    //} 
    //ECMA optional, Microsoft CLI does not currently support the no. prefix.
    //internal void No(int v) //0x01: typecheck, 0x02: rangecheck, 0x04: nullcheck
    //{
    //  emitop(0xFE); emit(0x19); emit(v);
    //}

    void __dupopt()
    {
      var l1 = lines[lines.Count - 1];
      for (int i = lines.Count - 2; i >= 0; i--)
      {
        var l2 = lines[i]; if (maxlabel - 1 >= l2) break;
        var c2 = code[l2]; if (c2 == 0x25) continue; //dup
        if (c2 != code[l1]) return;
        for (int t1 = l1 + 1, t2 = l2 + 1; t1 < code.Count; t1++, t2++) if (code[t1] != code[t2]) return;
        code[l1++] = 0x25; code.RemoveRange(l1, code.Count - l1); return;
      }
    }

    internal void Ldc_I4(int v)
    {
      maxstack = Math.Max(maxstack, ++curstack);
      if (v == -1) { emitop(0x15); return; }// ldc.i4.m1 
      if (v >= 0 && v <= 8) { emitop(0x16 + v); return; } // ldc.i4.0, ... ldc.i4.8
      if (v >= -128 && v <= 127) { emitop(0x1F); emit(&v, 1); __dupopt(); return; } // ldc.i4.s
      emitop(0x20); emit(&v, 4); __dupopt();// ldc.i4
    }
    internal void Ldc_I8(long v)
    {
      maxstack = Math.Max(maxstack, ++curstack); emitop(0x21); emit(&v, 8); __dupopt();
    }
    internal void Ldc_R4(float v)
    {
      maxstack = Math.Max(maxstack, ++curstack); emitop(0x22); emit(&v, 4); __dupopt();
    }
    internal void Ldc_R8(double v)
    {
      maxstack = Math.Max(maxstack, ++curstack); emitop(0x23); emit(&v, 8); __dupopt();
    }
    internal void Ldarg(int i)
    {
      maxstack = Math.Max(maxstack, ++curstack);
      if (i < 4) { emitop(0x02 + i); return; } // ldarg.0, ... ldarg.3  
      if (i <= 255) { emitop(0x0E); emit(i); return; } // ldarg.s
      emitop(0xFE); emit(0x09); emit(&i, 2); // ldarg
    }
    internal void Starg(int i)
    {
      curstack--;
      if (i <= 255) { emitop(0x10); emit(i); return; } // starg.s
      emitop(0xFE); emit(0x0B); emit(&i, 2); // starg
    }
    internal void Ldloc(int i)
    {
      maxstack = Math.Max(maxstack, ++curstack); Debug.Assert(i < locals.Count);
      if (i < 4) { emitop(0x06 + i); return; } // ldloc.0, ... ldloc.3  
      if (i <= 255) { emitop(0x11); emit(i); return; } // ldloc.s
      emitop(0xFE); emit(0x0C); emit(&i, 2); // ldloc
    }
    internal void Stloc(int i)
    {
      curstack--; Debug.Assert(i < locals.Count);
      if (i < 4) { emitop(0x0A + i); return; } // stloc.0, ... stloc.3  
      if (i <= 255) { emitop(0x13); emit(i); return; } // stloc.s
      emitop(0xFE); emit(0x0E); emit(&i, 2); // stloc
    }
    internal void Ldloca(int i)
    {
      maxstack = Math.Max(maxstack, ++curstack); Debug.Assert(i < locals.Count);
      if (i <= 255) { emitop(0x12); emit(i); return; } // ldloca.s
      emitop(0xFE); emit(0x0D); emit(&i, 2); // ldloca
    }
    internal void Ldarga(int i)
    {
      maxstack = Math.Max(maxstack, ++curstack); //Debug.Assert(i < locals.Count);
      if (i <= 255) { emitop(0x0F); emit(i); return; } // ldarga.s
      emitop(0xFE); emit(0x0A); emit(&i, 2); // ldarga
    }
    internal void Ldnull()
    {
      emitop(0x14); maxstack = Math.Max(maxstack, ++curstack);
    }
    internal void Ldtoken(Type type)
    {
      emitop(0xD0); emit(gettoken(type), 4); maxstack = Math.Max(maxstack, ++curstack);
    }
    internal void Ldtoken(MethodInfo type)
    {
      emitop(0xD0); emit(gettoken(type), 4); maxstack = Math.Max(maxstack, ++curstack);
    }
    //internal void Ldtoken(FieldInfo type)
    //{
    //  emitop(0xD0); emit(gettoken(type), 4); maxstack = Math.Max(maxstack, ++curstack);
    //}
    internal void Ldobj(Type type)
    {
      //ref optimization //todo: flatten, JIT compiler is too stupid
      int l = lines[lines.Count - 1]; var c = code[l];
      if (c == 0x12) { l = code[l + 1]; curstack--; reset(1); Ldloc(l); return; } //ldloca.s -> ldloc
      if (c == 0xFE && code[l + 1] == 0x0D) { l = readu2(l + 2); curstack--; reset(1); Ldloc(l); return; } //ldloca -> ldloc
      if (c == 0x0F) { l = code[l + 1]; curstack--; reset(1); Ldarg(l); return; } //ldarga.s -> ldarg
      if (c == 0xFE && code[l + 1] == 0x0A) { l = readu2(l + 2); curstack--; reset(1); Ldarg(l); return; } //ldarga -> ldarg
      if (c == 0x8F) { curstack++; reset(1); Ldelem(type); return; } //ldelema -> ldelem
      //
      switch (Type.GetTypeCode(type))
      {
        case TypeCode.SByte: emitop(0x46); return; //ldind.i1
        case TypeCode.Byte: emitop(0x47); return; //ldind.u1 
        case TypeCode.Int16: emitop(0x48); return; //ldind.i2
        case TypeCode.Char:
        case TypeCode.UInt16: emitop(0x49); return; //ldind.u2 
        case TypeCode.Int32: emitop(0x4A); return; //ldind.i4 
        case TypeCode.UInt32: emitop(0x4B); return; //ldind.u4 
        case TypeCode.UInt64:
        case TypeCode.Int64: emitop(0x4C); return; //ldind.i8
        case TypeCode.Single: emitop(0x4E); return; //ldind.r4
        case TypeCode.Double: emitop(0x4F); return; //ldind.r8
      }
      if (type.IsPointer) { emitop(0x4D); return; } //ldind.i 
      emitop(0x71); emit(gettoken(type), 4); //ldobj  //todo: check, type.IsByRef ldind.ref ? 
    }
    internal void Stobj(Type type)
    {
      curstack -= 2;
      switch (Type.GetTypeCode(type))
      {
        case TypeCode.SByte:
        case TypeCode.Byte: emitop(0x52); return; //stind.i1
        case TypeCode.Int16:
        case TypeCode.Char:
        case TypeCode.UInt16: emitop(0x53); return; //stind.i2 
        case TypeCode.Int32:
        case TypeCode.UInt32: emitop(0x54); return; //stind.i4
        case TypeCode.UInt64:
        case TypeCode.Int64: emitop(0x55); return; //stind.i8 
        case TypeCode.Single: emitop(0x56); return; //stind.r4
        case TypeCode.Double: emitop(0x57); return; //stind.r8
      }
      if (type.IsPointer) { emitop(0xDF); return; } //stind.i
      emitop(0x81); emit(gettoken(type), 4);
    }
    internal void Ldstr(string s)
    {
      var x = tokens.IndexOf(s); var t = x == -1 ? il.GetTokenFor(s) : 0x70000000 | x;
      emitop(0x72); emit(t, 4); maxstack = Math.Max(maxstack, ++curstack);
    }
    internal void Ldfld(FieldInfo fi)
    {
      emitop(0x7B); emit(gettoken(fi), 4);
    }
    internal void Ldflda(FieldInfo fi)
    {
      emitop(0x7C); emit(gettoken(fi), 4);
    }
    internal void Ldsfld(FieldInfo fi)
    {
      emitop(0x7E); emit(gettoken(fi), 4); maxstack = Math.Max(maxstack, ++curstack);
    }
    internal void Ldsflda(FieldInfo fi)
    {
      emitop(0x7F); emit(gettoken(fi), 4); maxstack = Math.Max(maxstack, ++curstack);
    }
    internal void Ldlen()
    {
      emitop(0x8E);
    }
    internal void Stfld(FieldInfo fi)
    {
      emitop(0x7D); emit(gettoken(fi), 4); curstack -= 2;
    }
    internal void Stsfld(FieldInfo fi)
    {
      emitop(0x80); emit(gettoken(fi), 4); curstack--;
    }
    internal void Ldelem(Type type)
    {
      curstack--;
      switch (Type.GetTypeCode(type))
      {
        case TypeCode.SByte: emitop(0x90); return; //ldelem.i1 
        case TypeCode.Byte: emitop(0x91); return; //ldelem.u1 
        case TypeCode.Int16: emitop(0x92); return; //ldelem.i2 
        case TypeCode.Char: emitop(0x93); return; //ldelem.u2 
        case TypeCode.UInt16: emitop(0x93); return; //ldelem.u2 
        case TypeCode.Int32: emitop(0x94); return; //ldelem.i4
        case TypeCode.UInt32: emitop(0x95); return; //ldelem.u4
        case TypeCode.UInt64:
        case TypeCode.Int64: emitop(0x96); return; //ldelem.i8
        case TypeCode.Single: emitop(0x98); return; //ldelem.r4 
        case TypeCode.Double: emitop(0x99); return; //ldelem.r8
      }
      emitop(0xA3); emit(gettoken(type), 4);
    }
    internal void Ldelema(Type type)
    {
      emitop(0x8F); emit(gettoken(type), 4); curstack--;
    }
    internal void Ldelem_Ref()
    {
      emitop(0x9A); curstack--;
    }
    internal void Stelem_Ref()
    {
      emitop(0xA2); curstack -= 3;
    }
    internal void Stelem(Type type)
    {
      curstack -= 3;
      switch (Type.GetTypeCode(type))
      {
        case TypeCode.Byte:
        case TypeCode.SByte: emitop(0x9C); return; //stelem.i1 
        case TypeCode.Char:
        case TypeCode.UInt16:
        case TypeCode.Int16: emitop(0x9D); return; //stelem.i2 
        case TypeCode.UInt32:
        case TypeCode.Int32: emitop(0x9E); return; //stelem.i4
        case TypeCode.UInt64:
        case TypeCode.Int64: emitop(0x9F); return; //stelem.i8 
        case TypeCode.Single: emitop(0xA0); return; //stelem.r4 
        case TypeCode.Double: emitop(0xA1); return; //stelem.r8
      }
      emitop(0xA4); emit(gettoken(type), 4);
    }
    internal void Constrained(Type type)
    {
      emitop(0xFE); emit(0x16); emit(gettoken(type), 4);
    }
    internal void Callvirt(MethodInfo mi)
    {
      emitop(0x6F); emit(gettoken(mi), 4);
      if (!mi.IsStatic) curstack--; curstack -= mi.GetParameters().Length; if (mi.ReturnType != typeof(void)) maxstack = Math.Max(maxstack, ++curstack);
    }
    internal void Call(MethodInfo mi)
    {
      emitop(0x28); emit(gettoken(mi), 4);
      if (!mi.IsStatic) curstack--; curstack -= mi.GetParameters().Length; if (mi.ReturnType != typeof(void)) maxstack = Math.Max(maxstack, ++curstack);
    }
    internal void Callx(MethodInfo mi)
    {
      if (mi.IsVirtual && !mi.DeclaringType.IsValueType) Callvirt(mi); else Call(mi);
    }
    internal void Calli(CallingConvention c, Type r, Type[] a)
    {
      var sig = SignatureHelper.GetMethodSigHelper(c, r);
      for (int i = 0; i < a.Length; i++) sig.AddArgument(a[i]);
      emitop(0x29); emit(gettoken(sig.GetSignature()), 4);
      if (r != typeof(void)) curstack++; curstack -= a.Length; curstack--;
    }
    internal void Newobj(ConstructorInfo ci)
    {
      emitop(0x73); emit(gettoken(ci), 4); curstack -= ci.GetParameters().Length; maxstack = Math.Max(maxstack, ++curstack);
    }
    internal void Newarr(Type type)
    {
      emitop(0x8D); emit(gettoken(type), 4);
    }
    internal void Initobj(Type type)
    {
      emitop(0xFE); emit(0x15); emit(gettoken(type), 4); curstack--;
    }
    internal void Sizeof(Type type)
    {
      //switch (Type.GetTypeCode(type))
      //{
      //  case TypeCode.Boolean:
      //  case TypeCode.SByte:
      //  case TypeCode.Byte: Ldc_I4(1); return;
      //  case TypeCode.Char:
      //  case TypeCode.Int16:
      //  case TypeCode.UInt16: Ldc_I4(2); return;
      //  case TypeCode.Int32:
      //  case TypeCode.UInt32:
      //  case TypeCode.Single: Ldc_I4(4); return;
      //  case TypeCode.Int64:
      //  case TypeCode.UInt64:
      //  case TypeCode.Double: Ldc_I4(8); return;
      //  case TypeCode.Decimal: Ldc_I4(16); return; //case TypeCode.DateTime: 
      //}
      if (TypeHelper.IsBlittable(type)) { Ldc_I4(TypeHelper.SizeOf(type)); return; }
      emitop(0xFE); emit(0x1C); emit(gettoken(type), 4); maxstack = Math.Max(maxstack, ++curstack);
    }
    internal void Ldftn(MethodInfo mi)
    {
      emitop(0xFE); emit(mi.IsVirtual ? 0x07 : 0x06); emit(gettoken(mi), 4); maxstack = Math.Max(maxstack, ++curstack);
    }
    internal void Box(Type type)
    {
      emitop(0x8C); emit(gettoken(type), 4);
    }
    internal void Unbox(Type type)
    {
      emitop(0x79); emit(gettoken(type), 4);
    }
    internal void Unbox_Any(Type type)
    {
      emitop(0xA5); emit(gettoken(type), 4);
    }
    internal void Mkrefany(Type type)
    {
      emitop(0xC6); emit(gettoken(type), 4);
    }
    internal void Castclass(Type type)
    {
      emitop(0x74); emit(gettoken(type), 4);
    }
    internal void Isinst(Type type)
    {
      emitop(0x75); emit(gettoken(type), 4);
    }
    internal void Localloc()
    {
      emitop(0xFE); emit(0x0F);
    }
    internal void Conv_I() { emitop(Check == 2 ? 0xD4 : 0xD3); }
    internal void Conv_U1() { emitop(Check == 2 ? 0x82 : 0xD2); }
    internal void Conv_U2() { emitop(Check == 2 ? 0x83 : 0xD1); }
    internal void Conv_U4() { emitop(Check == 2 ? 0x84 : 0x6D); }
    internal void Conv_U8() { emitop(Check == 2 ? 0x85 : 0x6E); }
    internal void Conv_I1() { emitop(Check == 2 ? 0xB3 : 0x67); }
    internal void Conv_I2() { emitop(Check == 2 ? 0xB5 : 0x68); }
    internal void Conv_I4() { emitop(Check == 2 ? 0xB7 : 0x69); }
    internal void Conv_I8() { emitop(Check == 2 ? 0xB9 : 0x6A); }
    internal void Conv_R4()
    {
      if (lines.Count != 0)
      {
        var l = lines[lines.Count - 1]; var c = code[l];
        if (c == 0x23) //ldc.r8
        {
          var v = readr8(l + 1); reset(1); curstack--; Ldc_R4((float)v); return;
        }
      }
      emitop(Check == 2 ? 0x76 : 0x6B);
    }
    internal void Conv_R8() { emitop(0x6C); }

    bool __binopt(int op)
    {
      //return false;
      if (lines.Count < 2) return false;
      var l1 = lines[lines.Count - 2]; if (maxlabel - 1 >= l1) return false;
      var l2 = lines[lines.Count - 1];
      var c1 = code[l1]; var c2 = code[l2];

      if (c2 == 0x16) // ldc.i4.0
        if (op == 0x58 || op == 0x59 || op == 0x60 || op == 0x61 || op == 0x62 || op == 0x63) //+, -, |, ^, <<, >> 
        {
          reset(1); curstack -= 1; return true;
        }
      if (c2 == 0x17) // ldc.i4.1
        if (op == 0x5A || op == 0x5B) //*, / 
        {
          reset(1); curstack -= 1; return true;
        }

      if (c1 >= 0x15 && c1 <= 0x20 && c2 >= 0x15 && c2 <= 0x20) //ldc.i4. 
      {
        var v1 = c1 == 0x15 ? -1 : c1 < 0x1F ? c1 - 0x16 : c1 == 0x1F ? (sbyte)code[l1 + 1] : readi4(l1 + 1);
        var v2 = c2 == 0x15 ? -1 : c2 < 0x1F ? c2 - 0x16 : c2 == 0x1F ? (sbyte)code[l2 + 1] : readi4(l2 + 1);
        if (v2 == 0 && (op == 0x5B || op == 0x5D)) return false; // '/', '%'
        reset(2); curstack -= 2;
        Ldc_I4(
          op == 0x58 ? v1 + v2 :
          op == 0x59 ? v1 - v2 :
          op == 0x5A ? v1 * v2 :
          op == 0x5B ? v1 / v2 :
          op == 0x5D ? v1 % v2 :
          op == 0x5F ? v1 & v2 :
          op == 0x60 ? v1 | v2 :
          op == 0x61 ? v1 ^ v2 :
          op == 0x62 ? v1 << v2 :
          op == 0x63 ? v1 >> v2 :
          op == 0xFE01 ? (v1 == v2 ? 1 : 0) :
          op == 0xFE02 ? (v1 > v2 ? 1 : 0) :
          op == 0xFE04 ? (v1 < v2 ? 1 : 0) :
          0);
        return true;
      }

      if (op > 0x5d) return false;

      if (c1 == 0x22 && c2 == 0x22) //ldc.r4   
      {
        var v1 = readr4(l1 + 1); var v2 = readr4(l2 + 1);
        reset(2); curstack -= 2; Debug.Assert(op >= 0x58 && op <= 0x5D);
        Ldc_R4(
          op == 0x58 ? v1 + v2 :
          op == 0x59 ? v1 - v2 :
          op == 0x5A ? v1 * v2 :
          op == 0x5B ? v1 / v2 :
          op == 0x5D ? v1 % v2 :
          0);
        return true;
      }
      if (c1 == 0x23 && c2 == 0x23) //ldc.r8   
      {
        var v1 = readr8(l1 + 1); var v2 = readr8(l2 + 1);
        reset(2); curstack -= 2; Debug.Assert(op >= 0x58 && op <= 0x5D);
        Ldc_R8(
          op == 0x58 ? v1 + v2 :
          op == 0x59 ? v1 - v2 :
          op == 0x5A ? v1 * v2 :
          op == 0x5B ? v1 / v2 :
          op == 0x5D ? v1 % v2 :
          0);
        return true;
      }
      return false;
    }
    internal void Add()
    {
      if (__binopt(0x58)) return;
      emitop(0x58); curstack--;
    }
    internal void Sub()
    {
      if (__binopt(0x59)) return;
      emitop(0x59); curstack--;
    }
    internal void Mul()
    {
      if (__binopt(0x5A)) return;
      emitop(0x5A); curstack--;
    }
    internal void Div()
    {
      if (__binopt(0x5B)) return;
      emitop(0x5B); curstack--;
    }
    internal void Rem()
    {
      if (__binopt(0x5D)) return;
      emitop(0x5D); curstack--;
    }
    internal void Or()
    {
      if (__binopt(0x60)) return;
      emitop(0x60); curstack--;
    }
    internal void And()
    {
      if (__binopt(0x5F)) return;
      emitop(0x5F); curstack--;
    }
    internal void Xor()
    {
      if (__binopt(0x61)) return;
      emitop(0x61); curstack--;
    }
    internal void Shl()
    {
      if (__binopt(0x62)) return;
      emitop(0x62); curstack--;
    }
    internal void Shr()
    {
      if (__binopt(0x63)) return;
      emitop(0x63); curstack--;
    }
    internal void Ret()
    {
      emitop(0x2A);
    }
    internal void Dup()
    {
      emitop(0x25); maxstack = Math.Max(maxstack, ++curstack);
    }
    internal void Pop()
    {
      emitop(0x26); curstack--;
    }
    internal void Neg()
    {
      emitop(0x65);
    }
    internal void Ceq()
    {
      if (__binopt(0xFE01)) return; curstack--;
      if (remlast(0x00176116)) return; //ldc.i4.1, xor, ldc.i4.0 //not optimization
      if (remlast(0x1601FE16)) return; //ldc.i4.0, ceq, ldc.i4.0 //not optimization
      emitop(0xFE); emit(0x01); // curstack--;
    }
    internal void Cgt()
    {
      if (__binopt(0xFE02)) return;
      emitop(0xFE); emit(0x02); curstack--;
    }
    internal void Clt()
    {
      if (__binopt(0xFE04)) return;
      emitop(0xFE); emit(0x04); curstack--;
    }
    internal void Not()
    {
      emitop(0x66);
    }
    internal void Br(int l)
    {
      jmp(0x38, l);
    }

    void reset(int n)
    {
      var x = lines[lines.Count - n]; code.RemoveRange(x, code.Count - x); lines.RemoveRange(lines.Count - n, n);
    }
    bool remlast(uint cc)
    {
      for (int i = lines.Count - 1; ; i--)
      {
        var c = (byte)(cc & 0xff);
        if (c == 0) { reset(lines.Count - i - 1); return true; }
        if (i < 0) return false;
        var l = lines[i]; if (maxlabel - 1 >= l) return false;
        if (code[l] != c) return false;
        cc >>= 8; if (c == 0xFE) { c = (byte)(cc & 0xff); if (code[l + 1] != c) return false; cc >>= 8; }
      }
    }
    bool remnull()
    {
      if (remlast(0x14)) { return true; } //ldnull
      if (remlast(0x16)) { return true; } //ldc.i4.0
      return false;
    }

    internal void Brfalse(int l)
    {
      curstack--; //IlCode
      if (remlast(0x000001FE))
      {
        if (remlast(0x0001FE16)) { jmp(0x3B, l); return; } //ceq, ldc.i4.0 -> beq 
        jmp(remnull() ? 0x3A : 0x40, l); return; //ceq -> brtrue : bneun 
      }
      if (remlast(0x000004FE)) { jmp(0x3C, l); return; } //clt -> bge
      if (remlast(0x000002FE)) { jmp(0x3E, l); return; } //cgt -> ble
      if (remlast(0x01FE1761)) { jmp(remnull() ? 0x39 : 0x3B, l); return; } //ceq, ldc.i4.1, xor -> brfalse : beq 
      if (remlast(0x04FE1761)) { jmp(0x3F, l); return; } //clt, ldc.i4.1, xor -> blt 
      if (remlast(0x02FE1761)) { jmp(0x3D, l); return; } //cgt, ldc.i4.1, xor -> bgt 
      jmp(0x39, l); //brfalse
    }
    internal void Brtrue(int l)
    {
      curstack--;
      if (remlast(0x000001FE))
      {
        if (remlast(0x0001FE16)) { jmp(0x40, l); return; } //ceq, ldc.i4.0 -> bneun //unchecked
        jmp(remnull() ? 0x39 : 0x3B, l); return;  //ceq -> brfalse : beq 
      }
      if (remlast(0x000004FE)) { jmp(0x3F, l); return; } //clt -> blt
      if (remlast(0x000002FE)) { jmp(0x3D, l); return; } //cgt -> bgt
      if (remlast(0x01FE1761)) { jmp(remnull() ? 0x3A : 0x40, l); return; } //ceq, ldc.i4.1, xor -> brtrue : bneun
      if (remlast(0x04FE1761)) { jmp(0x3C, l); return; } //clt, ldc.i4.1, xor -> bge 
      if (remlast(0x02FE1761)) { jmp(0x3E, l); return; } //cgt, ldc.i4.1, xor -> ble
      jmp(0x3A, l); //brtrue
    }
    //internal void Beq(int l)
    //{
    //  jmp(0x3B, l); curstack -= 2;
    //}
    //internal void Bge(int l)
    //{
    //  jmp(0x3C, l); curstack -= 2;
    //}
    //internal void Bgt(int l)
    //{
    //  jmp(0x3D, l); curstack -= 2;
    //}
    //internal void Ble(int l)
    //{
    //  jmp(0x3E, l); curstack -= 2;
    //}
    //internal void Blt(int l)
    //{
    //  jmp(0x3F, l); curstack -= 2;
    //}
    //internal void Bne_Un(int l)
    //{
    //  jmp(0x40, l); curstack -= 2;
    //}
    //internal void Bge_Un(int l)
    //{
    //  jmp(0x41, l); curstack -= 2;
    //}
    //internal void Bgt_Un(int l)
    //{
    //  jmp(0x42, l); curstack -= 2;
    //}
    //internal void Ble_Un(int l)
    //{
    //  jmp(0x43, l); curstack -= 2;
    //}
    //internal void Blt_Un(int l)
    //{
    //  jmp(0x44, l); curstack -= 2;
    //}
    internal void Leave(int l)
    {
      if (lines.Count != 0) { var x = lines[lines.Count - 1]; if (code[x] == 0xDD && maxlabel - 1 < x) return; }
      jmp(0xDD, l);
    }
    internal void Throw()
    {
      emitop(0x7A); curstack--;
    }
    internal void Endfinally()
    {
      emitop(0xDC);
    }
    //internal void Constrained(MethodInfo mi)
    //{
    //  emit(0xFE); emit(0x16); < T >
    //}
    internal int BeginTry()
    {
      if (excepts.Count == 0) excepts.Add(0);
      labels.Add(0x10000000 | code.Count); //return DefineLabel(); 
      return labels.Count - 1;
    }
    internal void Catch(int h, Type type)
    {
      protect(h, 1); excepts.Add(gettoken(type)); maxstack = Math.Max(maxstack, ++curstack);
    }
    internal void Finally(int h)
    {
      protect(h, 2); excepts.Add(0);
    }
    internal void EndTry(int h)
    {
      protect(h, 0); MarkLabel(h);
      excepts[0] = 0x01 | 0x40 | (excepts.Count << 10); //CorILMethod_Sect_EHTable | CorILMethod_Sect_FatFormat
    }

    internal int DeclareLocal(Type t, bool pinned = false)
    {
      if (pinned) pins.Add(locals.Count); locals.Add(t); return locals.Count - 1;
    }
    internal int GetLocal(Type type)
    {
      type = Compiler.filterdyn(type);
      //if (type.IsPointer && type.GetElementType().IsDefined(typeof(ComInterfaceAttribute), false)) 
      if (type.IsPointer) type = typeof(void*);
      for (int i = 0; i < temps.Count; i++)
      {
        var t = temps[i]; if ((t & 1) != 0) continue;
        if (locals[t >> 1] == type) { temps[i] |= 1; return t >> 1; }
      }
      var v = DeclareLocal(type); temps.Add((v << 1) | 1); return v;
    }
    internal void ReleaseLocal(int v)
    {
      for (int i = 0; i < temps.Count; i++) if ((temps[i] >> 1) == v) { temps[i] &= ~1; return; }
      Debug.Assert(false);
    }

    internal int DefineLabel()
    {
      labels.Add(0x10000000); return labels.Count - 1; //labels.Add(0x10000000 | code.Count);
    }
    internal void MarkLabel(int v)
    {
#if(true) //jmp optimization
      for (; ; )
      {
        var l = labels[labels.Count - 1]; if ((l & 0x30000000) != 0) break;
        var x = code.Count - 5; if ((l & 0x0fffffff) != x) break;
        var c = code[x]; Debug.Assert(is_jmp4(c)); if (c == 0xDD) break; //leave
        var d = readi4(x + 1); Debug.Assert(is_jmp4(c) && (d & 0xf0000000) == 0x40000000);
        if ((d & 0x0fffffff) != v) break; //Debug.WriteLine("optimize jmp " + (d & 0x0fffffff));
        reset(1); labels.RemoveAt(labels.Count - 1); //code.RemoveRange(x, 5); lines.RemoveAt(lines.Count - 1); labels.RemoveAt(labels.Count - 1);
        for (int t = 0, h; t < labels.Count; t++) if (((h = labels[t]) & 0x20000000) != 0 && (h & 0x0fffffff) > x) { labels[t] = 0x20000000 | x; }
        switch (c)
        {
          case 0x38: break;//br
          case 0x39: //brfalse
          case 0x3A: curstack++; Pop(); break; //brtrue
          default: curstack += 2; Pop(); Pop(); break;
        }
        break; //IlCode
      }
#endif
      Debug.Assert((labels[v] & 0x10000000) != 0);
      labels[v] = 0x20000000 | code.Count; maxlabel = code.Count + 1; //if (code.Count + 1 < maxlabel) { } maxlabel = Math.Max(code.Count + 1, maxlabel);
    }

    void Optimize()
    {
      for (int i = 0; i < labels.Count; i++)
      {
        var l = labels[i]; if ((l & 0x30000000) != 0) continue;
        var d = readi4(l + 1); Debug.Assert(is_jmp4(code[l]) && (d & 0xf0000000) == 0x40000000);
        var t = labels[d & 0x0fffffff]; Debug.Assert((t & 0x20000000) != 0); //MarkLabel missing
        writei4(l + 1, (t & 0x0fffffff) - (l + 5));
      }
      // return; // no optimize
      for (int i = labels.Count; --i >= 0;) if ((labels[i] & 0x30000000) != 0) labels.RemoveAt(i);
      for (; ; )
      {
        int im = -1;
        for (int i = 0, dm = int.MaxValue; i < labels.Count; i++)
        {
          var t = labels[i]; var c = code[t]; if (is_jmp1(c)) continue; Debug.Assert(is_jmp4(c));
          var d = readi4(t + 1); if (d < -128 || d > 127) continue; //if (d < -128 || d > 127 + 3) continue;
          if (Math.Abs(d) < Math.Abs(dm)) { dm = d; im = i; }
        }
        if (im == -1) break;
        var l = labels[im]; var x = getjmp(l); code[l] = (byte)to_jmp1(code[l]);
        setjmp(l, x > l ? x - 3 : x); code.RemoveRange(l + 2, 3);
        for (int i = 0; i < labels.Count; i++)
        {
          if (labels[i] > l) labels[i] -= 3;
          var t = labels[i]; var y = getjmp(t);
          if (t < l && y > l) { setjmp(t, y - 3); continue; }
          if (t > l && y < l) { setjmp(t, y + 3); continue; }
        }
        for (int i = 1; i < excepts.Count; i += 6)
          for (int k = i + 1; k < i + 4; k += 2)
          {
            if (excepts[k] > l) { excepts[k] -= 3; continue; }
            if (excepts[k] + excepts[k + 1] > l) { excepts[k + 1] -= 3; continue; }
          }
      }
    }

    void protect(int h, int f)
    {
      int a = labels[h], l = -1; for (int i = 1; i < excepts.Count; i += 6) if (excepts[i + 1] == a) l = i;
      if (l == -1 || excepts[l] == 0) Leave(h); else Endfinally();
      if (l != -1) { excepts[l + 1] = a & 0x0fffffff; excepts[l + 4] = code.Count - excepts[l + 3]; }
      if (f == 0) return;
      excepts.Add(f & 2); excepts.Add(a); excepts.Add(code.Count - (a & 0x0fffffff));
      excepts.Add(code.Count); excepts.Add(0);
    }

    void emitop(int b)
    {
      lines.Add(code.Count); emit(b);
    }
    void emit(int b)
    {
      code.Add(checked((byte)b));
    }
    void emit(void* p, int n)
    {
      for (int i = 0; i < n; i++) code.Add(((byte*)p)[i]);
    }
    void emit(int i, int n)
    {
      emit(&i, n);
    }
    int getjmp(int i)
    {
      var c = code[i]; if (is_jmp4(c)) return i + 5 + readi4(i + 1);
      Debug.Assert(is_jmp1(c)); return i + 2 + (sbyte)code[i + 1];
    }
    void setjmp(int i, int x)
    {
      var c = code[i]; if (is_jmp4(c)) { writei4(i + 1, x - (i + 5)); return; }
      Debug.Assert(is_jmp1(c)); code[i + 1] = (byte)checked((sbyte)(x - (i + 2)));
    }
    void jmp(int c, int l) { labels.Add(code.Count); l |= 0x40000000; emitop(c); emit(&l, 4); }
    void write(void* p, int i, int n) { for (int t = 0; t < n; t++) code[i + t] = ((byte*)p)[t]; }
    void writei4(int i, int v) { write(&v, i, 4); }
    void read(void* p, int i, int n) { for (int t = 0; t < n; t++) ((byte*)p)[t] = code[i + t]; }
    ushort readu2(int i) { ushort v = 0; read(&v, i, 2); return v; }
    int readi4(int i) { int v = 0; read(&v, i, 4); return v; }
    long readi8(int i) { long v = 0; read(&v, i, 8); return v; }
    float readr4(int i) { float v = 0; read(&v, i, 4); return v; }
    double readr8(int i) { double v = 0; read(&v, i, 8); return v; }
    int gettoken(Type type)
    {
      type = Compiler.filterdyn(type);
      var h = type.TypeHandle; var x = tokens.IndexOf(h);
      var t = x == -1 ? il.GetTokenFor(h) : 0x02000000 | x;
      Debug.Assert(t >> 24 == 2); return t;
    }

    int gettoken(MethodBase p)
    {
      if (p is DynamicMethod)
      {
        var i = tokens.IndexOf(p);
        var k = i == -1 ? il.GetTokenFor((DynamicMethod)p) : 0x06000000 | i;
        Debug.Assert(k >> 24 == 6); return k;
      }

      var h = p.MethodHandle; var x = tokens.IndexOf(h); var dt = p.DeclaringType;
      var t = x == -1 ? (dt.IsGenericType ? il.GetTokenFor(h, dt.TypeHandle) : il.GetTokenFor(h)) : 0x06000000 | x;
      Debug.Assert(t >> 24 == 6); return t;
    }
    int gettoken(FieldInfo p)
    {
      var h = p.FieldHandle; var x = tokens.IndexOf(h);
      if (x != -1) return 0x04000000 | x; var t = p.DeclaringType;
      x = t.IsGenericType ? il.GetTokenFor(h, t.TypeHandle) : il.GetTokenFor(h);
      Debug.Assert(x >> 24 == 4); return x;
    }
    int gettoken(byte[] a)
    {
      for (int i = 0; i < tokens.Count; i++)
      {
        var b = tokens[i] as byte[]; if (b == null || a.Length != b.Length) continue;
        int t = 0; for (; t < a.Length && a[t] == b[t]; t++) ;
        if (t == a.Length) return 0x11000000 | i;
      }
      var s = il.GetTokenFor(a); Debug.Assert(s >> 24 == 0x11); return s;
    }
    object stoken(int i)
    {
      var p = tokens[i & 0x00ffffff];
      if (p is string) return p;
      if (p is RuntimeTypeHandle) return TypeHelper.shortname(Type.GetTypeFromHandle((RuntimeTypeHandle)p));
      if (p is DynamicMethod) return TypeHelper.shortname(p);
      if (p is RuntimeMethodHandle) return TypeHelper.shortname(MethodBase.GetMethodFromHandle((RuntimeMethodHandle)p));
      if (p is RuntimeFieldHandle) return TypeHelper.shortname(FieldInfo.GetFieldFromHandle((RuntimeFieldHandle)p));
      if (TypeHelper.IsGenericFieldInfo(p)) return TypeHelper.shortname(FieldInfo.GetFieldFromHandle(TypeHelper.GetFieldHandle(p), TypeHelper.GetContext(p)));
      if (TypeHelper.IsGenericMethodInfo(p)) return TypeHelper.shortname(MethodInfo.GetMethodFromHandle(TypeHelper.GetMethodHandle(p), TypeHelper.GetContext(p)));
      return p;
    }

    static bool is_jmp4(int c) { return (c >= 0x38 && c <= 0x44) || c == 0xDD; } // br .. bltun, leave
    static bool is_jmp1(int c) { return (c >= 0x2B && c <= 0x37) || c == 0xDE; } // br.s .. bltun.s, leave.s
    static int to_jmp1(int c) { Debug.Assert(is_jmp4(c)); return c >= 0x38 && c <= 0x44 ? c - 0x0D : 0xDE; }
    static int to_jmp4(int c) { Debug.Assert(is_jmp1(c)); return c >= 0x2B && c <= 0x37 ? c + 0x0D : 0xDD; }
    static string ctex(int c)
    {
      switch (c)
      {
        case 0x38: return "br";
        case 0x39: return "brfalse";
        case 0x3A: return "brtrue";
        case 0x3B: return "beq";
        case 0x3C: return "bge";
        case 0x3D: return "bgt";
        case 0x3E: return "ble";
        case 0x3F: return "blt";
        case 0x40: return "bneun";
        case 0x41: return "bgeun";
        case 0x42: return "bgtun";
        case 0x43: return "bleun";
        case 0x44: return "bltun";
        case 0xDD: return "leave";
      }
      throw new Exception();
    }
  }

  static class TypeHelper
  {
    internal static List<WeakReference> cache = new List<WeakReference>(); //dynamics only
    internal static Assembly[] Assemblys
    {
      get { return assemblys ?? (assemblys = AppDomain.CurrentDomain.GetAssemblies()/*.Where(p => !p.IsDynamic).ToArray()*/); }
      set { assemblys = value; }
    }
    static Assembly[] assemblys;
    static Func<DynamicILInfo, byte[]> t1, t2, t3; static Func<DynamicILInfo, int> t4; static Func<DynamicILInfo, List<object>> t5;
    static Func<DynamicMethod, Type[]> t7;
    static Func<MethodBase, ParameterInfo[]> t9, t10, t11;
    static TypeHelper()
    {
      var tl = typeof(DynamicILInfo); var fl = BindingFlags.Instance | BindingFlags.NonPublic;
      var pa = Expression.Parameter(typeof(DynamicILInfo));
      t1 = Expression.Lambda<Func<DynamicILInfo, byte[]>>(Expression.Field(pa, tl.GetField("m_code", fl)), pa).Compile();
      t2 = Expression.Lambda<Func<DynamicILInfo, byte[]>>(Expression.Field(pa, tl.GetField("m_exceptions", fl)), pa).Compile();
      t3 = Expression.Lambda<Func<DynamicILInfo, byte[]>>(Expression.Field(pa, tl.GetField("m_localSignature", fl)), pa).Compile();
      t4 = Expression.Lambda<Func<DynamicILInfo, int>>(Expression.Field(pa, tl.GetField("m_maxStackSize", fl)), pa).Compile();
      t5 = Expression.Lambda<Func<DynamicILInfo, List<object>>>(Expression.Field(Expression.Field(pa, tl.GetField("m_scope", fl)), "m_tokens"), pa).Compile();
    }
    internal static bool IsBlittable(Type t)
    {
      if (t.IsPrimitive) return true; if (!t.IsValueType) return false;
      var a = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      for (int i = 0; i < a.Length; i++) if (!IsBlittable(a[i].FieldType)) return false;
      return true;
    }
    internal static int SizeOf(Type t)
    {
      switch (Type.GetTypeCode(t))
      {
        case TypeCode.Boolean: return sizeof(bool);
        case TypeCode.Char: return sizeof(char);
        case TypeCode.SByte:
        case TypeCode.Byte: return sizeof(byte);
        case TypeCode.Int16:
        case TypeCode.UInt16: return sizeof(short);
        case TypeCode.Int32:
        case TypeCode.UInt32: return sizeof(int);
        case TypeCode.Int64:
        case TypeCode.UInt64: return sizeof(long);
        case TypeCode.Single: return sizeof(float);
        case TypeCode.Double: return sizeof(double);
        case TypeCode.Decimal: return sizeof(decimal);
          //case TypeCode.DateTime: return sizeof(DateTime);
      }
      return Marshal.SizeOf(t);
    }
    internal static List<object> GetTokens(DynamicILInfo p) { return t5(p); }
    internal static Type[] GetParams(DynamicMethod a)
    {
      if (t7 == null)
      {
        var p = Expression.Parameter(typeof(DynamicMethod));
        t7 = Expression.Lambda<Func<DynamicMethod, Type[]>>(Expression.Field(p, typeof(DynamicMethod).GetField("m_parameterTypes", BindingFlags.Instance | BindingFlags.NonPublic)), p).Compile();
      }
      return t7(a);
    }
    internal static RuntimeTypeHandle GetContext(object p)//todo: Lambda
    {
      return (RuntimeTypeHandle)p.GetType().GetField("m_context", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(p);
    }
    internal static RuntimeMethodHandle GetMethodHandle(object p)//todo: Lambda
    {
      return (RuntimeMethodHandle)p.GetType().GetField("m_methodHandle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(p);
    }
    internal static RuntimeFieldHandle GetFieldHandle(object p)//todo: Lambda
    {
      return (RuntimeFieldHandle)p.GetType().GetField("m_fieldHandle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(p);
    }
    internal static bool IsGenericFieldInfo(object p)
    {
      return p.GetType().Name == "GenericFieldInfo";
    }
    internal static bool IsGenericMethodInfo(object p)
    {
      return p.GetType().Name == "GenericMethodInfo";
    }
    internal static ParameterInfo[] GetParametersNoCopy(MethodBase meth)
    {
      if (meth is DynamicMethod) return (t11 ?? (t11 = GetParametersNoCopy(meth.GetType())))(meth);
      if (meth is ConstructorInfo) return (t10 ?? (t10 = GetParametersNoCopy(meth.GetType())))(meth);
      return (t9 ?? (t9 = GetParametersNoCopy(meth.GetType())))(meth);
    }
    static Func<MethodBase, ParameterInfo[]> GetParametersNoCopy(Type t)
    {
      var me = t.GetMethod("GetParametersNoCopy", BindingFlags.NonPublic | BindingFlags.Instance);
      var pa = Expression.Parameter(typeof(MethodBase));
      return Expression.Lambda<Func<MethodBase, ParameterInfo[]>>(Expression.Call(Expression.Convert(pa, me.DeclaringType), me), pa).Compile();
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
          if (type == typeof(Compiler.__null)) return "<null>";
          if (type.DeclaringType == typeof(Compiler)) return type.Name;//dynamic
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
            if (t.DeclaringType == typeof(Compiler)) return 8;
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
      var ss = type.FullName.Replace('+','.');
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
      var type = p as Type; if (type != null) return Tuple.Create(type, "T:" + type.FullName.Replace('+','.'));
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
      var xml = TypeHelper.cache.Select(t => t.Target).OfType<XElement>().FirstOrDefault(t => t.Annotation<Assembly>() == assembly);
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
        if (type.DeclaringType == typeof(Compiler)) return string.Format("{0}\n{1}", type.Name, "Represents an object whose operation will be resolved at runtime.");
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
    internal static string tooltip(Compiler.map tpos, string text, bool skipdef = true)
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
        case 0x04: return string.Format("({0}) {1} {2}", "local variable", shortname(tpos.p as Type ?? ((Compiler.RepInfo)tpos.p).type), text.Substring(tpos.i, tpos.n));
        case 0x05: return string.Format("({0}) {1} {2}", "parameter", shortname(tpos.p as Type ?? ((Compiler.RepInfo)tpos.p).type), text.Substring(tpos.i, tpos.n));
        case 0x06: return string.Format("({0}) {1} {2}", "variable", shortname((Type)tpos.p), text.Substring(tpos.i, tpos.n));
        case 0x07: return string.Format("({0}) {1} {2}", "property", shortname(((Type)tpos.p).GetGenericArguments()[0]), text.Substring(tpos.i, tpos.n));
        case 0x08: return string.Format("{0} {1}", "namespace", tpos.p);
        case 0x01: return "(dynamic expression)\nThis operation will be resolved at runtime.";
      }
      return null;
    }
    internal static void msdn(string s)
    {
      s = string.Format("http://msdn.microsoft.com/query/dev10.query?appId=Dev10IDEF1&k=k({0})", s);
      try
      {
        var o = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
        try
        {
          var ws = Neuron.call(o, "Windows", null);
          for (int i = 0; i < (int)Neuron.get(ws, "Count"); i++)
          {
            var ie = Neuron.call(ws, "Item", i); if (ie == null) continue;
            var ss = Path.GetFileName((string)Neuron.get(ie, "FullName"));
            if (string.Compare(ss, "iexplore.exe", true) != 0) continue;
            Neuron.put(ie, "Visible", true);
            var h = Neuron.get(ie, "HWND"); var hwnd = new IntPtr((int)h);
            Native.ShowWindow(hwnd, 9); Native.BringWindowToTop(hwnd);
            Neuron.call(ie, "Navigate", s, 0x800); return;
          }
        }
        finally { Marshal.FinalReleaseComObject(o); }
      }
      catch { }
      Process.Start("iexplore.exe", s);
    }
    static Bitmap icons;
    internal static void drawicon(Graphics g, int x, int y, int i)
    {
      if (icons == null) { icons = Properties.Resources.typicons; icons.MakeTransparent(); }
      var d = CodeEditor.dpiscale(20);
      g.DrawImage(icons, new Rectangle(x, y, d, d), new Rectangle(i * 20, 0, 20, 20), GraphicsUnit.Pixel);
    }
    internal static bool IsOrIsSubclassOf(this Type t, Type x)
    {
      return t == x || t.IsSubclassOf(x);
    }
    internal static bool IsComDisposed(object p)
    {
      //There's no way to determine whether an object is disposed other than using
      //it and getting an ObjectDisposedException
      if (!p.GetType().IsCOMObject) return false;
      try { Marshal.Release(Marshal.GetIUnknownForObject(p)); return false; }
      catch { return true; }
    }
  }

  public class Neuron
  {
    private object[] data;
    public unsafe virtual object Invoke(int id, object p)
    {
      switch (id)
      {
        case 0: return data; //IsDynamic
        case 1: data = p as object[]; break; //to overwrite notify 
        case 2: return ToString(); //to overwrite ScriptEditor Title
        case 3: if (this.Invoke(5, null) != null) sp = null; Invoke("."); break; //to overwrite onstart
        case 4: Invoke("Dispose"); break; //to overwrite onstop
                                          //case 5: return null; //AutoStop
                                          //case 6: return null; //Step
      }
      return null;
    }
    public Delegate GetMethod(string name)
    {
      if (data == null) return null; var a = (object[])data[0];
      for (int i = a.Length - 1; ; i--)
      {
        var de = a[i] as Delegate; if (de != null) { if (de.Method.Name == name) return de; continue; }
        var dm = a[i] as DynamicMethod; if (dm == null) return null; if (dm.Name != name) continue;
        return GetDelegate(i);
      }
    }
    public void Invoke(string name)
    {
      if (GetMethod(name) is Action<Neuron> m) m(this);
    }
    #region Debug support
    public static Action<Neuron, Exception> Debugger; //todo: make private, possible with new exception handling
    [ThreadStatic]
    static internal int state, dbgpos;
    [ThreadStatic]
    static internal unsafe int* sp;
    unsafe void dbgstp(int i)
    {
      var t = ((int[])data[1])[(dbgpos = i) >> 5];
      if (t == 0 || (t & (1 << (i & 31))) == 0)
      {
        if (state == 0) return;
        if (state == 1 && sp > &i + 1) return;
        if (state == 3 && sp >= &i) return;
      }
      if (Debugger == null || state == 7) return;
      sp = &i; state = 7; Debugger(this, null);
    }
    [ThreadStatic]
    static internal unsafe int* stack;
    unsafe static void dbgstk(int id, int* pi, void* pv)
    {
      if (pv == null)
      {
        var p = stack; if (p == null) return; // >
        for (; ; ) { var v = p[0]; p = p[1] != 0 ? p + p[1] : null; if ((v & 0xffff) == id) { stack = p; return; } }
      }
      if (pi[0] != 0) return;
      if (id == 0 && stack != null && stack <= pi) stack = null; //after exceptions
      pi[0] = id | ((int)(((int*)pv) - pi) << 16);
      pi[1] = stack != null ? (int)(stack - pi) : 0; stack = pi;
    }
    internal static object get(object p, string s) { return p.GetType().InvokeMember(s, BindingFlags.GetProperty, null, p, null, null, null, null); }
    internal static void put(object p, string s, object v) { p.GetType().InvokeMember(s, BindingFlags.SetProperty, null, p, new object[] { v }, null, null, null); }
    internal static object call(object p, string s, params object[] a)
    {
      return p.GetType().InvokeMember(s, BindingFlags.InvokeMethod, null, p, a, null);
    }
    unsafe Delegate GetDelegate(int i)
    {
      var a = (object[])data[0];
      var de = a[i] as Delegate; if (de != null) return de;
      var dm = (DynamicMethod)a[i];
      var list = TypeHelper.GetTokens(dm.GetDynamicILInfo());
      if (list[0] != null) return (Delegate)(a[i] = list[0]);
      var tt = TypeHelper.GetParams(dm); Type type;
      if (dm.ReturnType != typeof(void)) { Array.Resize(ref tt, tt.Length + 1); tt[tt.Length - 1] = dm.ReturnType; type = Expression.GetFuncType(tt); }
      else type = Expression.GetActionType(tt);
      list[0] = a[i] = de = dm.CreateDelegate(type); return de;
    }
    #endregion
  }

}
