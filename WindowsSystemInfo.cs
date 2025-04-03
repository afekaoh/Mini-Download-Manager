using System.Text.RegularExpressions;

namespace Mini_Download_Manager;

using System;
using System.IO;
using System.Management;

public class WindowsSystemInfo
{
    public static double GetOsVersion()
    {
        var os = Environment.OSVersion;
        var major = os.Version.Major;
        var minor = os.Version.Minor;
        var build = os.Version.Build;
        return major switch
        {
            10 => build >= 22000 ? 11 : 10,
            6 => minor switch
            {
                3 => 8.1,
                2 => 8,
                1 => 7,
                0 => 6,
                _ => 0
            },
            _ => 0
        };
    }

    public static long GetTotalRam()
    {
        try
        {
            using var searcher =
                new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
            long totalRam = 0;
            foreach (var o in searcher.Get())
            {
                var wmi = (ManagementObject)o;
                totalRam += long.Parse(wmi["TotalVisibleMemorySize"].ToString() ?? string.Empty);
            }

            totalRam /= 1024; // Convert from KB to MB
            return totalRam;
        }
        catch
        {
            // ignored
        }
        return 0;
    }

    public static long GetAvailableDiskSpace(string driveName)
    {
        try
        {
            DriveInfo drive = new DriveInfo(driveName);
            if (drive.IsReady)
            {
                return drive.AvailableFreeSpace;
            }
            else
            {
                return -1; // Drive not ready
            }
        }
        catch (Exception)
        {
            return -1; // Error getting disk space
        }
    }
}