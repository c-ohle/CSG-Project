#pragma once

#include "math.h"
#include "rational.h"

static int __sizeof(CSG_TYPE t)
{
  return t <= CSG_TYPE_FLOAT ? 4 : t == CSG_TYPE_DOUBLE || t == CSG_TYPE_RATIONAL ? 8 : t == CSG_TYPE_DECIMAL ? 16 : t == CSG_TYPE_STRING ? 0 : sizeof(void*);
}
void conv(Rational* rr, UINT nr, const CSGVAR& v);
void conv(CSGVAR& v, const Rational* rr, UINT nr);
void conv(double* rr, UINT nr, const CSGVAR& v);
void conv(CSGVAR& v, const double* rr, UINT nr);

//struct Vector2F
//{
//  float x, y;
//  Vector2F() { x = y = 0; }
//  Vector2F(float x, float y) { this->x = x; this->y = y; }
//  operator CSGVAR() const { CSGVAR v; v.vt = CSG_TYPE_FLOAT; v.count = 2; *(const void**)&v.p = this;  return v; }
//  float operator[](int i) const { return i & 1 ? y : x; }
//  Vector2F operator -() const { return Vector2F(-x, -y); }
//  Vector2F operator ~() const { return Vector2F(-y, +x); }
//  Vector2F operator * (float b) const { return Vector2F(x * b, y * b); }
//  Vector2F operator + (const Vector2F& b) const { return Vector2F(x + b.x, y + b.y); }
//  Vector2F operator - (const Vector2F& b) const { return Vector2F(x - b.x, y - b.y); }
//  float operator ^ (const Vector2F& b) const { return x * b.y - y * b.x; }
//  float Dot() const { return x * x + y * y; }
//  float Length() const { return sqrtf(x * x + y * y); }
//  Vector2F Normal() const { return x != 0 || y != 0 ? *this * (1 / Length()) : Vector2F(0, 0); }
//};

struct Vector2
{
  double x, y;
  Vector2() { x = y = 0; }
  Vector2(double x, double y) { this->x = x; this->y = y; }
  Vector2(double a)
  {
    x = cos(a); if (abs(x) == 1) { y = 0; return; }
    y = sin(a); if (abs(y) == 1) { x = 0; return; }
  }
  operator CSGVAR() const { CSGVAR v; v.vt = CSG_TYPE_DOUBLE; v.count = 2; *(const void**)&v.p = this;  return v; }
  double operator[](int i) const { return i & 1 ? y : x; }
  Vector2 operator -() const { return Vector2(-x, -y); }
  Vector2 operator ~() const { return Vector2(-y, +x); }
  Vector2 operator * (double b) const { return Vector2(x * b, y * b); }
  Vector2 operator + (const Vector2& b) const { return Vector2(x + b.x, y + b.y); }
  Vector2 operator - (const Vector2& b) const { return Vector2(x - b.x, y - b.y); }
  double operator ^ (const Vector2& b) const { return x * b.y - y * b.x; }
  double Dot() const { return x * x + y * y; }
  double Length() const { return sqrt(x * x + y * y); }
  Vector2 Normal() const { return x != 0 || y != 0 ? *this * (1 / Length()) : Vector2(0, 0); }
  double Angle()
  {
    auto a = atan2(y, x); if (a >= 0) return a;
    a += 2 * M_PI; if (a == 2 * M_PI) return 0;
    return a;
  }
};

struct Vector3 { double x, y, z; };

struct Vector2R
{
  Rational x, y;
  Vector2R() {}
  Vector2R(const Rational x, const Rational y) : x(x), y(y) {}
  Vector2R(const double* p) : x(p[0]), y(p[1]) {}
  Vector2R operator -(const Vector2R& b) const { return Vector2R(x - b.x, y - b.y); }
  Rational operator ^(const Vector2R& b) const { return x * b.y - y * b.x; }
  Rational operator &(const Vector2R& b) const { return x * b.x + y * b.y; }
  Vector2R Normalize()
  {
    int i = (x.sign() < 0 ? -x : x).CompareTo(y.sign() < 0 ? -y : y) >= 0 ? 0 : 1;
    Rational l = i == 0 ? x : y, s = l.sign(); if (l.sign() < 0) l = -l;
    return Vector2R(i == 0 ? s : x / l, i == 1 ? s : y / l);
  }
  static Vector2R SinCos(double a, int prec)
  {
    auto x = cos(a); if (abs(x) == 1) return Vector2R(x < 0 ? -1 : +1, 0);
    auto y = sin(a); if (abs(y) == 1) return Vector2R(0, y < 0 ? -1 : +1);
    switch (prec)
    {
    case 0: return Vector2R((float)x, (float)y);
    case 1: return Vector2R(x, y);
    default:
      if (prec < 0)
      {
        double f = (1u << -prec);
        return Vector2R(round(x * f) / f, round(y * f) / f);
        //DECIMAL xx; VarDecFromR8(x, &xx); VarDecRound(&xx, -prec, &xx);
        //DECIMAL yy; VarDecFromR8(y, &yy); VarDecRound(&yy, -prec, &yy);
        //return Vector2R(xx, yy);
      }
      auto dm = y / (1 - x); *(UINT64*)&dm &= 0xffffffffffffffff << (52 - prec);
      auto m1 = (Rational)dm; auto m2 = m1 * m1; auto m3 = m2 + 1;
      return Vector2R((m2 - 1) / m3, (Rational)2 * m1 / m3);
    }
  }
};

struct Vector3R
{
  Rational x, y, z;
  Vector3R() {}
  Vector3R(const Rational x, const Rational y, const Rational z) : x(x), y(y), z(z) {}
  Vector3R(const double* p) : x(p[0]), y(p[1]), z(p[2]) {}
  int GetHashCode() const
  {
    auto h1 = (UINT)x.GetHashCode();
    auto h2 = (UINT)y.GetHashCode();
    auto h3 = (UINT)z.GetHashCode();
    h2 = ((h2 << 7) | (h3 >> 25)) ^ h3;
    h1 = ((h1 << 7) | (h2 >> 25)) ^ h2;
    return (int)h1;
  }
  bool Equals(const Vector3R& b) const { return x.Equals(b.x) && y.Equals(b.y) && z.Equals(b.z); }
  Vector3R operator -() const { return Vector3R(-x, -y, -z); }
  Vector3R operator +(const Vector3R& b) const { return Vector3R(x + b.x, y + b.y, z + b.z); }
  Vector3R operator -(const Vector3R& b) const { return Vector3R(x - b.x, y - b.y, z - b.z); }
  Vector3R operator *(const Rational& b) const { return Vector3R(x + b, y + b, z + b); }
  Vector3R operator ^(const Vector3R& b) const { return Vector3R(y * b.z - z * b.y, z * b.x - x * b.z, x * b.y - y * b.x); }
  Rational operator &(const Vector3R& b) const { return x * b.x + y * b.y + z * b.z; }
  void operator +=(const Vector3R& b) { x = x + b.x; y = y + b.y; z = z + b.z; }
  void operator -=(const Vector3R& b) { x = x - b.x; y = y - b.y; z = z - b.z; }
  void operator *=(const Rational& b) { x = x * b; y = y * b; z = z * b; }
  int LongAxis() const
  {
    auto sx = x.sign();
    auto sy = y.sign(); if ((sx | sy) == 0) return 2;
    auto sz = z.sign();
    auto ax = sx >= 0 ? x : -x;
    auto ay = sy >= 0 ? y : -y;
    auto az = sz >= 0 ? z : -z;
    auto l = ax.CompareTo(ay) > 0 ? 0 : 1;
    if ((l == 0 ? ax : ay).CompareTo(az) <= 0) l = 2;
    return l;
  }
  Vector3R Normalize() const
  {
    int i = LongAxis(); Rational l = i == 0 ? x : i == 1 ? y : z, s = l.sign(); if (s < 0) l = -l;
    return Vector3R(i == 0 ? s : x / l, i == 1 ? s : y / l, i == 2 ? s : z / l);
  }
  static Vector3R Ccw(const Vector3R& a, const Vector3R& b, const Vector3R& c) { return (b - a) ^ (c - a); }
  static Rational Dot(const Vector3R& a, const Vector3R& b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
  static int Inline(const Vector3R& a, const Vector3R& b, const Vector3R& c, int i = 0)
  {
    Rational::mach m = 0; // m.fetch(); ???
    auto ux = b.x - a.x; auto uy = b.y - a.y;
    auto vx = c.x - b.x; auto vy = c.y - b.y; Rational uz, vz;
    if ((ux * vy - uy * vx).sign() != 0) goto e;
    uz = b.z - a.z; vz = c.z - b.z;
    if ((uz * vx - ux * vz).sign() != 0) goto e;
    if ((uy * vz - uz * vy).sign() != 0) goto e;
    if (i == 0) { i = 1; goto e; }
    if ((ux * vx + uy * vy + uz * vz).sign() >= 0) { if (i == 2) i = 0; goto e; }
    if (i == 1) { i = -1; goto e; }
    i = (ux * ux + uy * uy + uz * uz).CompareTo(vx * vx + vy * vy + vz * vz); e:
    m.dispose(); return i;
  }
  int write(IStream* str) { CHR(x.write(str)); CHR(y.write(str)); CHR(z.write(str)); return 0; }
  int read(IStream* str) { CHR(x.read(str)); CHR(y.read(str)); CHR(z.read(str)); return 0; }
};

struct Vector4R
{
  Rational x, y, z, w;
  Vector4R() {}
  Vector4R(const Rational x, const Rational y, const Rational z, const Rational w) : x(x), y(y), z(z), w(w) {}
  Vector4R(const double* p) : x(p[0]), y(p[1]), z(p[2]), w(p[3]) {}
  int GetHashCode() const
  {
    auto h1 = (UINT)((const Vector3R*)this)->GetHashCode();
    auto h2 = (UINT)w.GetHashCode();
    h2 = ((h2 << 7) | (h1 >> 25)) ^ h1;
    h1 = ((h1 << 7) | (h2 >> 25)) ^ h2;
    return (int)h1;
  }
  bool Equals(const Vector4R& b) const { return x == b.x && y == b.y && z == b.z && w == b.w; }
  Vector4R operator -() const { return Vector4R(-x, -y, -z, -w); }
  static Vector4R PlaneFromPointNormal(const Vector3R& p, const Vector3R& n)
  {
    auto v = n.Normalize(); return Vector4R(v.x, v.y, v.z, -(p.x * v.x + p.y * v.y + p.z * v.z));
  }
  static Vector4R PlaneFromPoints(const Vector3R& a, const Vector3R& b, const Vector3R& c)
  {
    return PlaneFromPointNormal(a, Vector3R::Ccw(a, b, c));
  }
  Rational DotCoord(const Vector3R& p) const
  {
    return x * p.x + y * p.y + z * p.z + w;
  }
  Vector3R Intersect(const Vector3R& a, const Vector3R& b) const
  {
    auto u = x * a.x + y * a.y + z * a.z;
    auto v = x * b.x + y * b.y + z * b.z; auto w = (u + this->w) / (u - v);
    return Vector3R(a.x + (b.x - a.x) * w, a.y + (b.y - a.y) * w, a.z + (b.z - a.z) * w);
  }
};

static Vector2R& operator |(const Rational::mach& m, Vector2R& b)
{
  auto t = ((Rational::mach*) & m)->fetch();
  b.x = m | b.x; b.y = m | b.y;
  if (t) ((Rational::mach*) & m)->dispose(); return b;
}
static Vector3R& operator |(const Rational::mach& m, Vector3R& b)
{
  auto t = ((Rational::mach*) & m)->fetch();
  b.x = m | b.x; b.y = m | b.y; b.z = m | b.z;
  if (t) ((Rational::mach*) & m)->dispose(); return b;
}
static Vector4R& operator |(const Rational::mach& m, Vector4R& b)
{
  auto t = ((Rational::mach*) & m)->fetch();
  b.x = m | b.x; b.y = m | b.y; b.z = m | b.z; b.w = m | b.w;
  if (t) ((Rational::mach*) & m)->dispose(); return b;
}

struct Matrix3x4R
{
  Rational m[4][3];
  static void Transform(Vector3R* pp, UINT np, const Matrix3x4R& m)
  {
    for (UINT i = 0; i < np; i++)
    {
      auto& p = pp[i]; auto x = p.x; auto y = p.y;
      p.x = 0 | x * m.m[0][0] + y * m.m[1][0] + p.z * m.m[2][0] + m.m[3][0];
      p.y = 0 | x * m.m[0][1] + y * m.m[1][1] + p.z * m.m[2][1] + m.m[3][1];
      p.z = 0 | x * m.m[0][2] + y * m.m[1][2] + p.z * m.m[2][2] + m.m[3][2];
    }
  }
  //static void Transform(Vector4R* pp, UINT np, const Matrix3x4R& m)
  //{ 
  //  for (UINT i = 0; i < np; i++)
  //  {
  //    Rational::mach ma = 0; ma.fetch();
  //    auto& p = pp[i]; Vector3R v = *(Vector3R*)&p, t;
  //    t.x = (v.x * m.m[0][0] + v.y * m.m[1][0] + v.z * m.m[2][0]) * p.w + m.m[3][0];
  //    t.y = (v.x * m.m[0][1] + v.y * m.m[1][1] + v.z * m.m[2][1]) * p.w + m.m[3][1];
  //    t.z = (v.x * m.m[0][2] + v.y * m.m[1][2] + v.z * m.m[2][2]) * p.w + m.m[3][2];
  //    p.x = v.x * m.m[0][0] + v.y * m.m[1][0] + v.z * m.m[2][0];
  //    p.y = v.x * m.m[0][1] + v.y * m.m[1][1] + v.z * m.m[2][1];
  //    p.z = v.x * m.m[0][2] + v.y * m.m[1][2] + v.z * m.m[2][2];
  //    *(Vector3R*)&p = ma | (*(Vector3R*)&p).Normalize();
  //    p.w = ma | -(t.x * p.x + t.y * p.y + t.z * p.z);
  //    ma.dispose();
  //  }
  //}
  static void Multiply(Matrix3x4R& c, const Matrix3x4R& a, const Matrix3x4R& b)
  {
    for (UINT i = 0; i < 3; i++)
      for (UINT k = 0; k < 4; k++)
        c.m[k][i] = 0 | b.m[0][i] * a.m[k][0] + b.m[1][i] * a.m[k][1] + b.m[2][i] * a.m[k][2] + (k == 3 ? b.m[3][i] : 0);
  }
  static int Inverse(Matrix3x4R& b, const Matrix3x4R& a)
  {
    Rational::mach m = 0; m.fetch();
    auto x = a.m[2][0] * a.m[3][1] - a.m[2][1] * a.m[3][0];
    auto y = a.m[2][0] * a.m[3][2] - a.m[2][2] * a.m[3][0];
    auto z = a.m[2][1] * a.m[3][2] - a.m[2][2] * a.m[3][1];
    b.m[0][0] = a.m[1][1] * a.m[2][2] - a.m[1][2] * a.m[2][1];
    b.m[1][0] = a.m[1][2] * a.m[2][0] - a.m[1][0] * a.m[2][2];
    b.m[2][0] = a.m[1][0] * a.m[2][1] - a.m[1][1] * a.m[2][0];
    b.m[3][0] = a.m[1][1] * y - a.m[1][0] * z - a.m[1][2] * x;
    auto d = a.m[0][0] * b.m[0][0] + a.m[0][1] * b.m[1][0] + a.m[0][2] * b.m[2][0];
    if (d.sign() == 0) { m.dispose(); return EXCEPTION_INT_DIVIDE_BY_ZERO; }
    b.m[2][2] = a.m[0][0] * a.m[1][1] - a.m[0][1] * a.m[1][0];
    b.m[1][2] = a.m[0][2] * a.m[1][0] - a.m[0][0] * a.m[1][2];
    b.m[0][2] = a.m[0][1] * a.m[1][2] - a.m[0][2] * a.m[1][1];
    b.m[0][1] = a.m[0][2] * a.m[2][1] - a.m[0][1] * a.m[2][2];
    b.m[1][1] = a.m[0][0] * a.m[2][2] - a.m[0][2] * a.m[2][0];
    b.m[2][1] = a.m[0][1] * a.m[2][0] - a.m[0][0] * a.m[2][1];
    b.m[3][1] = a.m[0][0] * z - a.m[0][1] * y + a.m[0][2] * x;
    b.m[3][2] = -a.m[3][0] * b.m[0][2] - a.m[3][1] * b.m[1][2] - a.m[3][2] * b.m[2][2];
    for (UINT i = 0; i < 12; i++) ((Rational*)&b)[i] = m | ((Rational*)&b)[i] / d;
    m.dispose(); return 0;
  }
};

struct CVector : public ICSGVector
{
  UINT refcount = 1, length; Rational val;
  ~CVector() { for (UINT i = 1; i < length; i++) (&val)[i].Rational::~Rational(); }
  static CVector* Create(UINT c)
  {
    auto n = sizeof(CVector) + (c - 1) * sizeof(Rational);
    auto t = (CVector*)malloc(n); t->CVector::CVector(); t->length = c;
    for (UINT i = 1; i < c; i++) (&t->val)[i].Rational::Rational(); return t;
  }
  HRESULT __stdcall QueryInterface(REFIID riid, void** p)
  {
    if (riid == __uuidof(IUnknown) || riid == __uuidof(ICSGVector) || riid == __uuidof(IAgileObject))
    {
      InterlockedIncrement(&refcount); *p = static_cast<ICSGVector*>(this); return 0;
    }
    return E_NOINTERFACE;
  }
  ULONG __stdcall AddRef(void)
  {
    return InterlockedIncrement(&refcount);
  }
  ULONG __stdcall Release(void)
  {
    auto count = InterlockedDecrement(&refcount);
    if (!count) delete this;
    return count;
  }
  HRESULT __stdcall get_Length(UINT* n)
  {
    *n = length;  return 0;
  }
  HRESULT __stdcall GetString(UINT i, UINT digits, UINT flags, BSTR* p)
  {
    if (i >= length) return E_INVALIDARG;
    *p = (&val)[i].ToString(digits, flags); return 0;
  }
  HRESULT __stdcall GetHashCode(UINT i, UINT n, UINT* v)
  {
    if (i + n > length) return E_INVALIDARG;
    UINT h = (&val)[i].GetHashCode();
    while (--n)
    {
      UINT t = (&val)[++i].GetHashCode();
      h = ((h << 7) | (t >> 25)) ^ t;
    }
    *v = h; return 0;
  }
  HRESULT __stdcall CompareTo(UINT i, ICSGVector* pb, UINT ib, INT* p)
  {
    if (i >= length) return E_INVALIDARG;
    if (!pb) { *p = (&val)[i].sign();  return 0; }
    auto& b = *static_cast<const CVector*>(pb); if (ib >= b.length) return E_INVALIDARG;
    *p = (&val)[i].CompareTo((&b.val)[ib]); return 0;
  }
  HRESULT __stdcall Equals(UINT i, ICSGVector* pb, UINT ib, UINT c, BOOL* p)
  {
    auto& b = *static_cast<const CVector*>(pb);
    if (i + c > length || ib + c > b.length) return E_INVALIDARG;
    for (; c && (&val)[i] == (&b.val)[ib]; i++, ib++, c--);
    *p = c == 0; return 0;
  }
  HRESULT __stdcall Copy(UINT i, ICSGVector* pb, UINT ib, UINT c)
  {
    auto& b = *static_cast<const CVector*>(pb);
    if (i + c > length || ib + c > b.length) return E_INVALIDARG;
    for (; c--; i++, ib++) (&val)[i] = (&b.val)[ib];
    return 0;
  }
  HRESULT __stdcall GetValue(UINT i, CSGVAR* p)
  {
    if (i + max(p->count, 1) > length) return E_INVALIDARG;
    conv(*p, &val + i, length); return 0;
  }
  HRESULT __stdcall SetValue(UINT i, CSGVAR v)
  {
    auto n = max(1, v.count); if (i + n > length) return E_INVALIDARG;
    conv(&val + i, n, v); return 0;
  }
  HRESULT __stdcall Execute1(CSG_OP1 op, UINT ic, const ICSGVector* pa, UINT ia)
  {
    const auto& a = *static_cast<const CVector*>(pa); const auto& va = (&a.val)[ia];
    auto& c = *this; auto& vc = (&c.val)[ic];
    switch (op & 0xff)
    {
    case CSG_OP1_COPY: vc = va; return 0;
    case CSG_OP1_NEG: vc = -va; return 0;
    case CSG_OP1_TRANSPM: Matrix3x4R::Transform((Vector3R*)&vc, 1, *(const Matrix3x4R*)&va); return 0;
    case CSG_OP1_INV3X4: CHR(Matrix3x4R::Inverse(((Matrix3x4R*)&vc)[0], ((const Matrix3x4R*)&va)[0])); return 0;
    case CSG_OP1_DOT2: vc = 0 | (&va)[0] * (&va)[0] + (&va)[1] * (&va)[1]; return 0;
    case CSG_OP1_DOT3: vc = 0 | (&va)[0] * (&va)[0] + (&va)[1] * (&va)[1] + (&va)[2] * (&va)[2]; return 0;
    case CSG_OP1_NORM3: ((Vector3R*)&vc)[0] = ((const Vector3R*)&va)[0].Normalize(); return 0;
    case CSG_OP1_NUM: vc = va.eval(0); return 0;
    case CSG_OP1_DEN: vc = va.eval(1); return 0;
    case CSG_OP1_LSB: vc = va.eval(-2); return 0;
    case CSG_OP1_MSB: vc = va.eval(-1); return 0;
    case CSG_OP1_TRUNC: vc = va.eval(va.sign() >= 0 ? 2 : 3); return 0;
    case CSG_OP1_FLOOR: vc = va.eval(2); return 0;
    case CSG_OP1_CEIL: vc = va.eval(3); return 0;
    case CSG_OP1_ROUND: vc = va.eval(4); return 0;
    case CSG_OP1_RND10: vc = 0 | va.round(op >> 8); return 0;
    case CSG_OP1_COMPL: vc = (int)va.compl(); return 0;
    }
    return E_NOTIMPL;
  }
  HRESULT __stdcall Execute2(CSG_OP2 op, UINT ic, const ICSGVector* pa, UINT ia, const ICSGVector* pb, UINT ib)
  {
    const auto& a = *static_cast<const CVector*>(pa); const auto& va = (&a.val)[ia];
    const auto& b = *static_cast<const CVector*>(pb); const auto& vb = (&b.val)[ib];
    auto& c = *this; auto& vc = (&c.val)[ic];
    switch (op)
    {
    case CSG_OP2_ADD: vc = va + vb; return 0;
    case CSG_OP2_SUB: vc = va - vb; return 0;
    case CSG_OP2_MUL: vc = va * vb; return 0;
    case CSG_OP2_DIV: vc = va / vb; return 0;
    case CSG_OP2_MUL3X4: Matrix3x4R::Multiply(((Matrix3x4R*)&vc)[0], ((const Matrix3x4R*)&va)[0], ((const Matrix3x4R*)&vb)[0]); return 0;
    case CSG_OP2_PLANEP3: *(Vector4R*)&vc = 0 | Vector4R::PlaneFromPoints(*(const Vector3R*)&vc, *(const Vector3R*)&va, *(const Vector3R*)&vb); return 0;
    case CSG_OP2_PLANEPN: *(Vector4R*)&vc = 0 | Vector4R::PlaneFromPointNormal(*(const Vector3R*)&va, *(const Vector3R*)&vb); return 0;
    case CSG_OP2_POW: if (vb.den != 3) return E_INVALIDARG; vc = 0 | Rational::pow(va, vb.num); return 0;
    }
    return 0;
  }
  HRESULT __stdcall SinCos(UINT i, DOUBLE a, UINT flags)
  {
    auto* vp = &val + i; if (i + 1 > length) return E_INVALIDARG;
    ((Vector2R*)vp)[0] = 0 | Vector2R::SinCos(a, flags); return 0;
  }
  HRESULT __stdcall WriteToStream(IStream* str, UINT i, UINT n)
  {
    if (i + n > length) return E_INVALIDARG;
    return Rational::write(str, &val + i, n);
  }
  HRESULT __stdcall ReadFromStream(IStream* str, UINT i, UINT n)
  {
    if (i + n > length) return E_INVALIDARG;
    return Rational::read(str, &val + i, n);
  }
#if(0) 
  HRESULT __stdcall Compute(UINT i, const BYTE* code)
  {
    rr.p[i] = calc(code); return 0;
  }
  Rational calc(const BYTE*& code)
  {
    switch (*code++)
    {
    default: return rr.p[code[-1]];
    case 0xf0: return calc(code) + calc(code);
    case 0xf1: return calc(code) - calc(code);
    case 0xf2: return calc(code) * calc(code);
    case 0xf3: return calc(code) / calc(code);
    case 0xf4: return -calc(code);
    case 0xf5: return calc(code).CompareTo(calc(code));
    case 0xf6: return calc(code).sign();
    case 0xf7: return (int)(char)*code++;
    case 0xfd: return 0 ^ calc(code);
    case 0xfe: return 0 | calc(code);
    }
    return 0;
  }
#endif
};
