#pragma once

#include "math.h"

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

