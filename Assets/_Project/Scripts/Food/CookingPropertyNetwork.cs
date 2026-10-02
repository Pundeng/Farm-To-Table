using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CozyFoodFactory.Food
{
    public enum CookingProperty
    {
        Heat = 0,
        Water = 1,
        Time = 2,
        Cold = 3
    }

    public enum PropertyConnectionKind
    {
        Source,
        Collector,
        Pipe,
        Demand
    }

    public readonly struct PropertyConnection
    {
        public PropertyConnection(Vector2Int cell, Vector2Int sourceCell,
            CookingProperty property, PropertyConnectionKind kind, int units = 0,
            Vector2Int? requiredConnectionCell = null)
        {
            Cell = cell;
            SourceCell = sourceCell;
            Property = property;
            Kind = kind;
            Units = units;
            RequiredConnectionCell = requiredConnectionCell;
        }

        public Vector2Int Cell { get; }
        public Vector2Int SourceCell { get; }
        public CookingProperty Property { get; }
        public PropertyConnectionKind Kind { get; }
        public int Units { get; }
        public Vector2Int? RequiredConnectionCell { get; }
    }

    public readonly struct PropertySupplyStatus
    {
        public PropertySupplyStatus(int capacity, int connectedDemand,
            int connectedConsumers)
        {
            Capacity = capacity;
            ConnectedDemand = connectedDemand;
            ConnectedConsumers = connectedConsumers;
        }

        public int Capacity { get; }
        public int ConnectedDemand { get; }
        public int ConnectedConsumers { get; }
        public int AvailableCapacity => Math.Max(0, Capacity - ConnectedDemand);
        public bool IsWithinCapacity => ConnectedDemand <= Capacity;
    }

    // A pipe or collector retains its source owner even if a section is disconnected.
    // This prevents a disconnected section from silently joining another source.
    public sealed class CookingPropertyNetwork
    {
        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
        };

        private readonly Dictionary<Vector2Int, PropertyConnection> connections = new();
        private readonly Dictionary<Vector2Int, int> capacities = new();

        public IEnumerable<PropertyConnection> Connections => connections.Values;

        public void Clear()
        {
            connections.Clear();
            capacities.Clear();
        }

        internal CookingPropertyNetwork CopyForPreview()
        {
            var copy = new CookingPropertyNetwork();
            foreach (KeyValuePair<Vector2Int, PropertyConnection> entry in connections)
                copy.connections.Add(entry.Key, entry.Value);
            foreach (KeyValuePair<Vector2Int, int> entry in capacities)
                copy.capacities.Add(entry.Key, entry.Value);
            return copy;
        }

        public bool TryGetConnection(Vector2Int cell, out PropertyConnection connection) =>
            connections.TryGetValue(cell, out connection);

        public bool TryAddSource(Vector2Int cell, CookingProperty property, int capacity)
        {
            if (capacity < 1 || connections.ContainsKey(cell))
            {
                return false;
            }

            // Deposits may contain touching sources; constructed networks remain separate.
            if (Directions.Any(direction => connections.TryGetValue(cell + direction,
                    out PropertyConnection neighbor) &&
                    neighbor.Kind is PropertyConnectionKind.Collector or PropertyConnectionKind.Pipe))
            {
                return false;
            }

            connections.Add(cell, new PropertyConnection(cell, cell, property,
                PropertyConnectionKind.Source));
            capacities.Add(cell, capacity);
            return true;
        }

        public bool TryAddCollector(Vector2Int cell, Vector2Int sourceCell)
        {
            if (connections.ContainsKey(cell) ||
                !connections.TryGetValue(sourceCell, out PropertyConnection source) ||
                source.Kind != PropertyConnectionKind.Source ||
                !AreAdjacent(cell, sourceCell) ||
                TouchesForeignNetwork(cell, sourceCell))
            {
                return false;
            }

            connections.Add(cell, new PropertyConnection(cell, sourceCell,
                source.Property, PropertyConnectionKind.Collector));
            return true;
        }

        public bool TryAddPipe(Vector2Int cell)
        {
            if (connections.ContainsKey(cell) ||
                !TryGetSingleAdjacentOwner(cell, out Vector2Int sourceCell,
                    requireConductor: true) ||
                TouchesForeignNetwork(cell, sourceCell))
            {
                return false;
            }

            CookingProperty property = connections[sourceCell].Property;
            connections.Add(cell, new PropertyConnection(cell, sourceCell,
                property, PropertyConnectionKind.Pipe));
            return true;
        }

        public bool TryAddPipe(Vector2Int cell, Vector2Int sourceCell)
            {
                if (connections.ContainsKey(cell) ||
                    !connections.TryGetValue(sourceCell, out PropertyConnection source) ||
                    source.Kind != PropertyConnectionKind.Source)
                {
                    return false;
                }

                bool touchesOwnNetwork = false;
                foreach (Vector2Int direction in Directions)
                {
                    if (!connections.TryGetValue(cell + direction,
                            out PropertyConnection neighbor) ||
                        neighbor.SourceCell != sourceCell)
                    {
                        continue;
                    }

                    if (neighbor.Kind is PropertyConnectionKind.Collector or PropertyConnectionKind.Pipe)
                    {
                        touchesOwnNetwork = true;
                        break;
                    }
                }

                if (!touchesOwnNetwork)
                {
                    return false;
                }

                connections.Add(cell, new PropertyConnection(cell, sourceCell,
                    source.Property, PropertyConnectionKind.Pipe));
                return true;
            }

        // The caller supplies a demand cell; Processor port locations are not assumed here.
        public bool TryAddDemand(Vector2Int cell, int units = 1,
            Vector2Int? requiredConnectionCell = null)
        {
            if (units < 1 || connections.ContainsKey(cell) ||
                !TryGetSingleAdjacentOwner(cell, out Vector2Int sourceCell,
                    requireConductor: true) ||
                TouchesForeignNetwork(cell, sourceCell))
            {
                return false;
            }

            if (requiredConnectionCell.HasValue &&
                (!AreAdjacent(cell, requiredConnectionCell.Value) ||
                 !connections.TryGetValue(requiredConnectionCell.Value,
                     out PropertyConnection requiredConnection) ||
                 requiredConnection.SourceCell != sourceCell ||
                 requiredConnection.Kind is not (PropertyConnectionKind.Collector or
                     PropertyConnectionKind.Pipe)))
            {
                return false;
            }

            connections.Add(cell, new PropertyConnection(cell, sourceCell,
                connections[sourceCell].Property, PropertyConnectionKind.Demand,
                units, requiredConnectionCell));
            return true;
        }

        // A saved disconnected section retains its original source owner.
        public bool TryRestoreConnection(PropertyConnection saved)
        {
            if (saved.Kind is not (PropertyConnectionKind.Collector or
                    PropertyConnectionKind.Pipe or PropertyConnectionKind.Demand) ||
                connections.ContainsKey(saved.Cell) ||
                !connections.TryGetValue(saved.SourceCell, out PropertyConnection source) ||
                source.Kind != PropertyConnectionKind.Source ||
                source.Property != saved.Property ||
                saved.Kind == PropertyConnectionKind.Collector &&
                    !AreAdjacent(saved.Cell, saved.SourceCell) ||
                (saved.Kind == PropertyConnectionKind.Demand && TouchesForeignNetwork(saved.Cell, saved.SourceCell)))
            {
                return false;
            }

            connections.Add(saved.Cell, saved);
            return true;
        }

        public bool Remove(Vector2Int cell)
        {
            return connections.TryGetValue(cell, out PropertyConnection connection) &&
                connection.Kind != PropertyConnectionKind.Source &&
                connections.Remove(cell);
        }

        public bool TryGetStatus(Vector2Int sourceCell, out PropertySupplyStatus status)
        {
            if (!capacities.TryGetValue(sourceCell, out int capacity))
            {
                status = default;
                return false;
            }

            HashSet<Vector2Int> connected = GetConnectedConductors(sourceCell);
            int demand = 0;
            int consumers = 0;
            foreach (PropertyConnection connection in connections.Values)
            {
                if (connection.Kind != PropertyConnectionKind.Demand ||
                    connection.SourceCell != sourceCell ||
                    !IsDemandConnected(connection, connected))
                {
                    continue;
                }

                demand += connection.Units;
                consumers++;
            }

            status = new PropertySupplyStatus(capacity, demand, consumers);
            return true;
        }

        public bool IsSupplied(Vector2Int demandCell)
        {
            return connections.TryGetValue(demandCell, out PropertyConnection demand) &&
                demand.Kind == PropertyConnectionKind.Demand &&
                TryGetStatus(demand.SourceCell, out PropertySupplyStatus status) &&
                status.IsWithinCapacity &&
                IsDemandConnected(demand,
                    GetConnectedConductors(demand.SourceCell));
        }

        public bool IsConnectedToSource(Vector2Int cell)
        {
            if (!connections.TryGetValue(cell, out PropertyConnection connection))
            {
                return false;
            }

            HashSet<Vector2Int> connected = GetConnectedConductors(connection.SourceCell);
            return connection.Kind == PropertyConnectionKind.Demand
                ? IsDemandConnected(connection, connected)
                : connected.Contains(cell);
        }

        private HashSet<Vector2Int> GetConnectedConductors(Vector2Int sourceCell)
        {
            var visited = new HashSet<Vector2Int> { sourceCell };
            var pending = new Queue<Vector2Int>();
            pending.Enqueue(sourceCell);
            while (pending.Count > 0)
            {
                Vector2Int cell = pending.Dequeue();
                foreach (Vector2Int direction in Directions)
                {
                    Vector2Int neighbor = cell + direction;
                    if (visited.Contains(neighbor) ||
                        !connections.TryGetValue(neighbor, out PropertyConnection connection) ||
                        connection.SourceCell != sourceCell ||
                        connection.Kind == PropertyConnectionKind.Demand ||
                        (cell == sourceCell &&
                         connection.Kind != PropertyConnectionKind.Collector))
                    {
                        continue;
                    }

                    visited.Add(neighbor);
                    pending.Enqueue(neighbor);
                }
            }

            return visited;
        }

        private bool TryGetSingleAdjacentOwner(Vector2Int cell,
            out Vector2Int sourceCell, bool requireConductor)
        {
            sourceCell = default;
            bool found = false;
            foreach (Vector2Int direction in Directions)
            {
                if (!connections.TryGetValue(cell + direction,
                        out PropertyConnection neighbor) ||
                    neighbor.Kind == PropertyConnectionKind.Demand)
                {
                    continue;
                }

                if (neighbor.Kind == PropertyConnectionKind.Source && requireConductor)
                {
                    continue;
                }

                if (found && sourceCell != neighbor.SourceCell)
                {
                    return false;
                }

                sourceCell = neighbor.SourceCell;
                found = true;
            }

            return found;
        }

        private bool TouchesForeignNetwork(Vector2Int cell, Vector2Int sourceCell)
        {
            foreach (Vector2Int direction in Directions)
            {
                if (connections.TryGetValue(cell + direction,
                        out PropertyConnection neighbor) &&
                    neighbor.SourceCell != sourceCell &&
                    neighbor.Kind != PropertyConnectionKind.Demand)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsDemandConnected(PropertyConnection demand,
            HashSet<Vector2Int> cells)
        {
            if (demand.RequiredConnectionCell.HasValue)
            {
                Vector2Int required = demand.RequiredConnectionCell.Value;
                return cells.Contains(required) &&
                    (connections[required].Kind is PropertyConnectionKind.Collector or
                        PropertyConnectionKind.Pipe);
            }

            foreach (Vector2Int direction in Directions)
            {
                Vector2Int neighbor = demand.Cell + direction;
                if (cells.Contains(neighbor) &&
                    connections[neighbor].Kind != PropertyConnectionKind.Source)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AreAdjacent(Vector2Int a, Vector2Int b)
        {
            Vector2Int offset = a - b;
            return Math.Abs(offset.x) + Math.Abs(offset.y) == 1;
        }
    }
}
