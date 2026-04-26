using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class EnemyDebugFileLogger25D
{
    private const string LogDirectoryName = "EnemyLogs";
    private const string LogFilePrefix = "EnemyLog";

    private static bool isStarted;
    private static bool fileLoggingFailed;
    private static string currentLogFilePath = string.Empty;

    public static bool IsStarted => isStarted && !string.IsNullOrEmpty(currentLogFilePath);
    public static string CurrentLogFilePath => currentLogFilePath;

    public static void ResetForNewSession()
    {
        isStarted = false;
        fileLoggingFailed = false;
        currentLogFilePath = string.Empty;
    }

    public static void Write(string message)
    {
        Write("General", message, null);
    }

    public static void Write(string category, string message, Object context = null)
    {
        if (fileLoggingFailed)
            return;

        EnsureStarted();

        if (fileLoggingFailed || string.IsNullOrEmpty(currentLogFilePath))
            return;

        try
        {
            StringBuilder sb = new StringBuilder(256 + (message != null ? message.Length : 0));
            sb.AppendLine();
            sb.AppendLine("------------------------------------------------------------");
            sb.AppendLine($"Time: {Time.time:F3}");
            sb.AppendLine($"Frame: {Time.frameCount}");
            sb.AppendLine($"Category: {category}");

            if (context != null)
            {
                sb.AppendLine($"Context: {context.name}");
                sb.AppendLine($"ContextInstanceID: {context.GetInstanceID()}");
            }

            sb.AppendLine("------------------------------------------------------------");
            sb.AppendLine(message ?? string.Empty);

            File.AppendAllText(currentLogFilePath, sb.ToString(), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            fileLoggingFailed = true;
            Debug.LogWarning($"[EnemyDebugFileLogger25D] Failed to write enemy debug log. File logging will be disabled for this session. {ex.Message}");
        }
    }

    private static void EnsureStarted()
    {
        if (isStarted || fileLoggingFailed)
            return;

        try
        {
            string directoryPath = Path.Combine(Application.persistentDataPath, LogDirectoryName);
            Directory.CreateDirectory(directoryPath);

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            currentLogFilePath = Path.Combine(directoryPath, $"{LogFilePrefix}_{timestamp}.txt");

            string sceneName = SceneManager.GetActiveScene().name;

            StringBuilder header = new StringBuilder(512);
            header.AppendLine("============================================================");
            header.AppendLine("Enemy debug log started");
            header.AppendLine($"DateTime: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            header.AppendLine($"UnityTime: {Time.time:F3}");
            header.AppendLine($"Frame: {Time.frameCount}");
            header.AppendLine($"Scene: {sceneName}");
            header.AppendLine($"PersistentDataPath: {Application.persistentDataPath}");
            header.AppendLine($"LogFilePath: {currentLogFilePath}");
            header.AppendLine("============================================================");
            header.AppendLine();

            File.WriteAllText(currentLogFilePath, header.ToString(), Encoding.UTF8);
            isStarted = true;
        }
        catch (Exception ex)
        {
            fileLoggingFailed = true;
            currentLogFilePath = string.Empty;
            Debug.LogWarning($"[EnemyDebugFileLogger25D] Failed to start enemy debug file logging. {ex.Message}");
        }
    }
}
