using System;
using UnityEditor;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>Entry points for -executeMethod, so builds can run without anyone clicking through menus. See Tools/unity-batch.ps1.</summary>
    public static class CommandLineBuild
    {
        /// <summary>Same build as the menu item "CoolSchool/Build Playground Prototype". Exits 0 on success, 1 on failure.</summary>
        public static void BuildPlaygroundFromCommandLine()
        {
            int code = 1;
            try
            {
                if (PlaygroundPrototypeBuilder.RunBuild())
                {
                    AssetDatabase.SaveAssets();
                    Debug.Log("CoolSchool: command-line playground build finished.");
                    code = 0;
                }
                else
                {
                    Debug.LogError("CoolSchool: the playground build was cancelled.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("CoolSchool: the playground build failed: " + e);
            }
            EditorApplication.Exit(code);
        }
    }
}
