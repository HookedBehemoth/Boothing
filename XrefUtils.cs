using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnhollowerRuntimeLib.XrefScans;
using static UnhollowerRuntimeLib.XrefScans.XrefScanner;

public static class XrefUtils {
    public static IEnumerable<string> StringReferences(this MethodBase method) {
        return XrefScan(method).Where(xref => xref.Type == XrefType.Global).Select(xref => xref.ReadAsObject()?.ToString());
    }

    public static IEnumerable<MethodBase> CalledMethods(this MethodBase method) {
        return XrefScan(method).Where(xref => xref.Type == XrefType.Method).Select(xref => xref.TryResolve());
    }
}
