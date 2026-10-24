using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

// Strips an assembly to the listed types plus everything their fields / base types need, with no methods,
// properties, events or interfaces left, and renames it. args: in.dll out.dll newName Type1 Type2 ...
var res = new DefaultAssemblyResolver();
res.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0])));
res.AddSearchDirectory(Environment.GetEnvironmentVariable("EFT_MANAGED") ?? ".");
var a = AssemblyDefinition.ReadAssembly(args[0], new ReaderParameters { AssemblyResolver = res });
var mod = a.MainModule;
var keep = new HashSet<TypeDefinition>();
var q = new Queue<TypeDefinition>();
void Want(TypeReference r)
{
    if (r == null) return;
    if (r is GenericInstanceType g) { Want(g.ElementType); foreach (var x in g.GenericArguments) Want(x); return; }
    if (r is ArrayType at) { Want(at.ElementType); return; }
    if (r.IsGenericParameter) return;
    if (r.Scope is ModuleDefinition || r.Module == mod && r.Scope == mod)
    {
        var d = r.Resolve();
        if (d != null && d.Module == mod && keep.Add(d)) q.Enqueue(d);
    }
}
foreach (var n in args.Skip(3))
{
    var t = mod.GetType(n) ?? mod.GetTypes().FirstOrDefault(x => x.FullName == n);
    if (t == null) { Console.WriteLine("NOT FOUND " + n); continue; }
    if (keep.Add(t)) q.Enqueue(t);
}
while (q.Count > 0)
{
    var t = q.Dequeue();
    Want(t.BaseType);
    if (t.DeclaringType != null && keep.Add(t.DeclaringType)) q.Enqueue(t.DeclaringType);
    foreach (var f in t.Fields) if (Serialized(f)) Want(f.FieldType);
}
static bool Serialized(FieldDefinition f) =>
    !f.IsStatic && !f.HasConstant && !f.IsInitOnly && !f.IsNotSerialized &&
    (f.IsPublic || f.CustomAttributes.Any(c => c.AttributeType.Name == "SerializeField" || c.AttributeType.Name == "SerializeReference"));
int removed = 0;
foreach (var t in mod.GetTypes().Where(keep.Contains).ToList())
{
    t.Methods.Clear(); t.Properties.Clear(); t.Events.Clear(); t.Interfaces.Clear();
    foreach (var f in t.Fields.ToList()) if (!Serialized(f)) t.Fields.Remove(f);
    foreach (var f in t.Fields) foreach (var ca in f.CustomAttributes.ToList())
        if (ca.AttributeType.Name != "SerializeField" && ca.AttributeType.Name != "SerializeReference") f.CustomAttributes.Remove(ca);
    foreach (var ca in t.CustomAttributes.ToList()) if (ca.AttributeType.Name != "SerializableAttribute") t.CustomAttributes.Remove(ca);
    t.GenericParameters.ToList().ForEach(g => { g.CustomAttributes.Clear(); g.Constraints.Clear(); });
}
a.CustomAttributes.Clear(); mod.CustomAttributes.Clear();
void Prune(Mono.Collections.Generic.Collection<TypeDefinition> types)
{
    foreach (var t in types.ToList())
    {
        if (!keep.Contains(t) && t.Name != "<Module>") { types.Remove(t); removed++; continue; }
        if (t.Name == "<Module>") { t.Methods.Clear(); t.Fields.Clear(); t.NestedTypes.Clear(); continue; }
        Prune(t.NestedTypes);
    }
}
Prune(mod.Types);
mod.Resources.Clear();
var oldName = a.Name.Name;
a.Name.Name = args[2]; mod.Name = args[2] + ".dll";
foreach (var r in mod.AssemblyReferences) if (r.Name == "Assembly-CSharp-firstpass") r.Name = "EFTFirstpass";
a.Write(args[1]);
Console.WriteLine($"{oldName} -> {args[2]}: kept {keep.Count} types, removed {removed}: " + string.Join(", ", keep.Select(k => k.FullName).OrderBy(x => x)));
