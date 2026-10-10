using System.Runtime.CompilerServices;
using ZdoRpgAi.Core;

namespace ZdoRpgAi.Tests.Support;

public static class TestSetup {
    // Only errors reach the test output
    [ModuleInitializer]
    public static void Init() => Logger.Configure(new LogConfig { ConsoleLevel = LogLevel.Error });

    // The folder with ZdoRpgAi.sln, found upwards from the test binaries
    public static string RepoRoot {
        get {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ZdoRpgAi.sln"))) {
                dir = dir.Parent;
            }
            return dir?.FullName ?? throw new InvalidOperationException("ZdoRpgAi.sln not found above " + AppContext.BaseDirectory);
        }
    }
}
