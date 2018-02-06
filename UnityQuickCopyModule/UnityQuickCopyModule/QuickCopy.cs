using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using UnityEditor;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace UnityQuickCopyModule
{
    public class QuickCopy
    {
        #region 菜单

        [MenuItem("Assets/粘贴", false, 21)]
        private static void Paste()
        {
            ClipItem item;
            try
            {
                byte[] bytes = Convert.FromBase64String(GUIUtility.systemCopyBuffer);
                BinaryFormatter formatter = new BinaryFormatter();
                using (MemoryStream stream = new MemoryStream(bytes))
                {
                    item = formatter.Deserialize(stream) as ClipItem;
                }
            }
            catch (FormatException e)
            {
                throw new FormatException("没有从剪切板解析到有效的数据!");
            }
            if (item == null) throw new ArgumentException("没有从剪切板解析到有效的数据!");
            string assetPath = AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]);
            switch (item.Type)
            {
                case ContentType.File:
                    CopyListFileInEditor(item.Values, AssetPath2FullPath(assetPath));
                    break;
                case ContentType.Package:
                    if (item.Values.Count > 0 && Path.GetExtension(item.Values[0]).Equals(".unitypackage"))
                        Package2Folder.ImportPackageToFolder(item.Values[0], assetPath, true);
                    break;
                default:
                    break;
            }
        }

        [MenuItem("Assets/粘贴", true, 21)]
        private static bool PasteValidate()
        {
            if (string.IsNullOrEmpty(GUIUtility.systemCopyBuffer))
                return false;
            if (Selection.assetGUIDs.Length != 1)
                return false;
            if (!AssetDatabase.IsValidFolder(AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0])))
                return false;
            return true;
        }

        [MenuItem("Assets/复制 - 编辑器复制", false, 21)]
        private static void CopyToEditor()
        {
            ClipItem item = new ClipItem(ContentType.File);
            foreach (var guiD in Selection.assetGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guiD);
                item.Values.Add(AssetPath2FullPath(path));
            }
            CopyClipboardItem(item);
            Debug.Log("已复制" + Selection.assetGUIDs.Length + "条数据，可在其他 Unity 编辑器里粘贴！");
        }

        [MenuItem("Assets/复制 - 编辑器复制", true, 21)]
        private static bool CopyToEditorValidate()
        {
            return Selection.assetGUIDs.Length > 0;
        }


        [MenuItem("Assets/复制 - 剪切板复制", false, 21)]
        private static void CopyToClipboard()
        {
            StringBuilder stringBuilder = new StringBuilder("Set-Clipboard -Path ");
            for (int i = 0; i < Selection.assetGUIDs.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[i]);
                if (i != 0) stringBuilder.Append(",");
                stringBuilder.Append("\"");
                stringBuilder.Append(AssetPath2FullPath(path));
                stringBuilder.Append("\"");
            }
            if (RunCommand(stringBuilder.Replace("/", "\\").ToString()))
                Debug.Log("已复制文件列表到 剪切板！！！");
            else
                Debug.LogError("复制出错!");
        }

        [MenuItem("Assets/复制 - 剪切板复制", true, 21)]
        private static bool CopyToClipboardValidate()
        {
            return Selection.assetGUIDs.Length > 0;
        }

        [MenuItem("Assets/复制 - 导出包复制", false, 21)]
        private static void CopyAsPackage()
        {
            string[] assetPaths = new string[Selection.assetGUIDs.Length];
            for (int i = 0; i < Selection.assetGUIDs.Length; i++)
            {
                assetPaths[i] = AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[i]);
            }
            string outPath = Path.Combine(Application.temporaryCachePath,
                Random.Range(0, 1024) + ".unitypackage");
            AssetDatabase.ExportPackage(assetPaths, outPath,
                ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies);
            ClipItem item = new ClipItem(ContentType.Package);
            item.Values.Add(outPath);
            CopyClipboardItem(item);
            Debug.Log("已导出选中资源!可直接粘贴至任意Assets目录!");
        }
        [MenuItem("Assets/复制 - 导出包复制", true, 21)]
        private static bool CopyAsPackageValidate()
        {
            return Selection.assetGUIDs.Length > 0;
        }

        #endregion

        private static readonly string KeyAutoRefresh = "kAutoRefresh"; // 请勿修改

        private static string ApplicationDataPath
        {
            get { return new DirectoryInfo(Application.dataPath).Parent.FullName; }
        }

        public static string AssetPath2FullPath(string assetPath)
        {
            return Path.Combine(ApplicationDataPath, assetPath);
        }

        public static void CopyClipboardItem(ClipItem item)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            using (MemoryStream stream = new MemoryStream())
            {
                formatter.Serialize(stream, item);
                TextEditor te = new TextEditor {text = Convert.ToBase64String(stream.ToArray())};
                te.OnFocus();
                te.Copy();
            }
        }

        /// <summary>
        ///     编辑器里复制文件列表
        /// </summary>
        /// <param name="sourcePaths"></param>
        /// <param name="targetPath"></param>
        public static void CopyListFileInEditor(List<string> sourcePaths, string targetPath)
        {
            bool isAuto = EditorPrefs.GetBool(KeyAutoRefresh, true);
            if (isAuto) EditorPrefs.SetBool(KeyAutoRefresh, false);
            foreach (var path in sourcePaths)
            {
                string destName = Path.Combine(targetPath, Path.GetFileName(path));
                if (File.Exists(path))
                {
                    File.Copy(path, destName);
                }
                else
                {
                    CopyDir(path, destName);
                }
            }
            if (isAuto) EditorPrefs.SetBool(KeyAutoRefresh, true);
            AssetDatabase.Refresh();
        }

        public static void CopyDir(string sourcePath, string destinationPath)
        {
            DirectoryInfo info = new DirectoryInfo(sourcePath);
            if (!Directory.Exists(destinationPath))
                Directory.CreateDirectory(destinationPath);
            foreach (FileSystemInfo fsi in info.GetFileSystemInfos())
            {
                string destName = Path.Combine(destinationPath, fsi.Name);

                if (fsi is FileInfo)
                {
                    File.Copy(fsi.FullName, destName);
                }
                else
                {
                    Directory.CreateDirectory(destName);
                    CopyDir(fsi.FullName, destName);
                }
            }
        }

        /// <summary>
        ///     使用powerShell，换成bash ?
        ///     cmd.exe不可用
        ///     可使用多条命令
        /// </summary>
        /// <param name="command"></param>
        public static bool RunCommand(string command)
        {
            using (Process process = new Process())
            {
                process.StartInfo.FileName = "powershell";
                process.StartInfo.Arguments = command;

                process.StartInfo.CreateNoWindow = true; // 是否要查看powershell窗口执行过程
                process.StartInfo.ErrorDialog = true; // 该值指示不能启动进程时是否向用户显示错误对话框  
                process.StartInfo.UseShellExecute = false;
                // 默认设置
                //            process.StartInfo.RedirectStandardError = true;  
                //process.StartInfo.RedirectStandardInput = true;  
                //process.StartInfo.RedirectStandardOutput = true;  

                try
                {
                    process.Start();
                    process.WaitForExit();
                    process.Close();
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    return false;
                }
            }
            return true;
        }
    }

    [Serializable]
    public class ClipItem
    {
        private ContentType type;
        private List<String> values;

        public ContentType Type
        {
            get { return type; }

            set { type = value; }
        }

        public List<string> Values
        {
            get { return values; }

            set { values = value; }
        }

        public ClipItem()
        {
            values = new List<string>();
        }

        public ClipItem(ContentType type)
        {
            this.type = type;
            values = new List<string>();
        }
    }

    [Serializable]
    public enum ContentType
    {
        File,
        Package
    }

    public class Package2Folder
    {
        #region reflection stuff

#if !UNITY_5_3_OR_NEWER
        private delegate AssetsItem[] ImportPackageStep1Delegate(string packagePath, out string packageIconPath);

        private static Type assetServerType;

        private static Type AssetServerType
        {
            get
            {
                if (assetServerType == null)
                {
                    assetServerType = typeof(MenuItem).Assembly.GetType("UnityEditor.AssetServer");
                }

                return assetServerType;
            }
        }

        private static ImportPackageStep1Delegate importPackageStep1;

        private static ImportPackageStep1Delegate ImportPackageStep1
        {
            get
            {
                if (importPackageStep1 == null)
                {
                    importPackageStep1 = (ImportPackageStep1Delegate) Delegate.CreateDelegate(
                        typeof(ImportPackageStep1Delegate),
                        null,
                        AssetServerType.GetMethod("ImportPackageStep1"));
                }

                return importPackageStep1;
            }
        }

        private static MethodInfo importPackageStep2MethodInfo;
        private static MethodInfo ImportPackageStep2MethodInfo
        {
            get
            {
                if (importPackageStep2MethodInfo == null)
                {
                    importPackageStep2MethodInfo = AssetServerType.GetMethod("ImportPackageStep2");
                }

                return importPackageStep2MethodInfo;
            }
        }
#else
        private delegate object[] ExtractAndPrepareAssetListDelegate(string packagePath, out string packageIconPath,
            out bool allowReInstall);

        private static Type packageUtilityType;

        private static Type PackageUtilityType
        {
            get
            {
                if (packageUtilityType == null)
                {
                    packageUtilityType
= typeof(MenuItem).Assembly.GetType("UnityEditor.PackageUtility");
                }
                return packageUtilityType;
            }
        }

        private static ExtractAndPrepareAssetListDelegate extractAndPrepareAssetList;

        private static ExtractAndPrepareAssetListDelegate ExtractAndPrepareAssetList
        {
            get
            {
                if (extractAndPrepareAssetList == null)
                {
                    extractAndPrepareAssetList
= (ExtractAndPrepareAssetListDelegate) Delegate.CreateDelegate(
                        typeof(ExtractAndPrepareAssetListDelegate),
                        null,
                        PackageUtilityType.GetMethod("ExtractAndPrepareAssetList"));
                }

                return extractAndPrepareAssetList;
            }
        }

        private static FieldInfo destinationAssetPathFieldInfo;

        private static FieldInfo DestinationAssetPathFieldInfo
        {
            get
            {
                if (destinationAssetPathFieldInfo == null)
                {
                    Type importPackageItem
= typeof(MenuItem).Assembly.GetType("UnityEditor.ImportPackageItem");
                    destinationAssetPathFieldInfo
= importPackageItem.GetField("destinationAssetPath");
                }
                return destinationAssetPathFieldInfo;
            }
        }

        private static MethodInfo importPackageAssetsMethodInfo;
        private static MethodInfo ImportPackageAssetsMethodInfo
        {
            get
            {
                if (importPackageAssetsMethodInfo == null)
                {
                    // ImportPackageAssetsImmediately 是同步的导入5.4以上版本可用
                    importPackageAssetsMethodInfo
= PackageUtilityType.GetMethod("ImportPackageAssetsImmediately") ?? PackageUtilityType.GetMethod("ImportPackageAssets");
                }

                return importPackageAssetsMethodInfo;
            }
        }
#endif

        private static MethodInfo showImportPackageMethodInfo;

        private static MethodInfo ShowImportPackageMethodInfo
        {
            get
            {
                if (showImportPackageMethodInfo == null)
                {
                    Type packageImport = typeof(MenuItem).Assembly.GetType("UnityEditor.PackageImport");
                    showImportPackageMethodInfo = packageImport.GetMethod("ShowImportPackage");
                }

                return showImportPackageMethodInfo;
            }
        }

        #endregion reflection stuff

        public static void ImportPackageToFolder(string packagePath, string selectedFolderPath, bool interactive)
        {
            string packageIconPath;
            bool allowReInstall;

            object[] assetsItems = ExtractAssetsFromPackage(packagePath, out packageIconPath, out allowReInstall);

            if (assetsItems == null) return;

            foreach (object item in assetsItems)
            {
                ChangeAssetItemPath(item, selectedFolderPath);
            }

            if (interactive)
            {
                ShowImportPackageWindow(packagePath, assetsItems, packageIconPath, allowReInstall);
            }
            else
            {
                ImportPackageSilently(assetsItems);
            }
        }

        public static object[] ExtractAssetsFromPackage(string path, out string packageIconPath,
            out bool allowReInstall)
        {
#if !UNITY_5_3_OR_NEWER
            AssetsItem[] array = ImportPackageStep1(path, out packageIconPath);
            allowReInstall = false;
            return array;
#else
            object[] array
= ExtractAndPrepareAssetList(path, out packageIconPath, out allowReInstall);
            return array;
#endif
        }

        private static void ChangeAssetItemPath(object assetItem, string selectedFolderPath)
        {
#if !UNITY_5_3_OR_NEWER
            AssetsItem item = (AssetsItem) assetItem;
            item.exportedAssetPath = selectedFolderPath + item.exportedAssetPath.Remove(0, 6);
            item.pathName = selectedFolderPath + item.pathName.Remove(0, 6);
#else
            string destinationPath
= (string) DestinationAssetPathFieldInfo.GetValue(assetItem);
            destinationPath
= selectedFolderPath + destinationPath.Remove(0, 6);
            DestinationAssetPathFieldInfo.SetValue(assetItem, destinationPath);
#endif
        }

        public static void ShowImportPackageWindow(string path, object[] array, string packageIconPath,
            bool allowReInstall)
        {
#if !UNITY_5_3_OR_NEWER
            ShowImportPackageMethodInfo.Invoke(null, new object[] {path, array, packageIconPath});
#else
            ShowImportPackageMethodInfo.Invoke(null, new object[] {path, array, packageIconPath, allowReInstall});
#endif
        }

        public static void ImportPackageSilently(object[] assetsItems)
        {
#if !UNITY_5_3_OR_NEWER
            ImportPackageStep2MethodInfo.Invoke(null, new object[] {assetsItems, false});
#else
            ImportPackageAssetsMethodInfo.Invoke(null, new object[] {assetsItems, false});
#endif
        }

        private static string GetSelectedFolderPath()
        {
            UnityEngine.Object obj = Selection.activeObject;
            if (obj == null) return null;

            string path = AssetDatabase.GetAssetPath(obj.GetInstanceID());
            return !Directory.Exists(path) ? null : path;
        }
    }
}