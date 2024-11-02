using System;
using System.Text.RegularExpressions;
using EvoS.Framework.Misc;

namespace EvoS.Framework.Network.Static;

public readonly struct BuildVersionInfo
{
    private static readonly Regex versionRegex = new(@"^([\w-]+-\d+-\d+)(?:_(\d)+.(\d)+(?:\.(\d+))?(?:-([\w-]+))?)?$");
    
    public string AtlasVersion { get; } = "UNKNOWN";
    public int Major { get; } = -1;
    public int Minor { get; } = -1;
    public int Patch { get; } = -1;
    public string Branch { get; } = "UNKNOWN";
            
    public bool IsPatched => Major > 0 || Minor > 0 || Patch > 0;

    public BuildVersionInfo(string versionString)
    {
        if (versionString is not null)
        {
            Match match = versionRegex.Match(versionString);
            if (match.Success)
            {
                AtlasVersion = match.Groups[1].Value;
                Major = V(match.Groups[2].Value);
                Minor = V(match.Groups[3].Value);
                Patch = V(match.Groups[4].Value);
                Branch = IsPatched ? match.Groups[5].Value : "vanilla";
            }
        }
    }

    private static int V(string s)
    {
        if (s.IsNullOrEmpty())
        {
            return 0;
        }

        if (!Int32.TryParse(s, out int result))
        {
            return -1;
        }

        return result;
    }
}