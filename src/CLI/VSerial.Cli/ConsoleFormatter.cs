using System;
using System.Collections.Generic;
using VSerial.Core.Models;

namespace VSerial.Cli;

/// <summary>
/// Modern console formatting and styled output helper for VSerial CLI
/// </summary>
public static class ConsoleFormatter
{
    private const string Version = "2.0.0";

    public static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("========================================================================");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("  VSerial CLI ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"v{Version}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(" - Virtual COM Port Architecture for Windows");
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("  Powered by UMDF 2.0 User-Mode Driver Framework (x64)");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("========================================================================\n");
        Console.ResetColor();
    }

    public static void PrintVersion()
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("vserial version ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(Version);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(" (win-x64, UMDF 2.0, .NET 8.0)");
        Console.ResetColor();
    }

    public static void PrintHelp()
    {
        PrintBanner();

        WriteSection("USAGE");
        WriteUsage("vserial <command> [subcommand] [arguments] [options]\n");

        WriteSection("PORT PAIR COMMANDS");
        WriteCommand("create <portA> <portB>", "Quickly create a virtual COM port pair (auto ID)");
        WriteCommand("create <id> <portA> <portB>", "Create a pair with specified Pair ID");
        WriteCommand("delete <id>", "Safely remove the specified virtual COM port pair");
        WriteCommand("delete all", "Remove all virtual COM port pairs from system");
        WriteCommand("list", "Display all currently installed virtual COM port pairs");
        WriteCommand("pair add", "Add pair with flags: --id <id> -a <portA> -b <portB>");
        WriteCommand("pair remove", "Remove pair: --id <id> or --all");
        WriteCommand("pair list", "Alias for 'list'\n");

        WriteSection("DRIVER COMMANDS");
        WriteCommand("status", "Show driver installation, service status and active pairs");
        WriteCommand("driver install", "Install driver package (.inf) and trust security certificate");
        WriteCommand("driver uninstall", "Uninstall driver package and clean up all device nodes");
        WriteCommand("driver status", "Alias for 'status'\n");

        WriteSection("GLOBAL OPTIONS");
        WriteCommand("-h, --help", "Show help and usage documentation");
        WriteCommand("-v, --version", "Display application version information\n");

        WriteSection("EXAMPLES");
        WriteExample("vserial create COM10 COM11", "Create COM10 <-> COM11 with auto-assigned ID");
        WriteExample("vserial create 2 COM20 COM21", "Create Pair 2 explicitly with COM20 and COM21");
        WriteExample("vserial list", "Inspect status of all virtual port pairs");
        WriteExample("vserial delete 1", "Delete virtual COM port pair ID 1");
        WriteExample("vserial delete all", "Remove all virtual port pairs");
        WriteExample("vserial driver status", "Inspect UMDF kernel driver operational state");
        Console.WriteLine();
    }

    public static void PrintPairsTable(IReadOnlyList<VirtualPortPair> pairs)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("Virtual Serial Port Pairs:");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("┌──────────┬──────────────────┬──────────────────┬──────────────────────────────┐");
        Console.Write("│ ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"{"PAIR ID",-8}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(" │ ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"{"PORT A",-16}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(" │ ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"{"PORT B",-16}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(" │ ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"{"STATUS",-28}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(" │");
        Console.WriteLine("├──────────┼──────────────────┼──────────────────┼──────────────────────────────┤");

        if (pairs.Count == 0)
        {
            Console.Write("│ ");
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write($"{"  No virtual serial port pairs found in the system.",-76}");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(" │");
        }
        else
        {
            foreach (var p in pairs)
            {
                string portAStr = p.HasPortA ? p.PortA.PortName : "[Unassigned]";
                string portBStr = p.HasPortB ? p.PortB.PortName : "[Unassigned]";

                Console.Write("│ ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"{p.PairId,-8}");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(" │ ");

                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"{portAStr,-16}");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(" │ ");

                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"{portBStr,-16}");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(" │ ");

                if (p.Status == PairStatus.Normal)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write($"{"● Ready (Normal)",-28}");
                }
                else if (p.Status == PairStatus.DeviceProblem)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write($"{$"✖ Fault (Problem)",-28}");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write($"{"▲ Incomplete",-28}");
                }

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine(" │");
            }
        }

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("└──────────┴──────────────────┴──────────────────┴──────────────────────────────┘");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"Total: {pairs.Count} pair(s) loaded.\n");
        Console.ResetColor();
    }

    public static void PrintDriverStatus(DriverStatusInfo info)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("VSerial Driver & Hardware Subsystem Status:");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("  ├─ ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"{"Driver Package",-16}: ");
        if (info.IsInstalled)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Installed (Registered in DriverStore)");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Not Installed (Driver package not found)");
        }

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("  ├─ ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"{"Service State",-16}: ");
        if (info.OperationalStatus == DriverOperationalStatus.Running)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Running (Active)");
        }
        else if (info.OperationalStatus == DriverOperationalStatus.InstalledIdle)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Idle (Installed, no active devices)");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Not Running");
        }

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("  ├─ ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"{"INF Location",-16}: ");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(string.IsNullOrEmpty(info.InfPath) ? "[Not located]" : info.InfPath);

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("  ├─ ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"{"Active Pairs",-16}: ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"{info.ActivePairsCount} port pair(s) bound");

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("  └─ ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"{"Diagnostics",-16}: ");
        if (info.OperationalStatus == DriverOperationalStatus.Running)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Driver loaded and operating normally ({info.ActivePairsCount} active pair(s) loaded)");
        }
        else if (info.OperationalStatus == DriverOperationalStatus.InstalledIdle)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(info.ActivePairsCount > 0
                ? $"Detected {info.ActivePairsCount} configured pair(s), awaiting hardware initialization"
                : "Driver package ready; no virtual port pairs created yet");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Driver package is not installed or not registered in DriverStore");
        }

        Console.ResetColor();
        Console.WriteLine();
    }

    public static void Success(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(" ✔ ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void Info(string message)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write(" ℹ ");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void Warning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write(" ▲ ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void Error(string message, string? detail = null)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write(" ✖ Error: ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(message);
        if (!string.IsNullOrEmpty(detail))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"   {detail}");
        }
        Console.ResetColor();
    }

    private static void WriteSection(string title)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(title + ":");
        Console.ResetColor();
    }

    private static void WriteUsage(string usage)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write("  $ ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(usage);
        Console.ResetColor();
    }

    private static void WriteCommand(string cmd, string desc)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"  {cmd,-30}");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine(desc);
        Console.ResetColor();
    }

    private static void WriteExample(string ex, string desc)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("  # ");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(desc);

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write("  $ ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(ex);
        Console.ResetColor();
    }
}
