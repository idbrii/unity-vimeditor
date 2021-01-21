using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System;
using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;

using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Vim.Editor
{

    [InitializeOnLoad]
    public class VimExternalEditor : IExternalCodeEditor
    {

        static VimExternalEditor()
        {
            var editor = new VimExternalEditor();
            CodeEditor.Register(editor);
        }

        VimExternalEditor()
        {
            m_Installations = BuildInstalls();
        }

        static CodeEditor.Installation[] BuildInstalls()
        {
            var installs = new List<CodeEditor.Installation>(){
                // Unity will automatically filter out paths that don't
                // exist on disk. Use some standard paths and search in
                // PATH.
                new CodeEditor.Installation{
                    Name = "MacVim",
                    // Installed with brew
                    Path = "/usr/local/bin/mvim",
                },
                new CodeEditor.Installation{
                    Name = "Vim",
                    // Linux
                    Path = "/usr/share/vim/gvim",
                },
            };

            var all_installs = Environment.GetEnvironmentVariable("PATH")
                .Split(Path.PathSeparator)
                // We could limit our search to folders named vim, but that won't
                // catch scoop-installed vim and maybe others (chocolatey).
                .SelectMany(p => GetVimExeInFolder(p))
                .ToArray();

            //~ Debug.Log($"[VimExternalEditor] Possible installs:");
            //~ foreach (var entry in all_installs)
            //~ {
            //~     Debug.Log($"[VimExternalEditor] {entry.Path} {File.Exists(entry.Path)}");
            //~ }

            return all_installs;
        }


        static readonly string[] k_executable_names =
        {
#if UNITY_EDITOR_WIN
            // batch file is preferred because if user set it up, they
            // probably want to use it.
            "gvim.bat",
            // note: scoop shim opens a command prompt in background,
            // install batch file to prevent that.
            "gvim.exe",
#elif UNITY_EDITOR_OSX
            // Are there alternatives?
            "mvim",
            // installed with gtk?
            "gvim",
#else
            // Linux
            "gvim",
#endif
        };

        static IEnumerable<CodeEditor.Installation> GetVimExeInFolder(string folder)
        {
            if (!string.IsNullOrEmpty(folder))
            {
                return k_executable_names
                    .Select(exe => Path.Combine(folder, exe))
                    .Where(path => File.Exists(path))
                    .Select(path => new CodeEditor.Installation{
                        Name = $"Vim ({Path.GetFileName(path)})",
                        Path = path,
                    });
            }
            return Enumerable.Empty<CodeEditor.Installation>();
        }

        CodeEditor.Installation[] m_Installations;
        public CodeEditor.Installation[] Installations
        {
            get {
                return m_Installations;
            }
        }

        /// Callback to the IExternalCodeEditor when it has been chosen from the
        /// PreferenceWindow.
        public void Initialize(string editorInstallationPath)
        {
            //~ Debug.Log($"[VimExternalEditor] Initialize: {editorInstallationPath}");
            EditorPrefs.SetString(k_editorpath_key, editorInstallationPath);
        }

        const string k_editorpath_key = "vimcode_editorpath";
        static string GetVimEditorPath()
        {
            return EditorPrefs.GetString(k_editorpath_key, "/usr/local/bin/mvim");
        }

        const string k_servername_key = "vimcode_servername";
        static string GetServerName()
        {
            return EditorPrefs.GetString(k_servername_key, "Unity");
        }

        const string k_force_foreground_key = "vimcode_force_foreground";
        static bool ShouldForceToForeground()
        {
            return EditorPrefs.GetBool(k_force_foreground_key, false);
        }

        const string k_gen_vs_sln_key = "vimcode_gen_vs_sln";
        static bool ShouldGenerateVisualStudioSln()
        {
            return EditorPrefs.GetBool(k_gen_vs_sln_key, true);
        }

        enum SetPathBehaviour
        {
            None,
            ToProjectPath,
            ToScriptPath,
        }
        static readonly string[] k_SetPathChoices = {
            "Don't modify path variable",
            "Add project path (Assets\\**)",
            "Add script path (Assets\\Scripts\\**)",
        };

        const string k_shouldsetpath_key = "vimcode_setpath";
        static SetPathBehaviour GetSetPathBehaviour()
        {
            return (SetPathBehaviour)EditorPrefs.GetInt(k_shouldsetpath_key, (int)SetPathBehaviour.ToProjectPath);
        }

        const string k_extracommands_key = "vimcode_extracommands";
        static string GetExtraCommands()
        {
            return EditorPrefs.GetString(k_extracommands_key, "");
        }

        const string k_codeassets_key = "vimcode_codeassets";
        static string GetCodeAssets()
        {
            return EditorPrefs.GetString(k_codeassets_key, ".cs,.shader,.h,.m,.c,.cpp,.txt,.md,.json");
        }
        static string[] GetCodeAssetsAsList()
        {
            return GetCodeAssets().Split(',');
        }


        /// Unity calls this method when it populates "Preferences/External Tools" in
        /// order to allow the code editor to generate necessary GUI. For example, when
        /// creating an an argument field for modifying the arguments sent to the code
        /// editor.
        public void OnGUI()
        {
            var style = new GUIStyle
            {
                richText = true,
                         margin = new RectOffset(0, 4, 0, 0)
            };

            using (new EditorGUI.IndentLevelScope())
            {
                //~ var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(GetType().Assembly);
                //~ GUILayout.Label($"<size=10><color=grey>{package.displayName} v{package.version} enabled</color></size>", style);

                var prev_codeassets = GetCodeAssets();
                var new_codeassets = EditorGUILayout.TextField(new GUIContent(
                            "File extensions",
                            "Comma-separated list of file extensions to open in Vim. Clear it to open all files in vim."),
                        prev_codeassets);
                new_codeassets = new_codeassets.Trim();
                if (new_codeassets != prev_codeassets)
                {
                    EditorPrefs.SetString(k_codeassets_key, new_codeassets);
                }
                if (string.IsNullOrEmpty(new_codeassets))
                {
                    EditorGUILayout.HelpBox("All files will be opened in vim.", MessageType.Info);
                }
                if (GUILayout.Button("Reset file extensions", GUILayout.Width(200)))
                {
                    EditorPrefs.DeleteKey(k_codeassets_key);
                }

                var prev_should_gen_vs_sln = ShouldGenerateVisualStudioSln();
                var new_should_gen_vs_sln = EditorGUILayout.Toggle(new GUIContent(
                            "Generate Visual Studio Solution",
                            "Generate sln and csproj when user clicks 'Open C# Project' and when files are added/moved in the project. Useful for debugging with Visual Studio, working with vscode, using OmniSharp, etc."),
                        prev_should_gen_vs_sln);
                if (new_should_gen_vs_sln != prev_should_gen_vs_sln)
                {
                    EditorPrefs.SetBool(k_gen_vs_sln_key, new_should_gen_vs_sln);
                }

                var prev_should_force_fg = ShouldForceToForeground();
                var new_should_force_fg = EditorGUILayout.Toggle(new GUIContent(
                            "Force foreground",
                            "Tell vim to put itself in the foreground when opening a file. Don't enable unless Vim's failing to foreground itself."),
                        prev_should_force_fg);
                if (new_should_force_fg != prev_should_force_fg)
                {
                    EditorPrefs.SetBool(k_force_foreground_key, new_should_force_fg);
                }

                var prev_servername = GetServerName();
                var new_servername = EditorGUILayout.TextField(new GUIContent(
                            "Vim server name",
                            "The name to pass to --servername. Displayed at the top of Vim window."),
                        prev_servername);
                if (new_servername != prev_servername)
                {
                    EditorPrefs.SetString(k_servername_key, new_servername);
                }

                var prev_shouldsetpath = GetSetPathBehaviour();
                var new_shouldsetpath = (SetPathBehaviour)EditorGUILayout.Popup(new GUIContent(
                            "Set 'path' in vim",
                            "Adds {project}/Assets/** or {project}/Assets/Scripts/** to vim's 'path' variable to improve behaviour of gf and :find."),
                        (int)prev_shouldsetpath,
                        k_SetPathChoices);
                if (new_shouldsetpath != prev_shouldsetpath)
                {
                    EditorPrefs.SetInt(k_shouldsetpath_key, (int)new_shouldsetpath);
                }

                // This doesn't work if GetSetPathBehaviour is set. For some reason,
                // vim will only let me do one extra command.
                var prev_extracommands = GetExtraCommands();
                var new_extracommands = EditorGUILayout.TextField(new GUIContent(
                            "Extra commands before filename",
                            "Pass more commands to vim \n(like +\"runtime unity.vim\" to do extra setup in ~/.vim/unity.vim)."),
                        prev_extracommands);
                if (new_extracommands != prev_extracommands)
                {
                    EditorPrefs.SetString(k_extracommands_key, new_extracommands);
                }

                if (new_shouldsetpath != SetPathBehaviour.None && !string.IsNullOrEmpty(new_extracommands))
                {
                    EditorGUILayout.HelpBox("Set 'path' and Extra commands may not play well together. If files aren't opened correclty, try removing exta commands.", MessageType.Warning);
                }

            }

        }


        bool IsCodeAsset(string filePath)
        {
            var extensions = GetCodeAssetsAsList();
            var match = extensions.FirstOrDefault(ext => filePath.EndsWith(ext));
            return match != null;
        }

        /// The external code editor needs to handle the request to open a file.
        public bool OpenProject(string filePath, int line, int column)
        {
            if (!IsCodeAsset(filePath))
            {
                return false;
            }
            //~ Debug.Log($"[VimExternalEditor] OpenProject: {filePath}:{line}");
            var p = LaunchProcess(filePath, line, column);
            if (ShouldForceToForeground())
            {
                RequestForeground(p);
            }
            // Don't wait for process to exit. It might be the first time we
            // launched vim and then it will not terminate until vim exits.
            return true;
        }

        /// Unity calls this function during initialization in order to sync the
        /// Project. This is different from SyncIfNeeded in that it does not get a list
        /// of changes.
        public void SyncAll()
        {
            //~ Debug.Log($"[VimExternalEditor] SyncAll ");
            if (ShouldGenerateVisualStudioSln())
            {
                RegenerateVisualStudioSolution();
                Debug.Log($"[VimExternalEditor] Regenerated Visual Studio solution");
            }
        }

        // Unlike 'Open C# Project', this only generates the sln (does not
        // update asset database or open any file).
        //
        // Reflection to call internal method SyncVS.Synchronizer.Sync()
        static void RegenerateVisualStudioSolution()
        {
            // Unity calls CodeEditor.Sync instead of calling
            // SyncVS.Synchronizer.Sync to generate Visual Studio solution, so
            // use reflection to call it.
            // https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Editor/Mono/CodeEditor/CodeEditorProjectSync.cs#L40-L57
            // See also
            // https://forum.unity.com/threads/solved-unity-not-generating-sln-file-from-assets-open-c-project.538487/#post-5597260
			var sync_vs_type = Type.GetType("UnityEditor.SyncVS,UnityEditor");
			var synchronizer_field = sync_vs_type.GetField("Synchronizer", BindingFlags.NonPublic | BindingFlags.Static);
			var synchronizer_object = synchronizer_field.GetValue(sync_vs_type);
			var synchronizer_type = synchronizer_object.GetType();
			var synchronizer_sync_fn = synchronizer_type.GetMethod("Sync", BindingFlags.Public | BindingFlags.Instance);

			synchronizer_sync_fn.Invoke(synchronizer_object, null);
        }

        /// When you change Assets in Unity, this method for the current chosen
        /// instance of IExternalCodeEditor parses the new and changed Assets.
        public void SyncIfNeeded(string[] addedFiles, string[] deletedFiles, string[] movedFiles, string[] movedFromFiles, string[] importedFiles)
        {
            //~ Debug.Log($"[VimExternalEditor] SyncIfNeeded added={addedFiles.Length} deleted={deletedFiles.Length} moved={movedFiles.Length} movedFrom={movedFromFiles.Length} imported={importedFiles.Length}");
            // Imported occurs super frequently -- often many times after a
            // compile. Visual Studio solutions don't care about file contents
            // -- just structure. Don't really care about deleted files since
            // they won't break visual studio so ignore them too.
            if ((addedFiles.Length + movedFiles.Length) > 0 && ShouldGenerateVisualStudioSln())
            {
                RegenerateVisualStudioSolution();
                Debug.Log($"[VimExternalEditor] Regenerated Visual Studio solution for {addedFiles.Length} new files, {movedFiles.Length} moved files.");
            }
        }

        /// Unity stores the path of the chosen editor. An instance of
        /// IExternalCodeEditor can take responsibility for this path, by returning
        /// true when this method is being called. The out variable installation need
        /// to be constructed with the path and the name that should be shown in the
        /// "External Tools" code editor list.
        public bool TryGetInstallationForPath(string editorPath, out CodeEditor.Installation installation)
        {
            //~ Debug.Log($"[VimExternalEditor] TryGetInstallationForPath {editorPath}");
            // I don't understand why this function exists. I must return true
            // to be able to control what the selected editor does, but it's
            // just passing one of the paths I provided in Installations.
            installation = Installations.FirstOrDefault(install => install.Path == editorPath);
            return !string.IsNullOrEmpty(installation.Name);
        }

        ProcessStartInfo BuildVim()
        {
            ProcessStartInfo start_info = new ProcessStartInfo();
            start_info.CreateNoWindow = false;
            start_info.UseShellExecute = false;
            start_info.FileName = GetVimEditorPath();
            start_info.WindowStyle = ProcessWindowStyle.Hidden;
            return start_info;
        }

        Process LaunchProcess(string file, int line, int column)
        {
            ProcessStartInfo start_info = BuildVim();

            // If Unity doesn't have a column, they pass -1. Vim will abort
            // cursor on negative values, but maintains the current column on
            // 0.
            column = Math.Max(column, 0);
            // line 1 is the first line. Leave it at 0 so vim can return to the
            // previous line if using something like
            // https://github.com/farmergreg/vim-lastplace
            line = Math.Max(line, 0);

            var path = "";
            switch (GetSetPathBehaviour())
            {
                case SetPathBehaviour.None:
                    break;

                case SetPathBehaviour.ToProjectPath:
                    path = $"+\"set path+={Application.dataPath}/**\"";
                    break;

                case SetPathBehaviour.ToScriptPath:
                    path = $"+\"set path+={Application.dataPath}/Scripts/**\"";
                    break;
            }

            start_info.Arguments = $"--servername {GetServerName()} --remote-silent +\"call cursor({line},{column})\" {GetExtraCommands()} {path} \"{file}\"";

            //~ Debug.Log($"[VimExternalEditor] Launching {start_info.FileName} {start_info.Arguments}");

            return Process.Start(start_info);
        }


        void RequestForeground(Process p)
        {
            // Windows: user32 functions SetForegroundWindow and SetWindowPos
            // don't seem to work.
            // https://stackoverflow.com/questions/18071381/how-can-i-bring-a-process-window-to-the-front
            ProcessStartInfo start_info = BuildVim();
            // foreground() doesn't seem to work on Windows. :help foreground() recommends remote_foreground.
            // Use --clean to prevent delay of loading user's vim config.
            start_info.Arguments = $"--clean +\"call remote_foreground('{GetServerName()}')\" +quit";
            Process.Start(start_info);
        }

    }
}
