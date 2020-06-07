using System;
using System.Collections.Generic;
using static csg3mf.CDX;

namespace csg3mf
{
  public unsafe class Container : Neuron
  {
    public Container(IScene p) => Nodes = p ?? Factory.CreateScene();
    public readonly IScene Nodes;
    public readonly List<string> Infos = new List<string>();
    public override object Invoke(int id, object p)
    {
      if (id == 5) return this; //AutoStop
      if (id == 2) return "Script";
      if (id == 6) { OnUpdate?.Invoke(); return null; } //step
      if (id == 3) System.Windows.Forms.Application.RaiseIdle(null);
      return base.Invoke(id, p);
    }
    public Action OnUpdate;
  }
}
