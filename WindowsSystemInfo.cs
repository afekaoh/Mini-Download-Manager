using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Mini_Download_Manager;

public class WindowsSystemInfo
{
    public static int GetOsVersion()
    {
        var osStr = RuntimeInformation.OSDescription;
        var regex = new Regex(@"Microsoft Windows (\d+).\d+.\d+");
        var match = regex.Match(osStr);
        if (match.Success) return int.Parse(match.Groups[1].Value);

        throw new Exception("Unable to determine OS version");
    }

    public static long GetTotalRam()
    {
        using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
        long totalRam = 0;
        foreach (var o in searcher.Get())
        {
            var wmi = (ManagementObject)o;
            totalRam += long.Parse(wmi["TotalVisibleMemorySize"].ToString() ?? string.Empty);
        }

        totalRam /= 1024; // Convert from KB to MB
        return totalRam;
    }

    public static long GetAvailableDiskSpace(string driveName)
    {
        try
        {
            var drive = new DriveInfo(driveName);
            if (drive.IsReady) return drive.AvailableFreeSpace;

            return -1; // Drive not ready
        }
        catch (Exception)
        {
            return -1; // Error getting disk space
        }
    }
}