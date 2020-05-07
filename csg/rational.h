#pragma once
#pragma warning(disable:4723) //div zero possible

struct Rational
{
  Rational()
  {
    num = 0; den = 3;
  }
  Rational(int v)
  {
    num = v; den = 3;
  }
  Rational(const Rational& b)
  {
    num = b.num; den = b.den; addref();
  }
  Rational(double v)
  {
    if (v == 0) { num = 0; den = 3; return; }
    int h = ((int*)&v)[1], e = ((h >> 20) & 0x7FF) - 1075; _ASSERT(e != 0x3cc); //NaN
    auto ct = buffer + 2; auto r = ct + ct[-1] + 2;
    r[-1] = 2; r[0] = *(UINT*)&v; r[1] = (((UINT)h & 0x000FFFFF) | 0x00100000);
    if (e > 0) shl(r, e); auto p = r + r[-1];
    p[0] = p[1] = 1; if (e < 0) shl(p + 1, -e);
    this->Rational::Rational(ct, r, v < 0);
  }
  Rational(const DECIMAL& v)
  {
    auto ct = buffer + 2;
    auto r = ct + ct[-1] + 2; *(UINT64*)&r[0] = v.Lo64; r[2] = v.Hi32; r[-1] = r[2] != 0 ? 3u : r[1] != 0 ? 2u : 1u;
    auto s = r + r[-1] + 1;
    if (v.scale == 0) s[-1] = s[0] = 1;
    else pow10(v.scale, s);
    this->Rational::Rational(ct, r, v.sign != 0);
  }
  ~Rational()
  {
    release();
  }
  BSTR ToString() const
  {
    return ToString(63, 0x3);
  }
  BSTR ToString(int digits, int flags = 0x3) const //1: reps; 2: '…'; 4: CurrentCulture.NumberFormat, 0x1000: ptr
  {
    UINT tt[4]; const UINT* num = push(tt) - 1, * den = &num[num[0] + 2];
    auto nn = (num[0] > den[-1] ? num[0] : den[-1]) + 2;
    auto ct = buffer + 2; auto p1 = ct + ct[-1] + 1; auto p2 = p1 + nn + 1; auto p3 = p2 + nn;
    auto ss = (WCHAR*)(p3 + nn); UINT ns = 0, ab; INT64 ten = 0xA00000001;
    copy(p1 - 1, num, num[0] + 1); div(p1, den, p2);
    if (this->num < 0) ss[ns++] = '-';
    if (p2[-1] == 1 && p2[0] == 0) ss[ns++] = '0';
    else
    {
      for (ab = ns; p2[-1] > 1 || p2[0] != 0;) { div(p2, ((UINT*)&ten) + 1, p3); ss[ns++] = (WCHAR)('0' + *p2); auto t = p2; p2 = p3; p3 = t; }
      for (int i = ab, k = ns - 1; i < k; i++, k--) { auto t = ss[i]; ss[i] = ss[k]; ss[k] = t; }
    }
    for (int x = 0; p1[-1] > 1 || p1[0] != 0; x++)
    {
      if (x == digits) { if ((flags & 2) != 0) ss[ns++] = L'…'; break; }
      if (x == 0) ss[ns++] = '.'; // (flags & 4) != 0 ? '.' : ',' ...
      mul(p1, ((UINT*)&ten) + 1, p2);
      div(p2, den, p1); auto c = (WCHAR)('0' + p1[0]);
      if ((flags & 1) != 0) //reps
      {
        auto pp = (UINT*)(ss + (digits + ns - x));
        for (int j = 0; j < x; j++, pp += pp[0] + 1)
        {
          if (ss[ab = ns - x + j] != c) continue;
          if (!equals(pp, p2 - 1, pp[0] + 1)) continue;
          //int i = 0; for (; i <= (int)pp[0] && pp[i] == p2[i - 1]; i++); if (i <= (int)pp[0]) continue;
          for (int i = ns++; i > (int)ab; i--) ss[i] = ss[i - 1]; ss[ab] = '\''; goto ex;// return SysAllocStringLen(ss, ns);
        }
        copy(pp, p2 - 1, p2[-1] + 1);// for (int i = 0; i <= (int)p2[-1]; i++) pp[i] = p2[i - 1];
      }
      ss[ns++] = c; auto t = p1; p1 = p2; p2 = t;
    }
  ex: if (flags & 0x1000) { ss[((UINT*)ss)[-1] = ns] = 0; return ss; }
    return SysAllocStringLen(ss, ns);
  }
  static Rational Parse(const WCHAR* p, int n)
  {
    Rational a = 0, b = a, e = 1, f = 10; //3.42'56
    for (int i = n - 1, c; i >= 0; i--)
    {
      if ((c = p[i]) >= '0' && c <= '9') { if (c != '0') a = a + e * (c - '0'); e = e * f; continue; }
      if (c == '-') { a = -a; continue; }
      if (c == '.' || c == ',') { b = e; continue; }
      if (c == '\'') { a = a * e / (e - 1); continue; }
      if (c == '/') return Parse(p, i) / a;
      if ((c | 0x20) == 'e') { _ASSERT(a.den == 3); return Parse(p, i) * pow(10, a.num); } // < e +/-INT_MAX 
    }
    if (b.sign() != 0) a = a / b;
    return a;
  }
  bool Equals(const Rational& b) const
  {
    const Rational& a = *this;
    if (*(UINT64*)&a.den == *(UINT64*)&b.den) return true;
    if ((a.den | b.den) & 1) return false;
    if (a.num < 0 != b.num < 0) return false;
    auto pa = a.getptr();
    auto pb = b.getptr();
    auto n1 = pa[-1] + pa[pa[-1]];
    auto n2 = pb[-1] + pb[pb[-1]]; if (n1 != n2) return false;
    return equals(pa, pb, n2 + 1);
    //for (UINT i = 0; i <= n2; i++) if (pa[i] != pb[i]) return false;
    //return true;
  }
  int CompareTo(const Rational& b) const
  {
    int sa = sign(), sb = b.sign();
    if (sa != sb) return sa > sb ? +1 : -1; if (sa == 0) return 0;
    UINT tt[8]; const UINT* s = push(tt), * t = b.push(tt + 4);
    auto ct = buffer + 2; UINT* u = ct + ct[-1] + 1, * v;
    mul(s, t + t[-1] + 1, u);
    mul(t, s + s[-1] + 1, v = u + u[-1] + 1);
    return cmp(u, v) * sa;
  }
  UINT GetHashCode() const
  {
    if (den & 1) return num ^ den;
    auto p = getptr(); auto n = p[-1] + p[p[-1]];
    UINT h = 0; for (UINT i = 0; i <= n; h = ((h << 7) | (p[i] >> 25)) ^ p[i], i++); return h;
  }
  int sign() const
  {
    return num < 0 ? -1 : (num > 0 || !(den & 1)) ? +1 : 0;
  }
  void neg()
  {
    if (den & 1) num = -num;
    else num ^= 0x80000000;
  }
  explicit operator double() const
  {
    if (den & 1) return (double)num / (den >> 1);
    auto bits = getptr();
    int a = (int)bits[-1] - 1, b = bits[-1] + bits[bits[-1]], e = ((a << 1) + 2 - b) << 5, x;
    auto n = (UINT64)bits[a] << 32; auto d = (UINT64)bits[b] << 32;
    if (a - 0 > 0) { n |= bits[a - 1]; if (a - 1 > 0) { n = (n << (x = chz(bits[a]))) | (bits[a - 2] >> (32 - x)); e -= x; } }
    if (b - 2 > a) { d |= bits[b - 1]; if (b - 3 > a) { d = (d << (x = chz(bits[b]))) | (bits[b - 2] >> (32 - x)); e += x; } }
    auto r = (double)n / d; if (e != 0) r *= ::pow(2, e);
    return num < 0 ? -r : +r;
  }
  explicit operator DECIMAL() const
  {
    auto ct = buffer + 2; UINT t0[4], t1[4], t2[4];
    const UINT* s = push(t0) - 1, * t = s + s[0] + 1; UINT* p;
    auto d1 = (int)(s[0] << 5) - chz(s[s[0]]) - 96;
    if ((d1 = d1 > 0 ? d1 : 0) != 0) { shr(s + 1, &t1[1], d1); s = t1; }
    auto d2 = (int)(t[0] << 5) - chz(t[t[0]]) - 96;
    if ((d2 = d2 > 0 ? d2 : 0) != 0) { shr(t + 1, &t2[1], d2); t = t2; }
    DECIMAL b = { 0 }; (p = (UINT*)&b)[2] = t[1]; if (t[0] > 1) { p[3] = t[2]; if (t[0] > 2) p[1] = t[3]; }
    DECIMAL a = { 0 }; (p = (UINT*)&a)[2] = s[1]; if (s[0] > 1) { p[3] = s[2]; if (s[0] > 2) p[1] = s[3]; }
    VarDecDiv(&a, &b, &a);
    if (d1 != d2) { VarDecFromR8(::pow(2, d1 - d2), &b); VarDecMul(&b, &a, &a); }
    if (sign() < 0) p[0] |= (1u << 31); return a;
  }
  Rational operator -() const
  {
    auto b = *this; b.neg(); return b;
  }
  const Rational& operator =(const Rational& b)
  {
    b.addref(); release(); num = b.num; den = b.den; return *this;
  }
  Rational operator +(const Rational& b) const
  {
    const Rational& a = *this;
    auto sa = a.sign(); if (sa == 0) return b;
    auto sb = b.sign(); if (sb == 0) return a;
    auto ct = buffer + 2; UINT tt[8]; //auto ps = ct[-1];
    const UINT* s = a.push(tt), * t = b.push(tt + 4); int si;
    UINT* u = ct + ct[-1] + 2, * v;
    mul(s, t + t[-1] + 1, u);
    mul(t, s + s[-1] + 1, v = u + u[-1] + 1);
    if (sa > 0 == sb > 0) { add(u, v, u); si = sa > 0 ? +1 : -1; }
    else
    {
      if ((si = cmp(u, v)) == 0) { return 0; }
      sub(si > 0 ? u : v, si > 0 ? v : u, u); si = sa > 0 == si > 0 ? +1 : -1;
    }
    mul(s + s[-1] + 1, t + t[-1] + 1, u + u[-1] + 1);
    return Rational(ct, u, si < 0);
  }
  Rational operator -(const Rational& b) const
  {
    auto t = b; t.neg();
    return *this + t;
  }
  Rational operator *(const Rational& b) const
  {
    const Rational& a = *this;
    auto sa = a.sign(); if (sa == 0) return a; if (a.den == 3 && abs(a.num) == 1) return a.num == 1 ? b : -b;
    auto sb = b.sign(); if (sb == 0) return b; if (b.den == 3 && abs(b.num) == 1) return b.num == 1 ? a : -a;
    auto ct = buffer + 2; UINT tt[8];// auto ps = ct[-1];
    const UINT* s = a.push(tt), * t = b.push(tt + 4); UINT* r = ct + ct[-1] + 2;
    mul(s, t, r); mul(s + s[-1] + 1, t + t[-1] + 1, r + r[-1] + 1);
    return Rational(ct, r, sa > 0 != sb > 0);
  }
  Rational operator /(const Rational& b) const
  {
    const Rational& a = *this;
    auto sb = b.sign(); if (!sb) sb = 1 / sb; //EXCEPTION_INT_DIVIDE_BY_ZERO
    auto sa = a.sign(); if (sa == 0) return a;
    auto ct = buffer + 2; UINT tt[8];
    const UINT* s = a.push(tt), * t = b.push(tt + 4); UINT* r = ct + ct[-1] + 2;
    mul(s, t + t[-1] + 1, r); mul(s + s[-1] + 1, t, r + r[-1] + 1);
    return Rational(ct, r, sa > 0 != sb > 0);
  }
  inline bool operator ==(const Rational& b) const
  {
    return Equals(b);
  }
  inline bool operator !=(const Rational& b) const
  {
    return !Equals(b);
  }
  inline bool operator <=(const Rational& b) const
  {
    return CompareTo(b) <= 0;
  }
  inline bool operator >=(const Rational& b) const
  {
    return CompareTo(b) >= 0;
  }
  inline bool operator <(const Rational& b) const
  {
    return CompareTo(b) < 0;
  }
  inline bool operator >(const Rational& b) const
  {
    return CompareTo(b) > 0;
  }
  Rational eval(int x) const
  {
    if (!sign()) return Rational(x < 0 ? -1 : x == 1 ? 1 : 0);
    auto ct = buffer + 2; UINT tt[8]; auto s = (UINT*)push(tt); //_ASSERT(ct[-2] == 0);
    if (s != tt + 2 && x >= 0) { auto ps = ct[-1] + 1; for (UINT i = 0, n = s[-1] + s[s[-1]] + 2; i < n; i++) ct[ps + i] = s[(int)i - 1]; s = ct + ps + 1; }
    UINT* t = s + (s[-1] + 1), * r = t + (t[-1] + 1); const INT64 one = 0x100000001;
    switch (x)
    {
    case -1: return Rational(((x = (int)s[-1] - 1) << 5) | (31 - chz(s[x]))); //msb
    case -2: { for (UINT i = 0; i < s[-1]; i++) if (s[i] != 0) return Rational((int)((i << 5) + clz(s[i]))); return -1; } //lsb
    case 0: { *(INT64*)(s + s[-1]) = one; return Rational(ct, s, num < 0); } //num
    case 1: { *(INT64*)(t + t[-1]) = one; return Rational(ct, t, false); } //den
    }
    div(s, t, r); //round 
    if (*(INT64*)(s - 1) != 1)
    {
      if (x == 4) { shr(t, 1); auto e = cmp(s, t); if (e > 0) add(r, (UINT*)&one + 1, r); }
      else if ((num < 0) ^ (x == 3)) add(r, (UINT*)&one + 1, r);
    }
    *(INT64*)(r + r[-1]) = one; return Rational(ct, r, num < 0);
  }
  Rational round(int digits) const
  {
    auto p = pow(10, digits); return (p * *this).eval(4) / p;
  }
  UINT compl() const
  {
    UINT tt[8]; auto s = push(tt); return s[-1] + s[s[-1]];
  }
  static Rational pow(Rational a, int e)
  {
    if (a.den == 3 && a.num == 10 && (UINT)e <= 28)
    {
      auto ct = buffer + 2; auto r = ct + ct[-1] + 2;
      pow10(e, r); r[r[-1]] = r[r[-1] + 1] = 1;
      return Rational(ct, r, false);
    }
    Rational b(1); if (e == 0) return b;
    for (auto n = e > 0 ? e : -e; ; n >>= 1, a = a * a)
    {
      if ((n & 1) != 0) b = b * a;
      if (n == 1) break;
    }
    if (e < 0) b = Rational(1) / b; return b;
  }
  static void pow10(UINT e, UINT* s)
  {
    static USHORT aa[] = { 0 | (1 << 8), 1 | (1 << 8), 2 | (1 << 8), 3 | (1 << 8), 4 | (1 << 8), 5 | (1 << 8), 6 | (1 << 8), 7 | (1 << 8), 8 | (1 << 8), 9 | (1 << 8), 10 | (2 << 8), 12 | (2 << 8), 14 | (2 << 8), 16 | (2 << 8), 18 | (2 << 8), 20 | (2 << 8), 22 | (2 << 8), 24 | (2 << 8), 26 | (2 << 8), 28 | (2 << 8), 30 | (3 << 8), 33 | (3 << 8), 36 | (3 << 8), 39 | (3 << 8), 42 | (3 << 8), 45 | (3 << 8), 48 | (3 << 8), 51 | (3 << 8), 54 | (3 << 8) };
    static UINT bb[] = { 1, 10, 100, 1000, 10000, 100000, 1000000, 10000000, 100000000, 1000000000, 1410065408, 2,1215752192, 23,3567587328, 232,1316134912, 2328,276447232, 23283,2764472320, 232830,1874919424, 2328306,1569325056, 23283064,2808348672, 232830643,2313682944, 2328306436,1661992960, 1808227885, 5,3735027712, 902409669, 54,2990538752, 434162106, 542,4135583744, 46653770, 5421,2701131776, 466537709, 54210,1241513984, 370409800, 542101,3825205248, 3704098002, 5421010,3892314112, 2681241660, 54210108,268435456, 1042612833, 542101086, };
    UINT* pp = &bb[aa[e] & 0xff], np = aa[e] >> 8; // 0 - 28
    s[-1] = np; for (UINT i = 0; i < np; i++) s[i] = pp[i];
  }
private:
  UINT den; INT num;
  Rational(UINT* ct, UINT* r, bool neg)
  {
    auto n = r[-1] + r[r[-1]];
    if (ct[-2] != 0)
    {
      ct[-1] = (UINT)(r - ct) + n + 1; checkct(ct); r[-2] = 0x30000000;
      *(UINT64*)&den = (size_t)r; if (neg) num |= 0x80000000;
      return;
    }
    if (*(INT64*)(r + r[-1]) != 0x100000001)
    {
      auto l = n + 1; auto t = r + l + 1; auto m = ct + ct[-1] + 1; if (t < m) t = m;
      copy(t - 1, r - 1, l + 1); // for (int i = -1; i < (int)l; i++) t[i] = r[i];
      auto s = gcd(t, t + t[-1] + 1);
      if (*(INT64*)(s - 1) != 0x100000001)
      {
        t += n + 2;
        copy(t - 1, r - 1, l + 1); //for (int i = -1; i < (int)l; i++) t[i] = r[i];
        auto d = t + t[-1] + 1;
        div(t, s, r); div(d, s, r + r[-1] + 1); n = r[-1] + r[r[-1]];
      }
    }
    if (n == 2 && !((r[0] | r[2]) & 0x80000000))
    {
      num = neg ? -(int)r[0] : (int)r[0];
      den = (r[2] << 1) | 1; return;
    }
    auto p = (UINT*)::malloc((n + 3) << 2) + 2;
    p[-2] = 0; copy(p - 1, r - 1, n + 2);
    *(UINT64*)&den = (size_t)p; if (neg) num |= 0x80000000;
  }
  const UINT* push(UINT* p) const
  {
    if (!(den & 1)) return getptr();
    p[0] = p[2] = 1;
    p[1] = (UINT)(num < 0 ? -num : num);
    p[3] = den >> 1; return p + 1;
  }
  thread_local static UINT buffer[0x4000]; //64k 
#if(0)
  static void checkct(UINT* ct)
  {
    static UINT maxct;
    if (maxct < ct[-1])
    {
      maxct = ct[-1];
      TRACE(L"maxct %i\n", maxct);
    }
  }
#else
  __forceinline void checkct(UINT* ct) {}
#endif
#ifdef _WIN64
  __forceinline UINT* getptr() const { return ((UINT*)(*(size_t*)&den & 0x7fffffffffffffff)); }
#else
  __forceinline UINT* getptr() const { return ((UINT*)(*(size_t*)&den)); }
#endif
  __forceinline void addref() const
  {
    if (den & 1) return;
    getptr()[-2]++;
  }
  __forceinline void release() const
  {
    if (den & 1) return;
    auto p = getptr();
    if (p[-2]-- == 0)
      ::free(p - 2);
  }

  static UINT* gcd(UINT* a, UINT* b)
  {
    int shift = 0;
    if (a[0] == 0 || b[0] == 0)
    {
      int i1 = 0; for (; a[i1] == 0; i1++); i1 = clz(a[i1]) + (i1 << 5); if (i1 != 0) shr(a, i1);
      int i2 = 0; for (; b[i2] == 0; i2++); i2 = clz(b[i2]) + (i2 << 5); if (i2 != 0) shr(b, i2); shift = i1 < i2 ? i1 : i2;
    }
    for (; ; )
    {
      if (cmp(a, b) < 0) { auto t = a; a = b; b = t; }
      UINT max = a[-1], min = b[-1];
      if (min == 1)
      {
        if (max != 1)
        {
          if (b[0] == 0) break;
          UINT64 u = 0; for (auto i = a[-1]; i-- != 0; u = (u << 32) | a[i], u %= b[0]);
          a[-1] = 1; if (u == 0) { a[0] = b[0]; break; }
          a[0] = (UINT)u;
        }
        UINT xa = a[0], xb = b[0]; for (; (xa > xb ? xa %= xb : xb %= xa) != 0;); a[0] = xa | xb; break;
      }
      if (max == 2)
      {
        auto xa = a[-1] == 2 ? *(UINT64*)a : a[0];
        auto xb = b[-1] == 2 ? *(UINT64*)b : b[0];
        for (; (xa > xb ? xa %= xb : xb %= xa) != 0;);
        *(UINT64*)a = xa | xb; a[-1] = a[1] != 0 ? 2u : 1u; break;
      }
      if (min <= max - 2) { div(a, b, NULL); continue; }
      UINT64 uu1 = a[-1] >= max ? ((UINT64)a[max - 1] << 32) | a[max - 2] : a[-1] == max - 1 ? a[max - 2] : 0;
      UINT64 uu2 = b[-1] >= max ? ((UINT64)b[max - 1] << 32) | b[max - 2] : b[-1] == max - 1 ? b[max - 2] : 0;
      int cbit = chz(uu1 | uu2);
      if (cbit > 0)
      {
        uu1 = (uu1 << cbit) | (a[max - 3] >> (32 - cbit));
        uu2 = (uu2 << cbit) | (b[max - 3] >> (32 - cbit));
      }
      if (uu1 < uu2) { auto t1 = uu1; uu1 = uu2; uu2 = t1; auto t2 = a; a = b; b = t2; }
      if (uu1 == 0xffffffffffffffff || uu2 == 0xffffffffffffffff) { uu1 >>= 1; uu2 >>= 1; }
      if (uu1 == uu2) { sub(a, b, a); continue; }
      if ((uu2 >> 32) == 0) { div(a, b, NULL); continue; }
      UINT ma = 1, mb = 0, mc = 0, md = 1;
      for (; ; )
      {
        UINT uQuo = 1; UINT64 uuNew = uu1 - uu2;
        for (; uuNew >= uu2 && uQuo < 32; uuNew -= uu2, uQuo++);
        if (uuNew >= uu2)
        {
          UINT64 uuQuo = uu1 / uu2; if (uuQuo > 0xffffffff) break;
          uQuo = (UINT)uuQuo; uuNew = uu1 - uQuo * uu2;
        }
        UINT64 uuAdNew = ma + (UINT64)uQuo * mc;
        UINT64 uuBcNew = mb + (UINT64)uQuo * md;
        if (uuAdNew > 0x7FFFFFFF || uuBcNew > 0x7FFFFFFF) break;
        if (uuNew < uuBcNew || uuNew + uuAdNew > uu2 - mc) break;
        ma = (UINT)uuAdNew; mb = (UINT)uuBcNew;
        uu1 = uuNew; if (uu1 <= mb) break;
        uQuo = 1; uuNew = uu2 - uu1;
        for (; uuNew >= uu1 && uQuo < 32; uuNew -= uu1, uQuo++);
        if (uuNew >= uu1)
        {
          UINT64 uuQuo = uu2 / uu1; if (uuQuo > 0xffffffff) break;
          uQuo = (UINT)uuQuo; uuNew = uu2 - uQuo * uu1;
        }
        uuAdNew = md + (UINT64)uQuo * mb;
        uuBcNew = mc + (UINT64)uQuo * ma;
        if (uuAdNew > 0x7FFFFFFF || uuBcNew > 0x7FFFFFFF) break;
        if (uuNew < uuBcNew || uuNew + uuAdNew > uu1 - mb) break;
        md = (UINT)uuAdNew; mc = (UINT)uuBcNew;
        uu2 = uuNew; if (uu2 <= mc) break;
      }
      if (mb == 0) { if (uu1 / 2 >= uu2) div(a, b, NULL); else sub(a, b, a); continue; }
      int c1 = 0, c2 = 0; b[-1] = a[-1] = min;
      for (UINT iu = 0; iu < min; iu++)
      {
        UINT u1 = a[iu], u2 = b[iu];
        INT64 nn1 = (INT64)u1 * ma - (INT64)u2 * mb + c1; a[iu] = (UINT)nn1; c1 = (int)(nn1 >> 32);
        INT64 nn2 = (INT64)u2 * md - (INT64)u1 * mc + c2; b[iu] = (UINT)nn2; c2 = (int)(nn2 >> 32);
      }
      while (a[-1] > 1 && a[a[-1] - 1] == 0) a[-1]--;
      while (b[-1] > 1 && b[b[-1] - 1] == 0) b[-1]--;
    }
    if (shift != 0) shl(a, shift);
    return a;
  }
  static int cmp(const UINT* a, const UINT* b)
  {
    if (a[-1] != b[-1]) return a[-1] > b[-1] ? +1 : -1;
    for (auto i = a[-1]; i-- != 0;) if (a[i] != b[i]) return a[i] > b[i] ? +1 : -1; return 0;
  }
  static void add(const UINT* a, const UINT* b, UINT* r)
  {
    if (a[-1] < b[-1]) { auto t = a; a = b; b = t; }
    UINT c = 0, i = 0, na = a[-1], nb = b[-1];
    for (; i < nb; i++) { auto u = (UINT64)a[i] + b[i] + c; r[i] = ((UINT*)&u)[0]; c = ((UINT*)&u)[1]; }
    for (; i < na; i++) { auto u = (UINT64)a[i] + c; /*  */ r[i] = ((UINT*)&u)[0]; c = ((UINT*)&u)[1]; }
    r[-1] = na; if (c != 0) r[r[-1]++] = c;
  }
  static void sub(const UINT* a, const UINT* b, UINT* r)
  {
    UINT c = 0, i = 0, na = a[-1], nb = b[-1]; _ASSERT(na >= nb);
    for (; i < nb; i++) { auto u = (UINT64)a[i] - b[i] - c; r[i] = ((UINT*)&u)[0]; c = (UINT)-((int*)&u)[1]; }
    for (; i < na; i++) { auto u = (UINT64)a[i] /*  */ - c; r[i] = ((UINT*)&u)[0]; c = (UINT)-((int*)&u)[1]; }
    for (; i > 1 && r[i - 1] == 0; i--); r[-1] = i; _ASSERT(c == 0);
  }
  static void mul(const UINT* a, const UINT* b, UINT* r)
  {
    UINT na = a[-1], nb = b[-1];
    if (na == 1)
    {
      if (nb == 1) { *(UINT64*)r = (UINT64)a[0] * b[0]; r[-1] = r[1] != 0 ? 2u : 1; return; }
      if (a[0] == 1) { for (int i = -1; i < (int)nb; i++) r[i] = b[i]; return; }
    }
    if (nb == 1 && b[0] == 1) { for (int i = -1; i < (int)na; i++) r[i] = a[i]; return; }
    UINT nr = na + nb - 1; for (UINT i = 0; i < nr; i++) r[i] = 0;
    for (UINT i = na, k, c; i-- != 0;) //todo: _umul128
    {
      for (k = c = 0; k < nb; k++) { auto t = (UINT64)b[k] * a[i] + r[i + k] + c; r[i + k] = (UINT)t; c = (UINT)(t >> 32); }
      if (c == 0) continue;
      for (k = i + nb; c != 0 && k < nr; k++) { auto t = (UINT64)r[k] + c; r[k] = (UINT)t; c = (UINT)(t >> 32); }
      if (c == 0) continue; r[nr++] = c;
    }
    r[-1] = nr;
  }
  static void div(UINT* a, const UINT* b, UINT* m)
  {
    int na = (int)a[-1], nb = (int)b[-1];
    if (na < nb) { if (m == NULL) return; m[-1] = 1; m[0] = 0; return; }
    if (nb == 1)
    {
      UINT64 uu = 0, ub = b[0];
      for (int i = na; i-- != 0;) { uu = ((UINT64)(UINT)uu << 32) | a[i]; if (m != NULL) m[i] = (UINT)(uu / ub); uu %= ub; }
      a[-1] = 1; a[0] = (UINT)uu; if (m == NULL) return;
      for (; na > 1 && m[na - 1] == 0; na--); m[-1] = (UINT)na; return;
    }
    if (nb == 2 && na == 2)
    {
      if (m != NULL) *(UINT64*)m = *(UINT64*)a / *(UINT64*)b; *(UINT64*)a %= *(UINT64*)b;
      if (a[na - 1] == 0) a[-1] = 1; if (m == NULL) return;
      if (m[nb - 1] == 0) nb = 1; m[-1] = (UINT)nb; return;
    }
    int diff = na - nb, nc = diff;
    for (int i = na - 1; ; i--)
    {
      if (i < diff) { nc++; break; }
      if (b[i - diff] != a[i]) { if (b[i - diff] < a[i]) nc++; break; }
    }
    if (nc == 0) { a[-1] = (UINT)na; if (m == NULL) return; m[-1] = 1; m[0] = 0; return; }
    UINT uden = b[nb - 1], unex = nb > 1 ? b[nb - 2] : 0;
    int shl = chz(uden), shr = 32 - shl;
    if (shl > 0)
    {
      uden = (uden << shl) | (unex >> shr); unex <<= shl;
      if (nb > 2) unex |= b[nb - 3] >> shr;
    }
    for (int i = nc; --i >= 0;)
    {
      UINT hi = i + nb < na ? a[i + nb] : 0;
      UINT64 uu = ((UINT64)hi << 32) | a[i + nb - 1];
      UINT un = i + nb - 2 >= 0 ? a[i + nb - 2] : 0;
      if (shl > 0)
      {
        uu = (uu << shl) | (un >> shr); un <<= shl;
        if (i + nb >= 3) un |= a[i + nb - 3] >> shr;
      }
      UINT64 quo = uu / uden, rem = (UINT)(uu % uden);
      if (quo > 0xffffffff) { rem += uden * (quo - 0xffffffff); quo = 0xffffffff; }
      while (rem <= 0xffffffff && quo * unex > (((UINT64)(UINT)rem << 32) | un)) { quo--; rem += uden; }
      if (quo > 0)
      {
        UINT64 bor = 0;
        for (int k = 0; k < nb; k++)
        {
          bor += b[k] * quo; UINT sub = (UINT)bor;
          bor >>= 32; if (a[i + k] < sub) bor++;
          a[i + k] -= sub;
        }
        if (hi < bor)
        {
          UINT c = 0;
          for (int k = 0; k < nb; k++)
          {
            UINT64 t = (UINT64)a[i + k] + b[k] + c;
            a[i + k] = (UINT)t; c = (UINT)(t >> 32);
          }
          quo--;
        }
        na = i + nb;
      }
      if (m != NULL) m[i] = (UINT)quo;
    }
    for (; na > 1 && a[na - 1] == 0; na--); a[-1] = (UINT)na; if (m == NULL) return;
    for (; nc > 1 && m[nc - 1] == 0; nc--); m[-1] = (UINT)nc; return;
  }
  static void shl(UINT* p, int c)
  {
    auto s = c & 31; UINT d = (UINT)c >> 5, n = p[-1]; p[-1] = p[n] = 0;
    for (int i = (int)n; i >= 0; i--) p[i + d] = (p[i] << s) | (UINT)((UINT64)p[i - 1] >> (32 - s));
    for (int i = 0; i < (int)d; i++) p[i] = 0;
    n += d; p[-1] = p[n] != 0 ? n + 1 : n;
  }
  static void shr(UINT* p, int c)
  {
    int s = c & 31; UINT k = (UINT)c >> 5, i = 0, n = p[-1];
    for (; k + 1 < n; i++, k++) p[i] = (p[k] >> s) | (UINT)((UINT64)p[k + 1] << (32 - s));
    if ((k = p[k] >> s) || i == 0) p[i++] = k; p[-1] = i;
  }
  static void shr(const UINT* p, UINT* d, int c)
  {
    int s = c & 31; UINT k = (UINT)c >> 5, i = 0, n = p[-1];
    for (; k + 1 < n; i++, k++) d[i] = (p[k] >> s) | (UINT)((UINT64)p[k + 1] << (32 - s));
    if ((k = p[k] >> s) || i == 0) d[i++] = k; d[-1] = i;
  }
  __forceinline static int chz(UINT64 u)
  {
    DWORD i; return _BitScanReverse64(&i, u) ? 63 - i : 63;
  }
  __forceinline static int chz(UINT u)
  {
    DWORD i; return _BitScanReverse(&i, u) ? 31 - i : 31;
  }
  __forceinline static int clz(UINT u)
  {
    DWORD i; return _BitScanForward(&i, u) ? i : 31;
  }
  __forceinline static void copy(UINT* d, const UINT* s, UINT n)
  {
    //::memcpy(d, s, n << 2);
    for (UINT i = 0; i < n; i++) d[i] = s[i];
  }
  __forceinline static bool equals(const UINT* d, const UINT* s, UINT n)
  {
    //return ::memcmp(d, s, n << 2) == 0;
    for (UINT i = 0; i < n; i++) if (d[i] != s[i]) return false;
    return true;
  }
public:
  int write(IStream* str) const
  {
    CHR(writecount(str, (den & 1) | (num < 0 ? 2 : 0)));
    if (den & 1)
    {
      CHR(writecount(str, abs(num)));
      CHR(writecount(str, den >> 1));
    }
    else
    {
      auto p = getptr(); UINT n1 = p[-1], n2 = p[p[-1]];
      CHR(writecount(str, n1));
      CHR(writecount(str, n2));
      CHR(str->Write(p, n1 << 2, 0));
      CHR(str->Write(p + n1 + 1, n2 << 2, 0));
    }
    return 0;
  }
  int read(IStream* str)
  {
    UINT ct; CHR(readcount(str, ct)); if (ct > 7) return ct;
    if (!(den & 1)) *this = 0;
    if (ct & 1)
    {
      CHR(readcount(str, *(UINT*)&num)); if (ct & 2) num = -num;
      CHR(readcount(str, den)); den = (den << 1) | 1;
    }
    else
    {
      UINT n1, n2;
      CHR(readcount(str, n1));
      CHR(readcount(str, n2));
      auto p = (UINT*)::malloc((n1 + n2 + 3) << 2) + 2;
      p[-2] = 0; p[p[-1] = n1] = n2;
      *(UINT64*)&den = (size_t)p; if (ct & 2) num |= 0x80000000;
      CHR(str->Read(p, n1 << 2, 0));
      CHR(str->Read(p + n1 + 1, n2 << 2, 0));
    }
    return 0;
  }
  static int write(IStream* str, const Rational* pp, UINT np)
  {
    UINT ts = min(max(32, np), 1024), * ht = (UINT*)_alloca(ts * sizeof(UINT)); memset(ht, 0, ts * sizeof(UINT));
    for (UINT i = 0, k; i < np; i++)
    {
      const auto& p = pp[i];
      if (!(p.den & 1))
      {
        UINT& hc = ht[p.GetHashCode() % ts];
        if (!hc) hc = i + 1;
        else
        {
          for (k = hc - 1; k < i && !p.Equals(pp[k]); k++);
          if (k < i) { CHR(writecount(str, 8 + k)); continue; }
        }
      }
      CHR(p.write(str));
    }
    return 0;
  }
  static int read(IStream* str, Rational* pp, UINT np)
  {
    for (UINT i = 0; i < np; i++)
    {
      auto hr = pp[i].read(str); CHR(hr);
      if (hr >= 8) pp[i] = pp[hr - 8];
    }
    return 0;
  }
  static void compact(Rational* pp, UINT np)
  {
    UINT ts = min(max(32, np), 1024), * ht = (UINT*)_alloca(ts * sizeof(UINT)); memset(ht, 0, ts * sizeof(UINT));
    for (UINT i = 0, k; i < np; i++)
    {
      auto& p = pp[i]; if (p.den & 1) continue;
      UINT& hc = ht[p.GetHashCode() % ts];
      if (!hc) { hc = i + 1; continue; }
      for (k = hc - 1; k < i && !p.Equals(pp[k]); k++);
      if (k == i) continue;
      if(*(UINT64*)&p == *(UINT64*)&pp[k]) continue;
      p = pp[k];
    }
  }
  friend struct CVector;
  struct mach
  {
    mach(int v) { auto ct = buffer + 2; ct[-2]++; p = ct[-1]; } UINT p;
    bool fetch()
    {
      if ((p >> 31) != 0) return false; p |= (1u << 31); return true;
    }
    void dispose()
    {
      auto ct = buffer + 2; ct[-1] = p & 0x7fffffff; _ASSERT(ct[-2] != 0); ct[-2]--;
    }
    static Rational& fetch(const mach& a, Rational& b)
    {
      auto ct = buffer + 2; auto t = ct[-2];
      if (!(b.den & 1) && b.getptr()[-2] >= 0x30000000)
      {
        ct[-2] = 0; b.Rational::Rational(ct, b.getptr(), b.num < 0);
      }
      if ((a.p >> 31) == 0) { _ASSERT(t != 0); ct[-2] = t - 1; ct[-1] = a.p; }
      else ct[-2] = t;
      return b;
    }
    static int fetchsign(const mach& a, const Rational& b)
    {
      auto ct = buffer + 2; _ASSERT(ct[-2] != 0); ct[-2]--; ct[-1] = a.p; return b.sign();
    }
  };
};

__forceinline Rational& operator |(const Rational::mach& a, Rational& b) { return Rational::mach::fetch(a, b); }
__forceinline int operator ^(const Rational::mach& a, const Rational& b) { return Rational::mach::fetchsign(a, b); }



