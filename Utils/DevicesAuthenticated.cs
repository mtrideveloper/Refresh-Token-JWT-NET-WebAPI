using System.Security.Cryptography;

namespace JwtAuthApi_Sonnet45.Utils;

public static class DevicesAuthenticated
{
    public readonly static List<string> UserAgentNames = 
    [
        "Chrome",
        "Firefox",
        "Edge",
        "Safari",
        "Opera",
        "Brave",
        "Vivaldi",
        "Internet Explorer",
        "Samsung Internet",
        "UC Browser",
    ];

    public readonly static List<string> OSNames =
    [
        "Windows",
        "macOS",
        "Linux",
        "Android",
        "iOS",
        "Chrome OS",
        "Ubuntu",
        "Fedora",
        "Debian",
        "Red Hat",
    ];
}