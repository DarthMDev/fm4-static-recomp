// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
using System.Text;

namespace Fm4StaticRecomp.Launcher.Core.Platform;

public interface IPlatform
{
    bool SupportsDxgiPresentation { get; }
    string ExecutableName(string baseName);
    void CreateDirectoryLink(string link, string target);
    bool IsDirectoryLink(string path);
    Encoding ConsoleEncoding { get; }
}

public static class Platforms
{
    public static IPlatform Current { get; } =
        OperatingSystem.IsWindows() ? new WindowsPlatform() : new LinuxPlatform();
}
