using System;
using System.Collections.Generic;
using System.Reflection;
using Mathematics;

namespace CAD
{
    /// <summary>
    /// Populates a <see cref="CAD_Model"/> with an origin-shifted coordinate system,
    /// evenly spaced stations, and companion sketches anchored to each station.
    /// </summary>
    public sealed class CAD_ModelStationBuilder
    {
        private const double NumericTolerance = 1e-9;
        private static readonly string[] OriginPropertyCandidates = { "OriginPoint", "Origin", "Location", "BasePoint" };

        private readonly CAD_Model _model;

        public CAD_ModelStationBuilder(CAD_Model model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
        }

        /// <summary>The coordinate system created at the start offset.</summary>
        public CoordinateSystem StartPointCoordinateSystem { get; private set; }

        /// <summary>
        /// Adds stations and sketches along the model length while creating a start-point coordinate system
        /// offset from the world origin.
        /// </summary>
        /// <param name="modelLength">Total modeled length along the primary axis.</param>
        /// <param name="stationSpacing">Distance between consecutive stations.</param>
        /// <param name="startOffsetFromWorld">Offset from the world origin to the first station/coordinate system.</param>
        /// <param name="stationType">Station classification (axial, radial, etc.).</param>
        /// <param name="coordinateSystemName">Optional base name for generated coordinate systems.</param>
        public IReadOnlyList<CAD_Station> CreateStationsWithSketches(
            double modelLength,
            double stationSpacing,
            double startOffsetFromWorld,
            CAD_Station.StationTypeEnum stationType = CAD_Station.StationTypeEnum.Axial,
            string coordinateSystemName = null)
        {
            if (modelLength <= 0) throw new ArgumentOutOfRangeException(nameof(modelLength), "Model length must be positive.");
            if (stationSpacing <= 0) throw new ArgumentOutOfRangeException(nameof(stationSpacing), "Station spacing must be positive.");
            if (startOffsetFromWorld < 0) throw new ArgumentOutOfRangeException(nameof(startOffsetFromWorld), "Start offset cannot be negative.");
            if (startOffsetFromWorld >= modelLength) throw new ArgumentOutOfRangeException(nameof(startOffsetFromWorld), "Start offset must be less than the model length.");

            coordinateSystemName = string.IsNullOrWhiteSpace(coordinateSystemName)
                ? "StartPoint"
                : coordinateSystemName.Trim();

            var positions = BuildStationPositions(modelLength, stationSpacing, startOffsetFromWorld);
            if (positions.Count == 0)
            {
                throw new InvalidOperationException("No stations could be generated with the supplied parameters.");
            }

            StartPointCoordinateSystem = CreateCoordinateSystem($"{coordinateSystemName}_CS_0", positions[0]);

            var createdStations = new List<CAD_Station>(positions.Count);
            var baseIndex = _model.MyStations.Count;

            for (var i = 0; i < positions.Count; i++)
            {
                var position = positions[i];
                var station = CreateStation(baseIndex + i, stationType, position, coordinateSystemName, i == 0);
                createdStations.Add(station);
            }

            return createdStations;
        }

        private CAD_Station CreateStation(
            int sequentialIndex,
            CAD_Station.StationTypeEnum stationType,
            double position,
            string coordinateSystemName,
            bool useStartCoordinateSystem)
        {
            var stationId = $"STA_{sequentialIndex:000}";
            var station = new CAD_Station(stationId, stationType, position)
            {
                Name = $"Station_{position:0.###}",
                MyModel = _model
            };

            _model.AddStation(station);

            var coordinateSystem = !(!useStartCoordinateSystem || StartPointCoordinateSystem is null)
                ? StartPointCoordinateSystem
                : CreateCoordinateSystem($"{coordinateSystemName}_CS_{stationId}", position);

            var sketchPlane = CreateSketchPlane($"Plane_{stationId}", coordinateSystem);
            station.AddSketchPlane(sketchPlane);

            var sketch = CreateSketch($"Sketch_{stationId}", coordinateSystem, sketchPlane);
            sketchPlane.AddSketch(sketch);
            _model.AddSketch(sketch);

            return station;
        }

        private CAD_Sketch CreateSketch(string sketchId, CoordinateSystem coordinateSystem, CAD_SketchPlane sketchPlane)
        {
            var sketch = new CAD_Sketch(sketchId)
            {
                Version = _model.CurrentSketch?.Version ?? "1.0",
                MyModel = _model,
                MySketchPlane = sketchPlane,
                IsTwoD = true
            };

            sketch.AddCoordinateSystem(coordinateSystem);
            sketch.BaseCoordinateSystem = coordinateSystem;
            return sketch;
        }

        private CAD_SketchPlane CreateSketchPlane(string name, CoordinateSystem coordinateSystem)
        {
            var plane = new CAD_SketchPlane(name,
                                            CAD_SketchPlane.FunctionalTypeEnum.Section,
                                            CAD_SketchPlane.GeometryTypeEnum.Cartesian)
            {
                MyModel = _model,
                MyCoordinateSystem = coordinateSystem,
                IsWorkplane = true
            };

            plane.SetNormal(1, 0, 0);
            return plane;
        }

        private static CoordinateSystem CreateCoordinateSystem(string name, double xOffset)
        {
            var coordinateSystem = new CoordinateSystem();
            TrySetProperty(coordinateSystem, "Name", name);
            TrySetProperty(coordinateSystem, "ID", name);
            TrySetProperty(coordinateSystem, "Version", "1.0");
            AssignOrigin(coordinateSystem, xOffset);
            return coordinateSystem;
        }

        private static void AssignOrigin(CoordinateSystem coordinateSystem, double xOffset)
        {
            var originPoint = new Point
            {
                X_Value = xOffset,
                Y_Value = 0,
                Z_Value_Cartesian = 0
            };

            foreach (var candidate in OriginPropertyCandidates)
            {
                if (TrySetProperty(coordinateSystem, candidate, originPoint))
                {
                    return;
                }
            }

            var method = coordinateSystem.GetType()
                                         .GetMethod("SetOrigin", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            method?.Invoke(coordinateSystem, new object[] { originPoint });
        }

        private static bool TrySetProperty(object target, string propertyName, object value)
        {
            var property = target.GetType()
                                 .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property is null || !property.CanWrite)
            {
                return false;
            }

            if (!property.PropertyType.IsInstanceOfType(value))
            {
                return false;
            }

            property.SetValue(target, value);
            return true;
        }

        private static List<double> BuildStationPositions(double modelLength, double stationSpacing, double startOffsetFromWorld)
        {
            var positions = new List<double>();
            var position = startOffsetFromWorld;

            while (position <= modelLength + NumericTolerance)
            {
                positions.Add(Normalize(position));
                position += stationSpacing;
            }

            // Replace C# 8.0 index operator (^1) with explicit indexing for C# 7.3 compatibility
            if (positions.Count == 0 || Math.Abs(positions[positions.Count - 1] - modelLength) > NumericTolerance)
            {
                positions.Add(Normalize(modelLength));
            }

            return positions;
        }

        private static double Normalize(double value)
            => Math.Round(value, 6, MidpointRounding.AwayFromZero);
    }
}