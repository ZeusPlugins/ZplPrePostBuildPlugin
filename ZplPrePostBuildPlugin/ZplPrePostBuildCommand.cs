using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YoYoStudio.Core.Utils;
using YoYoStudio.Core.Utils.FileAPI;
using YoYoStudio.Plugins.Attributes;
using YoYoStudio.Resources;

namespace YoYoStudio
{
    namespace Plugins
    {
        namespace ZplPrePostBuildPlugin
        {
            [HarmonyPatch(typeof(CmdProcess), "RunAsync", new Type[] { typeof(bool), typeof(string) }, new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal })]
            public class ZplPrePostBuildPatches
            {
                public static bool Stop;
                public static CmdProcess BackupInstance;
                public static WeakEvent<CmdProcess.OnCmdCompletion> BackupCallbacks;

                public static string PostBuildProc;
                public static string PostBuildArgs;
                public static string PostBuildWDir;

                public static string PreBuildPath;
                public static string PostBuildPath;

                public static void TempReset()
                {
                    BackupInstance = null;
                    BackupCallbacks = null;
                    PreBuildPath = null;
                    PostBuildPath = null;
                }

                public static void DeleteScripts()
                {
                    try
                    {
                        if (PreBuildPath != null && PreBuildPath != "") FileIO.DeleteFile(PreBuildPath);
                        if (PostBuildPath != null && PostBuildPath != "") FileIO.DeleteFile(PostBuildPath);
                    }
                    catch { }
                }

                public static void OnPostBuildComplete(int _exitcode)
                {
                    // don't care about the exit code, delete all scripts regardless.
                    DeleteScripts();
                }

                public static void OnIgorComplete(int _exitcode)
                {
                    if (_exitcode != 0)
                    {
                        // igor failed, do nothing.
                        DeleteScripts();
                        TempReset();
                    }
                    else
                    {
                        // igor ok, execute post-build
                        Stop = true;
                        CmdProcess postbuild = new CmdProcess(PostBuildProc, PostBuildArgs, eOutputStream.AssetCompiler | eOutputStream.Output);
                        postbuild.OnCompletion += OnPostBuildComplete;
                        postbuild.RunAsync(true, PostBuildWDir);
                    }
                }

                public static void OnPreBuildComplete(int _exitcode)
                {
                    if (_exitcode != 0)
                    {
                        // pre-build script failed, perform the original callbacks
                        BackupCallbacks.Throw(_exitcode);
                        DeleteScripts();
                        TempReset();
                    }
                    else
                    {
                        // pre-build script is well, invoke Igor
                        Stop = true; // discard our hooked method.
                        BackupInstance.OnCompletion = BackupCallbacks;
                        BackupInstance.OnCompletion += OnIgorComplete; // for post-build
                        BackupInstance.RunAsync();
                    }
                }

                public static string PreprocessScript(string script, ref Dictionary<string, object> bff)
                {
                    string topreprocess = script;

                    // typical YoYo macro expansion syntax.
                    // ${thing} will be replaced with the value of thing
                    // nothing wrong with it, just noting.
                    foreach (var entry in bff)
                    {
                        topreprocess = topreprocess.Replace("${" + entry.Key + "}", JsonHelper.ReadStringField(bff, entry.Key));
                    }

                    return topreprocess;
                }

                public static bool Prefix(CmdProcess __instance, ref Task<int> __result)
                {
                    // when we want to run the original RunAsync without troubles
                    if (Stop)
                    {
                        Stop = false;
                        return true;
                    }

                    // no scripts are defined, better not bother.
                    if (ZplPrePostBuildCommand.PostBuildScript == "" && ZplPrePostBuildCommand.PreBuildScript == "")
                    {
                        return true;
                    }

                    // only bother if trying to run Igor
                    if (FileIO.GetFileName(__instance.Process) != "Igor.exe")
                    {
                        return true;
                    }

                    // the parser is awful, but it works.
                    Dictionary<string, string> igorArgs = ParseIgorString(__instance.Args);

                    // only run on package actions! aka PackageZip PackageDmg etcetc
                    if (!igorArgs["_Type"].StartsWith("Package"))
                    {
                        return true;
                    }

                    string bffPath = igorArgs["options"];
                    Dictionary<string, object> bff = JsonHelper.DeserializeObject(FileIO.ReadTextFile(bffPath));

                    string a_projectDir = JsonHelper.ReadStringField(bff, "projectDir");

                    string script_path = FileIO.PathCombine(a_projectDir, "PreBuild.bat");
                    string script_postbuild_path = FileIO.PathCombine(a_projectDir, "PostBuild.bat");
                    FileIO.WriteTextFile(script_path, PreprocessScript(ZplPrePostBuildCommand.PreBuildScript, ref bff));
                    FileIO.WriteTextFile(script_postbuild_path, PreprocessScript(ZplPrePostBuildCommand.PostBuildScript, ref bff));

                    string script_proc = "cmd.exe";
                    string script_args = $"/c \"\"{script_path}\" \"{bffPath}\"\"";

                    PostBuildArgs = $"/c \"\"{script_postbuild_path}\" \"{bffPath}\"\"";

                    CmdProcess prebuild = new CmdProcess(script_proc, script_args, eOutputStream.Output | eOutputStream.AssetCompiler);
                    prebuild.OnCompletion += OnPreBuildComplete;

                    PreBuildPath = script_path;
                    PostBuildPath = script_postbuild_path;
                    PostBuildProc = script_proc;

                    // DO NOT perform any callbacks! We will decide what to do with that.
                    BackupInstance = __instance;
                    BackupCallbacks = BackupInstance.OnCompletion;
                    __instance.OnCompletion = null;

                    // run script instead of Igor, then in OnPreBuildComplete we will run Igor.
                    PostBuildWDir = a_projectDir;
                    __result = prebuild.RunAsync(true, a_projectDir);

                    // do not run the original RunAsync, we will run it later.
                    return false;
                }

                public static Dictionary<string, string> ParseIgorString(string _Args)
                {
                    // -j=8 -options="D:\Progs\GMS23\Temp\GMS2TEMP\build.bff" -v -- Windows Run
                    Dictionary<string, string> ret = new Dictionary<string, string>();

                    string args = _Args;
                    while (args.Length > 0)
                    {
                        if (args.StartsWith("-- "))
                        {
                            args = args.Replace("-- ", "");
                            int platformSep = args.IndexOf(' ');
                            string thePlatform = args.Substring(0, platformSep);
                            args = args.Substring(1 + platformSep);
                            string theType = args;

                            ret["_Platform"] = thePlatform;
                            ret["_Type"] = theType;
                            break;
                        }

                        int dashIndex = args.IndexOf('-');
                        int spaceIndex = args.IndexOf(' ');
                        if (spaceIndex == -1) spaceIndex = int.MaxValue;
                        int eqIndex = args.IndexOf('=');
                        if (eqIndex == -1) eqIndex = int.MaxValue;

                        bool hasValue = eqIndex < spaceIndex;
                        int endIndex = hasValue ? eqIndex : spaceIndex;

                        string propertyValue = "";
                        string propertyName = args.Substring(1 + dashIndex, endIndex - 1);

                        args = args.Substring(1 + endIndex);

                        if (hasValue)
                        {
                            int valEnd;
                            if (args.StartsWith("\""))
                            {
                                // quoted option
                                valEnd = args.IndexOf('"', 1);
                                propertyValue = args.Substring(1, valEnd - 1);
                            }
                            else
                            {
                                valEnd = args.IndexOf(' ');
                                propertyValue = args.Substring(0, valEnd);
                            }

                            args = args.Substring(1 + valEnd).TrimStart(' ');
                        }

                        ret[propertyName] = propertyValue;
                    }

                    return ret;
                }
            }

            [ModuleName("PrePostBuild Command", "Handles the project file side of things.")]
            public class ZplPrePostBuildCommand : IModule, IDisposable
            {
                public ModulePackage IdeInterface { get; set; }
                public ModulePackage Package => IdeInterface;

                public static string PreBuildScript;
                public static string PostBuildScript;

                public static void ScriptReset()
                {
                    PreBuildScript = "";
                    PostBuildScript = "";
                }

                public string LoadNoteQuick(ResourceBase theNote)
                {
                    if (theNote != null)
                    {
                        // [0] - raw path as in JSON
                        // [1] - safe platform path
                        string safePlatformPath = FileIO.PathCombine(MacroExpansion.Expand("${project_dir}"), theNote.GetAllFilePaths()[1]);
                        string theText = FileIO.ReadTextFile(safePlatformPath);

                        return theText;
                    }

                    // note does not exist?
                    return "";
                }

                public void ReFetchNotesContent()
                {
                    ScriptReset();

                    var preref = ProjectManager.Current.FindResourceByName("PreBuildScript", typeof(GMNotes));
                    if (preref != null)
                    {
                        PreBuildScript = LoadNoteQuick(preref.resource); // will fetch a note from hard drive.
                    }

                    var postref = ProjectManager.Current.FindResourceByName("PostBuildScript", typeof(GMNotes));
                    if (postref != null)
                    {
                        PostBuildScript = LoadNoteQuick(postref.resource);
                    }
                }

                public void OnProjectLoaded()
                {
                    ReFetchNotesContent();
                }

                public void OnProjectSaved(bool _success)
                {
                    if (_success)
                    {
                        ReFetchNotesContent();
                    }
                }

                public void OnProjectClosed()
                {
                    ZplPrePostBuildPatches.DeleteScripts();
                }

                public void HarmonyInit()
                {
                    var harmony = new Harmony("com.nkrapivindev.prepostbuild");
                    harmony.PatchAll();
                }

                public void OnIDEInitialised()
                {
                    IDE.OnProjectLoaded += OnProjectLoaded;
                    IDE.OnProjectSaved += OnProjectSaved;
                    IDE.OnProjectClosed += OnProjectClosed;

                    ScriptReset();
                    HarmonyInit();
                    Log.WriteLine(eLog.Default, "[ZplPrePostBuild]: Initialized.");
                }

                public void Initialise(ModulePackage _ide)
                {
                    IdeInterface = _ide;
                    OnIDEInitialised();
                }

                #region IDisposable Support
                private bool disposed = false; // To detect redundant calls

                protected virtual void Dispose(bool disposing)
                {
                    if (!disposed)
                    {
                        if (disposing)
                        {
                            // TODO: dispose managed state (managed objects).
                        }

                        // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                        // TODO: set large fields to null.
                        IdeInterface = null;

                        disposed = true;
                    }
                }

                // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
                ~ZplPrePostBuildCommand()
                {
                    // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
                    Dispose(false);
                }

                // This code added to correctly implement the disposable pattern.
                public void Dispose()
                {
                    // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
                    Dispose(true);
                    // TODO: uncomment the following line if the finalizer is overridden above.
                    GC.SuppressFinalize(this);
                }
                #endregion

            }
        }
    }
}
