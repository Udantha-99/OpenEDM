using System;
using System.Collections.Generic;

class Program
{
    static void Main()
    {
        string[] lines = {
            "[SourceDrives]",
            "H",
            "I",
            "J",
            "K",
            "L",
            "",
            "[OpenEDMPath]",
            @"A:\{USERNAME}",
            "",
            "[AuditLogPath]",
            @"A:\WIP_Admin_Logs",
            "",
            "[LockPath]",
            @"A:\WIP_Locks",
            "",
            "[OpenEDMCleanup]",
            "True"
        };
        
        string currentSection = null;
        List<string> drives = new List<string>();
        string openedmPath = "";
        string auditLogPath = "";
        string lockPath = "";
        bool openedmCleanup = false;

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                currentSection = line.Substring(1, line.Length - 2).Trim().ToUpperInvariant();
                continue;
            }

            switch (currentSection)
            {
                case "SOURCEDRIVES":
                    drives.Add(line);
                    break;
                case "OPENEDMPATH":
                    if (string.IsNullOrWhiteSpace(openedmPath)) openedmPath = line;
                    break;
                case "AUDITLOGPATH":
                    if (string.IsNullOrWhiteSpace(auditLogPath)) auditLogPath = line;
                    break;
                case "LOCKPATH":
                    if (string.IsNullOrWhiteSpace(lockPath)) lockPath = line;
                    break;
                case "OPENEDMCLEANUP":
                    if (bool.TryParse(line, out bool b)) openedmCleanup = b;
                    break;
            }
        }
        
        Console.WriteLine($"drives: {drives.Count}");
        Console.WriteLine($"openedmPath: {openedmPath}");
        Console.WriteLine($"auditLogPath: {auditLogPath}");
        Console.WriteLine($"lockPath: {lockPath}");
        Console.WriteLine($"openedmCleanup: {openedmCleanup}");
    }
}
