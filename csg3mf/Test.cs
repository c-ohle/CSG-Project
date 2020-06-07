﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace csg3mf
{
  public unsafe struct Script
  {
    static byte* StackPtr = (byte*)Marshal.AllocHGlobal(65536).ToPointer();
    
    public static Expression<Func<object, object[]>> Compile(Type @this, string code)
    {
      var s = (char*)StackPtr + 16; var n = code.Length; fixed (char* p = code) Native.memcpy(s, p, (void*)((n + 1) << 1));
      #region remove comments
      for (int i = 0, l = n - 1; i < l; i++)
      {
        var c = s[i]; if (c <= 32) { s[i] = ' '; continue; }
        if (c == '"' || c == '\'') { for (++i; i < n && s[i] != c; i++) if (s[i] == '\\') i++; continue; }
        if (c != '/') continue;
        if (s[i + 1] == '/') { for (; i < n && s[i] != '\n'; i++) s[i] = ' '; continue; }
        if (s[i + 1] == '*') { var t = i; for (; i < l && !(s[i] == '/' && s[i - 1] == '*'); i++) ; for (; t <= i; t++) s[t] = ' '; continue; }
      }
      #endregion
      Script a; a.s = s; a.n = n; a.trim(); *(Script*)StackPtr = a; //todo: WeakReferene stack
      var stack = new Stack { Expression.Parameter(typeof(object), "this") }; stack.@this = Expression.Parameter(@this, "this");
      //stack.usings.Add(typeof(D3DView)); stack.nstats = stack.nusings = 0; //compat
      stack.npub = 1; a.Parse(stack, null, null, null, 0x08 | 0x04);
      var t1 = a.Parse(stack, null, null, null, 0x01);
      var t2 = Expression.Lambda<Func<object, object[]>>(t1, ".ctor", stack.Take(1)); return t2;
    }
    public static (int i, int n) GetErrorPos()
    {
      var e = (Script*)StackPtr; return ((int)(e->s - (char*)(StackPtr + 32)), e->n);
    }
    public override string ToString() => new string(s, 0, n);

    char* s; int n;
    class Stack : List<ParameterExpression> { internal ParameterExpression @this; internal List<object> usings = new List<object>(); internal int nstats, nusings, npub; internal List<Expression> list = new List<Expression>(); }

    Expression Parse(Stack stack, LabelTarget @return, LabelTarget @break, LabelTarget @continue, int flags)
    {
      var list = stack.list; int stackab = (flags & 1) != 0 ? 1 : stack.Count, listab = list.Count; var ep = *(Script*)StackPtr;
      if ((flags & 0x01) != 0) { stack.Add(stack.@this); list.Add(Expression.Assign(stack.@this, Expression.Convert(stack[0], stack.@this.Type))); }
      for (var c = this; c.n != 0;)
      {
        var a = c.block(); if (a.n == 0) continue;
        if (a.s[0] == '{') { if ((flags & 0x08) != 0) continue; a.trim(1, 1); list.Add(a.Parse(stack, @return, @break, @continue, 0)); continue; }
        var t = a; var n = t.next(); *(Script*)StackPtr = a;
        if (n.equals("using"))
        {
          if ((flags & 0x08) == 0) continue;
          if (t.take("static")) { var u = GetType(stack, t); stack.usings.Insert(stack.nstats++, u); }
          else stack.usings.Add(t.ToString().Replace(" ", string.Empty));
          stack.nusings = stack.usings.Count; continue;
        }
        if (n.equals("if"))
        {
          if ((flags & 0x08) != 0) continue;
          n = t.next(); n.trim(1, 1);
          a = c; var l = c; for (; a.n != 0;) { var e = a.next(); if (!e.equals("else")) break; a.block(); l = a; }
          if (l.s != c.s) { a = c; c = l; a.next(); a.n = (int)(l.s - a.s); a.trim(); } else a.n = 0;
          list.Add(Expression.IfThenElse(n.Parse(stack, typeof(bool)), t.Parse(stack, @return, @break, @continue, 0), a.n != 0 ? a.Parse(stack, @return, @break, @continue, 0) : Expression.Empty())); continue;
        }
        if (n.equals("for"))
        {
          if ((flags & 0x08) != 0) continue;
          n = t.next(); n.trim(1, 1); var br = Expression.Label("break"); var co = Expression.Label("continue");
          a = n.next(';'); var t1 = stack.Count; var t2 = list.Count; a.Parse(stack, null, null, null, 0x04);
          a = n.next(';'); var t3 = a.n != 0 ? a.Parse(stack, typeof(bool)) : null;
          var t4 = list.Count; var t5 = stack.Count;
          t.Parse(stack, @return, br, co, 0x04); list.Add(Expression.Label(co)); n.Parse(stack, null, null, null, 0x04);
          var t6 = (Expression)Expression.Block(stack.Skip(t5), list.Skip(t4)); stack.RemoveRange(t5, stack.Count - t5); list.RemoveRange(t4, list.Count - t4);
          if (t3 != null) t6 = Expression.IfThenElse(t3, t6, Expression.Break(br));
          list.Add(Expression.Loop(t6, br)); t6 = Expression.Block(stack.Skip(t1), list.Skip(t2));
          stack.RemoveRange(t1, stack.Count - t1); list.RemoveRange(t2, list.Count - t2);
          list.Add(t6); continue;
        }
        if (n.equals("switch"))
        {
          if ((flags & 0x08) != 0) continue;
          n = t.next(); n.trim(1, 1); var t1 = n.Parse(stack, null);
          n = t.next(); n.trim(1, 1); var t2 = stack.usings.Count; var t5 = (Expression)null;
          for (var ab = list.Count; n.n != 0;)
          {
            for (; n.n != 0;)
            {
              t = n; a = t.next(); if (a.equals("default")) { n = t; n.next(); break; }
              if (!a.equals("case")) break; n = t; list.Add(Convert(n.next(':').Parse(stack, t1.Type), t1.Type));
            }
            for (t = n; n.n != 0;)
            {
              a = n.block(); if (a.equals("break")) { t.n = (int)(a.s - t.s); t.trim(); break; }
              var s = a; s = s.next(); if (s.equals("return") || s.equals("continue")) { t.n = (int)(a.s - t.s) + a.n; t.trim(); break; }
            }
            var br = Expression.Label(); var t4 = t.Parse(stack, @return, br, @continue, 0x10);
            if (list.Count != ab) stack.usings.Add(Expression.SwitchCase(t4, list.Skip(ab))); else t5 = t4;
            list.RemoveRange(ab, list.Count - ab);
          }
          list.Add(Expression.Switch(t1, t5, stack.usings.Skip(t2).Cast<SwitchCase>().ToArray()));
          stack.usings.RemoveRange(t2, stack.usings.Count - t2); continue;
        }
        if (n.equals("return"))
        {
          if ((flags & 0x08) != 0) continue;
          if (@return.Type != typeof(void))
          {
            var t0 = @return.Type != typeof(Script) ? @return.Type : null;
            var t1 = t.Parse(stack, t0); if (t0 == null) @return.GetType().GetField("_type", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(@return, t0 = t1.Type);
            list.Add(Expression.Return(@return, Convert(t1, t0))); continue;
          }
          if (t.n != 0) n.error("invalid type"); list.Add(Expression.Return(@return)); continue;
        }
        if (n.equals("break")) { if ((flags & 0x08) != 0) continue; list.Add(Expression.Break(@break)); continue; }
        if (n.equals("continue")) { if ((flags & 0x08) != 0) continue; list.Add(Expression.Break(@continue)); continue; }
        if (n.equals("new")) { if ((flags & 0x08) != 0) continue; list.Add(a.Parse(stack, null)); continue; }
        var @public = false; if (n.equals("public")) { a = t; @public = true; }
        n = a; t = n.gettype(); if (t.n == 0) { if ((flags & 0x08) != 0) continue; list.Add(a.Parse(stack, null)); continue; }
        a = n; n = a.next();
        var type = !t.equals("var") ? GetType(stack, t) : null;
        if (a.n != 0 && a.s[0] == '(')
        {
          int i = 0; if ((flags & 0x01) != 0) for (i = 1; !n.equals(stack[i].Name); i++) ;
          var s = n.ToString(); var istack = i; if (i == 0) { istack = @public ? stack.npub++ : stack.Count; stack.Insert(istack, null); }
          t = a.next(); t.trim(1, 1); a.trim(1, 1); var ab = stack.Count;
          for (; t.n != 0;) { n = t.next(','); var v = n.gettype(); n.check(stack, ab); stack.Add(Expression.Parameter(GetType(stack, v), n.ToString())); }
          var t0 = i != 0 ? stack[istack].Type : type != typeof(void) ? Expression.GetFuncType(stack.Skip(ab).Select(p => p.Type).Concat(Enumerable.Repeat(type, 1)).ToArray()) : Expression.GetActionType(stack.Skip(ab).Select(p => p.Type).ToArray());
          var t1 = i != 0 ? stack[istack] : Expression.Variable(t0, s); stack[istack] = t1;
          if ((flags & 0x08) != 0) { stack.RemoveRange(ab, stack.Count - ab); continue; }
          var t2 = Expression.Lambda(t0, a.Parse(stack, Expression.Label(type, "return"), null, null, 0x02), s, stack.Skip(ab));
          stack.RemoveRange(ab, stack.Count - ab); list.Add(Expression.Assign(t1, t2)); continue;
        }
        for (; n.n != 0; n = a.next())
        {
          var v = a.next(','); var b = v.next('='); if (!((flags & 0x01) != 0 && type != null)) n.check(stack, stackab);
          if ((flags & 0x08) != 0) { if (type != null) stack.Add(Expression.Parameter(type, n.ToString())); continue; }
          var r = type == null || v.n != 0 ? v.Parse(stack, type) : null;
          int i = 0; if ((flags & 0x01) != 0 && type != null) for (i = 1; !n.equals(stack[i].Name); i++) ;
          var e = i != 0 ? stack[i] : Expression.Parameter(type ?? r.Type, n.ToString());
          if (r != null) list.Add(Expression.Assign(e, Convert(r, e.Type))); if (i == 0) stack.Add(e);
        }
      }

      *(Script*)StackPtr = ep;
      if ((flags & 0x04) != 0) return null;
      if ((flags & 0x02) != 0) list.Add(@return.Type != typeof(void) ? Expression.Label(@return, Expression.Default(@return.Type)) : Expression.Label(@return));
      if ((flags & 0x01) != 0) list.Add(Expression.NewArrayInit(typeof(object), stack.Take(stack.npub)));
      if ((flags & 0x10) != 0) list.Add(Expression.Label(@break));
      var block = stack.Count != stackab || list.Count - listab > 1 ? Expression.Block(stack.Skip(stackab), list.Skip(listab)) : list.Count - listab == 1 ? list[listab] : Expression.Empty();
      list.RemoveRange(listab, list.Count - listab); stack.RemoveRange(stackab, stack.Count - stackab);
      return block;
    }
    Expression Parse(Stack stack, Type wt)
    {
      if (n == 0) error("syntax");
      Script a = this, b = a, c, d; int op = 0, i1 = 0, i2 = 0;
      for (c = a; c.n != 0; i2++)
      {
        var t = c.next(); var o = t.opcode();
        if (o == 0) { i1 = 0; continue; }
        i1++; if (op >> 4 > o >> 4) continue;
        if (o >> 4 == 0x03 && (i2 == 0 || i1 != 1)) { i2--; continue; }
        if ((o == 0xd0 || o == 0xc0) && op == o) continue; // =, ?
        op = o; a.n = (int)(t.s - s); a.trim(); b = c;
      }
      if (op != 0)
      {
        if (op == 0xdf) // => 
        {
          if (wt == null) return Expression.Constant(this);
          var me = wt.GetMethod("Invoke"); if (me == null) error("unknown type");
          var pp = me.GetParameters(); if (a.s[0] == '(') a.trim(1, 1); var ab = stack.Count;
          int i = 0; for (; a.n != 0 && i < pp.Length; i++) { var n = a.next(','); stack.Add(Expression.Parameter(pp[i].ParameterType, n.ToString())); }
          if (a.n != 0 || i != pp.Length) error("invalid param count");
          var g = me.ReturnType.ContainsGenericParameters; Expression r;
          if (b.s[0] == '{') { b.trim(1, 1); r = b.Parse(stack, Expression.Label(g ? typeof(Script) : me.ReturnType, "return"), null, null, 0x02); }
          else r = b.Parse(stack, g ? null : wt);
          if (g) { if (r.Type == typeof(Script)) b.error("missing return"); var gg = wt.GetGenericArguments(); gg[gg.Length - 1] = r.Type; wt = wt.GetGenericTypeDefinition().MakeGenericType(gg); }
          r = Expression.Lambda(wt, r, stack.Skip(ab)); stack.RemoveRange(ab, stack.Count - ab); return r;
        }
        var ea = a.Parse(stack, null);
        switch (op)
        {
          case 0x54: return Expression.TypeIs(ea, GetType(stack, b));
          case 0x55: return Expression.TypeAs(ea, GetType(stack, b));
          case 0xc0: a = b.next(':'); return Expression.Condition(ea, a.Parse(stack, wt), b.Parse(stack, wt));
        }
        var eb = b.Parse(stack, ea.Type);
        if (op == 0x30 && (ea.Type == typeof(string) || eb.Type == typeof(string)))
        {
          if (ea.Type != typeof(string)) ea = Expression.Call(ea, ea.Type.GetMethod("ToString", Type.EmptyTypes));
          if (eb.Type != typeof(string)) eb = Expression.Call(eb, eb.Type.GetMethod("ToString", Type.EmptyTypes));
          return Expression.Call(typeof(string).GetMethod("Concat", new Type[] { typeof(string), typeof(string) }), ea, eb);
        }
        var sp = *(Script*)StackPtr; *(Script*)StackPtr = this; MethodInfo mo = null;
        switch (op)
        {
          case 0x20: case 0xd1: mo = refop("op_Multiply", ea, eb); break;
          case 0x21: case 0xd2: mo = refop("op_Division", ea, eb); break;
          case 0x22: case 0xd3: mo = refop("op_Modulus", ea, eb); break;
          case 0x30: case 0xd4: mo = refop("op_Addition", ea, eb); break;
          case 0x31: case 0xd5: mo = refop("op_Subtraction", ea, eb); break;
          case 0x70: case 0xd6: mo = refop("op_BitwiseAnd", ea, eb); break;
          case 0x80: case 0xd7: mo = refop("op_ExclusiveOr", ea, eb); break;
          case 0x90: case 0xd8: mo = refop("op_BitwiseOr", ea, eb); break;
          case 0x40: case 0xd9: mo = refop("op_LeftShift", ea, eb); break;
          case 0x41: case 0xda: mo = refop("op_RightShift", ea, eb); break;
          case 0x50: mo = refop("op_LessThan", ea, eb); break;
          case 0x51: mo = refop("op_GreaterThan", ea, eb); break;
          case 0x52: mo = refop("op_LessThanOrEqual", ea, eb); break;
          case 0x53: mo = refop("op_GreaterThanOrEqual", ea, eb); break;
          case 0x60: mo = refop("op_Equality", ea, eb); break;
          case 0x61: mo = refop("op_Inequality", ea, eb); break;
        }
        if (mo != null)
        {
          var pp = mo.GetParameters();
          ea = Convert(ea, pp[0].ParameterType);
          eb = Convert(eb, pp[1].ParameterType);
        }
        else if (ea.Type != eb.Type)
        {
          if (Convertible(eb.Type, ea.Type) != null) eb = Convert(eb, ea.Type);
          else if (Convertible(ea.Type, eb.Type) != null) ea = Convert(ea, eb.Type);
          else if ((op & 0xf0) == 0x60 && (ea.Type.IsEnum || eb.Type.IsEnum))
          {
            if (0.Equals((ea as ConstantExpression)?.Value)) ea = Expression.Convert(ea, eb.Type);
            if (0.Equals((eb as ConstantExpression)?.Value)) eb = Expression.Convert(eb, ea.Type);
          }
        }

        wt = op >= 0x70 && op <= 0x90 && ea.Type.IsEnum ? ea.Type : null; // | & ^
        if (wt != null)
        {
          var x = wt.GetEnumUnderlyingType();
          if (ea.Type != x) ea = Expression.Convert(ea, x);
          if (eb.Type != x) eb = Expression.Convert(eb, x);
        }
        switch (op)
        {
          case 0x20: eb = Expression.Multiply(ea, eb, mo); break;
          case 0x21: eb = Expression.Divide(ea, eb, mo); break;
          case 0x22: eb = Expression.Modulo(ea, eb, mo); break;
          case 0x30: eb = Expression.Add(ea, eb, mo); break;
          case 0x31: eb = Expression.Subtract(ea, eb, mo); break;
          case 0x40: eb = Expression.LeftShift(ea, eb, mo); break;
          case 0x41: eb = Expression.RightShift(ea, eb, mo); break;
          case 0x50: eb = Expression.LessThan(ea, eb, false, mo); break;
          case 0x51: eb = Expression.GreaterThan(ea, eb, false, mo); break;
          case 0x52: eb = Expression.LessThanOrEqual(ea, eb, false, mo); break;
          case 0x53: eb = Expression.GreaterThanOrEqual(ea, eb, false, mo); break;
          case 0x60: eb = Expression.Equal(ea, eb, false, mo); break;
          case 0x61: eb = Expression.NotEqual(ea, eb, false, mo); break;
          case 0x70: eb = Expression.And(ea, eb, mo); break;
          case 0x80: eb = Expression.ExclusiveOr(ea, eb, mo); break;
          case 0x90: eb = Expression.Or(ea, eb, mo); break;
          case 0xa0: eb = Expression.OrElse(ea, eb); break;
          case 0xb0: eb = Expression.AndAlso(ea, eb); break;
          case 0xc2: eb = Expression.Coalesce(ea, eb); break;
          case 0xd0: eb = Expression.Assign(ea, eb); break; // bugs in Expressions if (((MemberExpression)ea).Member.DeclaringType.IsValueType) ...
          case 0xd1: eb = Expression.Assign(ea, Expression.Multiply(ea, eb, mo)); break;//Expression.MultiplyAssign(ea, eb);
          case 0xd2: eb = Expression.Assign(ea, Expression.Divide(ea, eb, mo)); break; //Expression.DivideAssign(ea, eb);
          case 0xd3: eb = Expression.Assign(ea, Expression.Modulo(ea, eb, mo)); break;//return Expression.ModuloAssign(ea, eb);
          case 0xd4: eb = Expression.Assign(ea, Expression.Add(ea, eb, mo)); break; //return Expression.AddAssign(ea, eb);//if (((MemberExpression)ea).Member.DeclaringType.IsValueType)
          case 0xd5: eb = Expression.Assign(ea, Expression.Subtract(ea, eb, mo)); break; //return Expression.SubtractAssign(ea, eb);
          case 0xd6: eb = Expression.Assign(ea, Expression.And(ea, eb, mo)); break; //return Expression.AndAssign(ea, eb);
          case 0xd7: eb = Expression.Assign(ea, Expression.ExclusiveOr(ea, eb, mo)); break; //return Expression.ExclusiveOrAssign(ea, eb);
          case 0xd8: eb = Expression.Assign(ea, Expression.Or(ea, eb, mo)); break;//return Expression.OrAssign(ea, eb);
          case 0xd9: eb = Expression.Assign(ea, Expression.LeftShift(ea, eb)); break; //return Expression.LeftShiftAssign(ea, eb);
          case 0xda: eb = Expression.Assign(ea, Expression.RightShift(ea, eb)); break; //return Expression.RightShiftAssign(ea, eb);
        }
        if (wt != null) eb = Expression.Convert(eb, wt);
        *(Script*)StackPtr = sp; return eb;
      }
      b = this; a = b.next();
      if (a.n == 1)
      {
        if (a.s[0] == '+') { var t = b.Parse(stack, wt); return t; } //"op_UnaryPlus"
        if (a.s[0] == '-') { var t = b.Parse(stack, wt); return Expression.Negate(t, refop("op_UnaryNegation", t)); }
        if (a.s[0] == '!') { var t = b.Parse(stack, wt); return Expression.Not(t, refop("op_LogicalNot", t)); }
        if (a.s[0] == '~') { var t = b.Parse(stack, wt); return Expression.OnesComplement(t, refop("op_OnesComplement", t)); }
      }
      if (a.n == 2 && a.s[0] == '+' & a.s[1] == '+') return Expression.PreIncrementAssign(b.Parse(stack, wt));
      if (a.n == 2 && a.s[0] == '-' & a.s[1] == '-') return Expression.PreDecrementAssign(b.Parse(stack, wt));
      if (b.n == 2 && b.s[0] == '+' & b.s[1] == '+') return Expression.PostIncrementAssign(a.Parse(stack, wt));
      if (b.n == 2 && b.s[0] == '-' & b.s[1] == '-') return Expression.PostDecrementAssign(a.Parse(stack, wt));

      Expression left = null; Type type = null; bool checkthis = false, priv = false;
      if (a.s[0] == '(')
      {
        a.trim(1, 1); if (b.n != 0 && b.s[0] != '.' && b.s[0] != '[') { var t = b.Parse(stack, wt = GetType(stack, a)); return t.Type != wt ? Expression.Convert(t, wt) : t; }
        left = a.Parse(stack, wt); goto eval;
      }
      if (char.IsNumber(a.s[0]) || a.s[0] == '.')
      {
        if (a.n > 1 && a.s[0] == '0' && (a.s[1] | 0x20) == 'x')
        {
          a.trim(2, 0); if (a.n == 8 && a.s[0] > '7') { left = Expression.Constant(uint.Parse(a.ToString(), NumberStyles.HexNumber)); goto eval; }
          left = Expression.Constant(int.Parse(a.ToString(), NumberStyles.HexNumber)); goto eval;
        }
        var tc = TypeCode.Int32;
        switch (a.s[n - 1] | 0x20)
        {
          case 'f': a.trim(0, 1); tc = TypeCode.Single; break;
          case 'd': a.trim(0, 1); tc = TypeCode.Double; break;
          case 'm': a.trim(0, 1); tc = TypeCode.Decimal; break;
          case 'u': a.trim(0, 1); tc = TypeCode.UInt32; break;
          case 'l': a.trim(0, 1); tc = TypeCode.Int64; if ((a.s[n - 1] | 0x20) == 'u') { a.trim(0, 1); tc = TypeCode.UInt64; } break;
          default: for (int i = 0; i < a.n; i++) if (!char.IsNumber(a.s[i])) { tc = TypeCode.Double; break; } break;
        }
        if (wt != null) { var v = Type.GetTypeCode(wt); if (v > tc) tc = v; }
        left = Expression.Constant(System.Convert.ChangeType(a.ToString(), tc, CultureInfo.InvariantCulture));
        if (wt != null && wt.IsEnum) if (0.Equals(((ConstantExpression)left).Value)) left = Expression.Convert(left, wt);
        goto eval;
      }
      if (a.s[0] == '"') { var ss = new string(a.s, 1, a.n - 2); if (ss.IndexOf('\\') >= 0) ss = Regex.Unescape(ss); left = Expression.Constant(ss); goto eval; }
      if (a.s[0] == '\'') { if (a.n == 3) { left = Expression.Constant(a.s[1]); goto eval; } var ss = Regex.Unescape(new string(a.s, 1, a.n - 2)); if (ss.Length != 1) a.error("syntax"); left = Expression.Constant(ss[0]); goto eval; }
      if (a.equals("true")) { left = Expression.Constant(true); goto eval; }
      if (a.equals("false")) { left = Expression.Constant(false); goto eval; }
      if (a.equals("null")) { left = Expression.Constant(null, wt ?? typeof(object)); goto eval; }
      if (a.equals("typeof")) { a = b.next(); a.trim(1, 1); left = Expression.Constant(GetType(stack, a)); goto eval; }
      if (a.equals("new"))
      {
        for (a = b; b.n != 0 && b.s[0] != '(' && b.s[0] != '[' && b.s[0] != '{'; b.next()) ;
        a.n = (int)(b.s - a.s); a.trim(); var t = GetType(stack, a); a = b.next(); var tc = a.s[0]; a.trim(1, 1);
        if (tc == '[') while (a.n == 0 && b.s[0] == '[') { t = t.MakeArrayType(); a = b.next(); a.trim(1, 1); }
        var ab = stack.list.Count; if (tc != '{') for (; a.n != 0;) stack.list.Add(a.next(',').Parse(stack, null));
        if (tc == '[')
        {
          if (ab != stack.list.Count) left = Expression.NewArrayBounds(t, stack.list.Skip(ab));
          else
          {
            a = b.next(); if (a.n == 0 || a.s[0] != '{') a.error("syntax");
            for (a.trim(1, 1); a.n != 0;) stack.list.Add(Convert(a.next(',').Parse(stack, null), t));
            left = Expression.NewArrayInit(t, stack.list.Skip(ab));
          }
        }
        else
        {
          if (ab == stack.list.Count) left = Expression.New(t);
          else
          {
            var ct = GetMember(t, null, BindingFlags.Instance | BindingFlags.Public, stack, ab, stack.usings.Count) as ConstructorInfo;
            if (ct == null) error("invalid ctor");
            left = Expression.New(ct, stack.list.Skip(ab));
          }
          if (b.n != 0 && b.s[0] == '{') { a = b.next(); a.trim(1, 1); tc = '{'; }
          if (tc == '{')
          {
            var ic = left.Type.GetInterface("ICollection`1"); var ns = stack.list.Count;
            if (ic != null)
            {
              for (t = ic.GetGenericArguments()[0]; a.n != 0;) stack.list.Add(Convert(a.next(',').Parse(stack, t), t));
              left = Expression.ListInit((NewExpression)left, stack.list.Skip(ns));
            }
            else
            {
              var pp = stack.usings; var np = pp.Count;//var list = new List<MemberAssignment>();
              for (; a.n != 0;) { var p = a.next(','); var e = Expression.PropertyOrField(left, p.next('=').ToString()); pp.Add(Expression.Bind(e.Member, Convert(p.Parse(stack, e.Type), e.Type))); }
              left = Expression.MemberInit((NewExpression)left, pp.Skip(np).Cast<MemberAssignment>()); pp.RemoveRange(np, pp.Count - np);
            }
          }
        }
        stack.list.RemoveRange(ab, stack.list.Count - ab); goto eval;
      }
      for (int i = stack.Count - 1; i >= 0; i--) if (a.equals(stack[i].Name)) { left = stack[i]; goto eval; }
      var sa = a.ToString();
      for (int i = stack.nstats; --i >= 0;)
      {
        var fi = ((Type)stack.usings[i]).GetMember(sa, MemberTypes.Property | MemberTypes.Field, BindingFlags.Static | BindingFlags.Public);
        if (fi.Length != 0) { left = Expression.MakeMemberAccess(left, fi[0]); goto eval; }
      }
      for (d = a, c = b; c.n != 0 && (c.s[0] == '.' || c.s[0] == '·');)
      {
        if ((type = GetType(stack, d, false)) != null) { b = c; goto eval; }
        c.next(); c.next(); d = a; d.n = (int)(c.s - d.s); d.trim();
      }
      left = stack.@this; checkthis = true; eval:
      for (; b.n != 0 || checkthis; type = null, checkthis = priv = false)
      {
        if (checkthis) goto call; a = b.next();
        if (a.s[0] == '[')
        {
          a.trim(1, 1); var ab = stack.list.Count; for (; a.n != 0;) stack.list.Add(a.next(',').Parse(stack, null));
          left = left.Type.IsArray ? Expression.ArrayAccess(left, stack.list.Skip(ab)) : Expression.Property(left, left.Type == typeof(string) ? "Chars" : "Item", stack.list.Skip(ab).ToArray());
          stack.list.RemoveRange(ab, stack.list.Count - ab); continue;
        }
        if (a.s[0] == '(')
        {
          a.trim(1, 1); var ab = stack.list.Count; for (; a.n != 0;) stack.list.Add(a.next(',').Parse(stack, null));
          var pb = left.Type.GetMethod("Invoke").GetParameters(); if (pb.Length != stack.list.Count - ab) a.error("invalid param count");
          for (int i = 0; i < pb.Length; i++) stack.list[ab + i] = Convert(stack.list[ab + i], pb[i].ParameterType);
          left = Expression.Invoke(left, stack.list.Skip(ab)); stack.list.RemoveRange(ab, stack.list.Count - ab); continue;
        }
        if (a.n != 1 || !(a.s[0] == '.' || (priv = a.s[0] == '·'))) a.error("syntax");
        a = b.next(); call:
        var bf = (type != null ? BindingFlags.Static : BindingFlags.Instance) | BindingFlags.Public;
        var t1 = type ?? left.Type; if (priv || t1 == stack.@this.Type) bf |= BindingFlags.NonPublic | BindingFlags.FlattenHierarchy; // BindingFlags.NonPublic;
        var s = a.ToString();
        if (b.n > 1 && (b.s[0] == '(' || b.s[0] == '<'))
        {
          var ng = stack.usings.Count;
          if (b.s[0] == '<') { c = b.next(); c.trim(1, 1); for (; c.n != 0;) stack.usings.Add(GetType(stack, c.next(','))); }
          c = b.next(); c.trim(1, 1); var ab = stack.list.Count;
          for (; c.n != 0;) { var t = c.next(','); if (t.take("ref") || t.take("out")) { } stack.list.Add(t.Parse(stack, null)); }
          var me = GetMember(t1, s, bf, stack, ab, ng);
          if (me == null && checkthis)
          {
            left = null;
            for (int i = stack.nstats; --i >= 0;) if ((me = GetMember((Type)stack.usings[i], s, BindingFlags.Static | BindingFlags.Public, stack, ab, ng)) != null) break;
          }
          if (me == null && left != null)
          {
            stack.list.Insert(ab, left); left = null; if (extensions == null) GetExtensions();
            for (int i = 0; i < extensions.Length; i++) if ((me = GetMember(extensions[i], s, BindingFlags.Static | BindingFlags.Public, stack, ab, ng)) != null) break; ;
          }
          if (me == null) { a.n = (int)(b.s - a.s); a.trim(); a.error("unknown method" + " " + a.ToString()); }
          left = Expression.Call(left, (MethodInfo)me, stack.list.Skip(ab)); stack.list.RemoveRange(ab, stack.list.Count - ab); stack.usings.RemoveRange(ng, stack.usings.Count - ng);
          if (type == typeof(Debug) && !Debugger.IsAttached && (me.GetCustomAttribute(typeof(ConditionalAttribute)) as ConditionalAttribute)?.ConditionString == "DEBUG") left = Expression.Empty();
          continue;
        }
        var x = t1.GetMember(s, MemberTypes.Property | MemberTypes.Field, bf);
        if (x.Length != 0) { left = Expression.MakeMemberAccess(left, x[0]); continue; }
        if ((bf & BindingFlags.Static) != 0) { type = t1.GetNestedType(s, bf); if (type != null) goto eval; }
        left = null; break;
      }
      if (left == null || checkthis) a.error();
      return left;
    }

    MethodInfo refop(string op, Expression a)
    {
      Type t1 = a.Type; if (t1.IsPrimitive) return null;
      var me = t1.GetMethod(op, BindingFlags.Static | BindingFlags.Public, null, new Type[] { t1.MakeByRefType() }, null); if (me != null) return me;
      return null;
    }
    MethodInfo refop(string op, Expression a, Expression b)
    {
      Type t1 = a.Type, t2 = b.Type; if (t1 == t2 && t1.IsPrimitive) return null;
      MethodInfo match = null;
      for (int i = 0; i < 2; i++)
      {
        var mm = (i == 0 ? t1 : t2).GetMember(op, MemberTypes.Method, BindingFlags.Static | BindingFlags.Public);
        for (int k = 0; k < mm.Length; k++)
        {
          var m = (MethodInfo)mm[k]; var pp = m.GetParameters();
          if (pp[0].ParameterType == t1 && pp[1].ParameterType == t2) return m;
          if (Convertible(t1, pp[0].ParameterType) == null) continue;
          if (Convertible(t2, pp[1].ParameterType) == null) continue;
          match = m;
        }
      }
      return match;
      //var tt = new Type[2];
      //for (int i = 0; i < 6; i++) // < 8 for native
      //{
      //  if ((i & 1) == 0) { tt[0] = (i & 2) == 0 ? t1.MakeByRefType() : t1; tt[1] = (i & 4) == 0 ? t2.MakeByRefType() : t2; }
      //  else if (t1 == t2) continue;
      //  var me = ((i & 1) != 0 ? t1 : t2).GetMethod(op, BindingFlags.Static | BindingFlags.Public, null, tt, null);
      //  if (me != null) return me;
      //}
      //return null;
    }

    static Type GetType(Stack stack, Script a, bool ex = true)
    {
      if (a.s[a.n - 1] == ']')
      {
        var p = a; for (; ; ) { var x = p; x.next(); if (x.n == 0) break; p = x; }
        a.n = (int)(p.s - a.s); a.trim(); return GetType(stack, a, ex).MakeArrayType();
      }
      if (a.s[a.n - 1] == '>')
      {
        var p = a; for (; p.n != 0 && p.s[0] != '<'; p.next()) ; a.n = (int)(p.s - a.s); a.trim(); p.trim(1, 1);
        int n = 0; for (var z = p; z.n != 0; z.next(','), n++) ;
        var u = GetType(stack, $"{a}`{n}"); if (u == null && ex) a.error("unknown type");
        var w = new Type[n]; for (int i = 0; p.n != 0; i++) w[i] = GetType(stack, p.next(',')); return u.MakeGenericType(w);
      }
      if (a.equals("void")) return typeof(void);
      if (a.equals("bool")) return typeof(bool);
      if (a.equals("char")) return typeof(char);
      if (a.equals("sbyte")) return typeof(sbyte);
      if (a.equals("byte")) return typeof(byte);
      if (a.equals("int")) return typeof(int);
      if (a.equals("uint")) return typeof(uint);
      if (a.equals("short")) return typeof(short);
      if (a.equals("ushort")) return typeof(ushort);
      if (a.equals("long")) return typeof(long);
      if (a.equals("ulong")) return typeof(ulong);
      if (a.equals("decimal")) return typeof(decimal);
      if (a.equals("float")) return typeof(float);
      if (a.equals("double")) return typeof(double);
      if (a.equals("string")) return typeof(string);
      if (a.equals("object")) return typeof(object);
      var b = a; var s = b.next('.').ToString();
      for (int i = stack.nstats; --i >= 0;)
      {
        var st = ((Type)stack.usings[i]).GetNestedType(s); if (st == null) continue;
        while (b.n != 0) if ((st = st.GetNestedType(b.next('.').ToString())) == null) break; return st;
      }
      var t = GetType(stack, a.ToString().Replace(" ", string.Empty));
      if (t == null && ex) a.error("unknown type");
      return t;
    }
    static Type GetType(Stack stack, string s)
    {
      var ss = stack.usings; var nu = stack.nusings; Type t;
      for (int i = stack.nstats; i < nu; i++) { var u = (string)ss[i]; if (u.StartsWith(s) && (u.Length == s.Length || s[u.Length] == '.')) return null; }
      var a = assemblys ?? (assemblys = AppDomain.CurrentDomain.GetAssemblies());
      for (; ; )
      {
        var x = s.LastIndexOf('.');
        if (x != -1) for (int i = 0; i < a.Length; i++) if ((t = a[i].GetType(s)) != null) return t;
        if (x == -1) for (int i = 0; i < a.Length; i++) for (int k = stack.nstats; k < nu; k++) if ((t = a[i].GetType($"{(string)ss[k]}.{s}")) != null) return t;
        if (x == -1) return null; s = s.Substring(0, x) + '+' + s.Substring(x + 1);
      }
    }
    static MethodBase GetMember(Type type, string name, BindingFlags bf, Stack stack, int xt, int xg)
    {
      var mm = type.GetMember(name ?? ".ctor", name != null ? MemberTypes.Method : MemberTypes.Constructor, bf); if (mm.Length == 0) return null;
      var me = (MethodBase)null; var pp = (ParameterInfo[])null; var vt = (Type)null; var tt = stack.list; int nt = tt.Count - xt, best = int.MaxValue;
      for (int i = 0; i < mm.Length; i++)
      {
        var mt = (MethodBase)mm[i];
        if (stack.usings.Count > xg) if (!mt.IsGenericMethod || mt.GetGenericArguments().Length != stack.usings.Count - xg) continue;
        var pt = mt.GetParameters(); var lt = pt.Length;
        var at = lt != 0 && pt[lt - 1].ParameterType.IsArray && pt[lt - 1].IsDefined(typeof(ParamArrayAttribute), false) ? pt[lt - 1].ParameterType : null;
        if (at != null) if (lt == nt && Convertible(tt[xt + lt - 1].Type, at) != null) at = null; else at = at.GetElementType();
        if (lt < nt && !(at != null && nt >= lt - 1)) continue;
        if (lt > nt && !(pt[nt].HasDefaultValue || (at != null && nt == lt - 1))) continue;
        int t1 = 0, t2 = 0, conv = 0;
        for (int t = 0; t < nt; t++)
        {
          var p1 = tt[xt + t].Type; var p2 = at != null && t >= lt - 1 ? at : pt[t].ParameterType;
          if (p1 == p2) { t1++; continue; }
          if (p2.IsPointer) break;
          if (p2.IsByRef && p2.GetElementType() == p1) { t1++; continue; }
          if (p1 == typeof(Script))
          {
            var a = p2.GetMethod("Invoke")?.GetParameters(); if (a == null) break;
            var b = (Script)((ConstantExpression)tt[xt + t]).Value; b = b.next();
            if (b.s[0] == '(') b.trim(1, 1); int c = 0; for (; b.n != 0; b.next(','), c++) ;
            if (c != a.Length) break; t1++; continue;
          }
          if (p2.ContainsGenericParameters) { t2++; continue; }
          if (p1 == typeof(object) && tt[xt + t] is ConstantExpression && ((ConstantExpression)tt[xt + t]).Value == null) { t1++; continue; } //null
          if (Convertible(p1, p2) != null) { conv += Math.Abs(Type.GetTypeCode(p1) - Type.GetTypeCode(p2)); t2++; continue; }
          break;
        }
        if (t1 + t2 < nt) continue;
        if (conv > best) continue; best = conv;
        if (at != null && me != null) continue;
        me = mt; pp = pt; vt = at; if (t1 == nt && at == null) break;
      }
      if (me == null) return null;
      if (me.IsGenericMethod)
      {
        var aa = me.GetGenericArguments();
        if (stack.usings.Count > xg)
          for (int t = 0; t < aa.Length; t++) aa[t] = (Type)stack.usings[xg + t];
        else
        {
          for (int i = 0, j, f = 0; i < pp.Length && f < aa.Length; i++)
          {
            var t = pp[i].ParameterType; if (!t.ContainsGenericParameters) continue;
            var s = tt[xt + i].Type;
            if (!t.IsConstructedGenericType)
            {
              if (t.HasElementType) t = t.GetElementType();
              for (j = 0; j < aa.Length && !(aa[j].Name == t.Name && aa[j].ContainsGenericParameters); j++) ;
              if (j == aa.Length) continue; aa[j] = s; f++; continue;
            }
            var a = t.GetGenericArguments();
            if (s == typeof(Script))
            {
              for (j = 0; j < aa.Length && !(aa[j].Name == a[a.Length - 1].Name && aa[j].ContainsGenericParameters); j++) ;
              if (j == aa.Length) continue; var bb = me.GetGenericArguments();
              for (int x = 0, y; x < a.Length; x++) for (y = 0; y < bb.Length; y++) if (bb[y].Name == a[x].Name && a[x].ContainsGenericParameters) { a[x] = aa[y]; break; }
              var r = ((Script)((ConstantExpression)tt[xt + i]).Value).Parse(stack, t.GetGenericTypeDefinition().MakeGenericType(a));
              tt[xt + i] = r; aa[j] = ((LambdaExpression)r).ReturnType; f++; continue;
            }
            //if (!s.IsGenericType) { s = s.GetInterface(t.Name); if (s == null) continue; }
            if (t.IsInterface && t.Name != s.Name) { s = s.GetInterface(t.Name); if (s == null) continue; }
            var v = s.GetGenericArguments(); if (v.Length != a.Length) continue;
            for (int x = 0; x < a.Length; x++) for (int y = 0; y < aa.Length; y++) if (aa[y].Name == a[x].Name && aa[y].ContainsGenericParameters) { aa[y] = v[x]; f++; break; }
          }
        }
        me = ((MethodInfo)me).MakeGenericMethod(aa); pp = me.GetParameters();
      }
      for (int i = 0, n = vt != null ? pp.Length - 1 : pp.Length; i < n; i++)
      {
        var p = pp[i]; var t = p.ParameterType;
        if (i >= nt)
        {
          if (p.DefaultValue == null) { tt.Add(Expression.Default(t.IsByRef ? t.GetElementType() : t)); continue; }
          tt.Add(Expression.Constant(p.DefaultValue, t)); continue;
        }
        var pa = tt[xt + i];
        if (pa.Type == typeof(Script)) { pa = tt[xt + i] = ((Script)((ConstantExpression)pa).Value).Parse(stack, t); continue; }
        tt[xt + i] = Convert(pa, t);
      }
      if (vt != null)
      {
        xt += pp.Length - 1; for (int i = xt; i < tt.Count; i++) tt[i] = Convert(tt[i], vt);
        var e = Expression.NewArrayInit(vt, tt.Skip(xt)); tt.RemoveRange(xt, tt.Count - xt); tt.Add(e);
      }
      return me;
    }
    static void GetExtensions()
    {
      extensions = new Type[] { typeof(CSG), typeof(CDX), typeof(Enumerable), typeof(ParallelEnumerable) };
      //extensions = assemblys.SelectMany(p => p.GetExportedTypes()).Where(p => p.GetCustomAttribute(typeof(System.Runtime.CompilerServices.ExtensionAttribute)) != null).ToArray();
    }
    static Assembly[] assemblys; static Type[] extensions;
    static object Convertible(Type ta, Type tb, bool imp = true)
    {
      if (tb.IsAssignableFrom(ta)) return tb;
      int b = (int)Type.GetTypeCode(tb) - 4;
      if (b < 0)
      {
        if (tb.IsByRef) return Convertible(ta, tb.GetElementType(), imp);
        if (!imp) return null;
        if (!ta.IsValueType || !tb.IsValueType) return null; //Debug.WriteLine(ta + " " + tb);
        for (int j = 0; j < 2; j++)
        {
          var aa = (j == 0 ? tb : ta).GetMember("op_Implicit", MemberTypes.Method, BindingFlags.Static | BindingFlags.Public);
          for (int i = 0; i < aa.Length; i++)
            if (Convertible(ta, ((MethodBase)aa[i]).GetParameters()[0].ParameterType, false) != null) return aa[i];
        }
        return null;
      }
      int a = (int)Type.GetTypeCode(ta) - 4, m;
      switch (a)
      {
        case 0: m = 0b111111110000; break;
        case 1: m = 0b111010101000; break;
        case 2: m = 0b111111111001; break;
        case 3: m = 0b111010100000; break;
        case 4: m = 0b111111100000; break;
        case 5: m = 0b111010000000; break;
        case 6: m = 0b111110000000; break;
        case 7: m = 0b111000000000; break;
        case 8: m = 0b111000000000; break;
        case 9: m = 0b010000000000; break;
        default: return null;
      }
      return (m & (1 << b)) != 0 ? tb : null;
    }

    static Expression Convert(Expression a, Type t)
    {
      if (a.Type == t) return a;
      if (t == typeof(object) && !a.Type.IsClass) return Expression.Convert(a, t);
      if (t.IsAssignableFrom(a.Type)) return a;
      //if (t.IsByRef && t.GetElementType() == a.Type) return a;
      if (t.IsByRef) return Convert(a, t.GetElementType());
      if (a is ConstantExpression c)
      {
        var v = c.Value; if (v == null) return Expression.Constant(v, t);
        if (t.IsPrimitive && v is IConvertible) return Expression.Constant(System.Convert.ChangeType(v, t), t);
      }
      var x = Convertible(a.Type, t); if (x == null) throw new Exception("invalid conversion");
      if (x is MethodInfo me) return Expression.Convert(Convert(a, me.GetParameters()[0].ParameterType), t, me);
      return Expression.Convert(a, t);
    }
    void check(List<ParameterExpression> stack, int stackab)
    {
      if (!isname()) error();
      for (int i = stackab; i < stack.Count; i++) if (equals(stack[i].Name)) error("duplicate name");
    }
    class Exception : System.Exception { public Exception(string s) : base(s) { } }
    void error(string s = null) { if (n <= 0) n = 1; *(Script*)StackPtr = this; throw new Exception(s ?? "syntax"); }
    Script next() { var a = this; a.n = token(); s += a.n; n -= a.n; trim(); return a; }
    Script next(char c) { var p = this; for (; n != 0;) { var t = next(); if (t.n == 1 && t.s[0] == c) { p.n = (int)(t.s - p.s); p.trim(); break; } } return p; }
    Script block()
    {
      var p = this;
      for (var e = true; n != 0;)
      {
        //if (s[0] == ')' || s[0] == '}') { n = 1; error(); }
        var t = next(); if (t.s[0] == ';') { p.n = (int)(t.s - p.s); p.trim(); break; }
        if (e && t.s[t.n - 1] == '}') { p.n = (int)(t.s + t.n - p.s); p.trim(); break; }
        if (t.n == 3 && t.equals("new")) e = false;
      }
      return p;
    }
    Script gettype()
    {
      for (var t = this; ;)
      {
        var a = next(); if (n != 1 && s[0] == '.') { next(); continue; }
        if (a.isname() && (isname() || (n != 1 && (s[0] == '<' || s[0] == '['))))
        {
          if (s[0] == '<') next();
          while (n != 0 && s[0] == '[') { var i = next(); i.trim(1, 1); if (i.n != 0) goto r; }
          t.n = (int)(s - t.s); t.trim(); return t;
        }
      r: this = t; t.n = 0; return t;
      }
    }
    bool equals(string name)
    {
      var l = name.Length; if (l != n) return false;
      for (int i = 0; i < l; i++) if (s[i] != name[i]) return false; return true;
    }
    int token()
    {
      if (n == 0) return 0;
      int x = 0; var c = s[x++];
      if (c == '(' || c == '{' || c == '[')
      {
        for (int k = 1; x < n;)
        {
          c = s[x++];
          if (c == '"' || c == '\'') { for (char v; x < n && (v = s[x++]) != c;) if (v == '\\') x++; continue; }
          if (c == '(' || c == '{' || c == '[') { k++; continue; }
          if (c == ')' || c == '}' || c == ']') { if (--k == 0) break; continue; }
        }
        return x;
      }
      if (c == '"' || c == '\'') { for (char v; x < n && (v = s[x++]) != c;) if (v == '\\') x++; return x; }
      if (char.IsLetter(c) || c == '_') { for (; x < n && (char.IsLetterOrDigit(c = s[x]) || c == '_'); x++) ; return x; }
      if (c == '0' && x < n && (s[x] | 0x20) == 'x') { for (++x; x < n && char.IsLetterOrDigit(c = s[x]); x++) ; return x; }
      if (char.IsNumber(c) || (c == '.' && x < n && char.IsNumber(s[x])))
      {
        for (; x < n && char.IsNumber(c = s[x]); x++) ;
        if (c == '.') for (++x; x < n && char.IsNumber(c = s[x]); x++) ;
        if ((c | 0x20) == 'e') { for (++x; x < n && char.IsNumber(c = s[x]); x++) ; if (c == '+' || c == '-') x++; for (; x < n && char.IsNumber(c = s[x]); x++) ; }
        c |= (char)0x20; if (c == 'f' || c == 'd' || c == 'm') x++;
        return x;
      }
      if (c == ',' || c == ';' || c == '.') return x;
      if (c == '<')
      {
        for (int k = 1, y = x; y < n;)
        {
          var z = s[y++]; if (z == '<') { if (s[y - 2] == z) break; k++; continue; }
          if (z == '>') { if (--k == 0) return y; continue; }
          if (!(z <= ' ' || z == '.' || z == ',' || z == '_' || char.IsLetterOrDigit(z))) break;
        }
      }
      if (x == n) return x;
      if (c == '+' || c == '-' || c == '=') { if (s[x] == c) return x + 1; if (c != '=' && s[x] != '=') return x; }
      for (; x < n && (c = s[x]) > ' ' && c != '(' && c != '.' && c != '_' && c != '"' && c != '\'' && !char.IsLetterOrDigit(c); x++) ;//for (; x < e && "/=+-*%&|^!?:<>~".IndexOf(code[x]) >= 0; x++) ;
      return x;
    }
    int opcode()
    {
      switch (n)
      {
        case 1: switch (s[0]) { case '+': return 0x30; case '-': return 0x31; case '*': return 0x20; case '/': return 0x21; case '%': return 0x22; case '=': return 0xd0; case '?': return 0xc0; case '|': return 0x90; case '^': return 0x80; case '&': return 0x70; case '<': return 0x50; case '>': return 0x51; } break;
        case 2:
          if (s[1] == '=') { switch (s[0]) { case '*': return 0xd1; case '/': return 0xd2; case '%': return 0xd3; case '+': return 0xd4; case '-': return 0xd5; case '&': return 0xd6; case '^': return 0xd7; case '|': return 0xd8; case '=': return 0x60; case '!': return 0x61; case '<': return 0x52; case '>': return 0x53; } break; }
          if (s[1] == 's') { switch (s[0]) { case 'i': return 0x54; case 'a': return 0x55; } break; }
          if (s[0] == s[1]) { switch (s[0]) { case '<': return 0x40; case '>': return 0x41; case '&': return 0xb0; case '?': return 0xc2; case '|': return 0xa0; } break; }
          if (s[0] == '=' && s[1] == '>') return 0xdf;
          break;
        case 3: if (s[2] == '=' && s[0] == s[1]) switch (s[0]) { case '<': return 0xd9; case '>': return 0xda; } break;
      }
      return 0;
    }
    void trim()
    {
      for (; n != 0 && *s <= 32; s++, n--) ;
      for (; n != 0 && s[n - 1] <= 32; n--) ;
    }
    void trim(int a, int b = 0)
    {
      s += a; n -= a + b; if (n < 0) error(); trim();
    }
    bool isname()
    {
      return n != 0 && (char.IsLetter(s[0]) || s[0] == '_');
    }
    bool take(string v) { var l = v.Length; if (l > n) return false; for (int i = 0; i < l; i++) if (s[i] != v[i]) return false; s += l; n -= l; trim(); return true; }
  }

}
