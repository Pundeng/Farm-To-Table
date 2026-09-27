using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FantasyShapez.Food;
using UnityEngine;

namespace FantasyShapez.Buildings
{
    [Serializable]
    public sealed class BlueprintFile
    {
        public int version = 1;
        public BlueprintRecord[] records = Array.Empty<BlueprintRecord>();
    }

    [Serializable]
    public sealed class BlueprintRecord
    {
        public string id;
        public string name;
        public BlueprintBuilding[] buildings = Array.Empty<BlueprintBuilding>();
        public BlueprintConnection[] connections = Array.Empty<BlueprintConnection>();
        public int PartCount => (buildings?.Length ?? 0) + (connections?.Length ?? 0);
    }

    [Serializable]
    public sealed class BlueprintBuilding
    {
        public string definitionId;
        public int x;
        public int y;
        public BuildingRotation rotation;
        public string cropId;
    }

    [Serializable]
    public sealed class BlueprintConnection
    {
        public int x;
        public int y;
        public CookingProperty property;
        public PropertyConnectionKind kind;
    }

    public sealed class BlueprintLibrary
    {
        private readonly string path;
        private BlueprintFile file = new();
        private bool readOnly;
        public IReadOnlyList<BlueprintRecord> Records => file.records;
        public string LoadError { get; private set; }

        public BlueprintLibrary(string path)
        {
            this.path = path ?? throw new ArgumentNullException(nameof(path));
            Load();
        }

        public void Load()
        {
            file = new BlueprintFile();
            readOnly = false;
            LoadError = null;
            if (!File.Exists(path)) return;
            try
            {
                BlueprintFile loaded = JsonUtility.FromJson<BlueprintFile>(
                    File.ReadAllText(path, Encoding.UTF8));
                if (loaded == null || loaded.version != 1 || loaded.records == null ||
                    loaded.records.Any(record => record == null ||
                        string.IsNullOrWhiteSpace(record.id)) ||
                    loaded.records.Select(record => record.id).Distinct(
                        StringComparer.Ordinal).Count() != loaded.records.Length)
                    throw new InvalidDataException("Unsupported or malformed Blueprint Library.");
                file = loaded;
            }
            catch (Exception exception) when (exception is IOException or
                UnauthorizedAccessException or ArgumentException or InvalidDataException)
            {
                readOnly = true;
                LoadError = exception.Message;
            }
        }

        public bool TryAdd(string name, BuildingGroupCopy group, out string error)
        {
            if (group == null) { error = "Select buildings to save first."; return false; }
            if (!ValidName(name, out error)) return false;
            var record = new BlueprintRecord
            {
                id = Guid.NewGuid().ToString("N"),
                name = name.Trim(),
                buildings = group.Items.Select(item => new BlueprintBuilding
                {
                    definitionId = item.Option.Definition.Id,
                    x = item.Offset.x, y = item.Offset.y,
                    rotation = item.Rotation, cropId = item.CropId
                }).ToArray(),
                connections = group.PropertyItems.Select(item => new BlueprintConnection
                {
                    x = item.Offset.x, y = item.Offset.y,
                    property = item.Connection.Property,
                    kind = item.Connection.Kind
                }).ToArray()
            };
            return TryWrite(file.records.Append(record).ToArray(), out error);
        }

        public bool TryRename(string id, string name, out string error)
        {
            if (!ValidName(name, out error)) return false;
            BlueprintRecord[] records = CloneRecords();
            BlueprintRecord record = records.FirstOrDefault(item => item.id == id);
            if (record == null) { error = "Blueprint not found."; return false; }
            record.name = name.Trim();
            return TryWrite(records, out error);
        }

        public bool TryDuplicate(string id, out string error)
        {
            BlueprintRecord record = file.records.FirstOrDefault(item => item.id == id);
            if (record == null) { error = "Blueprint not found."; return false; }
            BlueprintRecord duplicate = JsonUtility.FromJson<BlueprintRecord>(
                JsonUtility.ToJson(record));
            duplicate.id = Guid.NewGuid().ToString("N");
            duplicate.name = record.name + " Copy";
            return TryWrite(file.records.Append(duplicate).ToArray(), out error);
        }

        public bool TryDelete(string id, out string error)
        {
            if (!file.records.Any(item => item.id == id))
            { error = "Blueprint not found."; return false; }
            return TryWrite(file.records.Where(item => item.id != id).ToArray(), out error);
        }

        public static bool TryResolve(BlueprintRecord record,
            IReadOnlyList<BuildingPlacementOption> options,
            out BuildingGroupCopy group, out string error)
        {
            group = null;
            error = "Blueprint data is incompatible.";
            if (record == null || record.PartCount < 1 || record.PartCount > 512 ||
                record.buildings == null || record.connections == null || options == null)
                return false;
            var items = new List<BuildingGroupCopyItem>();
            foreach (BlueprintBuilding saved in record.buildings)
            {
                if (saved == null || saved.x < 0 || saved.y < 0 ||
                    saved.x > 512 || saved.y > 512 ||
                    !Enum.IsDefined(typeof(BuildingRotation), saved.rotation)) return false;
                BuildingPlacementOption option = options.FirstOrDefault(candidate =>
                    candidate?.Definition?.Id == saved.definitionId &&
                    candidate.Definition.InstancePrefab != null);
                if (option == null)
                {
                    error = $"Missing building: {saved.definitionId}.";
                    return false;
                }
                // JsonUtility restores an omitted string as empty on some Unity versions.
                if (!string.IsNullOrEmpty(saved.cropId) &&
                    saved.definitionId != nameof(FarmPlot)) return false;
                items.Add(new BuildingGroupCopyItem(option,
                    new Vector2Int(saved.x, saved.y), saved.rotation,
                    cropId: string.IsNullOrEmpty(saved.cropId) ? null : saved.cropId));
            }
            var connections = new List<PropertyConnection>();
            foreach (BlueprintConnection saved in record.connections)
            {
                if (saved == null || saved.x < 0 || saved.y < 0 ||
                    saved.x > 512 || saved.y > 512 ||
                    !Enum.IsDefined(typeof(CookingProperty), saved.property) ||
                    saved.kind is not (PropertyConnectionKind.Collector or
                        PropertyConnectionKind.Pipe)) return false;
                connections.Add(new PropertyConnection(new Vector2Int(saved.x, saved.y),
                    Vector2Int.zero, saved.property,
                    saved.kind));
            }
            try { group = new BuildingGroupCopy(items, connections); }
            catch (ArgumentException) { return false; }
            error = null;
            return true;
        }

        private BlueprintRecord[] CloneRecords() => JsonUtility.FromJson<BlueprintFile>(
            JsonUtility.ToJson(file)).records;

        private static bool ValidName(string name, out string error)
        {
            error = string.IsNullOrWhiteSpace(name) || name.Trim().Length > 64
                ? "Use a Blueprint name of 1–64 characters." : null;
            return error == null;
        }

        private bool TryWrite(BlueprintRecord[] records, out string error)
        {
            if (readOnly)
            { error = $"Blueprint Library is read-only: {LoadError}"; return false; }
            string temporary = path + ".tmp";
            try
            {
                var candidate = new BlueprintFile { records = records };
                File.WriteAllText(temporary, JsonUtility.ToJson(candidate, true),
                    new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                file = candidate;
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException or
                UnauthorizedAccessException or ArgumentException)
            {
                error = exception.Message;
                return false;
            }
        }
    }
}
