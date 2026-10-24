using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace ServerRoomBackground.Repack
{
    /// <summary>
    /// Repacks live EFT's server-room menu background (level713 = Assets/Scenes/UI/Seasonal/EnvironmentUIScene_Season_LAB.unity,
    /// read in place from the live install, never written) plus exactly the objects it references into two bundles the
    /// SPT 4.1.6 client (Unity 2022.3.43) can load:
    ///   serverroom_assets.bundle  every referenced object of the live sharedassets / resources / globalgamemanagers files
    ///   serverroom_scene.bundle   the scene itself
    /// Every object is parsed with an exact 2022.3.43 type tree (oracle bundles built by the Unity editor from SPT's dlls
    /// + ServerRoomBackground.Scripts) and must consume exactly its byte size, or the run fails.
    /// Changes to the live data: texture / mesh stream data inlined, audio data moved into the bundle's .resource,
    /// live's audio mixer dropped (references nulled; the plugin routes to SPT's "UI" group), MonoScripts of the 5 classes
    /// SPT lacks point at ServerRoomBackground.Scripts, and EnvironmentUIRoot.CameraContainer (live: the camera-motion component)
    /// points at that component's Transform (SPT's field is a Transform).
    /// args: liveDataDir oracleOutDir outDir
    /// </summary>
    static class Program
    {
        const string EngineVersion = "2022.3.43f1";
        const string Level = "level713";
        const string SceneName = "EnvironmentUIScene_Season_LAB";
        const string ScenePath = "Assets/Scenes/UI/Seasonal/" + SceneName + ".unity";
        const string AssetsBundle = "serverroom_assets.bundle";
        const string SceneBundle = "serverroom_scene.bundle";
        const string ScriptsAsm = "ServerRoomBackground.Scripts";

        // Classes SPT 0.16.9 lacks (live namespace.class), rebuilt in ServerRoomBackground.Scripts.
        static readonly HashSet<string> Rebuilt = new HashSet<string>
        {
            "EFT.UI.StreamingVideoPlayer", "EFT.UI.HorizontalEnvironmentCameraMotion", "UI.Utilities.EnvironmentPortableRadioController",
            "Audio.AnimationEventLocalSoundPlayer", "AnimatorKeepStateOnDisable",
        };
        // Oracle assembly names -> real names (Unity reserves Assembly-CSharp*, so the oracle used renamed copies).
        static readonly Dictionary<string, string> OracleAsm = new Dictionary<string, string> { { "EFTAsm", "Assembly-CSharp" }, { "EFTFirstpass", "Assembly-CSharp-firstpass" } };
        static readonly HashSet<int> Dropped = new HashSet<int> { 240, 241, 243, 244, 245 }; // AudioMixer*, AudioMixerGroup/Snapshot/Effect controllers
        // Live script objects not shipped: the SPT client's own copy is assigned at runtime by the plugin.
        static readonly HashSet<string> DroppedScripts = new HashSet<string> { "Unity.Postprocessing.Runtime:UnityEngine.Rendering.PostProcessing.PostProcessResources" };
        // Live layouts with fields SPT's class lacks: extra float fields inserted after a field (names are ours; Unity
        // skips fields its class does not have). PrismEffects: 4 floats after secondaryToneValues (live 772 bytes, SPT 756).
        static readonly Dictionary<string, (string after, string[] floats)> TreePatches = new Dictionary<string, (string, string[])>
        {
            { "Assembly-CSharp:PrismEffects", ("secondaryToneValues", new[] { "liveOnly_0", "liveOnly_1", "liveOnly_2", "liveOnly_3" }) },
        };
        static readonly Dictionary<string, TypeTreeType> patched = new Dictionary<string, TypeTreeType>();
        static readonly string[] Builtin = { "Library/unity default resources", "Resources/unity_builtin_extra" };
        static string Norm(string p)
        {
            var l = p.ToLowerInvariant();
            if (l == "library/unity default resources" || l == "resources/unity default resources") return Builtin[0];
            if (l == "resources/unity_builtin_extra" || l == "library/unity_builtin_extra") return Builtin[1];
            return p;
        }

        static string live, outDir;
        static readonly AssetsManager am = new AssetsManager();
        static readonly Dictionary<int, TypeTreeType> engineTrees = new Dictionary<int, TypeTreeType>();
        static readonly Dictionary<string, TypeTreeType> scriptTrees = new Dictionary<string, TypeTreeType>();
        static readonly Dictionary<string, AssetsFileInstance> files = new Dictionary<string, AssetsFileInstance>();
        static BundleFileInstance oracleAssets, oracleScene;
        static AssetsFileInstance oracleCab, oracleShared, oracleLevel;
        static readonly List<string> log = new List<string>();
        static readonly List<string> bad = new List<string>();

        sealed class Obj
        {
            public string File; public long Pid; public AssetsFileInstance Inst; public AssetFileInfo Info; public int TypeId;
            public string ScriptKey; // asm:ns.class after remap (MonoBehaviours)
            public TypeTreeType Tree; public AssetTypeValueField Value; public bool Dropped;
        }

        static readonly Dictionary<(string, long), Obj> objs = new Dictionary<(string, long), Obj>();

        static void Log(string s) { Console.WriteLine(s); log.Add(s); }

        static int Main(string[] args)
        {
            live = args[0]; var oracleDir = args[1]; outDir = args[2];
            if (args.Length > 4 && args[3] == "dump") { LoadOracle(oracleDir); Dump(args[4], long.Parse(args[5])); return 0; }
            Directory.CreateDirectory(outDir);
            try
            {
                LoadOracle(oracleDir);
                Run();
                File.WriteAllLines(Path.Combine(outDir, "repack.log"), log);
                return 0;
            }
            catch (Exception e)
            {
                Log("FAILED: " + e);
                File.WriteAllLines(Path.Combine(outDir, "repack.log"), log);
                return 1;
            }
        }

        static string ScriptKey(string asm, string ns, string cls) => asm + ":" + (ns == "" ? "" : ns + ".") + cls;
        static string Bare(string asm) => asm.EndsWith(".dll") ? asm.Substring(0, asm.Length - 4) : asm;

        static void LoadOracle(string dir)
        {
            oracleAssets = am.LoadBundleFile(Path.Combine(dir, "oracle_assets.bundle"), true);
            oracleScene = am.LoadBundleFile(Path.Combine(dir, "oracle_scene.bundle"), true);
            foreach (var b in new[] { oracleAssets, oracleScene })
            {
                var dirs = b.file.BlockAndDirInfo.DirectoryInfos;
                for (int i = 0; i < dirs.Count; i++)
                {
                    if (!b.file.IsAssetsFile(i)) continue;
                    var inst = am.LoadAssetsFileFromBundle(b, i, false);
                    if (b == oracleAssets) oracleCab = inst;
                    else if (dirs[i].Name.EndsWith(".sharedAssets")) oracleShared = inst;
                    else oracleLevel = inst;
                    foreach (var t in inst.file.Metadata.TypeTreeTypes)
                    {
                        if (t.TypeId == 114 && t.ScriptTypeIndex != 0xffff)
                        {
                            var sp = inst.file.Metadata.ScriptTypes[t.ScriptTypeIndex];
                            if (sp.FileId != 0) continue;
                            var ms = am.GetBaseField(inst, sp.PathId);
                            var asm = Bare(ms["m_AssemblyName"].AsString);
                            if (OracleAsm.TryGetValue(asm, out var real)) asm = real;
                            scriptTrees[ScriptKey(asm, ms["m_Namespace"].AsString, ms["m_ClassName"].AsString)] = t;
                        }
                        else if (!engineTrees.ContainsKey(t.TypeId)) engineTrees[t.TypeId] = t;
                    }
                }
            }
            Log($"[oracle] {engineTrees.Count} engine trees ({string.Join(" ", engineTrees.Keys.OrderBy(k => k).Select(k => (AssetClassID)k))}), {scriptTrees.Count} script trees");
        }

        static AssetsFileInstance FileOf(string name)
        {
            if (files.TryGetValue(name, out var f)) return f;
            var p = Path.Combine(live, name);
            if (!File.Exists(p)) throw new FileNotFoundException("live file " + p);
            // Read-only: AssetsManager opens with FileShare.Read, nothing is ever written back to the live install.
            f = am.LoadAssetsFile(new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.Read), p, false);
            files[name] = f;
            return f;
        }

        static string ExtName(AssetsFileInstance inst, string self, int fid) => fid == 0 ? self : Norm(inst.file.Metadata.Externals[fid - 1].PathName);

        /// <summary>Reads an object with its exact tree; MonoBehaviours resolve their script first.</summary>
        static Obj Load(string file, long pid)
        {
            if (objs.TryGetValue((file, pid), out var o)) return o;
            var inst = FileOf(file);
            var info = inst.file.GetAssetInfo(pid);
            if (info == null) return null;
            o = new Obj { File = file, Pid = pid, Inst = inst, Info = info, TypeId = info.GetTypeId(inst.file) };
            var r = inst.file.Reader;
            long start = info.GetAbsoluteByteOffset(inst.file);
            if (o.TypeId == 114)
            {
                r.Position = start + 16; // m_GameObject (12) + m_Enabled (1, aligned 4)
                int sfid = r.ReadInt32(); long spid = r.ReadInt64();
                var ms = Load(ExtName(inst, file, sfid), spid);
                var ns = ms.Value["m_Namespace"].AsString; var cls = ms.Value["m_ClassName"].AsString;
                var asm = Rebuilt.Contains((ns == "" ? "" : ns + ".") + cls) ? ScriptsAsm : Bare(ms.Value["m_AssemblyName"].AsString);
                o.ScriptKey = ScriptKey(asm, ns, cls);
                if (DroppedScripts.Contains(o.ScriptKey)) { o.Dropped = true; objs[(file, pid)] = o; return o; }
                if (TreePatches.ContainsKey(o.ScriptKey) && scriptTrees.ContainsKey(o.ScriptKey)) o.Tree = Patched(o.ScriptKey);
                else if (!scriptTrees.TryGetValue(o.ScriptKey, out o.Tree)) { bad.Add($"NO TREE script {o.ScriptKey} ({file} {pid})"); objs[(file, pid)] = o; return o; }
            }
            else if (!engineTrees.TryGetValue(o.TypeId, out o.Tree)) { bad.Add($"NO TREE {(AssetClassID)o.TypeId} ({file} {pid})"); objs[(file, pid)] = o; return o; }
            var tf = new AssetTypeTemplateField(); tf.FromTypeTree(o.Tree);
            r.Position = start;
            o.Value = tf.MakeValue(r);
            if (r.Position - start != info.ByteSize)
            {
                bad.Add($"{file} {pid} {(o.ScriptKey ?? ((AssetClassID)o.TypeId).ToString())}: tree used {r.Position - start} of {info.ByteSize} bytes");
                o.Value = null;
            }
            objs[(file, pid)] = o;
            return o;
        }

        static TypeTreeType Patched(string key)
        {
            if (patched.TryGetValue(key, out var done)) return done;
            var src = scriptTrees[key]; var (after, names) = TreePatches[key];
            var str = src.StringBufferBytes.ToList();
            // Field names live in the type's own string buffer (offsets without the common-strings bit).
            string Name(TypeTreeNode n)
            {
                if ((n.NameStrOffset & 0x80000000) != 0) return null;
                int o = (int)n.NameStrOffset, e = o; while (src.StringBufferBytes[e] != 0) e++;
                return Encoding.ASCII.GetString(src.StringBufferBytes, o, e - o);
            }
            var nodes = src.Nodes.ToList();
            var floatNode = nodes.First(n => n.Level == 1 && Name(n) == "noiseScale"); // a float field of PrismEffects
            int at = nodes.FindIndex(n => n.Level == 1 && Name(n) == after);
            if (at < 0) throw new InvalidDataException($"tree patch {key}: no field {after}");
            int end = at + 1; while (end < nodes.Count && nodes[end].Level > 1) end++;
            var add = new List<TypeTreeNode>();
            foreach (var nm in names)
            {
                uint off = (uint)str.Count; str.AddRange(Encoding.ASCII.GetBytes(nm)); str.Add(0);
                add.Add(new TypeTreeNode { Version = floatNode.Version, Level = 1, TypeFlags = floatNode.TypeFlags, TypeStrOffset = floatNode.TypeStrOffset,
                    NameStrOffset = off, ByteSize = 4, MetaFlags = floatNode.MetaFlags, RefTypeHash = 0 });
            }
            nodes.InsertRange(end, add);
            for (int i = 0; i < nodes.Count; i++) nodes[i].Index = (uint)i;
            var t = new TypeTreeType { TypeId = src.TypeId, IsStrippedType = src.IsStrippedType, ScriptTypeIndex = src.ScriptTypeIndex, ScriptIdHash = src.ScriptIdHash,
                Nodes = nodes, StringBufferBytes = str.ToArray(), TypeDependencies = src.TypeDependencies ?? new int[0], IsRefType = false,
                // A different hash than SPT's class: Unity must convert by field name, not read the data as its own layout.
                TypeHash = new Hash128(MD5.HashData(Encoding.UTF8.GetBytes("live-layout:" + key))) };
            Log($"[tree] {key}: {names.Length} live-only float fields after {after}");
            return patched[key] = t;
        }

        static void Dump(string file, long pid)
        {
            var inst = FileOf(file); var info = inst.file.GetAssetInfo(pid);
            var r = inst.file.Reader; long start = info.GetAbsoluteByteOffset(inst.file);
            r.Position = start + 16; int sfid = r.ReadInt32(); long spid = r.ReadInt64();
            var ms = Load(ExtName(inst, file, sfid), spid);
            var key = ScriptKey(Bare(ms.Value["m_AssemblyName"].AsString), ms.Value["m_Namespace"].AsString, ms.Value["m_ClassName"].AsString);
            var tf = new AssetTypeTemplateField(); tf.FromTypeTree(scriptTrees[key]);
            Console.WriteLine($"{key} size {info.ByteSize}");
            r.Position = start;
            foreach (var c in tf.Children)
            {
                long p = r.Position - start;
                string val;
                try { var v = c.MakeValue(r); val = v.Value?.ToString() ?? ("{" + string.Join(",", v.Children.Take(4).Select(x => x.FieldName + "=" + (x.Value?.ToString() ?? "..."))) + "}"); }
                catch (Exception e) { val = "ERR " + e.Message; }
                Console.WriteLine($"  {p,4} {c.Type} {c.Name} = {val}");
                if (r.Position - start > info.ByteSize) { Console.WriteLine("  -- past end"); break; }
            }
            r.Position = start; var raw = r.ReadBytes((int)info.ByteSize);
            File.WriteAllBytes(Path.Combine(outDir, $"{file}_{pid}.bin"), raw);
        }

        static IEnumerable<AssetTypeValueField> PPtrs(AssetTypeValueField f)
        {
            if (f.TypeName.StartsWith("PPtr<") && f.Children.Count == 2 && f.Children[0].FieldName == "m_FileID") { yield return f; yield break; }
            if (f.Children == null) yield break;
            foreach (var c in f.Children) foreach (var p in PPtrs(c)) yield return p;
        }

        static void Run()
        {
            var level = FileOf(Level);
            // 1. Closure: every object of the scene, then everything they reference outside the built-in resources.
            var queue = new Queue<(string, long)>();
            foreach (var info in level.file.AssetInfos) queue.Enqueue((Level, info.PathId));
            var seen = new HashSet<(string, long)>();
            var missing = new Dictionary<string, int>(); int droppedRefs = 0;
            while (queue.Count > 0)
            {
                var key = queue.Dequeue();
                if (!seen.Add(key)) continue;
                var o = Load(key.Item1, key.Item2);
                if (o == null) { missing[key.Item1] = missing.TryGetValue(key.Item1, out var c) ? c + 1 : 1; continue; }
                if (o.Value == null || o.Dropped) continue;
                foreach (var p in PPtrs(o.Value))
                {
                    int fid = p["m_FileID"].AsInt; long pid = p["m_PathID"].AsLong;
                    if (pid == 0) continue;
                    var tf = ExtName(o.Inst, o.File, fid);
                    if (Builtin.Contains(tf)) continue;
                    var ti = FileOf(tf).file.GetAssetInfo(pid);
                    if (ti != null && Dropped.Contains(ti.GetTypeId(FileOf(tf).file))) { droppedRefs++; continue; }
                    var to = ti != null && ti.GetTypeId(FileOf(tf).file) == 114 ? Load(tf, pid) : null;
                    if (to != null && to.Dropped) { droppedRefs++; Log($"[closure] {o.File} {o.Pid} -> {to.ScriptKey} not shipped (SPT's copy assigned at runtime)"); continue; }
                    queue.Enqueue((tf, pid));
                }
            }
            if (bad.Count > 0) { foreach (var b in bad) Log("[BAD] " + b); throw new InvalidDataException(bad.Count + " objects do not match their trees"); }
            if (missing.Count > 0) throw new InvalidDataException("referenced objects not found: " + string.Join(", ", missing.Select(m => m.Key + "=" + m.Value)));
            var outside = objs.Values.Where(o => o.File != Level && !o.Dropped).OrderBy(o => o.File).ThenBy(o => o.Pid).ToList();
            Log($"[closure] {objs.Count(o => o.Value.File == Level)} scene objects + {outside.Count} referenced objects, {droppedRefs} references to live's audio mixer dropped");
            foreach (var g in outside.GroupBy(o => o.File + " " + (o.ScriptKey ?? ((AssetClassID)o.TypeId).ToString())).OrderBy(g => g.Key))
                Log($"  {g.Count(),4} {g.Key}");

            // 2. New path ids in the assets bundle (1 = its AssetBundle object).
            var newId = new Dictionary<(string, long), long>();
            long next = 2;
            foreach (var o in outside) newId[(o.File, o.Pid)] = next++;
            var cab = "CAB-" + Hex(MD5.HashData(Encoding.UTF8.GetBytes("serverroom:" + SceneName)));
            var cabPath = "archive:/" + cab + "/" + cab;
            var resource = new MemoryStream();

            // 3. Assets bundle file (built on the oracle CAB file: header, version and platform of a real 2022.3.43 bundle).
            var af = oracleCab.file;
            foreach (var i in af.AssetInfos.ToList()) af.Metadata.RemoveAssetInfo(i);
            af.Metadata.TypeTreeTypes.Clear(); af.Metadata.ScriptTypes.Clear();
            var builtinTemplate = oracleLevel.file.Metadata.Externals.First(e => Norm(e.PathName) == Builtin[0]);
            Log("[externals] live scene: " + string.Join(", ", level.file.Metadata.Externals.Select(e => e.PathName)));
            var extraTemplate = af.Metadata.Externals.FirstOrDefault(e => Norm(e.PathName) == Builtin[1]);
            af.Metadata.Externals.Clear();
            af.Metadata.Externals.Add(Clone(builtinTemplate, Builtin[0]));
            af.Metadata.Externals.Add(Clone(extraTemplate ?? builtinTemplate, Builtin[1]));
            af.Metadata.UnityVersion = EngineVersion;
            int AssetsExt(string name) => Array.IndexOf(Builtin, name) + 1;
            var cabScripts = new Dictionary<long, ushort>(); // MonoScript new id -> script type index
            int nulled = 0;
            foreach (var o in outside)
            {
                RemapPPtrs(o, n => newId.TryGetValue(n, out var id) ? (0, id) : ((int, long)?)null, AssetsExt, ref nulled);
                Inline(o, cabPath + ".resS", cabPath.Replace(cab + "/" + cab, cab + "/" + cab) + ".resource", resource);
                if (o.TypeId == 115)
                {
                    var ns = o.Value["m_Namespace"].AsString; var cls = o.Value["m_ClassName"].AsString;
                    var asm = Rebuilt.Contains((ns == "" ? "" : ns + ".") + cls) ? ScriptsAsm : Bare(o.Value["m_AssemblyName"].AsString);
                    o.Value["m_AssemblyName"].AsString = asm + ".dll";
                }
                ushort si = 0xffff;
                if (o.TypeId == 114)
                {
                    long msId = o.Value["m_Script"]["m_PathID"].AsLong;
                    if (!cabScripts.TryGetValue(msId, out si)) { si = (ushort)af.Metadata.ScriptTypes.Count; af.Metadata.ScriptTypes.Add(new AssetPPtr(0, msId)); cabScripts[msId] = si; }
                }
                EnsureType(af.Metadata, o, si);
                var info = AssetFileInfo.Create(af, newId[(o.File, o.Pid)], o.TypeId, si, null, false);
                info.SetNewData(o.Value);
                af.Metadata.AddAssetInfo(info);
            }
            // AssetBundle object: every object listed (so the plugin can make them all resident first).
            var abTree = engineTrees[142];
            var abTf = new AssetTypeTemplateField(); abTf.FromTypeTree(abTree);
            var ab = ValueBuilder.DefaultValueFieldFromTemplate(abTf);
            ab["m_Name"].AsString = AssetsBundle; ab["m_AssetBundleName"].AsString = AssetsBundle;
            ab["m_RuntimeCompatibility"].AsUInt = 1; ab["m_PathFlags"].AsInt = 7;
            foreach (var o in outside) AddContainer(ab, "serverroom/" + newId[(o.File, o.Pid)], 0, newId[(o.File, o.Pid)]);
            EnsureType(af.Metadata, new Obj { TypeId = 142, Tree = abTree }, 0xffff);
            var abInfo = AssetFileInfo.Create(af, 1, 142, 0xffff, null, false);
            abInfo.SetNewData(ab); af.Metadata.AddAssetInfo(abInfo);
            af.Metadata.TypeTreeEnabled = true;
            var cabBytes = WriteAssets(af);
            Verify(cabBytes, cab);
            Log($"[assets] {outside.Count} objects -> {AssetsBundle} ({cab}), {cabScripts.Count} script types, resource {resource.Length} bytes, {nulled} references nulled");

            // 4. Scene file: the live level file with exact trees, references into the assets bundle / built-ins.
            var md = level.file.Metadata;
            var oldExt = md.Externals.ToList();
            var sharedName = "BuildPlayer-" + SceneName + ".sharedAssets";
            var sharedPath = "archive:/BuildPlayer-" + SceneName + "/" + sharedName;
            var archTemplate = oracleLevel.file.Metadata.Externals[0];
            md.Externals.Clear();
            md.Externals.Add(Clone(archTemplate, sharedPath));
            md.Externals.Add(Clone(archTemplate, cabPath));
            md.Externals.Add(Clone(builtinTemplate, Builtin[0]));
            md.Externals.Add(Clone(extraTemplate ?? builtinTemplate, Builtin[1]));
            md.UnityVersion = EngineVersion;
            int SceneExt(string name) => Array.IndexOf(Builtin, name) + 3;
            var sceneObjs = objs.Values.Where(o => o.File == Level).ToList();
            int camFix = 0; nulled = 0;
            foreach (var o in sceneObjs)
            {
                if (o.ScriptKey == "Assembly-CSharp:EFT.UI.VideoEnvironmentUIRoot") camFix += FixCameraContainer(o);
                RemapPPtrs(o, n => n.Item1 == Level ? (0, n.Item2) : newId.TryGetValue(n, out var id) ? (2, id) : ((int, long)?)null, SceneExt, ref nulled, oldExt);
                Inline(o, null, cabPath + ".resource", resource);
            }
            for (int i = 0; i < md.ScriptTypes.Count; i++)
            {
                var sp = md.ScriptTypes[i];
                var tgt = sp.FileId == 0 ? Level : oldExt[sp.FileId - 1].PathName;
                if (!newId.TryGetValue((tgt, sp.PathId), out var id)) throw new InvalidDataException($"scene script type {i} -> {tgt} {sp.PathId} not in the closure");
                md.ScriptTypes[i] = new AssetPPtr(2, id);
            }
            for (int ti = 0; ti < md.TypeTreeTypes.Count; ti++)
            {
                var t = md.TypeTreeTypes[ti];
                var o = sceneObjs.FirstOrDefault(x => x.Info.TypeIdOrIndex == ti);
                if (o == null) throw new InvalidDataException($"scene type entry {ti} ({(AssetClassID)t.TypeId}) has no object");
                Fill(t, o.Tree, o.TypeId == 114 ? o.ScriptKey : null);
            }
            md.TypeTreeEnabled = true;
            foreach (var o in sceneObjs) o.Info.SetNewData(o.Value);
            var levelBytes = WriteAssets(level.file);
            Verify(levelBytes, "BuildPlayer-" + SceneName);
            Log($"[scene] {sceneObjs.Count} objects, {md.ScriptTypes.Count} script types, CameraContainer fixed {camFix}, {nulled} references nulled");

            // 5. Scene .sharedAssets: PreloadData (all asset bundle objects) + AssetBundle (the scene path).
            var sf = oracleShared.file;
            sf.Metadata.Externals.Clear();
            sf.Metadata.Externals.Add(Clone(archTemplate, cabPath));
            sf.Metadata.UnityVersion = EngineVersion;
            foreach (var info in sf.AssetInfos.ToList())
            {
                var v = am.GetBaseField(oracleShared, info);
                if (info.TypeId == 150)
                {
                    var arr = v["m_Assets"]["Array"]; arr.Children.Clear();
                    foreach (var o in outside)
                    {
                        var e = ValueBuilder.DefaultValueFieldFromArrayTemplate(arr);
                        e["m_FileID"].AsInt = 1; e["m_PathID"].AsLong = newId[(o.File, o.Pid)];
                        arr.Children.Add(e);
                    }
                }
                else if (info.TypeId == 142)
                {
                    v["m_Name"].AsString = SceneBundle; v["m_AssetBundleName"].AsString = SceneBundle;
                    var c = v["m_Container"]["Array"].Children[0]; c["first"].AsString = ScenePath;
                    var deps = v["m_Dependencies"]["Array"]; deps.Children.Clear();
                    var d = ValueBuilder.DefaultValueFieldFromArrayTemplate(deps); d.AsString = AssetsBundle; deps.Children.Add(d);
                }
                info.SetNewData(v);
            }
            var sharedBytes = WriteAssets(sf);

            // 6. Bundles.
            WriteBundle(oracleAssets, Path.Combine(outDir, AssetsBundle), new List<(string, bool, byte[])> { (cab, true, cabBytes), (cab + ".resource", false, resource.ToArray()) });
            WriteBundle(oracleScene, Path.Combine(outDir, SceneBundle), new List<(string, bool, byte[])> { (sharedName, true, sharedBytes), ("BuildPlayer-" + SceneName, true, levelBytes) });
            Log($"[done] {AssetsBundle} + {SceneBundle} -> {outDir}");
        }

        /// <summary>Live's EnvironmentUIRoot.CameraContainer points at the HorizontalEnvironmentCameraMotion component;
        /// SPT's field is a Transform: point it at that component's Transform.</summary>
        static int FixCameraContainer(Obj root)
        {
            var cc = root.Value["CameraContainer"];
            if (cc["m_FileID"].AsInt != 0) return 0;
            var target = objs.TryGetValue((Level, cc["m_PathID"].AsLong), out var t) ? t : null;
            if (target == null || target.TypeId != 114) return 0;
            var go = objs[(Level, target.Value["m_GameObject"]["m_PathID"].AsLong)];
            var tr = go.Value["m_Component"]["Array"].Children.Select(c => c["component"]["m_PathID"].AsLong).First(id => objs[(Level, id)].TypeId == 4);
            Log($"[scene] CameraContainer {cc["m_PathID"].AsLong} ({target.ScriptKey}) -> Transform {tr}");
            cc["m_PathID"].AsLong = tr;
            return 1;
        }

        static void RemapPPtrs(Obj o, Func<(string, long), (int, long)?> map, Func<string, int> builtinExt, ref int nulled, List<AssetsFileExternal> exts = null)
        {
            foreach (var p in PPtrs(o.Value))
            {
                int fid = p["m_FileID"].AsInt; long pid = p["m_PathID"].AsLong;
                if (pid == 0) { p["m_FileID"].AsInt = 0; continue; }
                var tf = fid == 0 ? o.File : Norm((exts ?? o.Inst.file.Metadata.Externals)[fid - 1].PathName);
                if (Builtin.Contains(tf)) { p["m_FileID"].AsInt = builtinExt(tf); continue; }
                var m = map((tf, pid));
                if (m == null) { p["m_FileID"].AsInt = 0; p["m_PathID"].AsLong = 0; nulled++; continue; }
                p["m_FileID"].AsInt = m.Value.Item1; p["m_PathID"].AsLong = m.Value.Item2;
            }
        }

        /// <summary>Texture / mesh stream data inline; audio data into the bundle's .resource.</summary>
        static void Inline(Obj o, string unused, string resourcePath, MemoryStream resource)
        {
            var v = o.Value;
            if (o.TypeId == 83)
            {
                var res = v["m_Resource"]; var src = res["m_Source"].AsString;
                if (src.Length == 0) return;
                var bytes = ReadLive(src, res["m_Offset"].AsULong, (long)res["m_Size"].AsULong);
                while (resource.Length % 32 != 0) resource.WriteByte(0);
                res["m_Offset"].AsULong = (ulong)resource.Length; res["m_Source"].AsString = resourcePath;
                resource.Write(bytes, 0, bytes.Length);
                return;
            }
            var sd = v.Children.FirstOrDefault(c => c.FieldName == "m_StreamData");
            if (sd == null || sd["path"].AsString.Length == 0) return;
            var data = ReadLive(sd["path"].AsString, sd["offset"].AsULong, sd["size"].AsUInt);
            AssetTypeValueField target = o.TypeId == 43 ? v["m_VertexData"]["m_DataSize"] : v.Children.FirstOrDefault(c => c.TypeName == "TypelessData");
            if (target == null) throw new InvalidDataException($"{o.File} {o.Pid} {(AssetClassID)o.TypeId}: stream data but no inline field");
            if (target.AsByteArray != null && target.AsByteArray.Length != 0) throw new InvalidDataException($"{o.File} {o.Pid}: both inline and streamed data");
            target.AsByteArray = data;
            sd["offset"].AsULong = 0; sd["size"].AsUInt = 0; sd["path"].AsString = "";
        }

        static byte[] ReadLive(string rel, ulong offset, long size)
        {
            using var fs = new FileStream(Path.Combine(live, rel), FileMode.Open, FileAccess.Read, FileShare.Read);
            fs.Position = (long)offset;
            var b = new byte[size]; int n = 0;
            while (n < size) { int r = fs.Read(b, n, (int)(size - n)); if (r <= 0) throw new EndOfStreamException(rel); n += r; }
            return b;
        }

        static void EnsureType(AssetsFileMetadata md, Obj o, ushort scriptIndex)
        {
            if (md.FindTypeTreeTypeIndexByID(o.TypeId, scriptIndex) >= 0) return;
            var t = new TypeTreeType { TypeId = o.TypeId, ScriptTypeIndex = scriptIndex };
            Fill(t, o.Tree, o.ScriptKey);
            md.TypeTreeTypes.Add(t);
        }

        static void Fill(TypeTreeType t, TypeTreeType src, string scriptKey)
        {
            t.IsStrippedType = src.IsStrippedType;
            t.Nodes = src.Nodes; t.StringBufferBytes = src.StringBufferBytes;
            t.TypeDependencies = src.TypeDependencies ?? new int[0];
            t.TypeHash = src.TypeHash; t.IsRefType = false;
            if (scriptKey != null)
            {
                // ScriptIdHash = MD4(class + namespace + assembly) of the assembly the class loads from.
                var asm = scriptKey.Substring(0, scriptKey.IndexOf(':'));
                var full = scriptKey.Substring(asm.Length + 1);
                int dot = full.LastIndexOf('.');
                var ns = dot < 0 ? "" : full.Substring(0, dot); var cls = dot < 0 ? full : full.Substring(dot + 1);
                t.ScriptIdHash = new Hash128(Md4.Hash(Encoding.UTF8.GetBytes(cls + ns + asm)));
            }
            else t.ScriptIdHash = src.ScriptIdHash;
        }

        static AssetsFileExternal Clone(AssetsFileExternal e, string path) =>
            new AssetsFileExternal { PathName = path, OriginalPathName = path, Type = e.Type, Guid = e.Guid, VirtualAssetPathName = "" };

        static void AddContainer(AssetTypeValueField bundle, string path, int fileId, long pathId)
        {
            var arr = bundle["m_Container"]["Array"];
            var e = ValueBuilder.DefaultValueFieldFromArrayTemplate(arr);
            e["first"].AsString = path;
            var pre = bundle["m_PreloadTable"]["Array"];
            var pp = ValueBuilder.DefaultValueFieldFromArrayTemplate(pre);
            pp["m_FileID"].AsInt = fileId; pp["m_PathID"].AsLong = pathId;
            e["second"]["preloadIndex"].AsInt = pre.Children.Count;
            e["second"]["preloadSize"].AsInt = 1;
            pre.Children.Add(pp);
            e["second"]["asset"]["m_FileID"].AsInt = fileId;
            e["second"]["asset"]["m_PathID"].AsLong = pathId;
            arr.Children.Add(e);
        }

        static string Hex(byte[] b) => BitConverter.ToString(b).Replace("-", "").ToLowerInvariant();

        static byte[] WriteAssets(AssetsFile file)
        {
            using var ms = new MemoryStream();
            file.Write(new AssetsFileWriter(ms));
            return ms.ToArray();
        }

        /// <summary>Re-reads a written file and parses every object with its own tree (exact size).</summary>
        static void Verify(byte[] bytes, string name)
        {
            var m = new AssetsManager();
            var inst = m.LoadAssetsFile(new MemoryStream(bytes), name, false);
            int ok = 0;
            foreach (var info in inst.file.AssetInfos)
            {
                var t = inst.file.Metadata.TypeTreeTypes[info.TypeIdOrIndex];
                var tf = new AssetTypeTemplateField(); tf.FromTypeTree(t);
                var r = inst.file.Reader; long s = info.GetAbsoluteByteOffset(inst.file); r.Position = s;
                tf.MakeValue(r);
                if (r.Position - s != info.ByteSize) throw new InvalidDataException($"verify {name}: object {info.PathId} used {r.Position - s} of {info.ByteSize}");
                ok++;
            }
            Log($"[verify] {name}: {ok} objects re-read exactly");
        }

        static void WriteBundle(BundleFileInstance reference, string path, List<(string name, bool serialized, byte[] data)> entries)
        {
            var bf = reference.file;
            var dirs = bf.BlockAndDirInfo.DirectoryInfos;
            var keep = dirs.ToList();
            dirs.Clear();
            try
            {
                foreach (var e in entries)
                {
                    var d = AssetBundleDirectoryInfo.Create(e.name, e.serialized);
                    d.SetNewData(e.data);
                    dirs.Add(d);
                }
                var tmp = path + ".tmp";
                using (var fs = File.Create(tmp)) bf.Write(new AssetsFileWriter(fs));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            finally { dirs.Clear(); dirs.AddRange(keep); }
        }
    }
}
