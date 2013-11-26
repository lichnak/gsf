﻿//******************************************************************************************************
//  UnsynchronizedClientSubscription.cs - Gbtc
//
//  Copyright © 2012, Grid Protection Alliance.  All Rights Reserved.
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
//  06/24/2011 - Ritchie
//       Generated original version of source code.
//  12/20/2012 - Starlynn Danyelle Gilliam
//       Modified Header.
//
//******************************************************************************************************

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Timers;
using GSF.Collections;
using GSF.IO;
using GSF.Parsing;
using GSF.TimeSeries.Adapters;
using Timer = System.Timers.Timer;

namespace GSF.TimeSeries.Transport
{
    /// <summary>
    /// Represents an unsynchronized client subscription to the <see cref="DataPublisher" />.
    /// </summary>
    internal class UnsynchronizedClientSubscription : FacileActionAdapterBase, IClientSubscription
    {
        #region [ Members ]

        // Events

        /// <summary>
        /// Indicates that a buffer block needed to be retransmitted because
        /// it was previously sent, but no confirmation was received.
        /// </summary>
        public event EventHandler BufferBlockRetransmission;

        /// <summary>
        /// Indicates to the host that processing for an input adapter (via temporal session) has completed.
        /// </summary>
        /// <remarks>
        /// This event is expected to only be raised when an input adapter has been designed to process
        /// a finite amount of data, e.g., reading a historical range of data during temporal processing.
        /// </remarks>
        public event EventHandler<EventArgs<IClientSubscription, EventArgs>> ProcessingComplete;

        // Fields
        private readonly AsyncDoubleBufferedQueue<ITimeSeriesEntity> m_processQueue;
        private readonly SignalIndexCache m_signalIndexCache;
        private readonly Guid m_clientID;
        private readonly Guid m_subscriberID;
        private DataPublisher m_parent;
        private string m_hostName;
        private volatile byte m_compressionStrength;
        private volatile bool m_usePayloadCompression;
        private volatile bool m_useCompactMeasurementFormat;
        private long m_lastPublishTime;
        private string m_requestedInputFilter;
        private double m_publishInterval;
        private bool m_includeTime;
        private bool m_useMillisecondResolution;
        private volatile long[] m_baseTimeOffsets;
        private volatile int m_timeIndex;
        private Timer m_baseTimeRotationTimer;
        private volatile bool m_initializedBaseTimeOffsets;
        private volatile bool m_startTimeSent;
        private IaonSession m_iaonSession;
        private int m_processThreadState;

        private readonly BlockAllocatedMemoryStream m_workingBuffer;
        private readonly List<byte[]> m_bufferBlockCache;
        private readonly object m_bufferBlockCacheLock;
        private uint m_bufferBlockSequenceNumber;
        private uint m_expectedBufferBlockConfirmationNumber;
        private Timer m_bufferBlockRetransmissionTimer;
        private double m_bufferBlockRetransmissionTimeout;

        private bool m_disposed;

        #endregion

        #region [ Constructors ]

        /// <summary>
        /// Creates a new <see cref="UnsynchronizedClientSubscription"/>.
        /// </summary>
        /// <param name="parent">Reference to parent.</param>
        /// <param name="clientID"><see cref="Guid"/> based client connection ID.</param>
        /// <param name="subscriberID"><see cref="Guid"/> based subscriber ID.</param>
        public UnsynchronizedClientSubscription(DataPublisher parent, Guid clientID, Guid subscriberID)
        {
            m_parent = parent;
            m_clientID = clientID;
            m_subscriberID = subscriberID;

            m_signalIndexCache = new SignalIndexCache();
            m_signalIndexCache.SubscriberID = subscriberID;

            m_processQueue = new AsyncDoubleBufferedQueue<ITimeSeriesEntity>();
            m_processQueue.ProcessException += (sender, e) => OnProcessException(e.Argument);

            m_workingBuffer = new BlockAllocatedMemoryStream();
            m_bufferBlockCache = new List<byte[]>();
            m_bufferBlockCacheLock = new object();

            InputSignalsUpdated += UnsynchronizedClientSubscription_InputSignalsUpdated;
        }

        #endregion

        #region [ Properties ]

        /// <summary>
        /// Gets the <see cref="Guid"/> client TCP connection identifier of this <see cref="UnsynchronizedClientSubscription"/>.
        /// </summary>
        public Guid ClientID
        {
            get
            {
                return m_clientID;
            }
        }

        /// <summary>
        /// Gets the <see cref="Guid"/> based subscriber ID of this <see cref="UnsynchronizedClientSubscription"/>.
        /// </summary>
        public Guid SubscriberID
        {
            get
            {
                return m_subscriberID;
            }
        }

        /// <summary>
        /// Gets the current signal index cache of this <see cref="UnsynchronizedClientSubscription"/>.
        /// </summary>
        public SignalIndexCache SignalIndexCache
        {
            get
            {
                return m_signalIndexCache;
            }
        }

        /// <summary>
        /// Gets the input filter requested by the subscriber when establishing this <see cref="IClientSubscription"/>.
        /// </summary>
        public string RequestedInputFilter
        {
            get
            {
                return m_requestedInputFilter;
            }
        }

        /// <summary>
        /// Gets or sets flag that determines if payload compression should be enabled in data packets of this <see cref="UnsynchronizedClientSubscription"/>.
        /// </summary>
        public bool UsePayloadCompression
        {
            get
            {
                return m_usePayloadCompression;
            }
            set
            {
                m_usePayloadCompression = value;

                if (m_usePayloadCompression)
                    m_useCompactMeasurementFormat = true;
            }
        }

        /// <summary>
        /// Gets or sets the compression strength value to use when <see cref="UsePayloadCompression"/> is <c>true</c> for this <see cref="UnsynchronizedClientSubscription"/>.
        /// </summary>
        public int CompressionStrength
        {
            get
            {
                return m_compressionStrength;
            }
            set
            {
                if (value < 0)
                    value = 0;

                if (value > 31)
                    value = 31;

                m_compressionStrength = (byte)value;
            }
        }

        /// <summary>
        /// Gets or sets flag that determines if the compact measurement format should be used in data packets of this <see cref="UnsynchronizedClientSubscription"/>.
        /// </summary>
        public bool UseCompactMeasurementFormat
        {
            get
            {
                return m_useCompactMeasurementFormat;
            }
            set
            {
                m_useCompactMeasurementFormat = value || m_usePayloadCompression;
            }
        }

        /// <summary>
        /// Gets size of timestamp in bytes.
        /// </summary>
        public int TimestampSize
        {
            get
            {
                if (!m_useCompactMeasurementFormat)
                    return 8;

                if (!m_includeTime)
                    return 0;

                if (!m_parent.UseBaseTimeOffsets)
                    return 8;

                return !m_useMillisecondResolution ? 4 : 2;
            }
        }

        /// <summary>
        /// Gets or sets host name used to identify connection source of client subscription.
        /// </summary>
        public string HostName
        {
            get
            {
                return m_hostName;
            }
            set
            {
                m_hostName = value;
            }
        }

        /// <summary>
        /// Gets or sets the desired processing interval, in milliseconds, for the adapter.
        /// </summary>
        /// <remarks>
        /// With the exception of the values of -1 and 0, this value specifies the desired processing interval for data, i.e.,
        /// basically a delay, or timer interval, over which to process data. A value of -1 means to use the default processing
        /// interval while a value of 0 means to process data as fast as possible.
        /// </remarks>
        public override int ProcessingInterval
        {
            get
            {
                return base.ProcessingInterval;
            }
            set
            {
                base.ProcessingInterval = value;

                // Update processing interval in private temporal session, if defined
                if ((object)m_iaonSession != null)
                    m_iaonSession.ProcessingInterval = value;
            }
        }

        /// <summary>
        /// Gets the flag indicating if this adapter supports temporal processing.
        /// </summary>
        /// <remarks>
        /// Although this adapter provisions support for temporal processing by proxying historical data to a remote sink, the adapter
        /// does not need to be automatically engaged within an actual temporal <see cref="IaonSession"/>, therefore this method returns
        /// <c>false</c> to make sure the adapter doesn't get automatically instantiated within a temporal session.
        /// </remarks>
        public override bool SupportsTemporalProcessing
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a formatted message describing the status of this <see cref="UnsynchronizedClientSubscription"/>.
        /// </summary>
        public override string Status
        {
            get
            {
                StringBuilder status = new StringBuilder();
                ClientConnection connection;

                if (m_parent.ClientConnections.TryGetValue(m_clientID, out connection))
                {
                    status.Append(connection.Status);
                    status.AppendLine();
                }

                status.Append(base.Status);

                if ((object)m_iaonSession != null)
                    status.Append(m_iaonSession.Status);

                return status.ToString();
            }
        }

        #endregion

        #region [ Methods ]

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="UnsynchronizedClientSubscription"/> object and optionally releases the managed resources.
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
                        // Remove reference to parent
                        m_parent = null;

                        // Dispose base time rotation timer
                        if ((object)m_baseTimeRotationTimer != null)
                        {
                            m_baseTimeRotationTimer.Dispose();
                            m_baseTimeRotationTimer = null;
                        }

                        if ((object)m_workingBuffer != null)
                            m_workingBuffer.Dispose();

                        // Dispose Iaon session
                        this.DisposeTemporalSession(ref m_iaonSession);
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
        /// Initializes <see cref="UnsynchronizedClientSubscription"/>.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();

            string setting;

            if (Settings.TryGetValue("inputMeasurementKeys", out setting))
                m_requestedInputFilter = setting;
            else
                m_requestedInputFilter = null;

            if (Settings.TryGetValue("publishInterval", out setting))
                m_publishInterval = int.Parse(setting);
            else
                m_publishInterval = -1;

            if (Settings.TryGetValue("includeTime", out setting))
                m_includeTime = setting.ParseBoolean();
            else
                m_includeTime = true;

            if (Settings.TryGetValue("useMillisecondResolution", out setting))
                m_useMillisecondResolution = setting.ParseBoolean();
            else
                m_useMillisecondResolution = false;

            if (Settings.TryGetValue("bufferBlockRetransmissionTimeout", out setting))
                m_bufferBlockRetransmissionTimeout = double.Parse(setting);
            else
                m_bufferBlockRetransmissionTimeout = 5.0D;

            if (m_parent.UseBaseTimeOffsets && m_includeTime)
            {
                m_baseTimeRotationTimer = new Timer();
                m_baseTimeRotationTimer.Interval = m_useMillisecondResolution ? 60000 : 420000;
                m_baseTimeRotationTimer.AutoReset = true;
                m_baseTimeRotationTimer.Elapsed += BaseTimeRotationTimer_Elapsed;
            }

            m_bufferBlockRetransmissionTimer = new Timer();
            m_bufferBlockRetransmissionTimer.AutoReset = false;
            m_bufferBlockRetransmissionTimer.Interval = m_bufferBlockRetransmissionTimeout * 1000.0D;
            m_bufferBlockRetransmissionTimer.Elapsed += BufferBlockRetransmissionTimer_Elapsed;

            // Handle temporal session initialization
            if (this.TemporalConstraintIsDefined())
                m_iaonSession = this.CreateTemporalSession();
        }

        /// <summary>
        /// Starts the <see cref="UnsynchronizedClientSubscription"/> or restarts it if it is already running.
        /// </summary>
        public override void Start()
        {
            Thread processThread;

            if (!Enabled)
                m_startTimeSent = false;

            base.Start();

            if ((object)m_baseTimeRotationTimer != null && m_includeTime)
                m_baseTimeRotationTimer.Start();

            // If state is stopping (1), set it to running (2)
            if (Interlocked.CompareExchange(ref m_processThreadState, 2, 1) == 0)
            {
                // If state is stopped (0), set it to running (2) and run the new thread
                if (Interlocked.CompareExchange(ref m_processThreadState, 2, 0) == 0)
                {
                    processThread = new Thread(ProcessMeasurements);
                    processThread.IsBackground = true;
                    processThread.Start();
                }
            }
        }

        /// <summary>
        /// Stops the <see cref="UnsynchronizedClientSubscription"/>.
        /// </summary>	
        public override void Stop()
        {
            base.Stop();

            // If state is running (2), set it to stopping (1)
            Interlocked.CompareExchange(ref m_processThreadState, 1, 2);

            if ((object)m_baseTimeRotationTimer != null)
            {
                m_baseTimeRotationTimer.Stop();
                m_baseTimeOffsets = null;
            }

            m_initializedBaseTimeOffsets = false;
        }

        /// <summary>
        /// Gets a short one-line status of this <see cref="UnsynchronizedClientSubscription"/>.
        /// </summary>
        /// <param name="maxLength">Maximum number of available characters for display.</param>
        /// <returns>A short one-line summary of the current status of this <see cref="AdapterBase"/>.</returns>
        public override string GetShortStatus(int maxLength)
        {
            return string.Format("Total input measurements: {0}, total output measurements: {1}", InputSignals.Count, OutputSignals.Count).PadLeft(maxLength);
        }

        /// <summary>
        /// Queues a collection of measurements for processing.
        /// </summary>
        /// <param name="entities">Collection of measurements to queue for processing.</param>
        /// <remarks>
        /// Measurements are filtered against the defined <see cref="AdapterBase.InputSignals"/> so we override method
        /// so that dynamic updates to keys will be synchronized with filtering to prevent interference.
        /// </remarks>
        public override void QueueEntitiesForProcessing(IEnumerable<ITimeSeriesEntity> entities)
        {
            if ((object)entities == null)
                return;

            if (!m_startTimeSent && entities.Any())
            {
                m_startTimeSent = true;

                ITimeSeriesEntity measurement = entities.FirstOrDefault(m => (object)m != null);
                Ticks timestamp = 0;

                if ((object)measurement != null)
                    timestamp = measurement.Timestamp;

                m_parent.SendDataStartTime(m_clientID, timestamp);
            }

            if (!entities.Any() || !Enabled)
                return;

            if (TrackLatestEntities)
            {
                double publishInterval;

                // Keep track of latest measurements
                base.QueueEntitiesForProcessing(entities);
                publishInterval = (m_publishInterval > 0) ? m_publishInterval : LagTime;

                if (DateTime.UtcNow.Ticks > m_lastPublishTime + Ticks.FromSeconds(publishInterval))
                {
                    //List<IMeasurement> currentMeasurements = new List<IMeasurement>();
                    //Measurement newMeasurement;

                    //// Create a new set of measurements that represent the latest known values setting value to NaN if it is old
                    //foreach (TemporalMeasurement measurement in LatestEntities)
                    //{
                    //    newMeasurement = new Measurement
                    //    {
                    //        ID = measurement.ID,
                    //        Key = measurement.Key,
                    //        Value = measurement.GetValue(RealTime),
                    //        Adder = measurement.Adder,
                    //        Multiplier = measurement.Multiplier,
                    //        Timestamp = measurement.Timestamp,
                    //        StateFlags = measurement.StateFlags
                    //    };

                    //    currentMeasurements.Add(newMeasurement);
                    //}

                    // Publish latest data values...
                    if ((object)m_processQueue != null)
                    {
                        // TODO: Re-enable sorting at compressed payload serialization at a later serialization
                        // Order measurements by signal type for better compression when enabled
                        //if (m_usePayloadCompression)
                        //    m_processQueue.Enqueue(currentMeasurements.OrderBy(measurement => measurement.GetSignalType(DataSource)));
                        //else                        
                        //m_processQueue.Enqueue(currentMeasurements);

                        m_processQueue.Enqueue(LatestEntities);
                    }
                }
            }
            else
            {
                // Publish unsynchronized on data receipt otherwise...
                if ((object)m_processQueue != null)
                {
                    // TODO: Re-enable sorting at compressed payload serialization at a later serialization
                    //// Order measurements by signal type for better compression when enabled
                    //if (m_usePayloadCompression)
                    //    m_processQueue.Enqueue(entities.OrderBy(measurement => measurement.GetSignalType(DataSource)));
                    //else
                    
                    m_processQueue.Enqueue(entities);
                }
            }
        }

        /// <summary>
        /// Handles the confirmation message received from the
        /// subscriber to indicate that a buffer block was received.
        /// </summary>
        /// <param name="sequenceNumber">The sequence number of the buffer block.</param>
        /// <returns>A list of buffer block sequence numbers for blocks that need to be retransmitted.</returns>
        public void ConfirmBufferBlock(uint sequenceNumber)
        {
            int sequenceIndex;
            int removalCount;

            // We are still receiving confirmations,
            // so stop the retransmission timer
            m_bufferBlockRetransmissionTimer.Stop();

            lock (m_bufferBlockCacheLock)
            {
                // Find the buffer block's location in the cache
                sequenceIndex = (int)(sequenceNumber - m_expectedBufferBlockConfirmationNumber);

                if (sequenceIndex >= 0 && sequenceIndex < m_bufferBlockCache.Count && (object)m_bufferBlockCache[sequenceIndex] != null)
                {
                    // Remove the confirmed block from the cache
                    m_bufferBlockCache[sequenceIndex] = null;

                    if (sequenceNumber == m_expectedBufferBlockConfirmationNumber)
                    {
                        // Get the number of elements to trim from the start of the cache
                        removalCount = m_bufferBlockCache.TakeWhile(m => (object)m == null).Count();

                        // Trim the cache
                        m_bufferBlockCache.RemoveRange(0, removalCount);

                        // Increase the expected confirmation number
                        m_expectedBufferBlockConfirmationNumber += (uint)removalCount;
                    }
                    else
                    {
                        // Retransmit if confirmations are received out of order
                        for (int i = 0; i < sequenceIndex; i++)
                        {
                            if ((object)m_bufferBlockCache[i] != null)
                            {
                                m_parent.SendClientResponse(m_clientID, ServerResponse.BufferBlock, ServerCommand.Subscribe, m_bufferBlockCache[i]);
                                OnBufferBlockRetransmission();
                            }
                        }
                    }
                }

                // If there are any objects lingering in the
                // cache, start the retransmission timer
                if (m_bufferBlockCache.Count > 0)
                    m_bufferBlockRetransmissionTimer.Start();
            }
        }

        private void ProcessMeasurements()
        {
            SpinWait spinner = new SpinWait();

            // If state is stopping (1), set it to stopped (0)
            // If state is running (2), continue looping
            while (Interlocked.CompareExchange(ref m_processThreadState, 0, 1) == 2)
            {
                try
                {
                    if ((object)m_parent == null || m_disposed)
                        return;

                    // Includes data packet flags and measurement count
                    const int PacketHeaderSize = DataPublisher.ClientResponseHeaderSize + 5;

                    IEnumerable<ITimeSeriesEntity> signals;
                    List<IBinaryMeasurement> packet = new List<IBinaryMeasurement>();
                    bool usePayloadCompression = m_usePayloadCompression;
                    bool useCompactMeasurementFormat = m_useCompactMeasurementFormat || usePayloadCompression;
                    TimeSeriesBuffer timeSeriesBuffer;
                    IBinaryMeasurement binaryMeasurement;
                    byte[] bufferBlock;
                    int binaryLength;
                    int packetSize = PacketHeaderSize;
                    ushort bufferBlockSignalIndex;

                    // If a set of base times has not yet been initialized, initialize a set by rotating
                    if (!m_initializedBaseTimeOffsets)
                    {
                        if (m_parent.UseBaseTimeOffsets)
                            RotateBaseTimes();

                        m_initializedBaseTimeOffsets = true;
                    }

                    signals = m_processQueue.Dequeue();

                    foreach (ITimeSeriesEntity signal in signals)
                    {
                        timeSeriesBuffer = signal as TimeSeriesBuffer;

                        if ((object)timeSeriesBuffer != null)
                        {
                            // Still sending buffer block measurements to client; we are expecting
                            // confirmations which will indicate whether retransmission is necessary,
                            // so we will restart the retransmission timer
                            m_bufferBlockRetransmissionTimer.Stop();

                            // Handle buffer block measurements as a special case - this can be any kind of data,
                            // measurement subscriber will need to know how to interpret buffer
                            bufferBlock = new byte[6 + timeSeriesBuffer.Length];

                            // Prepend sequence number
                            EndianOrder.BigEndian.CopyBytes(m_bufferBlockSequenceNumber, bufferBlock, 0);
                            m_bufferBlockSequenceNumber++;

                            // Copy signal index into buffer
                            bufferBlockSignalIndex = m_signalIndexCache.GetSignalIndex(timeSeriesBuffer.ID);
                            EndianOrder.BigEndian.CopyBytes(bufferBlockSignalIndex, bufferBlock, 4);

                            // Append measurement data and send
                            Buffer.BlockCopy(timeSeriesBuffer.Buffer, 0, bufferBlock, 6, timeSeriesBuffer.Length);
                            m_parent.SendClientResponse(m_workingBuffer, m_clientID, ServerResponse.BufferBlock, ServerCommand.Subscribe, bufferBlock);

                            lock (m_bufferBlockCacheLock)
                            {
                                // Cache buffer block for retransmission
                                m_bufferBlockCache.Add(bufferBlock);
                            }

                            // Start the retransmission timer in case we never receive a confirmation
                            m_bufferBlockRetransmissionTimer.Start();
                        }
                        else
                        {
                            // Serialize the current measurement - if the time-series entity is a measurement...
                            IMeasurement<double> measurement = signal as IMeasurement<double>;

                            if ((object)measurement != null)
                            {
                                if (useCompactMeasurementFormat)
                                    binaryMeasurement = new CompactMeasurement(measurement, m_signalIndexCache, m_includeTime, m_baseTimeOffsets, m_timeIndex, m_useMillisecondResolution);
                                else
                                    binaryMeasurement = new SerializableMeasurement(measurement, m_parent.GetClientEncoding(m_clientID));

                                // Determine the size of the measurement in bytes.
                                binaryLength = binaryMeasurement.BinaryLength;

                                // If the current measurement will not fit in the packet based on the max
                                // packet size, process the current packet and start a new packet.
                                if (packetSize + binaryLength > DataPublisher.MaxPacketSize)
                                {
                                    ProcessBinaryMeasurements(packet, useCompactMeasurementFormat, usePayloadCompression);
                                    packet.Clear();
                                    packetSize = PacketHeaderSize;
                                }

                                // Add the current measurement to the packet.
                                packet.Add(binaryMeasurement);
                                packetSize += binaryLength;
                            }
                        }
                    }

                    // Process the remaining measurements.
                    if (packet.Count > 0)
                        ProcessBinaryMeasurements(packet, useCompactMeasurementFormat, usePayloadCompression);

                    // Update latency statistics
                    m_parent.UpdateLatencyStatistics(signals.Select(m => (long)(m_lastPublishTime - m.Timestamp)));

                    if (signals.Any())
                        spinner.Reset();
                    else
                        spinner.SpinOnce();
                }
                catch (Exception ex)
                {
                    OnProcessException(new InvalidOperationException(string.Format("Error processing measurements: {0}", ex.Message), ex));
                    spinner.SpinOnce();
                }
            }
        }

        private void ProcessBinaryMeasurements(IEnumerable<IBinaryMeasurement> measurements, bool useCompactMeasurementFormat, bool usePayloadCompression)
        {
            // Reset working buffer
            m_workingBuffer.SetLength(0);

            // Serialize data packet flags into response
            DataPacketFlags flags = DataPacketFlags.NoFlags; // No flags means bit is cleared, i.e., unsynchronized

            if (useCompactMeasurementFormat)
                flags |= DataPacketFlags.Compact;

            m_workingBuffer.WriteByte((byte)flags);

            // No frame level timestamp is serialized into the data packet since all data is unsynchronized and essentially
            // published upon receipt, however timestamps are optionally included in the serialized measurements.

            // Serialize total number of measurement values to follow
            m_workingBuffer.Write(EndianOrder.BigEndian.GetBytes(measurements.Count()), 0, 4);

            // Attempt compression when requested - encoding of compressed buffer only happens if size would be smaller than normal serialization
            if (!usePayloadCompression || !measurements.Cast<CompactMeasurement>().CompressPayload(m_workingBuffer, m_compressionStrength, m_includeTime, ref flags))
            {
                // Serialize measurements to data buffer
                foreach (IBinaryMeasurement measurement in measurements)
                {
                    measurement.CopyBinaryImageToStream(m_workingBuffer);
                }
            }

            // Update data packet flags if it has updated compression flags
            if ((flags & DataPacketFlags.Compressed) > 0)
            {
                m_workingBuffer.Seek(0, SeekOrigin.Begin);
                m_workingBuffer.WriteByte((byte)flags);
            }

            // Publish data packet to client
            if ((object)m_parent != null)
                m_parent.SendClientResponse(m_workingBuffer, m_clientID, ServerResponse.DataPacket, ServerCommand.Subscribe, m_workingBuffer.ToArray());

            // Track last publication time
            m_lastPublishTime = DateTime.UtcNow.Ticks;
        }

        // Retransmits all buffer blocks for which confirmation has not yet been received
        private void BufferBlockRetransmissionTimer_Elapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            lock (m_bufferBlockCacheLock)
            {
                foreach (byte[] bufferBlock in m_bufferBlockCache)
                {
                    if ((object)bufferBlock != null)
                    {
                        m_parent.SendClientResponse(m_clientID, ServerResponse.BufferBlock, ServerCommand.Subscribe, bufferBlock);
                        OnBufferBlockRetransmission();
                    }
                }
            }

            // Restart the retransmission timer
            m_bufferBlockRetransmissionTimer.Start();
        }

        // Rotates base time offsets
        private void RotateBaseTimes()
        {
            if ((object)m_parent != null && (object)m_baseTimeRotationTimer != null)
            {
                if ((object)m_baseTimeOffsets == null)
                {
                    m_baseTimeOffsets = new long[2];
                    m_baseTimeOffsets[0] = RealTime;
                    m_baseTimeOffsets[1] = RealTime + (long)m_baseTimeRotationTimer.Interval * Ticks.PerMillisecond;
                    m_timeIndex = 0;
                }
                else
                {
                    int oldIndex = m_timeIndex;

                    // Switch to newer timestamp
                    m_timeIndex ^= 1;

                    // Now make older timestamp the newer timestamp
                    m_baseTimeOffsets[oldIndex] = RealTime + (long)m_baseTimeRotationTimer.Interval * Ticks.PerMillisecond;
                }

                // Since this function will only be called periodically, there is no real benefit
                // to maintaining this memory stream at a member level
                using (BlockAllocatedMemoryStream responsePacket = new BlockAllocatedMemoryStream())
                {
                    responsePacket.Write(EndianOrder.BigEndian.GetBytes(m_timeIndex), 0, 4);
                    responsePacket.Write(EndianOrder.BigEndian.GetBytes(m_baseTimeOffsets[0]), 0, 8);
                    responsePacket.Write(EndianOrder.BigEndian.GetBytes(m_baseTimeOffsets[1]), 0, 8);

                    m_parent.SendClientResponse(m_clientID, ServerResponse.UpdateBaseTimes, ServerCommand.Subscribe, responsePacket.ToArray());
                }
            }
        }

        private void OnBufferBlockRetransmission()
        {
            if ((object)BufferBlockRetransmission != null)
                BufferBlockRetransmission(this, EventArgs.Empty);
        }

        // Update signal index cache unless "detaching" from any real-time signals
        private void UnsynchronizedClientSubscription_InputSignalsUpdated(object sender, EventArgs e)
        {
            lock (this)
            {
                if (InputSignals.Count > 0)
                {
                    m_parent.UpdateSignalIndexCache(m_clientID, m_signalIndexCache, InputSignals);

                    if ((object)DataSource != null && (object)m_signalIndexCache != null)
                        InputSignals.UnionWith(ParseFilterExpression(DataSource, false, string.Join("; ", m_signalIndexCache.AuthorizedSignalIDs)));
                }
            }
        }

        // Explicitly implement status message event bubbler to satisfy IClientSubscription interface
        void IClientSubscription.OnStatusMessage(string status)
        {
            OnStatusMessage(status);
        }

        // Explicitly implement process exception event bubbler to satisfy IClientSubscription interface
        void IClientSubscription.OnProcessException(Exception ex)
        {
            OnProcessException(ex);
        }

        // Explicitly implement processing completed event bubbler to satisfy IClientSubscription interface
        void IClientSubscription.OnProcessingCompleted(object sender, EventArgs e)
        {
            if ((object)ProcessingComplete != null)
                ProcessingComplete(sender, new EventArgs<IClientSubscription, EventArgs>(this, e));
        }

        private void BaseTimeRotationTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            RotateBaseTimes();
        }

        #endregion
    }
}