using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace PhysicsCSAlevlProject;

public partial class Game1
{
    /// <summary>
    /// Gets consistent JSON serializer settings for mesh serialization/deserialization.
    /// This ensures oscillating particles and other types are properly handled.
    /// </summary>
    private static JsonSerializerSettings GetMeshJsonSettings()
    {
        return new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Objects,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
        };
    }

    /// <summary>
    /// saves the given mesh to a JSOn file at the given filepath
    /// </summary>
    /// <param name="mesh"></param>
    /// <param name="Name"></param>
    /// <param name="filePath"></param>
    private Task SaveMeshToJSON(Mesh mesh, string Name, string filePath)
    {
        try
        {
            var snapshot = mesh.DeepCopy();
            var settings = GetMeshJsonSettings();
            var fileWriteableMesh = new FileWriteableMesh(snapshot);
            Directory.CreateDirectory(filePath);
            filePath = Path.Combine(filePath, Name + ".json");
            string json = JsonConvert.SerializeObject(fileWriteableMesh, settings);
            return File.WriteAllTextAsync(filePath, json);
        }
        catch (Exception ex)
        {
            _logger.AddLog(
                $"Failed to save mesh '{Name}' to {filePath}: {ex.Message}",
                ImGuiLogger.LogTypes.Error
            );
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// returns a json string of the mesh used to save to the database
    /// </summary>
    /// <param name="mesh"></param>
    /// <returns>JSON String</returns>
    private string SaveMeshToJsonString(Mesh mesh)
    {
        try
        {
            var settings = GetMeshJsonSettings();
            var fileWriteableMesh = new FileWriteableMesh(mesh);
            string json = JsonConvert.SerializeObject(fileWriteableMesh, settings);
            return json;
        }
        catch (Exception ex)
        {
            _logger.AddLog(
                $"Failed to serialize mesh to JSON string: {ex.Message}",
                ImGuiLogger.LogTypes.Error
            );
            return string.Empty;
        }
    }

    /// <summary>
    /// attempts to load a Mesh from the file path provided and converts the file writable mesh to a regular mesh if it fails it returns active mesh
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    private Mesh LoadMeshFromJSON(string filePath)
    {
        try
        {
            var settings = GetMeshJsonSettings();
            string json = File.ReadAllText(filePath);
            FileWriteableMesh fileWriteableMesh = JsonConvert.DeserializeObject<FileWriteableMesh>(
                json,
                settings
            );

            Mesh mesh = fileWriteableMesh.ToMesh();
            mesh.RestoreStickReferences();

            return mesh;
        }
        catch (Exception ex)
        {
            _logger.AddLog(
                $"Failed to load mesh from {filePath}: {ex.Message}",
                ImGuiLogger.LogTypes.Error
            );
            return _activeMesh;
        }
    }

    /// <summary>
    /// converts a json string intoo a mesh used to laod from the database if it fails it returns active mesh
    /// </summary>
    /// <param name="json"></param>
    /// <returns></returns>
    private Mesh LoadMeshFromJsonString(string json)
    {
        try
        {
            var settings = GetMeshJsonSettings();
            FileWriteableMesh fileWriteableMesh = JsonConvert.DeserializeObject<FileWriteableMesh>(
                json,
                settings
            );

            Mesh mesh = fileWriteableMesh.ToMesh();
            mesh.RestoreStickReferences();

            return mesh;
        }
        catch (Exception ex)
        {
            _logger.AddLog(
                $"Failed to load mesh from JSON string: {ex.Message}",
                ImGuiLogger.LogTypes.Error
            );
            return _activeMesh;
        }
    }

    /// <summary>
    /// loads all mesh from a a directory path and returns a dictionary  of the meshes with the key being the filename
    /// </summary>
    /// <param name="directoryPath"></param>
    /// <returns>Dictionary of FileName and Mesh</returns>
    private Dictionary<string, Mesh> LoadAllMeshesFromDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            _logger.AddLog(
                $"Mesh directory not found: {directoryPath}",
                ImGuiLogger.LogTypes.Warning
            );
            return new Dictionary<string, Mesh>();
        }

        string[] jsonFiles = Directory.GetFiles(directoryPath, "*.json");
        Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        foreach (string filePath in jsonFiles)
        {
            try
            {
                _loadingQuickMeshFile = Path.GetFileName(filePath);
                Mesh mesh = LoadMeshFromJSON(filePath);
                string fileName = Path.GetFileNameWithoutExtension(Path.GetFileName(filePath));
                meshes[fileName] = mesh;

                lock (_quickMeshesLock)
                {
                    _quickMeshes ??= new Dictionary<string, Mesh>();
                    _quickMeshes[fileName] = mesh;
                }
            }
            catch (Exception ex)
            {
                _logger.AddLog(
                    $"Failed to load mesh from {filePath}: {ex.Message}",
                    ImGuiLogger.LogTypes.Error
                );
            }
        }

        _loadingQuickMeshFile = string.Empty;
        return meshes;
    }

    /// <summary>
    /// Loads the saved mesh library in the background so startup does not block the app while reading several large JSON files.
    /// Each file is added to the quick-mesh dictionary as soon as it finishes loading so already-loaded entries remain selectable.
    /// </summary>
    private async Task LoadAllMeshesFromDirectoryAsync(string directoryPath)
    {
        if (_isLoadingQuickMeshes)
        {
            return;
        }

        _isLoadingQuickMeshes = true;
        lock (_quickMeshesLock)
        {
            _quickMeshes = new Dictionary<string, Mesh>();
        }

        try
        {
            await Task.Run(() => LoadAllMeshesFromDirectory(directoryPath));
        }
        catch (Exception ex)
        {
            _logger.AddLog(
                $"Failed to asynchronously load local meshes: {ex.Message}",
                ImGuiLogger.LogTypes.Error
            );
        }
        finally
        {
            _isLoadingQuickMeshes = false;
            _loadingQuickMeshFile = string.Empty;
        }
    }
}
