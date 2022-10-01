using System;
using System.Reflection;

namespace IslandWorkshopSolver.Solver;

public enum PeakCycle
{
    [PeakAttr(false, false)] Unknown,
    [PeakAttr(false, true)] Cycle2Weak,
    [PeakAttr(true, true)] Cycle2Strong,
    [PeakAttr(false, true)] Cycle3Weak,
    [PeakAttr(true, true)] Cycle3Strong,
    [PeakAttr(false, true)] Cycle4Weak,
    [PeakAttr(true, true)] Cycle4Strong,
    [PeakAttr(false, true)] Cycle5Weak,
    [PeakAttr(true, true)] Cycle5Strong,
    [PeakAttr(false, true)] Cycle6Weak,
    [PeakAttr(true, true)] Cycle6Strong,
    [PeakAttr(true, true)] Cycle7Weak,
    [PeakAttr(false, true)] Cycle7Strong,
    [PeakAttr(false, false)] Cycle45,
    [PeakAttr(false, false)] Cycle5,
    [PeakAttr(false, false)] Cycle67
}

class PeakAttr : Attribute
{
    internal PeakAttr(bool reliable, bool terminal)
    {
        this.Reliable = reliable;
        this.Terminal = terminal;
    }
    public bool Reliable { get; private set; }
    public bool Terminal { get; private set; }
}

public static class Peaks
{
    public static bool IsReliable(this PeakCycle p)
    {
        PeakAttr attr = GetAttr(p);
        return attr.Reliable;
    }

    public static bool IsTerminal(this PeakCycle p)
    {
        PeakAttr attr = GetAttr(p);
        return attr.Terminal;
    }

    private static PeakAttr GetAttr(PeakCycle p)
    {
        return (PeakAttr)Attribute.GetCustomAttribute(ForValue(p), typeof(PeakAttr));
    }

    private static MemberInfo ForValue(PeakCycle p)
    {
        return typeof(PeakCycle).GetField(Enum.GetName(typeof(PeakCycle), p));
    }
}
