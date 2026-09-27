using System;
using System.Threading.Tasks;
using VSerial.Core.Exceptions;
using VSerial.Core.Logging;
using VSerial.Core.Services;

namespace VSerial.Cli;

/// <summary>
/// VSerial Command-Line Interface entry point.
/// Responsible for CLI argument parsing and formatted presentation,
/// delegating all underlying domain operations to VSerial.Core.
/// </summary>
public static class Program
{
    private static readonly IVSerialLogger Logger = new ConsoleLogger();
    private static readonly IVSerialService SerialService = new VSerialService(Logger);
    private static readonly IDriverService DriverService = new DriverService(SerialService, Logger);

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (args.Length == 0)
        {
            ConsoleFormatter.PrintHelp();
            return 1;
        }

        string cmd = args[0].ToLowerInvariant();

        if (cmd is "-h" or "--help" or "/?" or "help")
        {
            ConsoleFormatter.PrintHelp();
            return 0;
        }

        if (cmd is "-v" or "--version" or "version")
        {
            ConsoleFormatter.PrintVersion();
            return 0;
        }

        try
        {
            return cmd switch
            {
                "pair" => await HandlePairCommandAsync(args),
                "list" => await HandleListAsync(),
                "status" => await HandleStatusAsync(),
                "create" => await HandleCreateShortcutAsync(args),
                "delete" => await HandleDeleteShortcutAsync(args),
                "driver" => await HandleDriverCommandAsync(args),
                _ => HandleUnknownCommand(cmd)
            };
        }
        catch (VSerialException vex)
        {
            ConsoleFormatter.Error(
                vex.Message,
                vex.ErrorCode == VSerialErrorCode.AccessDenied
                    ? "Administrator privilege is required. Please rerun the terminal as Administrator."
                    : null
            );
            return vex.ErrorCode == VSerialErrorCode.AccessDenied ? 5 : 1;
        }
        catch (Exception ex)
        {
            ConsoleFormatter.Error($"An unexpected internal error occurred: {ex.Message}", ex.StackTrace);
            return 99;
        }
    }

    private static async Task<int> HandlePairCommandAsync(string[] args)
    {
        if (args.Length < 2)
        {
            ConsoleFormatter.Warning("Missing pair subcommand (add, remove, list).");
            ConsoleFormatter.PrintHelp();
            return 1;
        }

        string subCmd = args[1].ToLowerInvariant();
        return subCmd switch
        {
            "add" => await HandlePairAddAsync(args),
            "remove" => await HandlePairRemoveAsync(args),
            "list" => await HandleListAsync(),
            _ => HandleUnknownSubCommand("pair", subCmd)
        };
    }

    private static async Task<int> HandlePairAddAsync(string[] args)
    {
        int pairId = 0;
        string portA = string.Empty;
        string portB = string.Empty;

        for (int i = 2; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg is "--id" or "-id" && i + 1 < args.Length)
            {
                int.TryParse(args[++i], out pairId);
            }
            else if (arg is "--portA" or "-portA" or "-a" && i + 1 < args.Length)
            {
                portA = args[++i];
            }
            else if (arg is "--portB" or "-portB" or "-b" && i + 1 < args.Length)
            {
                portB = args[++i];
            }
        }

        if (pairId <= 0 || string.IsNullOrEmpty(portA) || string.IsNullOrEmpty(portB))
        {
            ConsoleFormatter.Error("Missing required parameters for adding pair.");
            Console.WriteLine("    Usage:   vserial pair add --id <PairId> --portA <PortA> --portB <PortB>");
            Console.WriteLine("    Example: vserial pair add --id 1 --portA COM10 --portB COM11\n");
            return 1;
        }

        var pair = await SerialService.CreatePairAsync(pairId, portA, portB);
        ConsoleFormatter.Success($"Virtual COM port pair created and activated successfully!");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"    Pair ID : {pair.PairId}");
        Console.WriteLine($"    Port A  : {pair.PortA.PortName} (Device Role 0)");
        Console.WriteLine($"    Port B  : {pair.PortB.PortName} (Device Role 1)");
        Console.WriteLine("    Both ports are ready for full-duplex communication in your applications.\n");
        Console.ResetColor();
        return 0;
    }

    private static async Task<int> HandlePairRemoveAsync(string[] args)
    {
        bool removeAll = false;
        int pairId = 0;

        for (int i = 2; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg is "--all" or "-all")
            {
                removeAll = true;
            }
            else if (arg is "--id" or "-id" && i + 1 < args.Length)
            {
                int.TryParse(args[++i], out pairId);
            }
        }

        if (removeAll)
        {
            await SerialService.DeleteAllPairsAsync();
            ConsoleFormatter.Success("All VSerial virtual port pairs have been removed successfully from the system.\n");
            return 0;
        }

        if (pairId <= 0)
        {
            ConsoleFormatter.Error("Specify the pair ID to remove (--id <PairId>) or use --all.");
            Console.WriteLine("    Example: vserial pair remove --id 1");
            Console.WriteLine("             vserial pair remove --all\n");
            return 1;
        }

        await SerialService.DeletePairAsync(pairId);
        ConsoleFormatter.Success($"Virtual COM port pair ID {pairId} was removed successfully.\n");
        return 0;
    }

    private static async Task<int> HandleListAsync()
    {
        ConsoleFormatter.PrintBanner();
        var pairs = await SerialService.GetPairsAsync();
        ConsoleFormatter.PrintPairsTable(pairs);
        return 0;
    }

    private static async Task<int> HandleStatusAsync()
    {
        ConsoleFormatter.PrintBanner();
        var status = await DriverService.GetDriverStatusAsync();
        ConsoleFormatter.PrintDriverStatus(status);
        return 0;
    }

    private static async Task<int> HandleCreateShortcutAsync(string[] args)
    {
        // Support: vserial create COM10 COM11
        // Support: vserial create 1 COM10 COM11
        if (args.Length == 3)
        {
            var suggestion = await SerialService.SuggestNextPairAsync();
            int pairId = suggestion.nextPairId;
            string portA = args[1];
            string portB = args[2];
            var pair = await SerialService.CreatePairAsync(pairId, portA, portB);
            ConsoleFormatter.Success($"Allocated Pair ID {pair.PairId} and created: {pair.DisplayName}\n");
            return 0;
        }

        if (args.Length >= 4 && int.TryParse(args[1], out int customId))
        {
            string portA = args[2];
            string portB = args[3];
            var pair = await SerialService.CreatePairAsync(customId, portA, portB);
            ConsoleFormatter.Success($"Allocated Pair ID {pair.PairId} and created: {pair.DisplayName}\n");
            return 0;
        }

        ConsoleFormatter.Error("Command argument format does not match expected pattern.");
        Console.WriteLine("    Usage:   vserial create <PortA> <PortB>");
        Console.WriteLine("    Or:      vserial create <PairId> <PortA> <PortB>");
        Console.WriteLine("    Example: vserial create COM10 COM11\n");
        return 1;
    }

    private static async Task<int> HandleDeleteShortcutAsync(string[] args)
    {
        // Support: vserial delete 1
        // Support: vserial delete all
        if (args.Length < 2)
        {
            ConsoleFormatter.Error("Please specify a Pair ID to delete, or 'all'.");
            Console.WriteLine("    Example: vserial delete 1");
            Console.WriteLine("             vserial delete all\n");
            return 1;
        }

        if (string.Equals(args[1], "all", StringComparison.OrdinalIgnoreCase))
        {
            await SerialService.DeleteAllPairsAsync();
            ConsoleFormatter.Success("All VSerial virtual port pairs have been removed successfully from the system.\n");
            return 0;
        }

        if (int.TryParse(args[1], out int pairId))
        {
            await SerialService.DeletePairAsync(pairId);
            ConsoleFormatter.Success($"Virtual COM port pair ID {pairId} was removed successfully.\n");
            return 0;
        }

        ConsoleFormatter.Error($"Invalid Pair ID: '{args[1]}'. Must be an integer or 'all'.\n");
        return 1;
    }

    private static async Task<int> HandleDriverCommandAsync(string[] args)
    {
        if (args.Length < 2)
        {
            ConsoleFormatter.Error("Please specify a driver subcommand: install, uninstall, or status.");
            Console.WriteLine("    Example: vserial driver status\n");
            return 1;
        }

        string subCmd = args[1].ToLowerInvariant();
        string? infPath = null;

        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] is "--inf" or "-inf" && i + 1 < args.Length)
            {
                infPath = args[++i];
            }
        }

        switch (subCmd)
        {
            case "install":
                await DriverService.InstallDriverAsync(infPath);
                ConsoleFormatter.Success("Virtual serial driver package successfully installed and trusted in Windows DriverStore!\n");
                return 0;

            case "uninstall":
                await DriverService.UninstallDriverAsync(infPath);
                ConsoleFormatter.Success("Driver package and all associated virtual serial devices have been completely uninstalled.\n");
                return 0;

            case "status":
                return await HandleStatusAsync();

            default:
                return HandleUnknownSubCommand("driver", subCmd);
        }
    }

    private static int HandleUnknownCommand(string cmd)
    {
        ConsoleFormatter.Error($"Unknown command: '{cmd}'.");
        Console.WriteLine("    Run 'vserial --help' to inspect available commands.\n");
        return 1;
    }

    private static int HandleUnknownSubCommand(string parent, string subCmd)
    {
        ConsoleFormatter.Error($"Unknown '{parent}' subcommand: '{subCmd}'.");
        Console.WriteLine($"    Run 'vserial {parent} --help' or 'vserial --help' for syntax.\n");
        return 1;
    }
}
