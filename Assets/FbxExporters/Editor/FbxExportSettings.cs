using System;
using System.IO;
using UnityEditorInternal;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace FbxExporters.EditorTools {

    [CustomEditor(typeof(ExportSettings))]
    public class ExportSettingsEditor : UnityEditor.Editor {
        Vector2 scrollPos = Vector2.zero;
        const float LabelWidth = 225;
        const float SelectableLabelMinWidth = 100;
        const float BrowseButtonWidth = 55;

        public override void OnInspectorGUI() {
            ExportSettings exportSettings = (ExportSettings)target;

            // Increasing the label width so that none of the text gets cut off
            EditorGUIUtility.labelWidth = LabelWidth;

            scrollPos = GUILayout.BeginScrollView (scrollPos);

            var version = FbxExporters.Editor.ModelExporter.GetVersionFromReadme ();
            if (!string.IsNullOrEmpty(version)) {
                GUILayout.Label ("Version: " + version, EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.Space ();
            }

            exportSettings.mayaCompatibleNames = EditorGUILayout.Toggle (
                new GUIContent ("Convert to Maya Compatible Naming:",
                    "In Maya some symbols such as spaces and accents get replaced when importing an FBX " +
                    "(e.g. \"foo bar\" becomes \"fooFBXASC032bar\"). " +
                    "On export, convert the names of GameObjects so they are Maya compatible." +
                    (exportSettings.mayaCompatibleNames ? "" :
                        "\n\nWARNING: Disabling this feature may result in lost material connections," +
                    " and unexpected character replacements in Maya.")
                ),
                exportSettings.mayaCompatibleNames);

            exportSettings.centerObjects = EditorGUILayout.Toggle (
                new GUIContent("Center Objects:",
                    "Center objects around a shared root and keep their relative placement unchanged."),
                exportSettings.centerObjects
            );

            GUILayout.BeginHorizontal ();
            GUILayout.Label (new GUIContent (
                "Export Path:",
                "Relative path for saving Model Prefabs."));

            var pathLabel = ExportSettings.GetRelativeSavePath();
            if (pathLabel == ".") { pathLabel = "(Assets root)"; }
            EditorGUILayout.SelectableLabel(pathLabel,
                EditorStyles.textField,
                GUILayout.MinWidth(SelectableLabelMinWidth),
                GUILayout.Height(EditorGUIUtility.singleLineHeight));

            if (GUILayout.Button ("Browse", EditorStyles.miniButton, GUILayout.Width (BrowseButtonWidth))) {
                string initialPath = ExportSettings.GetAbsoluteSavePath();

                // if the directory doesn't exist, set it to the default save path
                // so we don't open somewhere unexpected
                if (!System.IO.Directory.Exists (initialPath)) {
                    initialPath = Application.dataPath;
                }

                string fullPath = EditorUtility.OpenFolderPanel (
                        "Select Model Prefabs Path", initialPath, null
                        );

                // Unless the user canceled, make sure they chose something in the Assets folder.
                if (!string.IsNullOrEmpty (fullPath)) {
                    var relativePath = ExportSettings.ConvertToAssetRelativePath(fullPath);
                    if (string.IsNullOrEmpty(relativePath)) {
                        Debug.LogWarning ("Please select a location in the Assets folder");
                    } else {
                        ExportSettings.SetRelativeSavePath(relativePath);

                        // Make sure focus is removed from the selectable label
                        // otherwise it won't update
                        GUIUtility.hotControl = 0;
                        GUIUtility.keyboardControl = 0;
                    }
                }
            }
            GUILayout.EndHorizontal ();

            EditorGUILayout.Space ();

            GUILayout.BeginHorizontal ();
            GUILayout.Label (new GUIContent (
                "DCC Application:",
                "Select the Digital Content Creation (DCC) Application for which you would like to install the Unity integration."));

            // dropdown to select Maya version to use
            var options = ExportSettings.GetDCCOptions();
            // make sure we never initially have browse selected
            if (exportSettings.selectedDCCApp == options.Length - 1) {
                exportSettings.selectedDCCApp = 0;
            }
            int oldValue = exportSettings.selectedDCCApp;
            exportSettings.selectedDCCApp = EditorGUILayout.Popup(exportSettings.selectedDCCApp, options);
            if (exportSettings.selectedDCCApp == options.Length - 1) {
                var ext = "";
                switch (Application.platform) {
                case RuntimePlatform.WindowsEditor:
                    ext = "exe";
                    break;
                case RuntimePlatform.OSXEditor:
                    ext = "app";
                    break;
                default:
                    throw new System.NotImplementedException ();
                }

                string dccPath = EditorUtility.OpenFilePanel ("Select Digital Content Creation Application", ExportSettings.kDefaultAdskRoot, ext);

                // check that the path is valid and references the maya executable
                if (!string.IsNullOrEmpty (dccPath)) {
                    // get the directory of the executable
                    var md = Directory.GetParent (dccPath);
                    // UNI-29074 TODO: add Maya LT support
                    // Check that the executable is not in a MayaLT directory (thus being MayaLT instead of Maya executable).
                    // On Mac path resembles: /Applications/Autodesk/mayaLT2018/Maya.app
                    // On Windows path resembles: C:\Program Files\Autodesk\MayaLT2018\bin\maya.exe
                    // Therefore check both executable folder (for Mac) and its parent (for Windows)
                    if (md.Name.ToLower().StartsWith("mayalt") || md.Parent.Name.ToLower ().StartsWith ("mayalt")) {
                        Debug.LogError (string.Format("Unity Integration does not support Maya LT: \"{0}\"", md.FullName));
                        exportSettings.selectedDCCApp = oldValue;
                        return;
                    }

                    ExportSettings.DCCType foundDCC = ExportSettings.DCCType.Maya;
                    var foundDCCPath = TryFindDCC (dccPath, ext, ExportSettings.DCCType.Maya);
                    if (foundDCCPath == null && Application.platform == RuntimePlatform.WindowsEditor) {
                        if (!ExportSettings.IsEarlierThanMax2017 (dccPath)) {
                            Debug.LogWarning ("Earlier than 3ds Max 2017 is not supported");
                        } else {
                            foundDCCPath = TryFindDCC (dccPath, ext, ExportSettings.DCCType.Max);
                            foundDCC = ExportSettings.DCCType.Max;
                        }
                    }
                    if (foundDCCPath == null) {
                        Debug.LogError (string.Format ("Could not find supported DCC application at: \"{0}\"", Path.GetDirectoryName (dccPath)));
                        exportSettings.selectedDCCApp = oldValue;
                    } else {
                        dccPath = foundDCCPath;
                        ExportSettings.AddDCCOption (dccPath, foundDCC);
                    }
                    Repaint ();
                } else {
                    exportSettings.selectedDCCApp = oldValue;
                }
            }
            GUILayout.EndHorizontal ();

			var installIntegrationContent = new GUIContent(
                    "Install Unity Integration",
                    "Install and configure the Unity integration for the selected Digital Content Creation (DCC) application so that you can import and export directly with this project.");
            if (GUILayout.Button (installIntegrationContent)) {
                FbxExporters.Editor.IntegrationsUI.InstallDCCIntegration ();
            }

            GUILayout.FlexibleSpace ();
            GUILayout.EndScrollView ();

            if (GUI.changed) {
                EditorUtility.SetDirty (exportSettings);
                exportSettings.Save ();
            }
        }

        private static string TryFindDCC(string dccPath, string ext, ExportSettings.DCCType dccType){
            string dccName = "";
            switch (dccType) {
            case ExportSettings.DCCType.Maya:
                dccName = "maya";
                break;
            case ExportSettings.DCCType.Max:
                dccName = "3dsmax";
                break;
            default:
                throw new System.NotImplementedException ();
            }

            if (Path.GetFileNameWithoutExtension (dccPath).ToLower ().Equals (dccName)) {
                return dccPath;
            }

            // clicked on the wrong application, try to see if we can still find
            // a dcc in this directory.
            var dccDir = new DirectoryInfo(Path.GetDirectoryName(dccPath));
            FileSystemInfo[] files = {};
            switch(Application.platform){
            case RuntimePlatform.OSXEditor:
                files = dccDir.GetDirectories ("*." + ext);
                break;
            case RuntimePlatform.WindowsEditor:
                files = dccDir.GetFiles ("*." + ext);
                break;
            default:
                throw new System.NotImplementedException();
            }

            string newDccPath = null;
            foreach (var file in files) {
                var filename = Path.GetFileNameWithoutExtension (file.Name).ToLower ();
                if (filename.Equals (dccName)) {
                    newDccPath = file.FullName.Replace("\\","/");
                    break;
                }
            }
            return newDccPath;
        }

    }

    [FilePath("ProjectSettings/FbxExportSettings.asset",FilePathAttribute.Location.ProjectFolder)]
    public class ExportSettings : ScriptableSingleton<ExportSettings>
    {
        public const string kDefaultSavePath = ".";

        /// <summary>
        /// The path where all the different versions of Maya are installed
        /// by default. Depends on the platform.
        /// </summary>
        public static string kDefaultAdskRoot {
            get{
                switch (Application.platform) {
                case RuntimePlatform.WindowsEditor:
                    return "C:/Program Files/Autodesk";
                case RuntimePlatform.OSXEditor:
                    return "/Applications/Autodesk";
                default:
                    throw new NotImplementedException ();
                }
            }
        }

        // Note: default values are set in LoadDefaults().
        public bool mayaCompatibleNames;
        public bool centerObjects;

        public int selectedDCCApp = 0;

        /// <summary>
        /// The path where Convert To Model will save the new fbx and prefab.
        ///
        /// To help teams work together, this is stored to be relative to the
        /// Application.dataPath, and the path separator is the forward-slash
        /// (e.g. unix and http, not windows).
        ///
        /// Use GetRelativeSavePath / SetRelativeSavePath to get/set this
        /// value, properly interpreted for the current platform.
        /// </summary>
        [SerializeField]
        string convertToModelSavePath;

        // List of names in order that they appear in option list
        [SerializeField]
        private List<string> dccOptionNames;
        // List of paths in order that they appear in the option list
        [SerializeField]
        private List<string> dccOptionPaths;

        protected override void LoadDefaults()
        {
            mayaCompatibleNames = true;
            centerObjects = true;
            convertToModelSavePath = kDefaultSavePath;
            dccOptionPaths = null;
            dccOptionNames = null;
        }

        /// <summary>
        /// Increments the name if there is a duplicate in dccAppOptions.
        /// </summary>
        /// <returns>The unique name.</returns>
        /// <param name="name">Name.</param>
        private static string GetUniqueName(string name){
            if (!instance.dccOptionNames.Contains(name)) {
                return name;
            }
            var format = "{1} ({0})";
            int index = 1;
            // try extracting the current index from the name and incrementing it
            var result = System.Text.RegularExpressions.Regex.Match(name, @"\((?<number>\d+?)\)$");
            if (result != null) {
                var number = result.Groups["number"].Value;
                int tempIndex;
                if (int.TryParse (number, out tempIndex)) {
                    var indexOfNumber = name.LastIndexOf (number);
                    format = name.Remove (indexOfNumber, number.Length).Insert (indexOfNumber, "{0}");
                    index = tempIndex+1;
                }
            }

            string uniqueName = null;
            do {
                uniqueName = string.Format (format, index, name);
                index++;
            } while (instance.dccOptionNames.Contains(name));

            return uniqueName;
        }

        /// <summary>
        /// Find Maya and 3DsMax installations at default install path.
        /// Add results to given dictionary.
        /// 
        /// If MAYA_LOCATION is set, add this to the list as well.
        /// </summary>
        private static void FindDCCInstalls() {
            var dccOptionName = instance.dccOptionNames;
            var dccOptionPath = instance.dccOptionPaths;

            // If the location is given by the environment, use it.
            var location = System.Environment.GetEnvironmentVariable ("MAYA_LOCATION");
            if (!string.IsNullOrEmpty(location)) {
                location = location.TrimEnd('/');
                dccOptionPath.Add (GetMayaExePath (location.Replace ("\\", "/")));
                dccOptionName.Add ("MAYA_LOCATION");
            }

            if (!Directory.Exists (kDefaultAdskRoot)) {
                // no autodesk products installed
                return;
            }
            // List that directory and find the right version:
            // either the newest version, or the exact version we wanted.
            var adskRoot = new System.IO.DirectoryInfo(kDefaultAdskRoot);
            foreach(var productDir in adskRoot.GetDirectories()) {
                var product = productDir.Name;

                // Only accept those that start with 'maya' in either case.
                if (product.StartsWith ("maya", StringComparison.InvariantCultureIgnoreCase)) {
                    // UNI-29074 TODO: add Maya LT support
                    // Reject MayaLT -- it doesn't have plugins.
                    if (product.StartsWith ("mayalt", StringComparison.InvariantCultureIgnoreCase)) {
                        continue;
                    }
                    string version = product.Substring ("maya".Length);
                    dccOptionPath.Add (GetMayaExePath (productDir.FullName.Replace ("\\", "/")));
                    dccOptionName.Add (GetUniqueName ("Maya " + version));
                }

                if (product.StartsWith ("3ds max", StringComparison.InvariantCultureIgnoreCase)) {
                    var exePath = string.Format ("{0}/{1}", productDir.FullName.Replace ("\\", "/"), "3dsmax.exe");
                    if (!IsEarlierThanMax2017 (exePath)) {
                        Debug.LogWarning ("Earlier than 3ds Max 2017 is not supported");
                        continue;
                    }
                    string version = product.Substring ("3ds max ".Length);
                    dccOptionPath.Add (exePath);
                    dccOptionName.Add (GetUniqueName ("3ds Max " + version));
                }
            }
        }

        /// <summary>
        /// Gets the maya exe at Maya install location.
        /// </summary>
        /// <returns>The maya exe path.</returns>
        /// <param name="location">Location of Maya install.</param>
        private static string GetMayaExePath(string location)
        {
            switch (Application.platform) {
            case RuntimePlatform.WindowsEditor:
                return location + "/bin/maya.exe";
            case RuntimePlatform.OSXEditor:
                // MAYA_LOCATION on mac is set by Autodesk to be the
                // Contents directory. But let's make it easier on people
                // and allow just having it be the app bundle or a
                // directory that holds the app bundle.
                if (location.EndsWith(".app/Contents")) {
                    return location + "/MacOS/Maya";
                } else if (location.EndsWith(".app")) {
                    return location + "/Contents/MacOS/Maya";
                } else {
                    return location + "/Maya.app/Contents/MacOS/Maya";
                }
            default:
                throw new NotImplementedException ();
            }
        }

        public static GUIContent[] GetDCCOptions(){
            if (instance.dccOptionNames == null ||
                instance.dccOptionNames.Count != instance.dccOptionPaths.Count ||
                instance.dccOptionNames.Count == 0) {

                instance.dccOptionPaths = new List<string> ();
                instance.dccOptionNames = new List<string> ();
                FindDCCInstalls ();
            }

            // remove options that no longer exist
            List<string> pathsToDelete = new List<string>();
            List<string> namesToDelete = new List<string>();
            for(int i = 0; i < instance.dccOptionPaths.Count; i++) {
                var dccPath = instance.dccOptionPaths [i];
                if (!File.Exists (dccPath)) {
                    if (i == instance.selectedDCCApp) {
                        instance.selectedDCCApp = 0;
                    }
                    namesToDelete.Add (instance.dccOptionNames [i]);
                    pathsToDelete.Add (dccPath);
                }
            }
            foreach (var str in pathsToDelete) {
                instance.dccOptionPaths.Remove (str);
            }
            foreach (var str in namesToDelete) {
                instance.dccOptionNames.Remove (str);
            }

            if (instance.dccOptionPaths.Count <= 0) {
                return new GUIContent[]{
                    new GUIContent("<No DCC found>"),
                    new GUIContent("Browse...")
                };
            }

            GUIContent[] optionArray = new GUIContent[instance.dccOptionPaths.Count+1];
            for(int i = 0; i < instance.dccOptionPaths.Count; i++){
                optionArray [i] = new GUIContent(
                    instance.dccOptionNames[i],
                    instance.dccOptionPaths[i]
                );
            }
            optionArray [optionArray.Length - 1] = new GUIContent("Browse...");

            return optionArray;
        }

        public enum DCCType { Maya, Max };

        public static void AddDCCOption(string newOption, DCCType dcc){
            if (Application.platform == RuntimePlatform.OSXEditor && dcc == DCCType.Maya) {
                // on OSX we get a path ending in .app, which is not quite the exe
                newOption = GetMayaExePath(newOption);
            }

            var dccOptionPaths = instance.dccOptionPaths;
            if (dccOptionPaths.Contains(newOption)) {
                instance.selectedDCCApp = dccOptionPaths.IndexOf (newOption);
                return;
            }

            string optionName = "";
            switch (dcc) {
            case DCCType.Maya:
                var version = AskMayaVersion(newOption);

                // UNI-29074 TODO: add Maya LT support
                // make sure this is not Maya LT
                if (version.ToLower ().StartsWith ("lt")) {
                    Debug.LogError (string.Format("Unity Integration does not support Maya LT: \"{0}\"", newOption));
                    return;
                }
                optionName = GetUniqueName ("Maya " + version);
                break;
            case DCCType.Max:
                optionName = GetMaxOptionName (newOption);
                break;
            default:
                throw new System.NotImplementedException();
            }

            instance.dccOptionNames.Add (optionName);
            dccOptionPaths.Add (newOption);
            instance.selectedDCCApp = dccOptionPaths.Count - 1;
        }

        /// <summary>
        /// Ask the version number by running maya.
        /// </summary>
        static string AskMayaVersion(string exePath) {
            System.Diagnostics.Process myProcess = new System.Diagnostics.Process();
            myProcess.StartInfo.FileName = exePath;
            myProcess.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            myProcess.StartInfo.CreateNoWindow = true;
            myProcess.StartInfo.UseShellExecute = false;
            myProcess.StartInfo.RedirectStandardOutput = true;
            myProcess.StartInfo.Arguments = "-v";
            myProcess.EnableRaisingEvents = true;
            myProcess.Start();
            string resultString = myProcess.StandardOutput.ReadToEnd();
            myProcess.WaitForExit();

            // Output is like: Maya 2018, Cut Number 201706261615
            // We want the stuff after 'Maya ' and before the comma.
            // TODO: less brittle! Consider also the mel command "about -version".
            var commaIndex = resultString.IndexOf(',');
            return resultString.Substring(0, commaIndex).Substring("Maya ".Length);
        }

        /// <summary>
        /// Gets the unique label for a new 3DsMax dropdown option.
        /// </summary>
        /// <returns>The 3DsMax dropdown option label.</returns>
        /// <param name="exePath">Exe path.</param>
        public static string GetMaxOptionName(string exePath){
            return GetUniqueName (Path.GetFileName(Path.GetDirectoryName (exePath)));
        }

        public static bool IsEarlierThanMax2017(string exePath){
            var name = Path.GetFileName (Path.GetDirectoryName (exePath)).ToLower();
            name = name.Replace ("3ds max", "").Trim();
            int version;
            return int.TryParse (name, out version) && version < 2017;
        }

        public static string GetSelectedDCCPath()
        {
            return (instance.dccOptionPaths.Count>0) ? instance.dccOptionPaths [instance.selectedDCCApp] : "";
        }

        /// <summary>
        /// The path where Convert To Model will save the new fbx and prefab.
        /// This is relative to the Application.dataPath ; it uses '/' as the
        /// separator on all platforms.
        /// </summary>
        public static string GetRelativeSavePath() {
            var relativePath = instance.convertToModelSavePath;
            if (string.IsNullOrEmpty(relativePath)) {
                relativePath = kDefaultSavePath;
            }
            return NormalizePath(relativePath, isRelative: true);
        }

        /// <summary>
        /// The path where Convert To Model will save the new fbx and prefab.
        /// This is an absolute path, with platform separators.
        /// </summary>
        public static string GetAbsoluteSavePath() {
            var relativePath = GetRelativeSavePath();
            var absolutePath = Path.Combine(Application.dataPath, relativePath);
            return NormalizePath(absolutePath, isRelative: false,
                    separator: Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Set the path where Convert To Model will save the new fbx and prefab.
        /// This is interpreted as being relative to the Application.dataPath
        /// </summary>
        public static void SetRelativeSavePath(string newPath) {
            instance.convertToModelSavePath = NormalizePath(newPath, isRelative: true);
        }

        /// <summary>
        /// Convert an absolute path into a relative path like what you would
        /// get from GetRelativeSavePath.
        ///
        /// This uses '/' as the path separator.
        ///
        /// If 'requireSubdirectory' is the default on, return empty-string if the full
        /// path is not in a subdirectory of assets.
        /// </summary>
        public static string ConvertToAssetRelativePath(string fullPathInAssets, bool requireSubdirectory = true)
        {
            if (!Path.IsPathRooted(fullPathInAssets)) {
                fullPathInAssets = Path.GetFullPath(fullPathInAssets);
            }
            var relativePath = GetRelativePath(Application.dataPath, fullPathInAssets);
            if (requireSubdirectory && relativePath.StartsWith("..")) {
                if (relativePath.Length == 2 || relativePath[2] == '/') {
                    // The relative path has us pop out to another directory,
                    // so return an empty string as requested.
                    return "";
                }
            }
            return relativePath;
        }

        /// <summary>
        /// Compute how to get from 'fromDir' to 'toDir' via a relative path.
        /// </summary>
        public static string GetRelativePath(string fromDir, string toDir,
                char separator = '/')
        {
            // https://stackoverflow.com/questions/275689/how-to-get-relative-path-from-absolute-path
            // Except... the MakeRelativeUri that ships with Unity is buggy.
            // e.g. https://bugzilla.xamarin.com/show_bug.cgi?id=5921
            // among other bugs. So we roll our own.

            // Normalize the paths, assuming they're absolute paths (if they
            // aren't, they get normalized as relative paths)
            fromDir = NormalizePath(fromDir, isRelative: false);
            toDir = NormalizePath(toDir, isRelative: false);

            // Break them into path components.
            var fromDirs = fromDir.Split('/');
            var toDirs = toDir.Split('/');

            // Find the least common ancestor
            int lca = -1;
            for(int i = 0, n = System.Math.Min(fromDirs.Length, toDirs.Length); i < n; ++i) {
                if (fromDirs[i] != toDirs[i]) { break; }
                lca = i;
            }

            // Step up from the fromDir to the lca, then down from lca to the toDir.
            // If from = /a/b/c/d
            // and to  = /a/b/e/f/g
            // Then we need to go up 2 and down 3.
            var nStepsUp = (fromDirs.Length - 1) - lca;
            var nStepsDown = (toDirs.Length - 1) - lca;
            if (nStepsUp + nStepsDown == 0) {
                return ".";
            }

            var relDirs = new string[nStepsUp + nStepsDown];
            for(int i = 0; i < nStepsUp; ++i) {
                relDirs[i] = "..";
            }
            for(int i = 0; i < nStepsDown; ++i) {
                relDirs[nStepsUp + i] = toDirs[lca + 1 + i];
            }

            return string.Join("" + separator, relDirs);
        }

        /// <summary>
        /// Normalize a path, cleaning up path separators, resolving '.' and
        /// '..', removing duplicate and trailing path separators, etc.
        ///
        /// If the path passed in is a relative path, we remove leading path separators.
        /// If it's an absolute path we don't.
        ///
        /// If you claim the path is absolute but actually it's relative, we
        /// treat it as a relative path.
        /// </summary>
        public static string NormalizePath(string path, bool isRelative,
                char separator = '/')
        {
            // Use slashes to simplify the code (we're going to clobber them all anyway).
            path = path.Replace('\\', '/');

            // If we're supposed to be an absolute path, but we're actually a
            // relative path, ignore the 'isRelative' flag.
            if (!isRelative && !Path.IsPathRooted(path)) {
                isRelative = true;
            }

            // Build up a list of directory items.
            var dirs = path.Split('/');

            // Modify dirs in-place, reading from readIndex and remembering
            // what index we've written to.
            int lastWriteIndex = -1;
            for (int readIndex = 0, n = dirs.Length; readIndex < n; ++readIndex) {
                var dir = dirs[readIndex];

                // Skip duplicate path separators.
                if (dir == "") {
                    // Skip if it's not a leading path separator.
                   if (lastWriteIndex >= 0) {
                       continue; }

                   // Also skip if it's leading and we have a relative path.
                   if (isRelative) {
                       continue;
                   }
                }

                // Skip '.'
                if (dir == ".") {
                    continue;
                }

                // Erase the previous directory we read on '..'.
                // Exception: we can start with '..'
                // Exception: we can have multiple '..' in a row.
                //
                // Note: this ignores the actual file system and the funny
                // results you see when there are symlinks.
                if (dir == "..") {
                    if (lastWriteIndex == -1) {
                        // Leading '..' => handle like a normal directory.
                    } else if (dirs[lastWriteIndex] == "..") {
                        // Multiple ".." => handle like a normal directory.
                    } else {
                        // Usual case: delete the previous directory.
                        lastWriteIndex--;
                        continue;
                    }
                }

                // Copy anything else to the next index.
                ++lastWriteIndex;
                dirs[lastWriteIndex] = dirs[readIndex];
            }

            if (lastWriteIndex == -1 || (lastWriteIndex == 0 && dirs[lastWriteIndex] == "")) {
                // If we didn't keep anything, we have the empty path.
                // For an absolute path that's / ; for a relative path it's .
                if (isRelative) {
                    return ".";
                } else {
                    return "" + separator;
                }
            } else {
                // Otherwise print out the path with the proper separator.
                return String.Join("" + separator, dirs, 0, lastWriteIndex + 1);
            }
        }

        [MenuItem("Edit/Project Settings/Fbx Export", priority = 300)]
        static void ShowManager()
        {
            instance.name = "Fbx Export Settings";
            Selection.activeObject = instance;
            instance.Load();
        }

        public void Save()
        {
            instance.Save (true);
        }
    }

    public abstract class ScriptableSingleton<T> : ScriptableObject where T : ScriptableSingleton<T>
    {
        private static T s_Instance;
        public static T instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = ScriptableObject.CreateInstance<T>();
                    s_Instance.Load();
                }
                return s_Instance;
            }
        }

        protected ScriptableSingleton()
        {
            if (s_Instance != null)
            {
                Debug.LogError(typeof(T) + " already exists. Did you query the singleton in a constructor?");
            }
        }

        protected abstract void LoadDefaults();

        protected virtual void Load()
        {
            string filePath = GetFilePath();
            if (!System.IO.File.Exists(filePath)) {
                LoadDefaults();
            } else {
                try {
                    var fileData = System.IO.File.ReadAllText(filePath);
                    EditorJsonUtility.FromJsonOverwrite(fileData, s_Instance);
                } catch(Exception xcp) {
                    // Quash the exception and take the default settings.
                    Debug.LogException(xcp);
                    LoadDefaults();
                }
            }
        }

        protected virtual void Save(bool saveAsText)
        {
            if (s_Instance == null)
            {
                Debug.Log("Cannot save ScriptableSingleton: no instance!");
                return;
            }
            string filePath = GetFilePath();
            if (!string.IsNullOrEmpty(filePath))
            {
                string directoryName = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                }
                System.IO.File.WriteAllText(filePath, EditorJsonUtility.ToJson(s_Instance, true));
            }
        }

        private static string GetFilePath()
        {
            foreach(var attr in typeof(T).GetCustomAttributes(true)) {
                FilePathAttribute filePathAttribute = attr as FilePathAttribute;
                if (filePathAttribute != null)
                {
                    return filePathAttribute.filepath;
                }
            }
            return null;
        }
    }


    [AttributeUsage(AttributeTargets.Class)]
    public class FilePathAttribute : Attribute
    {
        public enum Location
        {
            PreferencesFolder,
            ProjectFolder
        }
        public string filepath
        {
            get;
            set;
        }
        public FilePathAttribute(string relativePath, FilePathAttribute.Location location)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                Debug.LogError("Invalid relative path! (its null or empty)");
                return;
            }
            if (relativePath[0] == '/')
            {
                relativePath = relativePath.Substring(1);
            }
            if (location == FilePathAttribute.Location.PreferencesFolder)
            {
                this.filepath = InternalEditorUtility.unityPreferencesFolder + "/" + relativePath;
            }
            else
            {
                this.filepath = relativePath;
            }
        }
    }

}
