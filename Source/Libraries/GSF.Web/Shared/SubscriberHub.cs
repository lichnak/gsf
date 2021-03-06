﻿//******************************************************************************************************
//  SubscriberHub.cs - Gbtc
//
//  Copyright © 2018, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the MIT License (MIT), the "License"; you may not use this
//  file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://opensource.org/licenses/MIT
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  07/12/2018 - Stephen C. Wills
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using GSF;
using GSF.Collections;
using GSF.Configuration;
using GSF.TimeSeries;
using GSF.TimeSeries.Transport;
using GSF.Web.Security;
using Microsoft.AspNet.SignalR;
using Newtonsoft.Json.Linq;

namespace openPDC
{
    /// <summary>
    /// SignalR hub that exposes server-side functions for the subscriber API.
    /// </summary>
    [AuthorizeHubRole]
    public class SubscriberHub : Hub
    {
        #region [ Members ]

        // Nested Types

        private sealed class Connection : IDisposable
        {
            public Dictionary<string, Subscriber> SubscriberLookup;
            private bool m_disposed;

            public Connection()
            {
                SubscriberLookup = new Dictionary<string, Subscriber>();
            }

            public void Dispose()
            {
                if (m_disposed)
                    return;

                try
                {
                    foreach (Subscriber subscriptionState in SubscriberLookup.Values)
                        subscriptionState.Dispose();
                }
                finally
                {
                    m_disposed = true;
                }
            }
        }

        private sealed class Subscriber : IDisposable
        {
            public DataSubscriber DataSubscriber;
            public DataSet Metadata;
            private bool m_disposed;

            public void Dispose()
            {
                if (m_disposed)
                    return;

                try
                {
                    DataSubscriber?.Dispose();
                    Metadata?.Dispose();
                }
                finally
                {
                    m_disposed = true;
                }
            }
        }

        #endregion

        #region [ Methods ]

        /// <summary>
        /// Initiates the subscriber connection.
        /// </summary>
        /// <param name="subscriberID">The ID of the subscriber to be connected.</param>
        public void Connect(string subscriberID)
        {
            Subscriber subscriber = GetOrCreate(subscriberID);
            subscriber.DataSubscriber.Start();
        }

        /// <summary>
        /// Sends a command to the publisher.
        /// </summary>
        /// <param name="subscriberID">The ID of the subscriber.</param>
        /// <param name="commandCode">The command to be sent.</param>
        /// <param name="message">The message to be sent to the publisher.</param>
        public void SendCommand(string subscriberID, ServerCommand commandCode, string message)
        {
            Subscriber subscriber = GetOrCreate(subscriberID);
            subscriber.DataSubscriber.SendServerCommand(commandCode, message);
        }

        /// <summary>
        /// Filters metadata and returns the result.
        /// </summary>
        /// <param name="subscriberID">The ID of the subscriber.</param>
        /// <param name="tableName">The metadata table from which to return rows.</param>
        /// <param name="filter">The filter to apply to the metadata table.</param>
        /// <param name="sortField">The field by which to sort the result set.</param>
        /// <param name="takeCount">The maximum number of records to be returned from the result set.</param>
        /// <returns>The result of the metadata query.</returns>
        public IEnumerable<object> GetMetadata(string subscriberID, string tableName, string filter, string sortField, int takeCount)
        {
            Subscriber subscriber = GetOrCreate(subscriberID);
            DataSet metadata = subscriber.Metadata;

            if ((object)metadata == null)
                return null;

            if (string.IsNullOrEmpty(tableName) || !metadata.Tables.Contains(tableName))
                return null;

            DataTable table = metadata.Tables[tableName];
            IEnumerable<DataRow> rows;

            if (string.IsNullOrEmpty(filter))
                rows = table.Select();
            else
                rows = table.Select(filter);

            if (!string.IsNullOrEmpty(sortField) && table.Columns.Contains(sortField))
                rows = rows.OrderBy(row => row[sortField]);

            if (takeCount > 0)
                rows = rows.Take(takeCount);

            return rows.Select(FromDataRow);
        }

        /// <summary>
        /// Subscribes to the internal data publisher.
        /// </summary>
        /// <param name="subscriberID">The ID of the subscriber.</param>
        /// <param name="data">Data from the client describing the subscription.</param>
        public void Subscribe(string subscriberID, JObject data)
        {
            Subscriber subscriber = GetOrCreate(subscriberID);
            SubscriptionInfo subscriptionInfo = ToSubscriptionInfo(data);
            subscriber.DataSubscriber.Subscribe(subscriptionInfo);
        }

        /// <summary>
        /// Unsubscribes from the publisher.
        /// </summary>
        /// <param name="subscriberID">The ID of the subscriber.</param>
        public void Unsubscribe(string subscriberID)
        {
            Subscriber subscriber = GetOrCreate(subscriberID);
            subscriber.DataSubscriber.Unsubscribe();
        }

        /// <summary>
        /// Disconnects from the publisher.
        /// </summary>
        /// <param name="subscriberID">The ID of the subscriber.</param>
        public void Disconnect(string subscriberID)
        {
            Subscriber subscriber = GetOrCreate(subscriberID);
            subscriber.DataSubscriber.Stop();
        }

        /// <summary>
        /// Closes the connection and releases all resources retained by the subscriber with this ID.
        /// </summary>
        /// <param name="subscriberID">The ID of the subscriber.</param>
        public void Dispose(string subscriberID)
        {
            string connectionID = Context.ConnectionId;

            if (s_connectionLookup.TryGetValue(connectionID, out Connection connection) && connection.SubscriberLookup.TryGetValue(subscriberID, out Subscriber subscriber))
            {
                connection.SubscriberLookup.Remove(subscriberID);
                subscriber.Dispose();
            }
        }

        /// <summary>
        /// Handles when a client disconnects from the hub.
        /// </summary>
        /// <param name="stopCalled">Indicates whether the client called stop.</param>
        /// <returns>The task to handle the disconnect.</returns>
        public override Task OnDisconnected(bool stopCalled)
        {
            return Task.Run(() =>
            {
                string connectionID = Context.ConnectionId;

                if (s_connectionLookup.TryRemove(connectionID, out Connection connection))
                    connection.Dispose();
            });
        }

        private Subscriber GetOrCreate(string subscriberID)
        {
            string connectionID = Context.ConnectionId;
            Connection connection = s_connectionLookup.GetOrAdd(connectionID, id => new Connection());

            return connection.SubscriberLookup.GetOrAdd(subscriberID, id =>
            {
                Subscriber subscriber = new Subscriber();
                subscriber.DataSubscriber = new DataSubscriber();
                subscriber.DataSubscriber.ConnectionEstablished += (sender, args) => Subscriber_ConnectionEstablished(connectionID, subscriberID);
                subscriber.DataSubscriber.ConnectionTerminated += (sender, args) => Subscriber_ConnectionTerminated(connectionID, subscriberID);
                subscriber.DataSubscriber.MetaDataReceived += (sender, args) => Subscriber_MetadataReceived(connectionID, subscriberID, subscriber, args.Argument);
                subscriber.DataSubscriber.ConfigurationChanged += (sender, args) => Subscriber_ConfigurationChanged(connectionID, subscriberID);
                subscriber.DataSubscriber.NewMeasurements += (sender, args) => Subscriber_NewMeasurements(connectionID, subscriberID, args.Argument);
                subscriber.DataSubscriber.StatusMessage += (sender, args) => Subscriber_StatusMessage(connectionID, subscriberID, args.Argument);
                subscriber.DataSubscriber.ProcessException += (sender, args) => Subscriber_ProcessException(connectionID, subscriberID, args.Argument);
                subscriber.DataSubscriber.ConnectionString = GetInternalPublisherConnectionString();
                subscriber.DataSubscriber.CompressionModes = CompressionModes.TSSC | CompressionModes.GZip;
                subscriber.DataSubscriber.AutoSynchronizeMetadata = false;
                subscriber.DataSubscriber.ReceiveInternalMetadata = true;
                subscriber.DataSubscriber.ReceiveExternalMetadata = true;
                subscriber.DataSubscriber.Initialize();
                return subscriber;
            });
        }

        private string GetInternalPublisherConnectionString()
        {
            ConfigurationFile configurationFile = ConfigurationFile.Current;
            CategorizedSettingsElementCollection internalDataPublisher = configurationFile.Settings["internaldatapublisher"];
            string configurationString = internalDataPublisher["ConfigurationString"]?.Value ?? "";
            Dictionary<string, string> settings = configurationString.ParseKeyValuePairs();

            if (!settings.TryGetValue("port", out string portSetting) || !int.TryParse(portSetting, out int port))
                port = 6165;

            if (settings.TryGetValue("interface", out string interfaceSetting))
                interfaceSetting = $"Interface={interfaceSetting}";
            else
                interfaceSetting = string.Empty;

            return $"Server=localhost:{port};{interfaceSetting};BypassStatistics=true";
        }

        private SubscriptionInfo ToSubscriptionInfo(JObject obj)
        {
            dynamic info = obj;
            bool synchronized = info.synchronized ?? false;

            if (synchronized)
                return ToSynchronizedSubscriptionInfo(obj);

            return ToUnsynchronizedSubscriptionInfo(obj);
        }

        private SynchronizedSubscriptionInfo ToSynchronizedSubscriptionInfo(JObject obj)
        {
            dynamic info = obj;

            if (info.remotelySynchronized == null)
                info.remotelySynchronized = info.RemotelySynchronized ?? false;

            if (info.framesPerSecond == null)
                info.framesPerSecond = info.FramesPerSecond ?? 30;

            return obj.ToObject<SynchronizedSubscriptionInfo>();
        }

        private UnsynchronizedSubscriptionInfo ToUnsynchronizedSubscriptionInfo(JObject obj)
        {
            dynamic info = obj;

            if (info.throttled == null)
                info.throttled = info.Throttled ?? false;

            return obj.ToObject<UnsynchronizedSubscriptionInfo>();
        }

        private object FromDataRow(DataRow dataRow)
        {
            JObject obj = new JObject();

            foreach (DataColumn dataColumn in dataRow.Table.Columns)
                obj[dataColumn.ColumnName] = JToken.FromObject(dataRow[dataColumn]);

            return obj;
        }

        private object FromMeasurement(IMeasurement measurement)
        {
            dynamic obj = new JObject();
            obj.signalID = measurement.ID;
            obj.value = measurement.Value;

            DateTime timestamp = measurement.Timestamp;
            DateTime epoch = UnixTimeTag.BaseTicks;
            obj.timestamp = (timestamp - epoch).TotalMilliseconds;
            return obj;
        }

        private void Subscriber_ConnectionEstablished(string connectionID, string subscriberID)
        {
            Clients.Client(connectionID).ConnectionEstablished(subscriberID);
        }

        private void Subscriber_ConnectionTerminated(string connectionID, string subscriberID)
        {
            Clients.Client(connectionID).ConnectionTerminated(connectionID, subscriberID);
        }

        private void Subscriber_NewMeasurements(string connectionID, string subscriberID, ICollection<IMeasurement> measurements)
        {
            object data = measurements
                .Select(FromMeasurement)
                .ToArray();

            Clients.Client(connectionID).NewMeasurements(subscriberID, data);
        }

        private void Subscriber_MetadataReceived(string connectionID, string subscriberID, Subscriber subscriber, DataSet metadata)
        {
            subscriber.Metadata = metadata;
            Clients.Client(connectionID).MetadataReceived(subscriberID);
        }

        private void Subscriber_ConfigurationChanged(string connectionID, string subscriberID)
        {
            Clients.Client(connectionID).ConfigurationChanged(subscriberID);
        }

        private void Subscriber_StatusMessage(string connectionID, string subscriberID, string message)
        {
            Clients.Client(connectionID).StatusMessage(subscriberID, message);
        }

        private void Subscriber_ProcessException(string connectionID, string subscriberID, Exception ex)
        {
            Clients.Client(connectionID).ProcessException(subscriberID, ex);
        }

        #endregion

        #region [ Static ]

        // Static Fields
        private static ConcurrentDictionary<string, Connection> s_connectionLookup;

        // Static Constructor
        static SubscriberHub()
        {
            s_connectionLookup = new ConcurrentDictionary<string, Connection>();
        }

        #endregion
    }
}