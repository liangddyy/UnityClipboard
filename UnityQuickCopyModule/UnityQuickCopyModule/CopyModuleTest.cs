using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace UnityQuickCopyModule
{
    /// <summary>
    /// 自动安装成Unity的模块
    /// </summary>
    public class CopyModuleTest
    {
        private static readonly string moduleOrDllName = "UnityQuickCopyModule";
        private static readonly string dllFileName = moduleOrDllName + ".dll";
        private static readonly string ivyFileName = "ivy.xml";

        private static readonly string ivyString =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><ivy-module version = \"2.0\" ><info version=\"{UnityVersion}\" organisation=\"Unity\" module=\"{ModuleName}\" e:packageType=\"UnityExtension\" e:unityVersion=\"{UnityVersion}\" xmlns:e=\"http://ant.apache.org/ivy/extra\" /><publications xmlns:e=\"http://ant.apache.org/ivy/extra\"><artifact name = \"Editor/{ModuleName}\" type=\"dll\" ext=\"dll\" e:guid=\"80a3616ca19596e4da0f10f14d241e9f\" /></publications></ivy-module>"
            ;

        public static string ExtensionsDir
        {
            get
            {
                return Path.Combine(EditorApplication.applicationContentsPath,
                    "UnityExtensions" + Path.DirectorySeparatorChar + "Unity" + Path.DirectorySeparatorChar);
            }
        }


        [MenuItem("Tools/安装QuickCopy模块", false, 111)]
        private static void InstallModule()
        {
            string moduleDir = Path.Combine(ExtensionsDir, moduleOrDllName);
            if (Directory.Exists(moduleDir) && File.Exists(Path.Combine(moduleDir, ivyFileName)))
            {
                EditorUtility.OpenWithDefaultApp(moduleDir);
                throw new ArgumentException("模块已安装，不必重复操作！");
            }
            var files = AssetDatabase.FindAssets(moduleOrDllName);
            if (!files.Any())
                throw new ArgumentException("操作中断，在当前项目中没找到需要的 Dll文件");
            string filePath = AssetDatabase.GUIDToAssetPath(files[0]);
            string dllFilePath = QuickCopy.AssetPath2FullPath(filePath);
            try
            {
                string modulePath = Path.Combine(ExtensionsDir, moduleOrDllName);
                if (!Directory.Exists(modulePath))
                    Directory.CreateDirectory(modulePath);
                string ivyFile = Path.Combine(modulePath, ivyFileName);
                string content = Regex.Replace(ivyString, "{UnityVersion}", Application.unityVersion);
                content = Regex.Replace(content, "{ModuleName}", moduleOrDllName);
                File.WriteAllText(ivyFile, content);
                string moduleEditorPath = Path.Combine(modulePath, "Editor");
                if (!Directory.Exists(moduleEditorPath))
                    Directory.CreateDirectory(moduleEditorPath);
                File.Copy(dllFilePath, Path.Combine(moduleEditorPath, dllFileName));
            }
            catch (Exception e)
            {
                Debug.Log(e.Message);
                throw new ArgumentException("安装失败，请使用管理员权限启动Unity后重试！");
            }
            Debug.Log("安装成功,下次启动Unity时生效！");
            EditorUtility.OpenWithDefaultApp(moduleDir);
        }
    }
}