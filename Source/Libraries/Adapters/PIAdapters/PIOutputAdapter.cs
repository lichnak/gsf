﻿//******************************************************************************************************
//  PIOutputAdapter.cs - Gbtc
//
//  Copyright © 2010, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the Eclipse Public License -v 1.0 (the "License"); you may
//  not use this file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://www.opensource.org/licenses/eclipse-1.0.php
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  08/13/2012 - Ryan McCoy
//       Generated original version of source code.
//  06/18/2014 - J. Ritchie Carroll
//       Updated code to use pooled PIConnection instances.
//  12/17/2014 - J. Ritchie Carroll
//       Updated to use AF-SDK - using single PIConnection for now
//
//******************************************************************************************************

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GSF;
using GSF.Collections;
using GSF.Data;
using GSF.IO;
using GSF.Threading;
using GSF.TimeSeries;
using GSF.TimeSeries.Adapters;
using GSF.Units;
using OSIsoft.AF.Asset;
using OSIsoft.AF.Data;
using OSIsoft.AF.PI;
using OSIsoft.AF.Search;
using OSIsoft.AF.Time;
using ConnectionPoint = System.Tuple<PIAdapters.PIConnection, OSIsoft.AF.PI.PIPoint>;

namespace PIAdapters
{
    /// <summary>
    /// Exports measurements to PI if the point tag or alternate tag corresponds to a PI point's tag name.
    /// </summary>
    [Description("OSI-PI: Archives measurements to an OSI-PI server using 64-bit PI SDK")]
    public class PIOutputAdapter : OutputAdapterBase
    {
        #region [ Members ]

        // Nested Types

        // Define measurement processing stages
        private enum ProcessingStage
        {
            AssociatingMeasurementsToConnectionPoints,
            CollatingMeasurementsIntoPointValues,
            ArchivingPointValuesToPIServer
        }

        // Fields

        // Define cached mapping between GSFSchema measurements and PI points
        private readonly ConcurrentDictionary<MeasurementKey, ConnectionPoint> m_mappedConnectionPoints;

        // Define a queue of requested mappings for GSFSchema measurements per connection
        private readonly ProcessQueue<Tuple<PIConnection, MeasurementKey>> m_queuedMappingRequests;

        private readonly ShortSynchronizedOperation m_restartConnection;    // Restart connection operation
        private readonly ConcurrentDictionary<Guid, string> m_tagMap;       // Tag name to measurement Guid lookup
        private readonly HashSet<MeasurementKey> m_pendingMappings;         // List of pending measurement mappings
        //private PIConnectionPool m_connectionPool;                          // PI server connection pool for data writes
        private PIConnection m_connection;                                  // PI server connection for meta-data synchronization
        private string m_serverName;                                        // Server for PI connection
        private string m_userName;                                          // Username for PI connection
        private string m_password;                                          // Password for PI connection
        private int m_connectTimeout;                                       // PI connection timeout
        private string m_tagMapCacheFileName;                               // Tag map cache file name
        private bool m_runMetadataSync;                                     // Flag for automatically creating and/or updating PI points on the server
        private string m_pointSource;                                       // Point source to set on PI points when automatically created by the adapter
        private string m_pointClass;                                        // Point class to use for new PI points when automatically created by the adapter
        //private int m_pointsPerConnection;                                  // Number of points each PI connection is set to handle
        private DateTime m_lastMetadataRefresh;                             // Tracks time of last meta-data refresh
        private volatile int m_processedMappings;                           // Total number of mappings processed so far
        private volatile int m_processedMeasurements;                       // Total number of measurements processed so far
        private volatile bool m_refreshingMetadata;                         // Flag that determines if meta-data is currently refreshing
        private double m_metadataRefreshProgress;                           // Current meta-data refresh progress
        private ProcessingStage m_processingStage;                          // Current data processing stage
        private Time m_timeSpentInLastStage1;                               // Time spent in last stage 1 processing - associating measurements to connection points
        private Time m_timeSpentInLastStage2;                               // Time spent in last stage 2 processing - collating measurements into point values
        private Time m_timeSpentInLastStage3;                               // Time spent in last stage 3 processing - archiving point values to PI server
        private int m_lastStage3ThreadCount;                                // Number of threads used to process last stage 3 inserts
        private int m_lastNumberOfPointsProcessed;                          // Last total number of points processed
        private bool m_disposed;                                            // Flag that determines if class is disposed

        #endregion

        #region [ Constructors ]

        /// <summary>
        /// Creates a new <see cref="PIOutputAdapter"/>
        /// </summary>
        public PIOutputAdapter()
        {
            //m_pointsPerConnection = PIConnectionPool.DefaultAccessCountPerConnection;
            m_mappedConnectionPoints = new ConcurrentDictionary<MeasurementKey, ConnectionPoint>();
            m_queuedMappingRequests = ProcessQueue<Tuple<PIConnection, MeasurementKey>>.CreateAsynchronousQueue(EstablishConnectionPointMappings);
            m_restartConnection = new ShortSynchronizedOperation(Start);
            m_tagMap = new ConcurrentDictionary<Guid, string>();
            m_pendingMappings = new HashSet<MeasurementKey>();
            m_lastMetadataRefresh = DateTime.MinValue;
        }

        #endregion

        #region [ Properties ]

        /// <summary>
        /// Returns true to indicate that this <see cref="PIOutputAdapter"/> is sending measurements to a historian, OSISoft PI.
        /// </summary>
        public override bool OutputIsForArchive
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Returns false to indicate that this <see cref="PIOutputAdapter"/> will connect synchronously
        /// </summary>
        protected override bool UseAsyncConnect
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets or sets the name of the PI server for the adapter's PI connection.
        /// </summary>
        [ConnectionStringParameter, Description("Defines the name of the PI server for the adapter's PI connection.")]
        public string ServerName
        {
            get
            {
                return m_serverName;
            }
            set
            {
                m_serverName = value;
            }
        }

        /// <summary>
        /// Gets or sets the name of the PI user ID for the adapter's PI connection.
        /// </summary>
        [ConnectionStringParameter, Description("Defines the name of the PI user ID for the adapter's PI connection."), DefaultValue("")]
        public string UserName
        {
            get
            {
                return m_userName;
            }
            set
            {
                m_userName = value;
            }
        }

        /// <summary>
        /// Gets or sets the password used for the adapter's PI connection.
        /// </summary>
        [ConnectionStringParameter, Description("Defines the password used for the adapter's PI connection."), DefaultValue("")]
        public string Password
        {
            get
            {
                return m_password;
            }
            set
            {
                m_password = value;
            }
        }

        /// <summary>
        /// Gets or sets the timeout interval (in milliseconds) for the adapter's connection
        /// </summary>
        [ConnectionStringParameter, Description("Defines the timeout interval (in milliseconds) for the adapter's connection"), DefaultValue(PIConnection.DefaultConnectTimeout)]
        public int ConnectTimeout
        {
            get
            {
                return m_connectTimeout;
            }
            set
            {
                m_connectTimeout = value;
            }
        }

        /// <summary>
        /// Gets or sets whether or not this adapter should automatically manage metadata for PI points.
        /// </summary>
        [ConnectionStringParameter, Description("Determines if this adapter should automatically manage metadata for PI points (recommended)."), DefaultValue(true)]
        public bool RunMetadataSync
        {
            get
            {
                return m_runMetadataSync;
            }
            set
            {
                m_runMetadataSync = value;
            }
        }

        /// <summary>
        /// Gets or sets the point source string used when automatically creating new PI points during the metadata update
        /// </summary>
        [ConnectionStringParameter, Description("Defines the point source string used when automatically creating new PI points during the metadata update"), DefaultValue("GSF")]
        public string PIPointSource
        {
            get
            {
                return m_pointSource;
            }
            set
            {
                m_pointSource = value;
            }
        }

        /// <summary>
        /// Gets or sets the point class string used when automatically creating new PI points during the metadata update. On the PI server, this class should inherit from classic.
        /// </summary>
        [ConnectionStringParameter, Description("Defines the point class string used when automatically creating new PI points during the metadata update. On the PI server, this class should inherit from classic."), DefaultValue("classic")]
        public string PIPointClass
        {
            get
            {
                return m_pointClass;
            }
            set
            {
                m_pointClass = value;
            }
        }

        ///// <summary>
        ///// Gets or sets total number of points a single <see cref="PIConnection"/> will handle for archiving data.
        ///// </summary>
        //[ConnectionStringParameter, Description("Defines the total number of points a single PI connection will handle for archiving data. This variable is dependent upon the sample rate of incoming data, i.e., if sample rate is low then points per connection can be high."), DefaultValue(PIConnectionPool.DefaultAccessCountPerConnection)]
        //public int PointsPerConnection
        //{
        //    get
        //    {
        //        return m_pointsPerConnection;
        //    }
        //    set
        //    {
        //        m_pointsPerConnection = value;
        //    }
        //}

        /// <summary>
        /// Gets or sets flag that determines if defined point compression will be used during archiving.
        /// </summary>
        [ConnectionStringParameter, Description("Defines the flag that determines if defined point compression will be used during archiving."), DefaultValue(true)]
        public bool UseCompression
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the filename to be used for tag map cache.
        /// </summary>
        [ConnectionStringParameter, Description("Defines the filename to be used for tag map cache file name. Leave blank for cache name to be same as adapter name with a \".cache\" extension."), DefaultValue("")]
        public string TagMapCacheFileName
        {
            get
            {
                return m_tagMapCacheFileName;
            }
            set
            {
                m_tagMapCacheFileName = value;
            }
        }

        /// <summary>
        /// Returns the detailed status of the data output source.
        /// </summary>
        public override string Status
        {
            get
            {
                StringBuilder status = new StringBuilder();

                status.Append(base.Status);
                status.AppendLine();
                status.AppendFormat("        OSI-PI server name: {0}", m_serverName);
                status.AppendLine();
                status.AppendFormat("       Connected to server: {0}", (object)m_connection == null ? "No" : m_connection.Connected ? "Yes" : "No");
                status.AppendLine();
                status.AppendFormat("    Meta-data sync enabled: {0}", m_runMetadataSync);
                status.AppendLine();
                status.AppendFormat("         Using compression: {0}", UseCompression);
                status.AppendLine();

                if (m_runMetadataSync)
                {
                    status.AppendFormat("       OSI-PI point source: {0}", m_pointSource);
                    status.AppendLine();
                    status.AppendFormat("        OSI-PI point class: {0}", m_pointClass);
                    status.AppendLine();
                }

                if ((object)m_queuedMappingRequests != null)
                {
                    status.AppendLine();
                    status.AppendLine(">> Mapping Request Queue Status");
                    status.AppendLine();
                    status.Append(m_queuedMappingRequests.Status);
                    status.AppendLine();
                }

                status.AppendFormat("   Tag-map cache file name: {0}", FilePath.TrimFileName(m_tagMapCacheFileName.ToNonNullString("[undefined]"), 51));
                status.AppendLine();
                status.AppendFormat(" Active tag-map cache size: {0:N0} mappings", m_mappedConnectionPoints.Count);
                status.AppendLine();
                status.AppendFormat("      Pending tag-mappings: {0:N0} mappings, {1:0.00%} complete", m_pendingMappings.Count, 1.0D - m_pendingMappings.Count / (double)m_mappedConnectionPoints.Count);
                status.AppendLine();

                if (m_runMetadataSync)
                {
                    status.AppendFormat("    Meta-data sync process: {0}, {1:0.00%} complete", m_refreshingMetadata ? "Active" : "Idle", m_metadataRefreshProgress);
                    status.AppendLine();
                }

                //if ((object)m_connectionPool != null)
                //{
                //    status.AppendFormat("   PI connection pool size: {0:N0}", m_connectionPool.Size);
                //    status.AppendLine();
                //}

                //status.AppendFormat("  PI points per connection: {0:N0}", m_pointsPerConnection);
                //status.AppendLine();

                status.AppendLine();
                status.AppendLine(">> Measurement Processing Stages");
                status.AppendLine();

                foreach (ProcessingStage stage in Enum.GetValues(typeof(ProcessingStage)).Cast<ProcessingStage>())
                {
                    status.AppendFormat("        Processing stage {0}: {1}", (int)stage + 1, stage.GetFormattedName());
                    status.AppendLine();
                }

                status.AppendLine();

                status.AppendFormat("  Current processing stage: {0}", (int)m_processingStage + 1);
                status.AppendLine();
                status.AppendFormat("Time spent in last stage 1: {0}", m_timeSpentInLastStage1.ToString(2));
                status.AppendLine();
                status.AppendFormat("Time spent in last stage 2: {0}", m_timeSpentInLastStage2.ToString(2));
                status.AppendLine();
                status.AppendFormat("Time spent in last stage 3: {0}", m_timeSpentInLastStage3.ToString(2));
                status.AppendLine();
                status.AppendFormat(" Last stage 3 thread count: {0:N0}", m_lastStage3ThreadCount);
                status.AppendLine();
                status.AppendFormat("Last processed point count: {0:N0}", m_lastNumberOfPointsProcessed);
                status.AppendLine();

                if (m_lastStage3ThreadCount > 0)
                {
                    status.AppendFormat("  Average work time/thread: {0} for last stage 3 batch", (m_timeSpentInLastStage3 / m_lastStage3ThreadCount).ToString(2));
                    status.AppendLine();
                }

                if (m_lastNumberOfPointsProcessed > 0)
                {
                    // Adjust the PointsPerConnection setting to optimize the write time per point
                    status.AppendFormat("  Average write time/point: {0} for last stage 3 batch", (m_timeSpentInLastStage3 / m_lastNumberOfPointsProcessed).ToString(2));
                    status.AppendLine();
                }

                return status.ToString();
            }
        }

        #endregion

        #region [ Methods ]

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="PIOutputAdapter"/> object and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (!m_disposed)
            {
                try
                {
                    if (disposing)
                    {
                        if ((object)m_queuedMappingRequests != null)
                            m_queuedMappingRequests.Dispose();

                        if ((object)m_connection != null)
                        {
                            m_connection.Disconnected -= m_connection_Disconnected;
                            m_connection.Dispose();
                            m_connection = null;
                        }

                        //if ((object)m_connectionPool != null)
                        //{
                        //    m_connectionPool.Dispose();
                        //    m_connectionPool = null;
                        //}

                        if ((object)m_mappedConnectionPoints != null)
                            m_mappedConnectionPoints.Clear();
                    }
                }
                finally
                {
                    m_disposed = true;          // Prevent duplicate dispose.
                    base.Dispose(disposing);    // Call base class Dispose().
                }
            }
        }

        /// <summary>
        /// Returns a brief status of this <see cref="PIOutputAdapter"/>
        /// </summary>
        /// <param name="maxLength">Maximum number of characters in the status string</param>
        /// <returns>Status</returns>
        public override string GetShortStatus(int maxLength)
        {
            return string.Format("Archived {0:N0} measurements to PI.", m_processedMeasurements).CenterText(maxLength);
        }

        /// <summary>
        /// Initializes this <see cref="PIOutputAdapter"/>.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();

            Dictionary<string, string> settings = Settings;
            string setting;

            if (!settings.TryGetValue("ServerName", out m_serverName))
                throw new InvalidOperationException("Server name is a required setting for PI connections. Please add a server in the format servername=myservername to the connection string.");

            if (settings.TryGetValue("UserName", out setting))
                m_userName = setting;
            else
                m_userName = null;

            if (settings.TryGetValue("Password", out setting))
                m_password = setting;
            else
                m_password = null;

            if (!settings.TryGetValue("ConnectTimeout", out setting) || !int.TryParse(setting, out m_connectTimeout))
                m_connectTimeout = PIConnection.DefaultConnectTimeout;

            //if (!settings.TryGetValue("PointsPerConnection", out setting) || !int.TryParse(setting, out m_pointsPerConnection))
            //    m_pointsPerConnection = PIConnectionPool.DefaultAccessCountPerConnection;

            if (settings.TryGetValue("UseCompression", out setting))
                UseCompression = setting.ParseBoolean();
            else
                UseCompression = true;

            if (settings.TryGetValue("RunMetadataSync", out setting))
                m_runMetadataSync = setting.ParseBoolean();
            else
                m_runMetadataSync = true; // By default, assume that PI tags will be automatically maintained

            if (settings.TryGetValue("PIPointSource", out setting))
                m_pointSource = setting;
            else
                m_pointSource = "GSF";

            if (settings.TryGetValue("PIPointClass", out setting))
                m_pointClass = setting;
            else
                m_pointClass = "classic";

            if (!settings.TryGetValue("TagCacheName", out setting) || string.IsNullOrWhiteSpace(setting))
                setting = Name + "_TagMap.cache";

            m_tagMapCacheFileName = FilePath.GetAbsolutePath(setting);
        }

        /// <summary>
        /// Connects to the configured PI server.
        /// </summary>
        protected override void AttemptConnection()
        {
            m_processedMappings = 0;

            m_connection = new PIConnection
            {
                ServerName = m_serverName,
                UserName = m_userName,
                Password = m_password,
                ConnectTimeout = m_connectTimeout
            };

            m_connection.Disconnected += m_connection_Disconnected;
            m_connection.Open();

            //m_connectionPool = new PIConnectionPool
            //{
            //    AccessCountPerConnection = m_pointsPerConnection
            //};

            //m_connectionPool.Disconnected += m_connection_Disconnected;

            m_mappedConnectionPoints.Clear();

            lock (m_pendingMappings)
            {
                m_pendingMappings.Clear();
            }

            m_queuedMappingRequests.Clear();
            m_queuedMappingRequests.Start();

            // Kick off meta-data refresh
            RefreshMetadata();
        }

        /// <summary>
        /// Closes this <see cref="PIOutputAdapter"/> connections to the PI server.
        /// </summary>
        protected override void AttemptDisconnection()
        {
            m_queuedMappingRequests.Stop();
            m_queuedMappingRequests.Clear();

            m_mappedConnectionPoints.Clear();

            lock (m_pendingMappings)
            {
                m_pendingMappings.Clear();
            }

            //if ((object)m_connectionPool != null)
            //{
            //    m_connectionPool.Disconnected -= m_connection_Disconnected;
            //    m_connectionPool.Dispose();
            //    m_connectionPool = null;
            //}

            if ((object)m_connection != null)
            {
                m_connection.Disconnected -= m_connection_Disconnected;
                m_connection.Dispose();
                m_connection = null;
            }
        }

        /// <summary>
        /// Sorts measurements and sends them to the configured PI server in batches
        /// </summary>
        /// <param name="measurements">Measurements to queue</param>
        protected override void ProcessMeasurements(IMeasurement[] measurements)
        {
            if ((object)measurements == null || measurements.Length <= 0 || (object)m_connection == null)
                return;

            // We use dictionaries to create grouping constructs whereby each PI connection will have its own set of points
            // and associated values so that archive operations for each PI connection can be processed in parallel
            ConcurrentDictionary<PIConnection, ConcurrentDictionary<PIPoint, List<IMeasurement>>> connectionPointMeasurements = new ConcurrentDictionary<PIConnection, ConcurrentDictionary<PIPoint, List<IMeasurement>>>();
            ConcurrentDictionary<PIConnection, Dictionary<PIPoint, AFValues>> connectionPointValues = new ConcurrentDictionary<PIConnection, Dictionary<PIPoint, AFValues>>();
            Ticks startTime, endTime;
            int numberOfPointsProcessed = 0;
            int stage3ThreadCount = 0;

            startTime = DateTime.UtcNow.Ticks;
            m_processingStage = ProcessingStage.AssociatingMeasurementsToConnectionPoints;  // Stage 1

            // Create initial connection point to measurements grouping or associate incoming measurements with a connection point if one is not already defined
            Parallel.ForEach(measurements, measurement =>
            {
                try
                {
                    ConnectionPoint connectionPoint;

                    // If adapter gets disabled while executing this thread - go ahead and exit
                    if (!Enabled)
                        return;

                    // Lookup connection point mapping for this measurement, if it wasn't found - go ahead and exit
                    if (!m_mappedConnectionPoints.TryGetValue(measurement.Key, out connectionPoint))
                        return;

                    // If connection point is not defined, kick off process to create a new mapping
                    if ((object)connectionPoint == null)
                    {
                        bool mappingRequested = false;

                        lock (m_pendingMappings)
                        {
                            // Only start mapping process if one is not already pending
                            if (!m_pendingMappings.Contains(measurement.Key))
                            {
                                mappingRequested = true;
                                m_pendingMappings.Add(measurement.Key);
                            }
                        }

                        if (mappingRequested)
                        {
                            lock (m_queuedMappingRequests.SyncRoot)
                            {
                                // No mapping is defined for this point, get a connection from the
                                // server pool and queue-up a mapping for this point                                
                                //PIConnection connection = m_connectionPool.GetPooledConnection(m_serverName, m_userName, m_password, m_connectTimeout);
                                m_queuedMappingRequests.Add(new Tuple<PIConnection, MeasurementKey>(m_connection, measurement.Key));
                            }
                        }
                    }
                    else
                    {
                        PIConnection connection = connectionPoint.Item1;
                        PIPoint point = connectionPoint.Item2;

                        // Create a grouping per connection that contains a lookup table of PIPoint to associated measurements
                        ConcurrentDictionary<PIPoint, List<IMeasurement>> pointMeasurements = connectionPointMeasurements.GetOrAdd(connection, c => new ConcurrentDictionary<PIPoint, List<IMeasurement>>());

                        // Get or add a list of measurements associated with this PIPoint
                        List<IMeasurement> measurementValues = pointMeasurements.GetOrAdd(point, p => new List<IMeasurement>());

                        lock (measurementValues)
                        {
                            measurementValues.Add(measurement);
                        }

                        Interlocked.Increment(ref numberOfPointsProcessed);
                    }
                }
                catch (Exception ex)
                {
                    OnProcessException(new InvalidOperationException(string.Format("Failed to associate measurement value with connection point for '{0}': {1}", measurement.Key, ex.Message), ex));
                }
            });

            endTime = DateTime.UtcNow.Ticks;
            m_timeSpentInLastStage1 = (endTime - startTime).ToSeconds();

            // If adapter gets disabled while executing this thread - go ahead and exit
            if (!Enabled)
                return;

            startTime = endTime;
            m_processingStage = ProcessingStage.CollatingMeasurementsIntoPointValues;   // Stage 2

            // Create series of AFValues to insert into PI per connection
            Parallel.ForEach(connectionPointMeasurements, connectionPointMeasurement =>
            {
                // Create a dictionary of PIPoints each with a AFValues collection for each connection
                Dictionary<PIPoint, AFValues> pointValues = connectionPointValues.GetOrAdd(connectionPointMeasurement.Key, c => new Dictionary<PIPoint, AFValues>());
                ConcurrentDictionary<PIPoint, List<IMeasurement>> pointMeasurements = connectionPointMeasurement.Value;

                foreach (KeyValuePair<PIPoint, List<IMeasurement>> pointMeasurement in pointMeasurements)
                {
                    // If adapter gets disabled while executing this thread - go ahead and exit
                    if (!Enabled)
                        return;

                    PIPoint point = pointMeasurement.Key;
                    List<IMeasurement> measurementValues = pointMeasurement.Value;
                    DateTime timestamp;
                    double value;

                    AFValues values = pointValues.GetOrAdd(point, p => new AFValues());

                    // Populate AFValues collection with measurements
                    foreach (IMeasurement measurement in measurementValues)
                    {
                        // If adapter gets disabled while executing this thread - go ahead and exit
                        if (!Enabled)
                            return;

                        try
                        {
                            timestamp = new DateTime(measurement.Timestamp).ToLocalTime();
                            value = measurement.AdjustedValue;

                            // Add measurement value to PI values collection
                            values.Add(new AFValue(value, new AFTime(timestamp)));
                        }
                        catch (Exception ex)
                        {
                            OnProcessException(new InvalidOperationException(string.Format("Failed to collate measurement value into OSI-PI point value collection for '{0}': {1}", measurement.Key, ex.Message), ex));
                        }
                    }
                }
            });

            endTime = DateTime.UtcNow.Ticks;
            m_timeSpentInLastStage2 = (endTime - startTime).ToSeconds();

            // If adapter gets disabled while executing this thread - go ahead and exit
            if (!Enabled)
                return;

            startTime = endTime;
            m_processingStage = ProcessingStage.ArchivingPointValuesToPIServer;     // Stage 3

            // Handle inserts for each PI connection in parallel
            Parallel.ForEach(connectionPointValues, connectionPointValue =>
            {
                Dictionary<PIPoint, AFValues> pointValues = connectionPointValue.Value;
                Interlocked.Increment(ref stage3ThreadCount);

                foreach (KeyValuePair<PIPoint, AFValues> pointValue in pointValues)
                {
                    // If adapter gets disabled while executing this thread - go ahead and exit
                    if (!Enabled)
                        return;

                    PIPoint point = pointValue.Key;
                    AFValues values = pointValue.Value;

                    if ((object)point != null && (object)values != null)
                    {
                        try
                        {
                            // Add values for current PI point
                            point.UpdateValues(values, UseCompression ? AFUpdateOption.Insert : AFUpdateOption.InsertNoCompression);
                            m_processedMeasurements += values.Count;
                        }
                        catch (Exception ex)
                        {
                            OnProcessException(new InvalidOperationException(string.Format("Failed to archive OSI-PI point values for tag '{0}': {1}", point.Name, ex.Message), ex));
                        }
                    }
                }
            });

            endTime = DateTime.UtcNow.Ticks;
            m_timeSpentInLastStage3 = (endTime - startTime).ToSeconds();
            m_lastNumberOfPointsProcessed = numberOfPointsProcessed;
            m_lastStage3ThreadCount = stage3ThreadCount;
        }

        private void EstablishConnectionPointMappings(Tuple<PIConnection, MeasurementKey>[] connectionKeys)
        {
            ConnectionPoint connectionPoint;
            bool mappingEstablished = false;

            foreach (Tuple<PIConnection, MeasurementKey> connectionKey in connectionKeys)
            {
                // If adapter gets disabled while executing this thread - go ahead and exit
                if (!Enabled)
                    return;

                PIConnection connection = connectionKey.Item1;
                MeasurementKey key = connectionKey.Item2;

                try
                {
                    if (m_mappedConnectionPoints.TryGetValue(key, out connectionPoint))
                    {
                        if ((object)connectionPoint == null || (object)connectionPoint.Item1 == null || (object)connectionPoint.Item2 == null)
                        {
                            // Create new connection point
                            connectionPoint = CreateMappedConnectionPoint(connection, key);

                            if ((object)connectionPoint != null)
                            {
                                m_mappedConnectionPoints[key] = connectionPoint;
                                mappingEstablished = true;
                            }
                        }
                        else
                        {
                            // Connection point is already established
                            mappingEstablished = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    OnProcessException(new InvalidOperationException(string.Format("Failed to create connection point mapping for '{0}': {1}", key, ex.Message), ex));
                }
                finally
                {
                    lock (m_pendingMappings)
                    {
                        m_pendingMappings.Remove(key);
                    }

                    if (!mappingEstablished)
                    {
                        m_mappedConnectionPoints.TryRemove(key, out connectionPoint);
                        //m_connectionPool.ReturnPooledConnection(connection);
                    }

                    // Provide some level of feed back on progress of mapping process
                    m_processedMappings++;

                    if (m_processedMappings % 100 == 0)
                        OnStatusMessage("Mapped {0:N0} PI tags to measurements, {1:0.00%} complete...", m_mappedConnectionPoints.Count - m_pendingMappings.Count, 1.0D - m_pendingMappings.Count / (double)m_mappedConnectionPoints.Count);
                }
            }
        }

        private ConnectionPoint CreateMappedConnectionPoint(PIConnection connection, MeasurementKey key)
        {
            PIPoint point = null;

            // Map measurement to PI point
            try
            {
                // Two ways to find points here
                // 1. if we are running metadata sync from the adapter, look for the signal ID in the EXDESC field
                // 2. if the pi points are being manually maintained, look for either the point tag or alternate tag in the actual pi point tag
                Guid signalID = key.SignalID;
                bool foundPoint = false;

                if (m_runMetadataSync)
                {
                    // Attempt lookup by EXDESC signal ID                           
                    point = GetPIPoint(connection.Server, signalID);
                    foundPoint = (object)point != null;
                }

                if (!foundPoint)
                {
                    // Lookup meta-data for current measurement
                    DataRow[] rows = DataSource.Tables["ActiveMeasurements"].Select(string.Format("SignalID='{0}'", signalID));

                    if (rows.Length > 0)
                    {
                        DataRow measurementRow = rows[0];
                        string tagName = measurementRow["PointTag"].ToNonNullString().Trim();

                        // Use alternate tag if one is defined
                        if (!string.IsNullOrWhiteSpace(measurementRow["AlternateTag"].ToString()) && !measurementRow["SignalType"].ToString().Equals("DIGI", StringComparison.OrdinalIgnoreCase))
                            tagName = measurementRow["AlternateTag"].ToString().Trim();

                        // Attempt lookup by tag name
                        point = GetPIPoint(connection.Server, tagName);

                        if ((object)point == null)
                        {
                            if (!m_refreshingMetadata)
                                OnStatusMessage("WARNING: No PI points found for tag '{0}'. Data will not be archived for '{1}'.", tagName, key);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnProcessException(new InvalidOperationException(string.Format("Failed to map '{0}' to a PI tag: {1}", key, ex.Message), ex));
            }

            // If no point could be mapped, return null connection mapping so key can be removed from tag-map
            if ((object)point == null)
                return null;

            return new ConnectionPoint(connection, point);
        }

        /// <summary>
        /// Sends updated metadata to PI.
        /// </summary>
        protected override void ExecuteMetadataRefresh()
        {
            if (!Initialized || (object)m_connection == null)
                return;

            if (!m_connection.Connected)
                return;

            MeasurementKey[] inputMeasurements = InputMeasurementKeys;
            int previousMeasurementReportingInterval = MeasurementReportingInterval;
            DateTime latestUpdateTime = DateTime.MinValue;
            DateTime updateTime;
            bool refreshMetadata;

            m_refreshingMetadata = m_runMetadataSync;

            // For first runs, don't report archived points until PI meta-data has been established
            MeasurementReportingInterval = 0;

            // Attempt to load tag-map from existing cache, if any
            LoadCachedTagMap();

            if (m_tagMap.Count > 0)
                MeasurementReportingInterval = previousMeasurementReportingInterval;

            // Establish initial connection point dictionary (much of the meta-data may already exist)
            EstablishConnectionPointDictionary(inputMeasurements);

            if (!m_runMetadataSync)
            {
                MeasurementReportingInterval = previousMeasurementReportingInterval;
                return;
            }

            try
            {
                OnStatusMessage("Beginning metadata refresh...");

                if ((object)inputMeasurements != null && inputMeasurements.Length > 0)
                {
                    PIServer server = m_connection.Server;
                    int processed = 0, total = inputMeasurements.Length;
                    AdoDataConnection database = null;
                    DataTable measurements = DataSource.Tables["ActiveMeasurements"];

                    foreach (MeasurementKey key in inputMeasurements)
                    {
                        // If adapter gets disabled while executing this thread - go ahead and exit
                        if (!Enabled)
                            return;

                        Guid signalID = key.SignalID;
                        DataRow[] rows = measurements.Select(string.Format("SignalID='{0}'", signalID));
                        DataRow measurementRow;
                        PIPoint point;
                        string tagName;

                        refreshMetadata = false;

                        if (rows.Length <= 0)
                        {
                            m_tagMap.TryRemove(signalID, out tagName);
                            continue;
                        }

                        // Get matching measurement row
                        measurementRow = rows[0];

                        // Get tag-name as defined in meta-data
                        tagName = measurementRow["PointTag"].ToNonNullString().Trim();

                        // If tag name is not defined in measurements there is no need to continue processing
                        if (string.IsNullOrWhiteSpace(tagName))
                        {
                            m_tagMap.TryRemove(signalID, out tagName);
                            continue;
                        }

                        // Use alternate tag if one is defined - note that digitals are an exception since they use this field for special labeling
                        if (!string.IsNullOrWhiteSpace(measurementRow["AlternateTag"].ToString()) && !measurementRow["SignalType"].ToString().Equals("DIGI", StringComparison.OrdinalIgnoreCase))
                            tagName = measurementRow["AlternateTag"].ToString().Trim();

                        // Lookup PI point trying signal ID and tag name
                        point = GetPIPoint(server, signalID, tagName);

                        if ((object)point == null)
                        {
                            try
                            {
                                // Attempt to add point if it doesn't exist
                                Dictionary<string, object> attributes = new Dictionary<string, object>();
                                attributes[PICommonPointAttributes.PointClassName] = m_pointClass;
                                attributes[PICommonPointAttributes.PointType] = PIPointType.Float32;

                                point = server.CreatePIPoint(tagName, attributes);

                                refreshMetadata = true;
                            }
                            catch (Exception ex)
                            {
                                OnProcessException(new InvalidOperationException(string.Format("Failed to add PI tag '{0}' for measurement '{1}': {2}", tagName, key, ex.Message), ex));
                            }
                        }

                        if ((object)point != null)
                        {
                            // Update tag-map for faster future loads
                            m_tagMap[signalID] = tagName;

                            try
                            {
                                // Rename tag-name if needed - PI tags are not case sensitive
                                if (string.Compare(point.Name, tagName, StringComparison.InvariantCultureIgnoreCase) != 0)
                                {
                                    point.SetAttribute(PICommonPointAttributes.Tag, tagName);
                                    point.SaveAttributes(PICommonPointAttributes.Tag);
                                }
                            }
                            catch (Exception ex)
                            {
                                OnProcessException(new InvalidOperationException(string.Format("Failed to rename PI tag '{0}' to '{1}' for measurement '{2}': {3}", point.Name, tagName, key, ex.Message), ex));
                            }

                            if (!refreshMetadata)
                            {
                                // Validate that PI point has a properly defined Guid in the extended description
                                point.LoadAttributes(PICommonPointAttributes.ExtendedDescriptor);
                                string exdesc = point.GetAttribute(PICommonPointAttributes.ExtendedDescriptor) as string;

                                if (string.IsNullOrWhiteSpace(exdesc))
                                    refreshMetadata = true;
                                else
                                    refreshMetadata = string.Compare(exdesc.Trim(), signalID.ToString(), StringComparison.OrdinalIgnoreCase) != 0;
                            }
                        }

                        // Determine last update time for this meta-data record
                        try
                        {
                            // See if ActiveMeasurements contains updated on column
                            if (measurements.Columns.Contains("UpdatedOn"))
                            {
                                updateTime = Convert.ToDateTime(measurementRow["UpdatedOn"]);
                            }
                            else
                            {
                                // Attempt to lookup last update time for record
                                if ((object)database == null)
                                    database = new AdoDataConnection("systemSettings");

                                updateTime = Convert.ToDateTime(database.Connection.ExecuteScalar(string.Format("SELECT UpdatedOn FROM Measurement WHERE SignalID = '{0}'", signalID)));
                            }
                        }
                        catch (Exception)
                        {
                            updateTime = DateTime.UtcNow;
                        }

                        // Tracked latest update time in meta-data, this will become last meta-data refresh time
                        if (updateTime > latestUpdateTime)
                            latestUpdateTime = updateTime;

                        if ((refreshMetadata || updateTime > m_lastMetadataRefresh) && (object)point != null)
                        {
                            try
                            {
                                List<string> updatedAttributes = new List<string>();

                                Action<string, string> updateAttribute = (attribute, newValue) =>
                                {
                                    string oldValue = point.GetAttribute(attribute) as string;

                                    if (string.IsNullOrEmpty(oldValue) || string.CompareOrdinal(oldValue, newValue) != 0)
                                    {
                                        point.SetAttribute(attribute, newValue);
                                        updatedAttributes.Add(attribute);
                                    }
                                };

                                // Load current attributes
                                point.LoadAttributes(
                                    PICommonPointAttributes.PointSource,
                                    PICommonPointAttributes.Descriptor,
                                    PICommonPointAttributes.ExtendedDescriptor,
                                    PICommonPointAttributes.Tag,
                                    PICommonPointAttributes.EngineeringUnits);

                                // Update tag meta-data if it has changed
                                updateAttribute(PICommonPointAttributes.PointSource, m_pointSource);
                                updateAttribute(PICommonPointAttributes.Descriptor, measurementRow["Description"].ToString());
                                updateAttribute(PICommonPointAttributes.ExtendedDescriptor, measurementRow["SignalID"].ToString());
                                updateAttribute(PICommonPointAttributes.Tag, tagName);

                                // Engineering units is a new field for this view -- handle the case that it's not there
                                if (measurements.Columns.Contains("EngineeringUnits"))
                                    updateAttribute(PICommonPointAttributes.EngineeringUnits, measurementRow["EngineeringUnits"].ToString());

                                // Save any changes
                                if (updatedAttributes.Count > 0)
                                    point.SaveAttributes(updatedAttributes.ToArray());
                            }
                            catch (Exception ex)
                            {
                                OnProcessException(new InvalidOperationException(string.Format("Failed to update PI tag '{0}' metadata from measurement '{1}': {2}", tagName, key, ex.Message), ex));
                            }
                        }

                        processed++;
                        m_metadataRefreshProgress = processed / (double)total;

                        if (processed % 100 == 0)
                        {
                            OnStatusMessage("Updated {0:N0} PI tags and associated metadata, {1:0.00%} complete...", processed, m_metadataRefreshProgress);
                        }

                        // If mapping for this connection was removed, it may have been because there was no meta-data so
                        // we re-add key to dictionary with null value, actual mapping will happen dynamically as needed
                        if (!m_mappedConnectionPoints.ContainsKey(key))
                            m_mappedConnectionPoints.TryAdd(key, null);
                    }

                    if ((object)database != null)
                        database.Dispose();

                    if (m_tagMap.Count > 0 && Enabled)
                    {
                        // Cache tag-map for faster future PI adapter startup
                        try
                        {
                            OnStatusMessage("Caching tag-map for faster future loads...");

                            using (FileStream tagMapCache = File.Create(m_tagMapCacheFileName))
                            {
                                Serialization.Serialize(m_tagMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), GSF.SerializationFormat.Binary, tagMapCache);
                            }

                            OnStatusMessage("Tag-map cached for faster future loads.");
                        }
                        catch (Exception ex)
                        {
                            OnProcessException(new InvalidOperationException(string.Format("Failed to cache tag-map to file '{0}': {1}", m_tagMapCacheFileName, ex.Message), ex));
                        }
                    }
                }
                else
                {
                    OnStatusMessage("OSI-PI historian output adapter is not configured to receive any input measurements - metadata refresh canceled.");
                }
            }
            catch (Exception ex)
            {
                OnProcessException(ex);
            }
            finally
            {
                m_refreshingMetadata = false;
            }

            if (Enabled)
            {
                m_lastMetadataRefresh = latestUpdateTime > DateTime.MinValue ? latestUpdateTime : DateTime.UtcNow;

                OnStatusMessage("Completed metadata refresh successfully.");

                // Re-establish connection point dictionary since meta-data and tags may exist in PI server now that didn't before.
                // This will also start showing warning messages in CreateMappedConnectionPoint function for un-mappable points
                // now that meta-data refresh has completed.
                EstablishConnectionPointDictionary(inputMeasurements);
            }

            // Restore original measurement reporting interval
            MeasurementReportingInterval = previousMeasurementReportingInterval;
        }

        // Resets the PI tag to MeasurementKey mapping for loading data into PI by finding PI points that match either the GSFSchema point tag or alternate tag
        private void EstablishConnectionPointDictionary(MeasurementKey[] inputMeasurements)
        {
            OnStatusMessage("Establishing connection points for mapping...");

            List<MeasurementKey> newTags = new List<MeasurementKey>();

            if ((object)inputMeasurements != null && inputMeasurements.Length > 0)
            {
                foreach (MeasurementKey key in inputMeasurements)
                {
                    // Add key to dictionary with null value if not defined, actual mapping will happen dynamically as needed
                    if (!m_mappedConnectionPoints.ContainsKey(key))
                        m_mappedConnectionPoints.TryAdd(key, null);

                    newTags.Add(key);
                }
            }

            if (newTags.Count > 0)
            {
                // Determine which tags no longer exist
                ConnectionPoint removedConnectionPoint;
                HashSet<MeasurementKey> tagsToRemove = new HashSet<MeasurementKey>(m_mappedConnectionPoints.Keys);

                // If there are existing tags that are not part of new updates, these need to be removed
                tagsToRemove.ExceptWith(newTags);

                if (tagsToRemove.Count > 0)
                {
                    foreach (MeasurementKey key in tagsToRemove)
                    {
                        m_mappedConnectionPoints.TryRemove(key, out removedConnectionPoint);
                    }

                    OnStatusMessage("Detected {0:N0} tags that have been removed from OSI-PI output - primary tag-map has been updated...", tagsToRemove.Count);
                }

                if (m_mappedConnectionPoints.Count == 0)
                    OnStatusMessage("WARNING: No PI tags were mapped to measurements - no tag-map exists so no points will be archived.");
            }
            else
            {
                if (m_mappedConnectionPoints.Count > 0)
                    OnStatusMessage("WARNING: No PI tags were mapped to measurements - existing tag-map with {0:N0} tags remains in use.", m_mappedConnectionPoints.Count);
                else
                    OnStatusMessage("WARNING: No PI tags were mapped to measurements - no tag-map exists so no points will be archived.");
            }
        }

        private void LoadCachedTagMap()
        {
            // Map measurement Guids to any existing point tags for faster PI point name resolution
            if (File.Exists(m_tagMapCacheFileName))
            {
                // Attempt to load tag-map from existing cache file
                try
                {
                    using (FileStream tagMapCache = File.Open(m_tagMapCacheFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        m_tagMap.Merge(true, Serialization.Deserialize<Dictionary<Guid, string>>(tagMapCache, GSF.SerializationFormat.Binary));

                        OnStatusMessage("Loaded {0:N0} mappings from tag-map cache.", m_tagMap.Count);

                        // Use last write time of file as the last meta-data refresh time - rough value is OK
                        if (m_lastMetadataRefresh == DateTime.MinValue)
                            m_lastMetadataRefresh = File.GetLastWriteTimeUtc(m_tagMapCacheFileName);
                    }
                }
                catch (Exception ex)
                {
                    OnProcessException(new InvalidOperationException(string.Format("Failed to load mappings from cached tag-map to file '{0}': {1}", m_tagMapCacheFileName, ex.Message), ex));
                }
            }
        }

        // It is recommended that the GetPIPoint overloads are called from a PIConnection.Execute function
        private PIPoint GetPIPoint(PIServer server, string tagName)
        {
            PIPoint point;

            PIPoint.TryFindPIPoint(server, tagName, out point);

            return point;
        }

        private PIPoint GetPIPoint(PIServer server, Guid signalID)
        {
            string cachedTagName;
            return GetPIPoint(server, signalID, out cachedTagName);
        }

        private PIPoint GetPIPoint(PIServer server, Guid signalID, out string cachedTagName)
        {
            PIPoint point = null;

            // See if cached mapping exists between guid and tag name - tag name lookups are faster than GetPoints query
            if (m_tagMap.TryGetValue(signalID, out cachedTagName))
                point = GetPIPoint(server, cachedTagName);

            if ((object)point == null)
            {
                // Point was not previously cached, lookup tag using signal ID stored in extended description field
                IEnumerable<PIPoint> points = PIPoint.FindPIPoints(server, string.Format("EXDESC='{0}'", signalID), false, new[] { PICommonPointAttributes.ExtendedDescriptor }, AFSearchTextOption.ExactMatch);

                point = points.FirstOrDefault();

                if ((object)point != null)
                    cachedTagName = point.Name;
            }

            return point;
        }

        private PIPoint GetPIPoint(PIServer server, Guid signalID, string tagName)
        {
            string cachedTagName;

            PIPoint point = GetPIPoint(server, signalID, out cachedTagName);

            // If point was not found in cache or cached tag name does not match current tag name, attempt to lookup using current tag name
            if ((object)point == null || string.Compare(cachedTagName, tagName, StringComparison.OrdinalIgnoreCase) != 0)
                point = GetPIPoint(server, tagName);

            return point;
        }

        private void m_connection_Disconnected(object sender, EventArgs e)
        {
            // Since we may get a plethora of these requests, we use a synchronized operation to restart once
            m_restartConnection.RunOnceAsync();
        }

        #endregion
    }
}