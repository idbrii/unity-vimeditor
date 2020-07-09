using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Linq;
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
        string m_VimPath = "/usr/local/bin/mvim";

        static VimExternalEditor()
        {
            var editor = new VimExternalEditor();
            CodeEditor.Register(editor);
        }

        VimExternalEditor()
        {
            m_Installations = new CodeEditor.Installation[]{
                // Unity will automatically filter out paths that don't exist on disk.
                // TODO: Come up with more possible paths. Try looking in PATH.
                new CodeEditor.Installation{
                    Name = "MacVim",
                         Path = "/usr/local/bin/mvim",
                },
                    new CodeEditor.Installation{
                        Name = "Vim",
                        Path = "/usr/share/vim/gvim",
                    },
                    new CodeEditor.Installation{
                        Name = "Vim",
                        Path = "C:/Program Files/vim/Vim/vim82/gvim.exe",
                    },
            };
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
            m_VimPath = editorInstallationPath;
        }

        const string k_servername_key = "vimcode_servername";
        static string GetServerName()
        {
            return EditorPrefs.GetString(k_servername_key, "Unity");
        }

        const string k_shouldsetpath_key = "vimcode_setpath";
        static bool ShouldSetPath()
        {
            return EditorPrefs.GetBool(k_shouldsetpath_key, true);
        }


        const string k_extracommands_key = "vimcode_extracommands";
        static string GetExtraCommands()
        {
            return EditorPrefs.GetString(k_extracommands_key, "");
        }

        const string k_codeassets_key = "vimcode_codeassets";
        static string GetCodeAssets()
        {
            return EditorPrefs.GetString(k_codeassets_key, ".cs,.txt,.md,.json");
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
                if (GUILayout.Button("Reset file extensions"))
                {
                    EditorPrefs.DeleteKey(k_codeassets_key);
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

                var prev_shouldsetpath = ShouldSetPath();
                var new_shouldsetpath = EditorGUILayout.Toggle(new GUIContent(
                            "Set 'path' in vim",
                            "Adds {project}/Assets/** to vim's 'path' variable to improve behaviour of gf and :find."),
                        prev_shouldsetpath);
                if (new_shouldsetpath != prev_shouldsetpath)
                {
                    EditorPrefs.SetBool(k_shouldsetpath_key, new_shouldsetpath);
                }

                // This doesn't work if ShouldSetPath is set. For some reason,
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

                if (new_shouldsetpath && !string.IsNullOrEmpty(new_extracommands))
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
            p.WaitForExit();
            return true;
        }

        /// Unity calls this function during initialization in order to sync the
        /// Project. This is different from SyncIfNeeded in that it does not get a list
        /// of changes.
        public void SyncAll()
        {
            //~ Debug.Log($"[VimExternalEditor] SyncAll ");
        }

        /// When you change Assets in Unity, this method for the current chosen
        /// instance of IExternalCodeEditor parses the new and changed Assets.
        public void SyncIfNeeded(string[] addedFiles, string[] deletedFiles, string[] movedFiles, string[] movedFromFiles, string[] importedFiles)
        {
            //~ Debug.Log($"[VimExternalEditor] SyncIfNeeded {addedFiles.Length}");
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

        Process LaunchProcess(string file, int line, int column)
        {
            ProcessStartInfo start_info = new ProcessStartInfo();
            start_info.CreateNoWindow = false;
            start_info.UseShellExecute = false;
            start_info.FileName = m_VimPath;
            start_info.WindowStyle = ProcessWindowStyle.Hidden;

            // If Unity doesn't have a column, they pass -1. Vim will abort
            // cursor on negative values, but maintains the current column on
            // 0.
            column = Math.Max(column, 0);
            // line 1 is the first line. Leave it at 0 so vim can return to the
            // previous line if using something like
            // https://github.com/farmergreg/vim-lastplace
            line = Math.Max(line, 0);

            var path = "";
            if (ShouldSetPath())
            {
                path = $"+\"set path+={Application.dataPath}/**\"";
            }

            start_info.Arguments = $"--servername {GetServerName()} --remote-silent +\"call cursor({line},{column})\" {path} {GetExtraCommands()} \"{file}\"";

            //~ Debug.Log($"[VimExternalEditor] Launching {m_VimPath} {start_info.Arguments}");

            return Process.Start(start_info);
        }

    }
}
