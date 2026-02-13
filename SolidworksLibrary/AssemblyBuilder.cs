using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using SldWorks;
using SwConst;
using CAD;
using Mathematics;
using SE_Library;

namespace SolidworksLibrary
{
    public class AssemblyBuilder
    {
        // ================================================================
        // Instance state for database operations
        // ================================================================
        private readonly string _databasePath;
        private string ConnectionString => $"Data Source={_databasePath};Version=3;Foreign Keys=True;";

        /// <summary>
        /// Creates an AssemblyBuilder instance for SolidWorks operations only (no database).
        /// </summary>
        public AssemblyBuilder()
        {
            _databasePath = null;
        }

        /// <summary>
        /// Creates an AssemblyBuilder instance configured for database operations.
        /// </summary>
        /// <param name="databasePath">Full path to the SQLite database file.</param>
        public AssemblyBuilder(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("Database path must not be null or empty.", nameof(databasePath));
            _databasePath = databasePath;
        }

        // ================================================================
        // Database: Schema initialization
        // ================================================================

        /// <summary>
        /// Reads all *_Schema.sql files from <paramref name="sqlFolderPath"/> and executes
        /// them against the database to create or ensure tables, indexes, and views exist.
        /// </summary>
        public void InitializeSchema(string sqlFolderPath)
        {
            if (string.IsNullOrWhiteSpace(sqlFolderPath))
                throw new ArgumentException("SQL folder path must not be null or empty.", nameof(sqlFolderPath));
            if (!Directory.Exists(sqlFolderPath))
                throw new DirectoryNotFoundException($"SQL folder not found: {sqlFolderPath}");

            var sqlFiles = Directory.GetFiles(sqlFolderPath, "*_Schema.sql")
                                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                    .ToArray();

            if (sqlFiles.Length == 0)
                throw new FileNotFoundException($"No *_Schema.sql files found in: {sqlFolderPath}");

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                foreach (var sqlFile in sqlFiles)
                {
                    var sql = File.ReadAllText(sqlFile);
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        // ================================================================
        // Database: Write operations
        // ================================================================

        /// <summary>
        /// Persists one or more <see cref="CAD_Assembly"/> objects (and their full dependent
        /// entity graph) to the database.
        /// Uses INSERT OR REPLACE for idempotent upsert semantics.
        /// </summary>
        public void SaveAssemblies(IEnumerable<CAD_Assembly> assemblies)
        {
            if (assemblies == null) throw new ArgumentNullException(nameof(assemblies));

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var txn = conn.BeginTransaction())
                {
                    foreach (var asm in assemblies)
                    {
                        SaveAssemblyCore(conn, asm);
                    }
                    txn.Commit();
                }
            }
        }

        /// <summary>Convenience overload — persists a single assembly.</summary>
        public void SaveAssembly(CAD_Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            SaveAssemblies(new[] { assembly });
        }

        private void SaveAssemblyCore(SQLiteConnection conn, CAD_Assembly asm)
        {
            var assemblyId = asm.Name ?? GenerateId();

            // --- Ensure FK references ---

            string positionPointId = null;
            if (asm.MyPosition != null)
            {
                positionPointId = asm.MyPosition.PointID ?? GenerateId();
                EnsurePoint(conn, asm.MyPosition, positionPointId);
            }

            string orientationVectorId = null;
            if (asm.MyOrientation != null)
            {
                orientationVectorId = asm.MyOrientation.VectorID ?? GenerateId();
                EnsureVector(conn, asm.MyOrientation, orientationVectorId);
            }

            string currentCsId = null;
            if (asm.CurrentCS != null)
            {
                currentCsId = asm.CurrentCS.CoordinateSystemID
                              ?? asm.CurrentCS.Name
                              ?? GenerateId();
                EnsureCoordinateSystem(conn, asm.CurrentCS, currentCsId);
            }

            string currentComponentId = null;
            if (asm.CurrentComponent != null)
            {
                currentComponentId = asm.CurrentComponent.Name ?? GenerateId();
                EnsureComponent(conn, asm.CurrentComponent, currentComponentId);
            }

            string previousComponentId = null;
            if (asm.PreviousComponent != null)
            {
                previousComponentId = asm.PreviousComponent.Name ?? GenerateId();
                EnsureComponent(conn, asm.PreviousComponent, previousComponentId);
            }

            string nextComponentId = null;
            if (asm.NextComponent != null)
            {
                nextComponentId = asm.NextComponent.Name ?? GenerateId();
                EnsureComponent(conn, asm.NextComponent, nextComponentId);
            }

            string myModelId = null;
            if (asm.MyModel != null)
            {
                myModelId = asm.MyModel.Name ?? GenerateId();
                EnsureModel(conn, asm.MyModel, myModelId);
            }

            string currentConfigId = null;
            if (asm.CurrentConfiguration != null)
            {
                currentConfigId = asm.CurrentConfiguration.ID
                                  ?? asm.CurrentConfiguration.Name
                                  ?? GenerateId();
                EnsureConfiguration(conn, asm.CurrentConfiguration, currentConfigId);
            }

            string myPartId = null;
            if (asm.MyPart != null)
            {
                myPartId = asm.MyPart.Name ?? asm.MyPart.PartNumber ?? GenerateId();
                EnsurePart(conn, asm.MyPart, myPartId);
            }

            string currentInterfaceId = null;
            if (asm.CurrentInterface != null)
            {
                currentInterfaceId = asm.CurrentInterface.ID
                                     ?? asm.CurrentInterface.Name
                                     ?? GenerateId();
                EnsureInterface(conn, asm.CurrentInterface, currentInterfaceId);
            }

            // --- INSERT OR REPLACE the assembly row ---
            const string assemblySql =
                @"INSERT OR REPLACE INTO CAD_Assembly
                  (AssemblyID, Name, Version, Description,
                   IsSubAssembly, IsConfigurationItem,
                   MyPositionPointID, MyOrientationVectorID, CurrentCSID,
                   CurrentComponentID, PreviousComponentID, NextComponentID,
                   MyModelID, CurrentConfigurationID, MyPartID, CurrentInterfaceID)
                  VALUES
                  (@id, @name, @ver, @desc,
                   @sub, @cfg,
                   @posId, @oriId, @csId,
                   @curCompId, @prevCompId, @nextCompId,
                   @modelId, @configId, @partId, @ifaceId);";

            using (var cmd = new SQLiteCommand(assemblySql, conn))
            {
                cmd.Parameters.AddWithValue("@id", assemblyId);
                cmd.Parameters.AddWithValue("@name", (object)asm.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ver", (object)asm.Version ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@desc", (object)asm.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@sub", asm.IsSubAssembly ? 1 : 0);
                cmd.Parameters.AddWithValue("@cfg", asm.IsConfigurationItem ? 1 : 0);
                cmd.Parameters.AddWithValue("@posId", (object)positionPointId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@oriId", (object)orientationVectorId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@csId", (object)currentCsId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@curCompId", (object)currentComponentId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@prevCompId", (object)previousComponentId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@nextCompId", (object)nextComponentId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@modelId", (object)myModelId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@configId", (object)currentConfigId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@partId", (object)myPartId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ifaceId", (object)currentInterfaceId ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }

            // --- Junction tables: clear and re-insert each collection ---
            SaveAssemblyCoordinateSystems(conn, assemblyId, asm.MyCoordinateSystems);
            SaveAssemblyComponents(conn, assemblyId, asm.MyComponents);
            SaveAssemblyConfigurations(conn, assemblyId, asm.MyConfigurations);
            SaveAssemblyMissionRequirements(conn, assemblyId, asm.MissionRequirements);
            SaveAssemblySystemRequirements(conn, assemblyId, asm.SystemRequirements);
            SaveAssemblyInterfaces(conn, assemblyId, asm.MyInterfaces);
            SaveAssemblyStations(conn, assemblyId, "Axial", asm.AxialStations);
            SaveAssemblyStations(conn, assemblyId, "Radial", asm.RadialStations);
            SaveAssemblyStations(conn, assemblyId, "Angular", asm.AngularStations);
            SaveAssemblyStations(conn, assemblyId, "Wing", asm.WingStations);
        }

        // ================================================================
        // Database: Junction table save helpers
        // ================================================================

        private void SaveAssemblyCoordinateSystems(SQLiteConnection conn, string assemblyId,
            List<CoordinateSystem> coordinateSystems)
        {
            DeleteJunction(conn, "CAD_Assembly_CoordinateSystem", "AssemblyID", assemblyId);
            for (int i = 0; i < coordinateSystems.Count; i++)
            {
                var cs = coordinateSystems[i];
                var csId = cs.CoordinateSystemID ?? cs.Name ?? GenerateId();
                EnsureCoordinateSystem(conn, cs, csId);
                InsertJunction(conn, "CAD_Assembly_CoordinateSystem", "AssemblyID", assemblyId,
                    "CoordinateSystemID", csId, i);
            }
        }

        private void SaveAssemblyComponents(SQLiteConnection conn, string assemblyId,
            List<CAD_Component> components)
        {
            DeleteJunction(conn, "CAD_Assembly_Component", "AssemblyID", assemblyId);
            for (int i = 0; i < components.Count; i++)
            {
                var comp = components[i];
                var compId = comp.Name ?? GenerateId();
                EnsureComponent(conn, comp, compId);
                InsertJunction(conn, "CAD_Assembly_Component", "AssemblyID", assemblyId,
                    "ComponentID", compId, i);
            }
        }

        private void SaveAssemblyConfigurations(SQLiteConnection conn, string assemblyId,
            List<CAD_Configuration> configurations)
        {
            DeleteJunction(conn, "CAD_Assembly_Configuration", "AssemblyID", assemblyId);
            for (int i = 0; i < configurations.Count; i++)
            {
                var config = configurations[i];
                var configId = config.ID ?? config.Name ?? GenerateId();
                EnsureConfiguration(conn, config, configId);
                InsertJunction(conn, "CAD_Assembly_Configuration", "AssemblyID", assemblyId,
                    "ConfigurationID", configId, i);
            }
        }

        private void SaveAssemblyMissionRequirements(SQLiteConnection conn, string assemblyId,
            List<MissionRequirement> requirements)
        {
            DeleteJunction(conn, "CAD_Assembly_MissionRequirement", "AssemblyID", assemblyId);
            for (int i = 0; i < requirements.Count; i++)
            {
                var req = requirements[i];
                var reqId = req.GetID() ?? req.Name ?? GenerateId();
                EnsureMissionRequirement(conn, req, reqId);
                InsertJunction(conn, "CAD_Assembly_MissionRequirement", "AssemblyID", assemblyId,
                    "MissionRequirementID", reqId, i);
            }
        }

        private void SaveAssemblySystemRequirements(SQLiteConnection conn, string assemblyId,
            List<SystemRequirement> requirements)
        {
            DeleteJunction(conn, "CAD_Assembly_SystemRequirement", "AssemblyID", assemblyId);
            for (int i = 0; i < requirements.Count; i++)
            {
                var req = requirements[i];
                var reqId = req.GetID() ?? req.GetTitle() ?? GenerateId();
                EnsureSystemRequirement(conn, req, reqId);
                InsertJunction(conn, "CAD_Assembly_SystemRequirement", "AssemblyID", assemblyId,
                    "SystemRequirementID", reqId, i);
            }
        }

        private void SaveAssemblyInterfaces(SQLiteConnection conn, string assemblyId,
            List<CAD_Interface> interfaces)
        {
            DeleteJunction(conn, "CAD_Assembly_Interface", "AssemblyID", assemblyId);
            for (int i = 0; i < interfaces.Count; i++)
            {
                var iface = interfaces[i];
                var ifId = iface.ID ?? iface.Name ?? GenerateId();
                EnsureInterface(conn, iface, ifId);
                InsertJunction(conn, "CAD_Assembly_Interface", "AssemblyID", assemblyId,
                    "InterfaceID", ifId, i);
            }
        }

        private void SaveAssemblyStations(SQLiteConnection conn, string assemblyId,
            string category, List<CAD_Station> stations)
        {
            using (var cmd = new SQLiteCommand(
                "DELETE FROM CAD_Assembly_Station WHERE AssemblyID = @aid AND StationCategory = @cat;", conn))
            {
                cmd.Parameters.AddWithValue("@aid", assemblyId);
                cmd.Parameters.AddWithValue("@cat", category);
                cmd.ExecuteNonQuery();
            }

            for (int i = 0; i < stations.Count; i++)
            {
                var st = stations[i];
                var stId = st.ID ?? GenerateId();
                EnsureStation(conn, st, stId);

                using (var cmd = new SQLiteCommand(
                    @"INSERT INTO CAD_Assembly_Station (AssemblyID, StationID, StationCategory, SortOrder)
                      VALUES (@aid, @sid, @cat, @ord);", conn))
                {
                    cmd.Parameters.AddWithValue("@aid", assemblyId);
                    cmd.Parameters.AddWithValue("@sid", stId);
                    cmd.Parameters.AddWithValue("@cat", category);
                    cmd.Parameters.AddWithValue("@ord", i);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ================================================================
        // Database: Read operations
        // ================================================================

        /// <summary>
        /// Loads all assemblies from the database.
        /// Reconstructs the full object graph.
        /// </summary>
        public List<CAD_Assembly> LoadAssemblies()
        {
            var assemblies = new List<CAD_Assembly>();

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                const string sql = "SELECT * FROM CAD_Assembly ORDER BY AssemblyID;";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var asm = ReadAssemblyFromRow(reader);
                            assemblies.Add(asm);
                        }
                    }
                }

                foreach (var asm in assemblies)
                {
                    var asmId = asm.Name;
                    if (asmId == null) continue;
                    LoadAssemblyChildren(conn, asm, asmId);
                }
            }

            return assemblies;
        }

        /// <summary>Loads a single assembly by its ID.</summary>
        public CAD_Assembly LoadAssembly(string assemblyId)
        {
            if (string.IsNullOrWhiteSpace(assemblyId))
                throw new ArgumentException("Assembly ID must not be null or empty.", nameof(assemblyId));

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                CAD_Assembly asm = null;
                using (var cmd = new SQLiteCommand(
                    "SELECT * FROM CAD_Assembly WHERE AssemblyID = @id;", conn))
                {
                    cmd.Parameters.AddWithValue("@id", assemblyId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                            asm = ReadAssemblyFromRow(reader);
                    }
                }

                if (asm == null) return null;

                LoadAssemblyChildren(conn, asm, assemblyId);
                return asm;
            }
        }

        // ================================================================
        // Database: Private helpers — Read
        // ================================================================

        private static CAD_Assembly ReadAssemblyFromRow(SQLiteDataReader reader)
        {
            return new CAD_Assembly
            {
                Name = reader["Name"] as string,
                Version = reader["Version"] as string,
                Description = reader["Description"] as string,
                IsSubAssembly = Convert.ToInt32(reader["IsSubAssembly"]) != 0,
                IsConfigurationItem = Convert.ToInt32(reader["IsConfigurationItem"]) != 0
            };
        }

        private void LoadAssemblyChildren(SQLiteConnection conn, CAD_Assembly asm, string assemblyId)
        {
            // Load MyPosition point
            var posId = GetScalar(conn,
                "SELECT MyPositionPointID FROM CAD_Assembly WHERE AssemblyID = @id;", assemblyId);
            if (posId != null)
                asm.MyPosition = LoadPoint(conn, posId);

            // Load MyOrientation vector
            var oriId = GetScalar(conn,
                "SELECT MyOrientationVectorID FROM CAD_Assembly WHERE AssemblyID = @id;", assemblyId);
            if (oriId != null)
                asm.MyOrientation = LoadVector(conn, oriId);

            // Load CurrentCS
            var csId = GetScalar(conn,
                "SELECT CurrentCSID FROM CAD_Assembly WHERE AssemblyID = @id;", assemblyId);
            if (csId != null)
                asm.CurrentCS = LoadCoordinateSystem(conn, csId);

            // Load CurrentComponent
            var curCompId = GetScalar(conn,
                "SELECT CurrentComponentID FROM CAD_Assembly WHERE AssemblyID = @id;", assemblyId);
            if (curCompId != null)
                asm.CurrentComponent = LoadComponentStub(conn, curCompId);

            // Load PreviousComponent
            var prevCompId = GetScalar(conn,
                "SELECT PreviousComponentID FROM CAD_Assembly WHERE AssemblyID = @id;", assemblyId);
            if (prevCompId != null)
                asm.PreviousComponent = LoadComponentStub(conn, prevCompId);

            // Load NextComponent
            var nextCompId = GetScalar(conn,
                "SELECT NextComponentID FROM CAD_Assembly WHERE AssemblyID = @id;", assemblyId);
            if (nextCompId != null)
                asm.NextComponent = LoadComponentStub(conn, nextCompId);

            // Load MyModel
            var modelId = GetScalar(conn,
                "SELECT MyModelID FROM CAD_Assembly WHERE AssemblyID = @id;", assemblyId);
            if (modelId != null)
                asm.MyModel = LoadModel(conn, modelId);

            // Load CurrentConfiguration
            var configId = GetScalar(conn,
                "SELECT CurrentConfigurationID FROM CAD_Assembly WHERE AssemblyID = @id;", assemblyId);
            if (configId != null)
                asm.CurrentConfiguration = LoadConfigurationStub(conn, configId);

            // Load MyPart
            var partId = GetScalar(conn,
                "SELECT MyPartID FROM CAD_Assembly WHERE AssemblyID = @id;", assemblyId);
            if (partId != null)
                asm.MyPart = LoadPartStub(conn, partId);

            // Load CurrentInterface
            var ifaceId = GetScalar(conn,
                "SELECT CurrentInterfaceID FROM CAD_Assembly WHERE AssemblyID = @id;", assemblyId);
            if (ifaceId != null)
                asm.CurrentInterface = LoadInterfaceStub(conn, ifaceId);

            // Load collections
            LoadAssemblyCoordinateSystemCollection(conn, asm, assemblyId);
            LoadAssemblyComponentCollection(conn, asm, assemblyId);
            LoadAssemblyConfigurationCollection(conn, asm, assemblyId);
            LoadAssemblyMissionRequirementCollection(conn, asm, assemblyId);
            LoadAssemblySystemRequirementCollection(conn, asm, assemblyId);
            LoadAssemblyInterfaceCollection(conn, asm, assemblyId);
            LoadAssemblyStationCollection(conn, asm, assemblyId, "Axial", asm.AxialStations);
            LoadAssemblyStationCollection(conn, asm, assemblyId, "Radial", asm.RadialStations);
            LoadAssemblyStationCollection(conn, asm, assemblyId, "Angular", asm.AngularStations);
            LoadAssemblyStationCollection(conn, asm, assemblyId, "Wing", asm.WingStations);
        }

        // ================================================================
        // Database: Collection loaders
        // ================================================================

        private void LoadAssemblyCoordinateSystemCollection(SQLiteConnection conn, CAD_Assembly asm,
            string assemblyId)
        {
            const string sql =
                @"SELECT cs.* FROM CAD_Assembly_CoordinateSystem acs
                  JOIN CoordinateSystem cs ON acs.CoordinateSystemID = cs.CoordinateSystemID
                  WHERE acs.AssemblyID = @id ORDER BY acs.SortOrder;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", assemblyId);
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        asm.MyCoordinateSystems.Add(ReadCoordinateSystemFromRow(conn, reader));
            }
        }

        private void LoadAssemblyComponentCollection(SQLiteConnection conn, CAD_Assembly asm,
            string assemblyId)
        {
            const string sql =
                @"SELECT c.* FROM CAD_Assembly_Component ac
                  JOIN CAD_Component c ON ac.ComponentID = c.ComponentID
                  WHERE ac.AssemblyID = @id ORDER BY ac.SortOrder;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", assemblyId);
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        asm.MyComponents.Add(ReadComponentStubFromRow(reader));
            }
        }

        private void LoadAssemblyConfigurationCollection(SQLiteConnection conn, CAD_Assembly asm,
            string assemblyId)
        {
            const string sql =
                @"SELECT cfg.* FROM CAD_Assembly_Configuration acfg
                  JOIN CAD_Configuration cfg ON acfg.ConfigurationID = cfg.ConfigurationID
                  WHERE acfg.AssemblyID = @id ORDER BY acfg.SortOrder;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", assemblyId);
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        asm.MyConfigurations.Add(ReadConfigurationStubFromRow(reader));
            }
        }

        private void LoadAssemblyMissionRequirementCollection(SQLiteConnection conn, CAD_Assembly asm,
            string assemblyId)
        {
            const string sql =
                @"SELECT mr.* FROM CAD_Assembly_MissionRequirement amr
                  JOIN MissionRequirement mr ON amr.MissionRequirementID = mr.MissionRequirementID
                  WHERE amr.AssemblyID = @id ORDER BY amr.SortOrder;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", assemblyId);
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        asm.MissionRequirements.Add(ReadMissionRequirementFromRow(reader));
            }
        }

        private void LoadAssemblySystemRequirementCollection(SQLiteConnection conn, CAD_Assembly asm,
            string assemblyId)
        {
            const string sql =
                @"SELECT sr.* FROM CAD_Assembly_SystemRequirement asr
                  JOIN SystemRequirement sr ON asr.SystemRequirementID = sr.SystemRequirementID
                  WHERE asr.AssemblyID = @id ORDER BY asr.SortOrder;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", assemblyId);
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        asm.SystemRequirements.Add(ReadSystemRequirementFromRow(reader));
            }
        }

        private void LoadAssemblyInterfaceCollection(SQLiteConnection conn, CAD_Assembly asm,
            string assemblyId)
        {
            const string sql =
                @"SELECT i.* FROM CAD_Assembly_Interface ai
                  JOIN CAD_Interface i ON ai.InterfaceID = i.InterfaceID
                  WHERE ai.AssemblyID = @id ORDER BY ai.SortOrder;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", assemblyId);
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        asm.MyInterfaces.Add(ReadInterfaceStubFromRow(reader));
            }
        }

        private void LoadAssemblyStationCollection(SQLiteConnection conn, CAD_Assembly asm,
            string assemblyId, string category, List<CAD_Station> targetList)
        {
            const string sql =
                @"SELECT st.* FROM CAD_Assembly_Station ast
                  JOIN CAD_Station st ON ast.StationID = st.StationID
                  WHERE ast.AssemblyID = @id AND ast.StationCategory = @cat
                  ORDER BY ast.SortOrder;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", assemblyId);
                cmd.Parameters.AddWithValue("@cat", category);
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        targetList.Add(ReadStationStubFromRow(reader));
            }
        }

        // ================================================================
        // Database: Row-to-object readers
        // ================================================================

        private static CAD_Component ReadComponentStubFromRow(SQLiteDataReader reader)
        {
            return new CAD_Component
            {
                Name = reader["Name"] as string,
                Version = reader["Version"] as string,
                Path = reader["Path"] as string,
                IsAssembly = Convert.ToInt32(reader["IsAssembly"]) != 0,
                IsConfigurationItem = Convert.ToInt32(reader["IsConfigurationItem"]) != 0,
                WBS_Level = Convert.ToInt32(reader["WBS_Level"])
            };
        }

        private static CAD_Configuration ReadConfigurationStubFromRow(SQLiteDataReader reader)
        {
            return new CAD_Configuration
            {
                Name = reader["Name"] as string,
                Description = reader["Description"] as string,
                ID = reader["ConfigurationID"] as string,
                Revision = reader["Revision"] as string
            };
        }

        private static MissionRequirement ReadMissionRequirementFromRow(SQLiteDataReader reader)
        {
            var id = reader["MissionRequirementID"] as string;
            var description = reader["Description"] as string;
            var mr = new MissionRequirement(id, description);
            mr.Name = reader["Name"] as string;
            var reqType = reader["RequirementType"];
            if (reqType != null && reqType != DBNull.Value)
                mr.MyType = (Requirement.RequirementTypeEnum)Convert.ToInt32(reqType);
            return mr;
        }

        private static SystemRequirement ReadSystemRequirementFromRow(SQLiteDataReader reader)
        {
            var id = reader["SystemRequirementID"] as string;
            var name = reader["Name"] as string;
            return new SystemRequirement(id, name);
        }

        private static CAD_Interface ReadInterfaceStubFromRow(SQLiteDataReader reader)
        {
            return new CAD_Interface
            {
                Name = reader["Name"] as string,
                ID = reader["ID"] as string,
                Version = reader["Version"] as string,
                InterfaceKind = (CAD_Interface.InterfaceType)
                    Convert.ToInt32(reader["InterfaceKind"] ?? 0)
            };
        }

        private static CAD_Station ReadStationStubFromRow(SQLiteDataReader reader)
        {
            var stationType = (CAD_Station.StationTypeEnum)Convert.ToInt32(reader["MyType"]);
            double locationValue = 0;
            switch (stationType)
            {
                case CAD_Station.StationTypeEnum.Axial:
                    locationValue = Convert.ToDouble(reader["AxialLocation"]); break;
                case CAD_Station.StationTypeEnum.Radial:
                    locationValue = Convert.ToDouble(reader["RadialLocation"]); break;
                case CAD_Station.StationTypeEnum.Angular:
                    locationValue = Convert.ToDouble(reader["AngularLocation"]); break;
                case CAD_Station.StationTypeEnum.Wing:
                    locationValue = Convert.ToDouble(reader["WingLocation"]); break;
            }

            var station = new CAD_Station(null, reader["StationID"] as string, stationType)
            {
                Name = reader["Name"] as string,
                Version = reader["Version"] as string
            };
            station.SetLocation(stationType, locationValue);
            return station;
        }

        private static CAD_Model ReadModelFromRow(SQLiteDataReader reader)
        {
            return new CAD_Model
            {
                Name = reader["Name"] as string,
                Version = reader["Version"] as string,
                Description = reader["Description"] as string,
                FilePath = reader["FilePath"] as string,
                CAD_AppName = (CAD_Model.CAD_AppEnum)Convert.ToInt32(reader["CAD_AppName"]),
                ModelType = (CAD_Model.CAD_ModelTypeEnum)Convert.ToInt32(reader["ModelType"]),
                FileType = (CAD_Model.CAD_FileTypeEnum)Convert.ToInt32(reader["FileType"])
            };
        }

        private CoordinateSystem ReadCoordinateSystemFromRow(SQLiteConnection conn,
            SQLiteDataReader reader)
        {
            var cs = new CoordinateSystem
            {
                CoordinateSystemID = reader["CoordinateSystemID"] as string,
                Name = reader["Name"] as string,
                MyType = (CoordinateSystem.CoordinateSystemTypeEnum)
                    Convert.ToInt32(reader["MyType"]),
                IsWCS = Convert.ToInt32(reader["IsWCS"]) != 0,
                Is2D = Convert.ToInt32(reader["Is2D"]) != 0
            };
            var originId = reader["OriginLocationPointID"] as string;
            if (originId != null) cs.OriginLocation = LoadPoint(conn, originId);
            var baseVecId = reader["BaseVectorID"] as string;
            if (baseVecId != null) cs.BaseVector = LoadVector(conn, baseVecId);
            return cs;
        }

        private static Point ReadPointFromRow(SQLiteDataReader reader)
        {
            return new Point
            {
                PointID = reader["PointID"] as string,
                IsWeightPoint = Convert.ToInt32(reader["IsWeightPoint"]) != 0,
                MyType = (Point.PointTypeEnum)Convert.ToInt32(reader["MyType"]),
                Is2D = Convert.ToInt32(reader["Is2D"]) != 0,
                X_Value = Convert.ToDouble(reader["X_Value"]),
                Y_Value = Convert.ToDouble(reader["Y_Value"]),
                Z_Value_Cartesian = Convert.ToDouble(reader["Z_Value_Cartesian"]),
                R_Value_Cylindrical = Convert.ToDouble(reader["R_Value_Cylindrical"]),
                Theta_Value_Cylindrical = Convert.ToDouble(reader["Theta_Value_Cylindrical"]),
                Z_Value_Cylindrical = Convert.ToDouble(reader["Z_Value_Cylindrical"]),
                R_Value_Spherical = Convert.ToDouble(reader["R_Value_Spherical"]),
                Theta_Value_Spherical = Convert.ToDouble(reader["Theta_Value_Spherical"]),
                Phi_Value = Convert.ToDouble(reader["Phi_Value"]),
                Longitude = Convert.ToDouble(reader["Longitude"]),
                Latitude = Convert.ToDouble(reader["Latitude"]),
                Altitude = Convert.ToDouble(reader["Altitude"]),
                Real_Value = Convert.ToDouble(reader["Real_Value"]),
                Complex_Value = Convert.ToDouble(reader["Complex_Value"])
            };
        }

        // ================================================================
        // Database: Shared entity loaders
        // ================================================================

        private CAD_Component LoadComponentStub(SQLiteConnection conn, string compId)
        {
            const string sql = "SELECT * FROM CAD_Component WHERE ComponentID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", compId);
                using (var reader = cmd.ExecuteReader())
                    return reader.Read() ? ReadComponentStubFromRow(reader) : null;
            }
        }

        private CAD_Configuration LoadConfigurationStub(SQLiteConnection conn, string configId)
        {
            const string sql = "SELECT * FROM CAD_Configuration WHERE ConfigurationID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", configId);
                using (var reader = cmd.ExecuteReader())
                    return reader.Read() ? ReadConfigurationStubFromRow(reader) : null;
            }
        }

        private CAD_Part LoadPartStub(SQLiteConnection conn, string partId)
        {
            const string sql = "SELECT * FROM CAD_Part WHERE PartID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", partId);
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return new CAD_Part
                    {
                        Name = reader["Name"] as string,
                        Version = reader["Version"] as string,
                        PartNumber = reader["PartNumber"] as string,
                        Description = reader["Description"] as string
                    };
                }
            }
        }

        private CAD_Interface LoadInterfaceStub(SQLiteConnection conn, string ifaceId)
        {
            const string sql = "SELECT * FROM CAD_Interface WHERE InterfaceID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", ifaceId);
                using (var reader = cmd.ExecuteReader())
                    return reader.Read() ? ReadInterfaceStubFromRow(reader) : null;
            }
        }

        private Point LoadPoint(SQLiteConnection conn, string pointId)
        {
            const string sql = "SELECT * FROM Point WHERE PointID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", pointId);
                using (var reader = cmd.ExecuteReader())
                    return reader.Read() ? ReadPointFromRow(reader) : null;
            }
        }

        private Vector LoadVector(SQLiteConnection conn, string vectorId)
        {
            const string sql = "SELECT * FROM Vector WHERE VectorID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", vectorId);
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    var v = new Vector
                    {
                        VectorID = reader["VectorID"] as string,
                        Name = reader["Name"] as string,
                        IsKnotVector = Convert.ToInt32(reader["IsKnotVector"]) != 0,
                        VectorType = (Vector.VectorTypeEnum)Convert.ToInt32(reader["VectorType"]),
                        X_Value = Convert.ToDouble(reader["X_Value"]),
                        Y_Value = Convert.ToDouble(reader["Y_Value"]),
                        Z_Value = Convert.ToDouble(reader["Z_Value"]),
                        Cyl_R = Convert.ToDouble(reader["Cyl_R"]),
                        Cyl_Theta = Convert.ToDouble(reader["Cyl_Theta"]),
                        L = Convert.ToDouble(reader["L"]),
                        Sph_R = Convert.ToDouble(reader["Sph_R"]),
                        Sph_Theta = Convert.ToDouble(reader["Sph_Theta"]),
                        Phi = Convert.ToDouble(reader["Phi"])
                    };
                    var startPtId = reader["StartPointID"] as string;
                    if (startPtId != null) v.StartPoint = LoadPoint(conn, startPtId);
                    var endPtId = reader["EndPointID"] as string;
                    if (endPtId != null) v.EndPoint = LoadPoint(conn, endPtId);
                    return v;
                }
            }
        }

        private CoordinateSystem LoadCoordinateSystem(SQLiteConnection conn, string csId)
        {
            const string sql = "SELECT * FROM CoordinateSystem WHERE CoordinateSystemID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", csId);
                using (var reader = cmd.ExecuteReader())
                    return reader.Read() ? ReadCoordinateSystemFromRow(conn, reader) : null;
            }
        }

        private CAD_Model LoadModel(SQLiteConnection conn, string modelId)
        {
            const string sql = "SELECT * FROM CAD_Model WHERE ModelID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", modelId);
                using (var reader = cmd.ExecuteReader())
                    return reader.Read() ? ReadModelFromRow(reader) : null;
            }
        }

        // ================================================================
        // Database: Private helpers — Write (Ensure* methods)
        // ================================================================

        private static void EnsureComponent(SQLiteConnection conn, CAD_Component comp, string compId)
        {
            const string sql =
                @"INSERT OR IGNORE INTO CAD_Component
                  (ComponentID, Name, Version, Path, IsAssembly, IsConfigurationItem, WBS_Level)
                  VALUES (@id, @name, @ver, @path, @isAsm, @isCfg, @wbs);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", compId);
                cmd.Parameters.AddWithValue("@name", (object)comp.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ver", (object)comp.Version ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@path", (object)comp.Path ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@isAsm", comp.IsAssembly ? 1 : 0);
                cmd.Parameters.AddWithValue("@isCfg", comp.IsConfigurationItem ? 1 : 0);
                cmd.Parameters.AddWithValue("@wbs", comp.WBS_Level);
                cmd.ExecuteNonQuery();
            }
        }

        private static void EnsureConfiguration(SQLiteConnection conn, CAD_Configuration config,
            string configId)
        {
            const string sql =
                @"INSERT OR IGNORE INTO CAD_Configuration
                  (ConfigurationID, Name, Description, Revision)
                  VALUES (@id, @name, @desc, @rev);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", configId);
                cmd.Parameters.AddWithValue("@name", (object)config.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@desc", (object)config.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@rev", (object)config.Revision ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        private static void EnsureMissionRequirement(SQLiteConnection conn, MissionRequirement req,
            string reqId)
        {
            const string sql =
                @"INSERT OR IGNORE INTO MissionRequirement
                  (MissionRequirementID, Name, Description, RequirementType)
                  VALUES (@id, @name, @desc, @type);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", reqId);
                cmd.Parameters.AddWithValue("@name", (object)req.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@desc", (object)req.GetTitle() ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@type", (int)req.MyType);
                cmd.ExecuteNonQuery();
            }
        }

        private static void EnsureSystemRequirement(SQLiteConnection conn, SystemRequirement req,
            string reqId)
        {
            const string sql =
                @"INSERT OR IGNORE INTO SystemRequirement
                  (SystemRequirementID, Name, Description)
                  VALUES (@id, @name, @desc);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", reqId);
                cmd.Parameters.AddWithValue("@name", (object)req.GetTitle() ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@desc", DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        private static void EnsurePart(SQLiteConnection conn, CAD_Part part, string partId)
        {
            const string sql =
                @"INSERT OR IGNORE INTO CAD_Part
                  (PartID, Name, Version, PartNumber, Description)
                  VALUES (@id, @name, @ver, @pn, @desc);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", partId);
                cmd.Parameters.AddWithValue("@name", (object)part.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ver", (object)part.Version ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@pn", (object)part.PartNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@desc", (object)part.Description ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        private void EnsureModel(SQLiteConnection conn, CAD_Model model, string modelId)
        {
            const string sql =
                @"INSERT OR IGNORE INTO CAD_Model
                  (ModelID, Name, Version, Description, FilePath, CAD_AppName, ModelType, FileType)
                  VALUES (@id, @name, @ver, @desc, @fp, @app, @mt, @ft);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", modelId);
                cmd.Parameters.AddWithValue("@name", (object)model.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ver", (object)model.Version ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@desc", (object)model.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fp", (object)model.FilePath ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@app", (int)model.CAD_AppName);
                cmd.Parameters.AddWithValue("@mt", (int)model.ModelType);
                cmd.Parameters.AddWithValue("@ft", (int)model.FileType);
                cmd.ExecuteNonQuery();
            }
        }

        private void EnsureCoordinateSystem(SQLiteConnection conn, CoordinateSystem cs, string csId)
        {
            string originPtId = null;
            if (cs.OriginLocation != null)
            {
                originPtId = cs.OriginLocation.PointID ?? GenerateId();
                EnsurePoint(conn, cs.OriginLocation, originPtId);
            }
            string baseVecId = null;
            if (cs.BaseVector != null)
            {
                baseVecId = cs.BaseVector.VectorID ?? GenerateId();
                EnsureVector(conn, cs.BaseVector, baseVecId);
            }
            const string sql =
                @"INSERT OR IGNORE INTO CoordinateSystem
                  (CoordinateSystemID, Name, MyType, IsWCS, Is2D,
                   OriginLocationPointID, BaseVectorID)
                  VALUES (@id, @name, @type, @wcs, @is2d, @originId, @baseVecId);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", csId);
                cmd.Parameters.AddWithValue("@name", (object)cs.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@type", (int)cs.MyType);
                cmd.Parameters.AddWithValue("@wcs", cs.IsWCS ? 1 : 0);
                cmd.Parameters.AddWithValue("@is2d", cs.Is2D ? 1 : 0);
                cmd.Parameters.AddWithValue("@originId", (object)originPtId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@baseVecId", (object)baseVecId ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        private void EnsureVector(SQLiteConnection conn, Vector vector, string vectorId)
        {
            string startPtId = null;
            if (vector.StartPoint != null)
            {
                startPtId = vector.StartPoint.PointID ?? GenerateId();
                EnsurePoint(conn, vector.StartPoint, startPtId);
            }
            string endPtId = null;
            if (vector.EndPoint != null)
            {
                endPtId = vector.EndPoint.PointID ?? GenerateId();
                EnsurePoint(conn, vector.EndPoint, endPtId);
            }
            const string sql =
                @"INSERT OR IGNORE INTO Vector
                  (VectorID, Name, IsKnotVector, VectorType,
                   X_Value, Y_Value, Z_Value, Cyl_R, Cyl_Theta, L,
                   Sph_R, Sph_Theta, Phi, StartPointID, EndPointID)
                  VALUES
                  (@id, @name, @knot, @type,
                   @x, @y, @z, @cr, @ct, @l,
                   @sr, @st, @phi, @startId, @endId);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", vectorId);
                cmd.Parameters.AddWithValue("@name", (object)vector.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@knot", vector.IsKnotVector ? 1 : 0);
                cmd.Parameters.AddWithValue("@type", (int)vector.VectorType);
                cmd.Parameters.AddWithValue("@x", vector.X_Value);
                cmd.Parameters.AddWithValue("@y", vector.Y_Value);
                cmd.Parameters.AddWithValue("@z", vector.Z_Value);
                cmd.Parameters.AddWithValue("@cr", vector.Cyl_R);
                cmd.Parameters.AddWithValue("@ct", vector.Cyl_Theta);
                cmd.Parameters.AddWithValue("@l", vector.L);
                cmd.Parameters.AddWithValue("@sr", vector.Sph_R);
                cmd.Parameters.AddWithValue("@st", vector.Sph_Theta);
                cmd.Parameters.AddWithValue("@phi", vector.Phi);
                cmd.Parameters.AddWithValue("@startId", (object)startPtId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@endId", (object)endPtId ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        private static void EnsurePoint(SQLiteConnection conn, Point point, string pointId)
        {
            const string sql =
                @"INSERT OR IGNORE INTO Point
                  (PointID, IsWeightPoint, MyType, Is2D,
                   X_Value, Y_Value, Z_Value_Cartesian,
                   R_Value_Cylindrical, Theta_Value_Cylindrical, Z_Value_Cylindrical,
                   R_Value_Spherical, Theta_Value_Spherical, Phi_Value,
                   Longitude, Latitude, Altitude, Real_Value, Complex_Value)
                  VALUES
                  (@id, @wpt, @type, @is2d,
                   @x, @y, @zc, @rc, @tc, @zcyl,
                   @rs, @ts, @phi, @lon, @lat, @alt, @real, @cplx);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", pointId);
                cmd.Parameters.AddWithValue("@wpt", point.IsWeightPoint ? 1 : 0);
                cmd.Parameters.AddWithValue("@type", (int)point.MyType);
                cmd.Parameters.AddWithValue("@is2d", point.Is2D ? 1 : 0);
                cmd.Parameters.AddWithValue("@x", point.X_Value);
                cmd.Parameters.AddWithValue("@y", point.Y_Value);
                cmd.Parameters.AddWithValue("@zc", point.Z_Value_Cartesian);
                cmd.Parameters.AddWithValue("@rc", point.R_Value_Cylindrical);
                cmd.Parameters.AddWithValue("@tc", point.Theta_Value_Cylindrical);
                cmd.Parameters.AddWithValue("@zcyl", point.Z_Value_Cylindrical);
                cmd.Parameters.AddWithValue("@rs", point.R_Value_Spherical);
                cmd.Parameters.AddWithValue("@ts", point.Theta_Value_Spherical);
                cmd.Parameters.AddWithValue("@phi", point.Phi_Value);
                cmd.Parameters.AddWithValue("@lon", point.Longitude);
                cmd.Parameters.AddWithValue("@lat", point.Latitude);
                cmd.Parameters.AddWithValue("@alt", point.Altitude);
                cmd.Parameters.AddWithValue("@real", point.Real_Value);
                cmd.Parameters.AddWithValue("@cplx", point.Complex_Value);
                cmd.ExecuteNonQuery();
            }
        }

        private static void EnsureStation(SQLiteConnection conn, CAD_Station station, string stationId)
        {
            const string sql =
                @"INSERT OR IGNORE INTO CAD_Station
                  (StationID, Name, Version, MyType,
                   AxialLocation, RadialLocation, AngularLocation,
                   WingLocation, FloorLocation)
                  VALUES (@id, @name, @ver, @type,
                          @axial, @radial, @angular, @wing, @floor);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", stationId);
                cmd.Parameters.AddWithValue("@name", (object)station.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ver", (object)station.Version ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@type", (int)station.MyType);
                cmd.Parameters.AddWithValue("@axial", station.AxialLocation);
                cmd.Parameters.AddWithValue("@radial", station.RadialLocation);
                cmd.Parameters.AddWithValue("@angular", station.AngularLocation);
                cmd.Parameters.AddWithValue("@wing", station.WingLocation);
                cmd.Parameters.AddWithValue("@floor", station.FloorLocation);
                cmd.ExecuteNonQuery();
            }
        }

        private static void EnsureInterface(SQLiteConnection conn, CAD_Interface iface, string ifaceId)
        {
            const string sql =
                @"INSERT OR IGNORE INTO CAD_Interface
                  (InterfaceID, Name, ID, Version, InterfaceKind)
                  VALUES (@id, @name, @uid, @ver, @kind);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", ifaceId);
                cmd.Parameters.AddWithValue("@name", (object)iface.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@uid", (object)iface.ID ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ver", (object)iface.Version ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@kind", (int)iface.InterfaceKind);
                cmd.ExecuteNonQuery();
            }
        }

        // ================================================================
        // Database: Generic helpers
        // ================================================================

        private static void DeleteJunction(SQLiteConnection conn, string table,
            string keyCol, string keyVal)
        {
            using (var cmd = new SQLiteCommand($"DELETE FROM {table} WHERE {keyCol} = @id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", keyVal);
                cmd.ExecuteNonQuery();
            }
        }

        private static void InsertJunction(SQLiteConnection conn, string table,
            string parentCol, string parentVal,
            string childCol, string childVal, int sortOrder)
        {
            using (var cmd = new SQLiteCommand(
                $@"INSERT INTO {table} ({parentCol}, {childCol}, SortOrder)
                   VALUES (@pid, @cid, @ord);", conn))
            {
                cmd.Parameters.AddWithValue("@pid", parentVal);
                cmd.Parameters.AddWithValue("@cid", childVal);
                cmd.Parameters.AddWithValue("@ord", sortOrder);
                cmd.ExecuteNonQuery();
            }
        }

        private static string GetScalar(SQLiteConnection conn, string sql, string id)
        {
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                return cmd.ExecuteScalar() as string;
            }
        }

        private static string GenerateId() => Guid.NewGuid().ToString("N");

        // ================================================================
        // SolidWorks: Assembly Document Creation
        // ================================================================

        public AssemblyDoc CreateAssemblyDocument(SldWorks.SldWorks swApp, out ModelDoc2 modelDoc)
        {
            string assemblyTemplate = swApp.GetUserPreferenceStringValue(
                (int)swUserPreferenceStringValue_e.swDefaultTemplateAssembly);

            if (string.IsNullOrEmpty(assemblyTemplate))
            {
                throw new InvalidOperationException("Assembly template not found in SolidWorks settings.");
            }

            object model = swApp.NewDocument(assemblyTemplate, 0, 0, 0);
            if (model == null)
            {
                throw new InvalidOperationException("Failed to create assembly document.");
            }

            modelDoc = (ModelDoc2)model;
            return (AssemblyDoc)model;
        }

        public AssemblyDoc CreateAssemblyWithComponents(SldWorks.SldWorks swApp,
            CoordinateSystem coordinateSystem, SolidworksModel basePart, SolidworksModel otherPart,
            out ModelDoc2 modelDoc)
        {
            if (basePart == null) throw new ArgumentNullException(nameof(basePart));
            if (otherPart == null) throw new ArgumentNullException(nameof(otherPart));

            var assemblyDoc = CreateAssemblyDocument(swApp, out modelDoc);

            var cadAssy = new CAD_Assembly();
            CoordinateSystem currentCS = coordinateSystem ?? new CoordinateSystem();
            cadAssy.AddCoordinateSystem(currentCS);

            InsertComponent(assemblyDoc, modelDoc, basePart, true, 0, 0, 0);

            double offsetX = coordinateSystem?.OriginLocation?.X_Value ?? 0;
            double offsetY = coordinateSystem?.OriginLocation?.Y_Value ?? 0;
            double offsetZ = coordinateSystem?.OriginLocation?.Z_Value_Cartesian ?? 0;

            InsertComponent(assemblyDoc, modelDoc, otherPart, false, offsetX, offsetY, offsetZ);

            return assemblyDoc;
        }

        // ================================================================
        // SolidWorks: Component Insertion
        // ================================================================

        public CAD_Component InsertComponent(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            SolidworksModel partModel, bool fixedPosition,
            double x, double y, double z)
        {
            if (partModel?.SwModelObject == null)
            {
                Console.WriteLine("Invalid part model provided.");
                return null;
            }

            ModelDoc2 partDoc = (ModelDoc2)partModel.SwModelObject;
            string partPath = partDoc.GetPathName();

            if (string.IsNullOrEmpty(partPath))
            {
                Console.WriteLine("Part has no file path. Save the part first.");
                return null;
            }

            Component2 swComponent = assemblyDoc.AddComponent5(
                partPath,
                (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig,
                "",
                false,
                "",
                x, y, z);

            if (swComponent == null)
            {
                Console.WriteLine($"Failed to insert component: {partPath}");
                return null;
            }

            if (fixedPosition)
            {
                swComponent.Select4(false, null, false);
                assemblyDoc.FixComponent();
            }

            var cadComponent = new CAD_Component
            {
                Name = swComponent.Name2,
                Path = partPath,
                IsAssembly = false,
            };

            return cadComponent;
        }

        // ================================================================
        // SolidWorks: Component Insertion with Transform
        // ================================================================

        public CAD_Component InsertComponentWithTransform(SldWorks.SldWorks swApp,
            AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            SolidworksModel partModel, CoordinateSystem localCS)
        {
            if (partModel?.SwModelObject == null || localCS == null)
            {
                return null;
            }

            double x = localCS.OriginLocation?.X_Value ?? 0;
            double y = localCS.OriginLocation?.Y_Value ?? 0;
            double z = localCS.OriginLocation?.Z_Value_Cartesian ?? 0;

            var component = InsertComponent(assemblyDoc, modelDoc, partModel, false, x, y, z);

            if (component != null && localCS.Vectors != null && localCS.Vectors.Count >= 2)
            {
                ApplyComponentTransform(swApp, modelDoc, component.Name, localCS);
            }

            return component;
        }

        // ================================================================
        // SolidWorks: Transform Application
        // ================================================================

        public void ApplyComponentTransform(SldWorks.SldWorks swApp,
            ModelDoc2 modelDoc, string componentName, CoordinateSystem cs)
        {
            if (string.IsNullOrEmpty(componentName) || cs == null) return;

            modelDoc.Extension.SelectByID2(componentName, "COMPONENT", 0, 0, 0, false, 0, null, 0);

            SelectionMgr selMgr = (SelectionMgr)modelDoc.SelectionManager;
            Component2 comp = (Component2)selMgr.GetSelectedObject6(1, -1);

            if (comp == null) return;

            MathUtility mathUtil = (MathUtility)swApp.GetMathUtility();

            double[] transformData = new double[16];

            Vector xAxis = cs.Vectors.Count > 0 ? cs.Vectors[0] : null;
            Vector yAxis = cs.Vectors.Count > 1 ? cs.Vectors[1] : null;
            Vector zAxis = cs.Vectors.Count > 2 ? cs.Vectors[2] : null;

            if (xAxis != null)
            {
                transformData[0] = xAxis.X_Value;
                transformData[1] = xAxis.Y_Value;
                transformData[2] = xAxis.Z_Value;
            }
            else
            {
                transformData[0] = 1; transformData[1] = 0; transformData[2] = 0;
            }

            if (yAxis != null)
            {
                transformData[3] = yAxis.X_Value;
                transformData[4] = yAxis.Y_Value;
                transformData[5] = yAxis.Z_Value;
            }
            else
            {
                transformData[3] = 0; transformData[4] = 1; transformData[5] = 0;
            }

            if (zAxis != null)
            {
                transformData[6] = zAxis.X_Value;
                transformData[7] = zAxis.Y_Value;
                transformData[8] = zAxis.Z_Value;
            }
            else
            {
                transformData[6] = 0; transformData[7] = 0; transformData[8] = 1;
            }

            transformData[9] = cs.OriginLocation?.X_Value ?? 0;
            transformData[10] = cs.OriginLocation?.Y_Value ?? 0;
            transformData[11] = cs.OriginLocation?.Z_Value_Cartesian ?? 0;
            transformData[12] = 1.0;
            transformData[13] = 0;
            transformData[14] = 0;
            transformData[15] = 0;

            MathTransform transform = (MathTransform)mathUtil.CreateTransform(transformData);
            comp.Transform2 = transform;

            modelDoc.ClearSelection2(true);
        }

        // ================================================================
        // SolidWorks: Core Mate Helpers
        // ================================================================

        public bool CreateComponentMate(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name,
            swMateType_e mateType, swMateAlign_e alignment = swMateAlign_e.swMateAlignALIGNED,
            double value1 = 0, string entityType = "COMPONENT")
        {
            bool selected1 = modelDoc.Extension.SelectByID2(
                comp1Name, entityType, 0, 0, 0, false, 1, null, 0);
            bool selected2 = modelDoc.Extension.SelectByID2(
                comp2Name, entityType, 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2)
            {
                modelDoc.ClearSelection2(true);
                return false;
            }

            int errors = 0;
            Mate2 mate = assemblyDoc.AddMate5(
                (int)mateType,
                (int)alignment,
                false, value1, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            modelDoc.ClearSelection2(true);
            return mate != null && errors == 0;
        }

        public bool CreateFaceMate(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string face1Name,
            string comp2Name, string face2Name,
            swMateType_e mateType, double value1 = 0)
        {
            string title = modelDoc.GetTitle();
            bool selected1 = modelDoc.Extension.SelectByID2(
                $"{face1Name}@{comp1Name}@{title}",
                "FACE", 0, 0, 0, false, 1, null, 0);
            bool selected2 = modelDoc.Extension.SelectByID2(
                $"{face2Name}@{comp2Name}@{title}",
                "FACE", 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2)
            {
                modelDoc.ClearSelection2(true);
                return false;
            }

            int errors = 0;
            Mate2 mate = assemblyDoc.AddMate5(
                (int)mateType,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, value1, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            modelDoc.ClearSelection2(true);
            return mate != null && errors == 0;
        }

        // ================================================================
        // SolidWorks: Face-Based Mate Methods
        // ================================================================

        public bool CreateCoincidentMate(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string component1Name, string face1Name,
            string component2Name, string face2Name)
        {
            return CreateFaceMate(assemblyDoc, modelDoc, component1Name, face1Name,
                component2Name, face2Name, swMateType_e.swMateCOINCIDENT);
        }

        public bool CreateConcentricMate(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string component1Name, string face1Name,
            string component2Name, string face2Name)
        {
            return CreateFaceMate(assemblyDoc, modelDoc, component1Name, face1Name,
                component2Name, face2Name, swMateType_e.swMateCONCENTRIC);
        }

        public bool CreateDistanceMate(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string component1Name, string face1Name,
            string component2Name, string face2Name, double distance)
        {
            return CreateFaceMate(assemblyDoc, modelDoc, component1Name, face1Name,
                component2Name, face2Name, swMateType_e.swMateDISTANCE, distance);
        }

        // ================================================================
        // SolidWorks: Joint Builder Dispatch
        // ================================================================

        public CAD_Joint BuildJoint(SldWorks.SldWorks swApp, AssemblyDoc assemblyDoc,
            ModelDoc2 modelDoc, SolidworksModel part1, SolidworksModel part2, CAD_Joint joint)
        {
            if (part1 == null) throw new ArgumentNullException(nameof(part1));
            if (part2 == null) throw new ArgumentNullException(nameof(part2));
            if (joint == null) throw new ArgumentNullException(nameof(joint));

            string comp1Name = GetComponentNameFromModel(assemblyDoc, part1);
            string comp2Name = GetComponentNameFromModel(assemblyDoc, part2);

            if (string.IsNullOrEmpty(comp1Name) || string.IsNullOrEmpty(comp2Name))
            {
                Console.WriteLine("Could not find component names for the provided parts.");
                return null;
            }

            bool success;
            switch (joint.JointType)
            {
                case CAD_Joint.JointTypeEnum.Rigid:
                    success = BuildRigidJoint(assemblyDoc, modelDoc, comp1Name, comp2Name);
                    break;
                case CAD_Joint.JointTypeEnum.Revolute:
                    success = BuildRevoluteJoint(assemblyDoc, modelDoc, comp1Name, comp2Name);
                    break;
                case CAD_Joint.JointTypeEnum.Slider:
                    success = BuildSliderJoint(assemblyDoc, modelDoc, comp1Name, comp2Name);
                    break;
                case CAD_Joint.JointTypeEnum.Cylindrical:
                    success = BuildCylindricalJoint(assemblyDoc, modelDoc, comp1Name, comp2Name);
                    break;
                case CAD_Joint.JointTypeEnum.PinSlot:
                    success = BuildPinSlotJoint(assemblyDoc, modelDoc, comp1Name, comp2Name);
                    break;
                case CAD_Joint.JointTypeEnum.Planar:
                case CAD_Joint.JointTypeEnum.InPlane:
                    success = BuildPlanarJoint(assemblyDoc, modelDoc, comp1Name, comp2Name);
                    break;
                case CAD_Joint.JointTypeEnum.Ball:
                    success = BuildBallJoint(assemblyDoc, modelDoc, comp1Name, comp2Name);
                    break;
                case CAD_Joint.JointTypeEnum.LeadScrew:
                    success = BuildLeadScrewJoint(assemblyDoc, modelDoc, comp1Name, comp2Name);
                    break;
                default:
                    success = BuildRigidJoint(assemblyDoc, modelDoc, comp1Name, comp2Name);
                    break;
            }

            if (success)
            {
                Console.WriteLine($"Created {joint.JointType} joint between {comp1Name} and {comp2Name}");
            }

            return success ? joint : null;
        }

        // ================================================================
        // SolidWorks: Simple Joint Builders
        // ================================================================

        public bool BuildRigidJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateLOCK);
        }

        public bool BuildCylindricalJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateCONCENTRIC);
        }

        public bool BuildPinSlotJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateSLOT);
        }

        public bool BuildPlanarJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateCOINCIDENT);
        }

        public bool BuildLeadScrewJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateSCREW, value1: 0.01);
        }

        public bool BuildGearJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name, double ratio)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateGEAR, value1: ratio);
        }

        public bool BuildRackPinionJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name, double pinionPitch)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateRACKPINION, value1: pinionPitch);
        }

        public bool BuildCamJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateTANGENT);
        }

        public bool BuildTangentJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateTANGENT);
        }

        public bool BuildPerpendicularJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMatePERPENDICULAR);
        }

        public bool BuildAngleJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name, double angleRadians)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateANGLE, value1: angleRadians);
        }

        public bool BuildParallelJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMatePARALLEL);
        }

        public bool BuildSymmetricJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateSYMMETRIC);
        }

        public bool BuildWidthJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateWIDTH);
        }

        public bool BuildPathJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMatePATH);
        }

        public bool BuildLinearCouplerJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name, double ratio)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateLINEARCOUPLER, value1: ratio);
        }

        // ================================================================
        // SolidWorks: Joint Builders with Fallback Logic
        // ================================================================

        public bool BuildRevoluteJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            if (CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateHINGE))
                return true;

            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateCONCENTRIC);
        }

        public bool BuildSliderJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            if (CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateSLOT))
                return true;

            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMatePARALLEL);
        }

        public bool BuildBallJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            if (CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateUNIVERSALJOINT))
                return true;

            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateCOINCIDENT);
        }

        // ================================================================
        // SolidWorks: Helper Methods
        // ================================================================

        public string GetComponentNameFromModel(AssemblyDoc assemblyDoc, SolidworksModel model)
        {
            if (model?.SwModelObject == null) return null;

            ModelDoc2 partDoc = (ModelDoc2)model.SwModelObject;
            string partPath = partDoc.GetPathName();

            object[] components = (object[])assemblyDoc.GetComponents(true);
            if (components != null)
            {
                foreach (object obj in components)
                {
                    Component2 comp = (Component2)obj;
                    if (comp.GetPathName().Equals(partPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return comp.Name2;
                    }
                }
            }

            return null;
        }

        // ================================================================
        // SolidWorks: Component Operations
        // ================================================================

        public void FixComponent(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc, string componentName)
        {
            if (modelDoc.Extension.SelectByID2(componentName, "COMPONENT", 0, 0, 0, false, 0, null, 0))
            {
                assemblyDoc.FixComponent();
                modelDoc.ClearSelection2(true);
            }
        }

        public void FloatComponent(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc, string componentName)
        {
            if (modelDoc.Extension.SelectByID2(componentName, "COMPONENT", 0, 0, 0, false, 0, null, 0))
            {
                assemblyDoc.UnfixComponent();
                modelDoc.ClearSelection2(true);
            }
        }

        public void MoveComponent(SldWorks.SldWorks swApp, ModelDoc2 modelDoc,
            string componentName, double dx, double dy, double dz)
        {
            if (!modelDoc.Extension.SelectByID2(componentName, "COMPONENT", 0, 0, 0, false, 0, null, 0))
                return;

            SelectionMgr selMgr = (SelectionMgr)modelDoc.SelectionManager;
            Component2 comp = (Component2)selMgr.GetSelectedObject6(1, -1);

            if (comp == null) return;

            MathTransform currentTransform = comp.Transform2;
            MathUtility mathUtil = (MathUtility)swApp.GetMathUtility();

            double[] translation = { 1, 0, 0, 0, 1, 0, 0, 0, 1, dx, dy, dz, 1, 0, 0, 0 };
            MathTransform translateTransform = (MathTransform)mathUtil.CreateTransform(translation);

            comp.Transform2 = (MathTransform)currentTransform.Multiply(translateTransform);
            modelDoc.ClearSelection2(true);
        }

        // ================================================================
        // SolidWorks: Assembly Information
        // ================================================================

        public List<string> GetComponentNames(AssemblyDoc assemblyDoc)
        {
            var names = new List<string>();
            object[] components = (object[])assemblyDoc.GetComponents(true);

            if (components != null)
            {
                foreach (object obj in components)
                {
                    Component2 comp = (Component2)obj;
                    names.Add(comp.Name2);
                }
            }

            return names;
        }

        public int GetComponentCount(AssemblyDoc assemblyDoc)
        {
            object[] components = (object[])assemblyDoc.GetComponents(true);
            return components?.Length ?? 0;
        }

        // ================================================================
        // SolidWorks: Save Operations
        // ================================================================

        public bool SaveAssemblyFile(ModelDoc2 modelDoc, string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return false;

            int errors = 0, warnings = 0;
            bool result = modelDoc.Extension.SaveAs(
                filePath,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                null, ref errors, ref warnings);

            if (!result)
            {
                Console.WriteLine($"Failed to save assembly. Errors: {errors}, Warnings: {warnings}");
            }

            return result;
        }

        // ================================================================
        // SolidWorks: Rebuild and Update
        // ================================================================

        public void RebuildAssembly(ModelDoc2 modelDoc)
        {
            modelDoc.ForceRebuild3(true);
        }

        public void ZoomToFit(ModelDoc2 modelDoc)
        {
            modelDoc.ViewZoomtofit2();
        }
    }
}
