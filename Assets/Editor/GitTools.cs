using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

/// Tools > Git Commit All: 変更を全ステージして規定メッセージでコミットする。
/// メッセージは EditorPrefs "GitTools.Message" があればそれを使用（コミット後クリア）。
public static class GitTools
{
    [MenuItem("Tools/Git Commit All")]
    public static void CommitAll()
    {
        string root = Directory.GetCurrentDirectory();
        string lockFile = Path.Combine(root, ".git", "index.lock");
        if (File.Exists(lockFile)) File.Delete(lockFile);

        string msg = EditorPrefs.GetString("GitTools.Message", "");
        if (string.IsNullOrEmpty(msg)) msg = "WIP: 定期コミット";
        EditorPrefs.DeleteKey("GitTools.Message");

        UnityEngine.Debug.Log("[GitTools] add: " + Run(root, "add -A"));
        UnityEngine.Debug.Log("[GitTools] commit: " + Run(root,
            $"-c user.name=Radian -c user.email=snine9801@gmail.com commit -m \"{msg}\""));
        UnityEngine.Debug.Log("[GitTools] log: " + Run(root, "log --oneline -3"));
    }

    static string Run(string cwd, string args)
    {
        var psi = new ProcessStartInfo("git", args)
        {
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };
        using (var p = Process.Start(psi))
        {
            string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit();
            return $"[exit {p.ExitCode}] {o}";
        }
    }
}
