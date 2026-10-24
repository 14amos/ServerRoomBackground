using System;
using System.IO;
using System.Linq;
using Mono.Cecil;

// Renames assemblies in a folder (args: dir old=new ...) and rewrites references to them in every dll there.
var dir = args[0];
var map = args.Skip(1).Select(a => a.Split('=')).ToDictionary(p => p[0], p => p[1]);
var res = new DefaultAssemblyResolver(); res.AddSearchDirectory(dir); res.AddSearchDirectory(Environment.GetEnvironmentVariable("EFT_MANAGED") ?? ".");
foreach (var f in Directory.GetFiles(dir, "*.dll"))
{
    var bytes = File.ReadAllBytes(f);
    var a = AssemblyDefinition.ReadAssembly(new MemoryStream(bytes), new ReaderParameters { AssemblyResolver = res });
    bool dirty = false;
    foreach (var r in a.MainModule.AssemblyReferences) if (map.TryGetValue(r.Name, out var n)) { r.Name = n; dirty = true; }
    var name = a.Name.Name;
    if (map.TryGetValue(name, out var nn)) { a.Name.Name = nn; a.MainModule.Name = nn + ".dll"; dirty = true; }
    if (!dirty) continue;
    var outPath = map.ContainsKey(name) ? Path.Combine(dir, map[name] + ".dll") : f;
    a.Write(outPath + ".tmp"); a.Dispose();
    if (map.ContainsKey(name)) File.Delete(f);
    File.Move(outPath + ".tmp", outPath, true);
    Console.WriteLine("rewrote " + Path.GetFileName(f) + " -> " + Path.GetFileName(outPath));
}
