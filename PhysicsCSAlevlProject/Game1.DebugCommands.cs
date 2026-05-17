using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text.RegularExpressions;
using ImGuiNET;
using Raylib_cs;

namespace PhysicsCSAlevlProject;

/// <summary>
/// Attribute to mark methods as console commands. The CommandPath property specifies the command path that will be used to invoke the method from the debug console. For example, a method marked with [ConsoleCommand("Mesh.AddTire")] can be invoked by entering "Mesh.AddTire" in the debug console, along with any required parameters. This attribute allows for easy registration and organization of debug commands within the application.
/// </summary>
class ConsoleCommandAttribute : Attribute
{
    public string CommandPath { get; }

    public ConsoleCommandAttribute(string commandPath)
    {
        CommandPath = commandPath;
    }
}

/// <summary>
/// Registry for console commands that can be invoked from the debug console. This class allows for registering methods as console commands using the ConsoleCommandAttribute, and provides functionality to invoke these commands based on user input. The commands are stored in a dictionary for efficient lookup, and the registry supports invoking commands with parameters as well as listing all registered commands. This system enables a flexible and extensible way to add debug functionality to the application without hardcoding command handling logic in the main game loop.
/// </summary>
class CommandRegistry
{
    private readonly ImGuiLogger _logger;
    private readonly Dictionary<
        string,
        (object instance, MethodInfo method, string commandPath)
    > _commands = new();

    public CommandRegistry(ImGuiLogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Registers all methods of the given instance that are marked with the ConsoleCommandAttribute as console commands. The method uses reflection to find all methods in the specified type that have the ConsoleCommandAttribute, and adds them to the _commands dictionary using the command path as the key. This allows for dynamic registration of commands based on the attributes applied to methods, making it easy to add new commands by simply marking methods with the appropriate attribute. The logRegistration parameter can be set to false to suppress logging of each registered command, which can be useful when registering a large number of commands or when registering commands in a context where logging is not desired.
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="type"></param>
    /// <param name="logRegistration"></param>
    public void RegisterType(object instance, Type type, bool logRegistration = true)
    {
        var methods = type.GetMethods(
            BindingFlags.Public
                | BindingFlags.Instance
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly
        );

        foreach (var method in methods)
        {
            var attr = method.GetCustomAttribute<ConsoleCommandAttribute>();
            if (attr != null)
            {
                string commandKey = attr.CommandPath.ToLower();
                _commands[commandKey] = (instance, method, attr.CommandPath);
                if (logRegistration)
                {
                    _logger.AddLog($"Registered command: {attr.CommandPath}");
                }
            }
        }
    }

    /// <summary>
    /// Returns a list of all registered command paths in the registry. This method extracts the command paths from the _commands dictionary, ensuring that they are distinct and sorted alphabetically for easier readability. The returned list can be used to display available commands to the user or for debugging purposes to verify which commands have been registered in the system.
    /// </summary>
    /// <returns></returns>
    public IEnumerable<string> GetRegisteredCommandPaths()
    {
        return _commands
            .Select(entry => entry.Value.commandPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(commandPath => commandPath, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Attempts to invoke a console command based on the provided command information. The method constructs the full command key from the command path and command name, and looks it up in the _commands dictionary. If a matching command is found, it invokes the associated method with the provided parameters. If the invocation is successful, it returns true. If an error occurs during invocation, it logs the error message and also returns true to indicate that the command was recognized but failed to execute. If no matching command is found, it returns false to indicate that the command is unknown. This method allows for dynamic execution of commands based on user input from the debug console, providing a flexible way to interact with the application for debugging and testing purposes.
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    public bool TryInvokeCommand(ImGuiLogger.Command command)
    {
        string fullPath = string.Join(".", command.CommandPath) + "." + command.ExCommand;
        string key = fullPath.ToLower();

        if (_commands.TryGetValue(key, out var entry))
        {
            try
            {
                var (instance, method, _) = entry;
                object[] parameters = [command.Parameters];
                method.Invoke(instance, parameters);
                return true;
            }
            catch (Exception ex)
            {
                _logger.AddLog(
                    $"Error invoking command {fullPath}: {ex.InnerException?.Message ?? ex.Message}",
                    ImGuiLogger.LogTypes.Error
                );
                return true;
            }
        }

        return false;
    }
}

public partial class Game1
{
    /// <summary>
    /// The CommandRegistry is used to store and manage console commands
    /// </summary>
    private CommandRegistry _commandRegistry;

    /// <summary>
    /// The mesh that is currently bound to the command registry
    /// </summary>
    private Mesh _commandRegistryMeshBinding;

    /// <summary>
    /// Refreshes the command bindings for the active mesh in the command registry. This method checks if the command registry and active mesh are available, and if the active mesh has changed since the last binding, it registers the new active mesh with the command registry. This allows console commands that operate on the mesh to always reference the current active mesh without needing to specify it as a parameter in each command. By keeping track of the last bound mesh, this method avoids unnecessary re-registrations when the active mesh has not changed, improving efficiency while ensuring that commands remain up-to-date with the current state of the application.
    /// </summary>
    private void RefreshMeshCommandBindings()
    {
        if (_commandRegistry == null || _activeMesh == null)
        {
            return;
        }

        if (!ReferenceEquals(_commandRegistryMeshBinding, _activeMesh))
        {
            _commandRegistry.RegisterType(_activeMesh, typeof(Mesh), false);
            _commandRegistryMeshBinding = _activeMesh;
        }
    }

    /// <summary>
    /// Processes pending debug commands from the ImGuiLogger's command queue. This method checks if there are any commands waiting to be executed, and if so, it dequeues each command and attempts to invoke it using the CommandRegistry. If a command is recognized and successfully invoked, it executes the associated method. If a command is recognized but fails to execute due to an error, it logs the error message. If a command is not recognized, it logs a warning indicating that the command is unknown. This method allows for dynamic execution of debug commands entered by the user in the debug console, providing a powerful tool for testing and debugging the application without needing to modify code or restart the application.
    /// </summary>
    private void ProcessDebugCommands()
    {
        RefreshMeshCommandBindings();

        if (_logger.HasPendingCommands)
        {
            _logger.TryDequeueCommand(out var command);

            if (command.CommandPath[0] == "Exit")
            {
                Raylib.CloseWindow();
                return;
            }

            if (!_commandRegistry.TryInvokeCommand(command))
            {
                _logger.AddLog(
                    $"Unknown command: {string.Join(".", command.CommandPath)}.{command.ExCommand}",
                    ImGuiLogger.LogTypes.Error
                );
            }
        }
    }

    /// <summary>
    /// Lists all registered console commands in the ImGuiLogger. This method retrieves the list of registered command paths from the CommandRegistry and logs them to the ImGuiLogger for display in the debug console. If the command registry is not ready, it logs a warning message instead. This command can be invoked from the debug console to provide users with a reference of available commands they can use for debugging and testing purposes. The method also checks if any parameters were provided when invoking the command, and if so, it logs a warning that this command does not take parameters, helping to guide users on correct usage.
    /// </summary>
    /// <param name="parameters"></param>
    [ConsoleCommand("Commands.List")]
    private void ListCommands(string[] parameters)
    {
        if (_commandRegistry == null)
        {
            _logger.AddLog("Command registry is not ready yet.", ImGuiLogger.LogTypes.Warning);
            return;
        }

        var commands = _commandRegistry.GetRegisteredCommandPaths().ToList();
        _logger.AddLog($"Available commands ({commands.Count}):", ImGuiLogger.LogTypes.Info);

        foreach (var command in commands)
        {
            _logger.AddLog($"- {command}", ImGuiLogger.LogTypes.Info);
        }

        if (parameters.Length > 0)
        {
            _logger.AddLog("Commands.List does not take parameters.", ImGuiLogger.LogTypes.Warning);
        }
    }

    /// <summary>
    /// Adds a tire-shaped structure to the active mesh based on the provided parameters. The command expects five parameters: centerX, centerY, OuterStickCount, radius, and spokeLength. It creates a hub-and-spoke tire structure by adding particles and sticks to the active mesh according to the specified parameters. If the parameters are invalid or cannot be parsed correctly, it logs an error message indicating the issue. This command allows users to quickly create complex structures in the mesh for testing and debugging purposes using simple console commands.
    /// </summary>
    /// <param name="parameters"></param>
    [ConsoleCommand("Mesh.AddTire")]
    private void AddTire(string[] parameters)
    {
        if (parameters.Length < 5)
        {
            _logger.AddLog(
                $"Not enough parameters for Mesh.AddTire(centerX, centerY, OuterStickCount, radius, spokeLength) Expected 5, got {parameters.Length}",
                ImGuiLogger.LogTypes.Error
            );
            return;
        }

        try
        {
            _activeMesh.CreateHubSpokeTire(
                [parameters[0], parameters[1], parameters[2], parameters[3], parameters[4]]
            );

            _logger.AddLog(
                $"Added tire to active mesh at ({parameters[0]}, {parameters[1]}) with outer radius {parameters[3]} and inner radius {parameters[4]} and {parameters[2]} outer sticks"
            );
        }
        catch (Exception ex)
        {
            _logger.AddLog(
                $"Invalid parameters for Mesh.AddTire Could not parse floats/ints from '{parameters[0]}', '{parameters[1]}', '{parameters[2]}', '{parameters[3]}', or '{parameters[4]}' - {ex.Message}",
                ImGuiLogger.LogTypes.Error
            );
        }
    }

    /// <summary>
    /// adds a particle with parameters for use in the debug console
    /// </summary>
    /// <param name="parameters"></param>
    [ConsoleCommand("Particle.Add")]
    private void AddParticle(string[] parameters)
    {
        if (parameters.Length < 3)
        {
            _logger.AddLog(
                $"Not enough parameters for Particle.Add(x, y, mass) Expected 3, got {parameters.Length}",
                ImGuiLogger.LogTypes.Error
            );
            return;
        }

        if (
            float.TryParse(parameters[0], out float x)
            && float.TryParse(parameters[1], out float y)
            && float.TryParse(parameters[2], out float mass)
        )
        {
            int id = _activeMesh.AddParticle(new Vector2(x, y), mass, false, Color.White);
            _logger.AddLog($"Added particle at ({x}, {y}) with mass {mass} (ID {id})");
        }
        else
        {
            _logger.AddLog(
                $"Invalid parameters for Particle.Add Could not parse floats from '{parameters[0]}', '{parameters[1]}', or '{parameters[2]}'",
                ImGuiLogger.LogTypes.Error
            );
        }
    }

    /// <summary>
    /// adds a stick with parameters for use in the debug console
    /// </summary>
    /// <param name="parameters"></param>/
    [ConsoleCommand("Stick.Add")]
    private void AddStick(string[] parameters)
    {
        if (parameters.Length < 2)
        {
            _logger.AddLog(
                $"Not enough parameters for Stick.Add(id1, id2) Expected 2, got {parameters.Length}",
                ImGuiLogger.LogTypes.Error
            );
            return;
        }

        if (int.TryParse(parameters[0], out int p1Id) && int.TryParse(parameters[1], out int p2Id))
        {
            if (_activeMesh.Particles.ContainsKey(p1Id) && _activeMesh.Particles.ContainsKey(p2Id))
            {
                _activeMesh.AddStickBetween(p1Id, p2Id);
                _logger.AddLog($"Added stick between particles {p1Id} and {p2Id}");
            }
            else
            {
                _logger.AddLog(
                    $"One or both particle IDs not found: {p1Id}, {p2Id}",
                    ImGuiLogger.LogTypes.Error
                );
            }
        }
        else
        {
            _logger.AddLog(
                $"Invalid parameters for Stick.Add Could not parse floats from '{parameters[0]}' and '{parameters[1]}'",
                ImGuiLogger.LogTypes.Error
            );
        }
    }

    /// <summary>
    /// Tests the database connection and logs the result. This command can be invoked from the debug console to verify that the application can successfully connect to the database. If the connection test is successful, it logs a success message. If the connection test fails, it catches the exception and logs an error message with details about the failure. This command does not take any parameters, and if any parameters are provided, it logs a warning indicating that they are not expected. This allows developers to quickly check database connectivity without needing to write additional code or use external tools.
    /// </summary>
    /// <param name="parameters"></param>
    [ConsoleCommand("Database.TestConnection")]
    private void TestConnection(string[] parameters)
    {
        if (parameters.Length > 0)
        {
            _logger.AddLog(
                "Database.TestConnection does not take parameters.",
                ImGuiLogger.LogTypes.Warning
            );
        }

        try
        {
            _database.TestConnection();
            _logger.AddLog("Database connection successful");
        }
        catch (Exception ex)
        {
            _logger.AddLog($"Database connection failed: {ex.Message}", ImGuiLogger.LogTypes.Error);
        }
    }
}

/// <summary>
/// the logger class for ImGui with support for different types of logs, auto condensing for repeated messages and colouring of the logs
/// also includes a debug console support with command history
/// </summary>
public class ImGuiLogger
{
    public enum LogTypes
    {
        Info,
        Warning,
        Error,
    };

    /// <summary>
    /// Represents a log message with its content, count of occurrences, and log type (info, warning, error). This class is used to store log messages in the ImGuiLogger, allowing for features like message aggregation (counting repeated messages) and categorization by log type for filtering and display purposes in the debug console. Each MessageLog instance contains the actual log message, how many times it has been logged (for repeated messages), and the type of log to determine how it should be displayed in the UI.
    /// </summary>
    class MessageLog
    {
        public string Message;
        public int Count;
        public LogTypes Type;
    }

    /// <summary>
    /// Represents a console command with its command path, command name, and parameters. This class is used to store commands that are entered by the user in the debug console, allowing them to be queued for execution. Each Command instance contains the full path of the command (split into parts), the specific command to execute, and any parameters that were provided with the command. This structure enables the CommandRegistry to look up and invoke the correct method based on the command path and name when processing user input from the debug console.
    /// </summary>
    public class Command
    {
        public string[] CommandPath;
        public string ExCommand;
        public string[] Parameters;
    }

    /// <summary>
    /// the queue of log messages that have been added and for display  in the imgui fiole
    /// </summary>
    readonly Queue<MessageLog> _logs = new();

    /// <summary>
    /// the queue of console commands that have been added but not yet processed for execution
    /// </summary>
    readonly Queue<Command> _commands = new();

    /// <summary>
    /// the raw text inputed by the user in the debug console input field
    /// </summary>
    private string _commandInput = "";

    /// <summary>
    /// the proccessed log messages that are ready for saving to a file
    /// </summary>
    private List<string> _logStringHistory = new();
    private readonly Dictionary<string, Func<string>> _envVars = new();

    /// <summary>
    /// Registers an environment variable that can be used in console commands. The name parameter specifies the name of the environment variable, which can be referenced in commands using the syntax $VariableName. The resolver parameter is a function that returns the current value of the environment variable as a string. When a command is processed, any occurrences of $VariableName will be replaced with the value returned by the corresponding resolver function. This allows for dynamic values to be used in console commands, such as referencing the current active mesh or other runtime information without needing to hardcode values into commands. By using environment variables, users can create more flexible and powerful commands that adapt to the current state of the application.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="resolver"></param>
    public void RegisterEnvVar(string name, Func<string> resolver)
    {
        _envVars[name] = resolver;
    }

    /// <summary>
    /// replaces the text of any environment variables in the command string with their current values by using regular expressions to find occurrences of $VariableName and replacing them with the output of the corresponding resolver function from the _envVars dictionary
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    private string ResolveEnvVars(string command)
    {
        return Regex.Replace(
            command,
            @"\$(\w+)",
            match =>
            {
                string varName = match.Groups[1].Value;
                if (_envVars.TryGetValue(varName, out var resolver))
                    return resolver();
                return match.Value;
            }
        );
    }

    /// <summary>
    /// saves the text inputed by the user for comand history if the user wants to recall it later with arrow keys
    /// </summary>/
    private readonly List<string> _commandHistory = new();

    /// <summary>
    /// where the user is in the command history
    /// </summary>
    private int _historyIndex = -1;

    private string _savedInput = "";

    /// <summary>
    /// deterimines on wether it should auto scroll the logs to the bottom when a new log is added this is toggled by the user
    /// </summary>
    public bool autoScrollLogs = true;

    /// <summary>
    /// deterimes wether the log messages should wrap in the log display
    /// </summary>
    public bool wrapText = true;

    /// <summary>
    /// a toggle for if the debug console is supported and should be displayed
    /// </summary>
    public bool supportDebugConsole = true;

    /// <summary>
    /// Dictionary to track the visibility of different log types (info, warning, error) in the debug console. This allows users to filter which types of log messages they want to see by toggling the corresponding checkboxes in the UI. Each log type can be shown or hidden based on the user's preference, making it easier to focus on specific types of messages when debugging. For example, a user might choose to hide info messages and only show warnings and errors to quickly identify issues without being overwhelmed by less critical information.
    /// </summary>
    readonly Dictionary<LogTypes, bool> _logTypeVisibility = new Dictionary<LogTypes, bool>
    {
        { LogTypes.Info, true },
        { LogTypes.Warning, true },
        { LogTypes.Error, true },
    };

    public void AddLog(string message, LogTypes type = LogTypes.Info)
    {
        var lastLog = _logs.LastOrDefault();
        if (lastLog != null && lastLog.Message == message && lastLog.Type == type)
        {
            lastLog.Count++;
        }
        else
        {
            _logs.Enqueue(
                new MessageLog
                {
                    Message = message,
                    Count = 1,
                    Type = type,
                }
            );
            var log = _logs.Peek();
            string displayMessage = log.Count > 1 ? $"{log.Message} (x{log.Count})" : log.Message;
            _logStringHistory.Add(displayMessage);
        }
    }

    /// <summary>
    /// Adds a console command to the command queue for later processing. The command string is trimmed and checked for validity before being added to the queue. If the command is empty or consists only of whitespace, it is ignored. The method also resolves any environment variables in the command string before parsing it into its components (command path, command name, and parameters). The parsed command is then enqueued for execution, and a log message is added to indicate that the command has been queued. This allows users to enter commands in the debug console, which can then be processed asynchronously in the main game loop without blocking the UI or requiring immediate execution.
    /// </summary>
    /// <param name="command"></param>
    public void AddCommand(string command)
    {
        command = command?.Trim();
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        command = ResolveEnvVars(command);

        string commandWithoutParams = command.Split('(')[0];
        string[] pathParts = commandWithoutParams.Split('.');

        if (pathParts.Length == 0)
            return;

        string[] parameters = Array.Empty<string>();
        if (command.Contains('(') && command.Contains(')'))
        {
            int start = command.IndexOf('(') + 1;
            int end = command.LastIndexOf(')');
            if (end > start)
            {
                string paramStr = command.Substring(start, end - start);
                parameters = paramStr.Split(
                    ',',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
                );
            }
        }

        string exCommand = pathParts[^1];
        string[] commandPath = pathParts.Length > 1 ? pathParts[..^1] : Array.Empty<string>();

        _commands.Enqueue(
            new Command
            {
                CommandPath = commandPath,
                ExCommand = exCommand,
                Parameters = parameters,
            }
        );

        AddLog(
            $"Command queued: {string.Join(".", commandPath)}.{exCommand}({string.Join(", ", parameters)})"
        );
    }

    /// <summary>
    /// attempts to dequeue a command from the command queue if the command one is avoable it is returned through out
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    public bool TryDequeueCommand(out Command command)
    {
        return _commands.TryDequeue(out command);
    }

    /// <summary>
    /// returns true if there are pending commands in the command queue
    /// </summary>
    public bool HasPendingCommands => _commands.Count > 0;

    /// <summary>
    /// draws all logs in ImGui with options to filter by log type, auto scroll and wrap text.
    /// </summary>
    /// <param name="openWindow"></param>
    public void DrawLogs(ref bool openWindow)
    {
        if (!ImGui.Begin("Logs", ref openWindow))
        {
            ImGui.End();
            return;
        }
        bool infoVisible = _logTypeVisibility[LogTypes.Info];
        if (ImGui.Checkbox("Info", ref infoVisible))
            _logTypeVisibility[LogTypes.Info] = infoVisible;
        ImGui.SameLine();
        bool errorVisible = _logTypeVisibility[LogTypes.Error];
        if (ImGui.Checkbox("Error", ref errorVisible))
            _logTypeVisibility[LogTypes.Error] = errorVisible;
        ImGui.SameLine();
        bool warningVisible = _logTypeVisibility[LogTypes.Warning];
        if (ImGui.Checkbox("Warning", ref warningVisible))
            _logTypeVisibility[LogTypes.Warning] = warningVisible;

        ImGui.Separator();
        ImGui.Checkbox("Auto Scroll Logs", ref autoScrollLogs);

        ImGui.Checkbox("Wrap Text", ref wrapText);

        ImGui.SeparatorText("Logs");
        if (supportDebugConsole)
        {
            ImGui.Separator();
            ImGui.Text("Debug Console (Up/Down for history):");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 80);

            if (ImGui.IsKeyPressed(ImGuiKey.UpArrow) && _commandHistory.Count > 0)
            {
                if (_historyIndex == -1)
                {
                    _savedInput = _commandInput;
                    _historyIndex = _commandHistory.Count - 1;
                }
                else if (_historyIndex > 0)
                {
                    _historyIndex--;
                }
                _commandInput = _commandHistory[_historyIndex];
            }
            else if (ImGui.IsKeyPressed(ImGuiKey.DownArrow))
            {
                if (_historyIndex >= 0)
                {
                    _historyIndex++;
                    if (_historyIndex >= _commandHistory.Count)
                    {
                        _historyIndex = -1;
                        _commandInput = _savedInput;
                    }
                    else
                    {
                        _commandInput = _commandHistory[_historyIndex];
                    }
                }
            }

            bool enterPressed = ImGui.InputText(
                "##Command",
                ref _commandInput,
                256,
                ImGuiInputTextFlags.EnterReturnsTrue
            );
            ImGui.SameLine();
            if (
                (ImGui.Button("Execute") || enterPressed)
                && !string.IsNullOrWhiteSpace(_commandInput)
            )
            {
                if (_commandHistory.Count == 0 || _commandHistory[^1] != _commandInput)
                {
                    _commandHistory.Add(_commandInput);
                }
                _historyIndex = -1;
                _savedInput = "";

                AddCommand(_commandInput);
                _commandInput = "";
            }
        }
        ImGui.BeginChild("LogScrollArea", new System.Numerics.Vector2(0, 0));
        if (wrapText)
            ImGui.PushTextWrapPos(0.0f);
        foreach (var log in _logs)
        {
            string displayMessage = log.Count > 1 ? $"{log.Message} (x{log.Count})" : log.Message;
            _logStringHistory.Add(displayMessage);
            var color = log.Type switch
            {
                LogTypes.Info => new System.Numerics.Vector4(1f, 1f, 1f, 1f),
                LogTypes.Warning => new System.Numerics.Vector4(1f, 1f, 0f, 1f),
                LogTypes.Error => new System.Numerics.Vector4(1f, 0f, 0f, 1f),
                _ => new System.Numerics.Vector4(1f, 1f, 1f, 1f),
            };
            if (_logTypeVisibility[log.Type])
                ImGui.TextColored(color, displayMessage);
        }
        if (wrapText)
            ImGui.PopTextWrapPos();
        if (autoScrollLogs)
        {
            ImGui.SetScrollHereY(1.0f);
        }
        ImGui.EndChild();

        ImGui.End();
    }

    /// <summary>
    /// converts all of the logs to a string to be saved to a log file
    /// </summary>
    /// <returns></returns>
    public string GetLogsAsString()
    {
        string historyString = string.Join(Environment.NewLine, _logStringHistory);
        return historyString;
    }

    /// <summary>
    /// saves logs to a file in the specified file path
    /// </summary>
    /// <param name="filePath"></param>
    public void SaveLogsToFile(string filePath)
    {
        try
        {
            System.IO.File.WriteAllText(filePath, GetLogsAsString());
            AddLog($"Logs saved to {filePath}");
        }
        catch (Exception ex)
        {
            AddLog($"Failed to save logs to {filePath}: {ex.Message}", LogTypes.Error);
        }
    }
}
