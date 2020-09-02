// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure
{
    [EventSource(Name = "Microsoft-AspNetCore-Server-Kestrel")]
    internal sealed class KestrelEventSource : EventSource
    {
        public static readonly KestrelEventSource Log = new KestrelEventSource();

        private IncrementingPollingCounter _connectionsPerSecondCounter;
        private IncrementingPollingCounter _tlsHandshakesPerSecondCounter;
        private PollingCounter _totalConnectionsCounter;
        private PollingCounter _currentConnectionsCounter;
        private PollingCounter _totalTlsHandshakesCounter;
        private PollingCounter _currentTlsHandshakesCounter;
        private PollingCounter _failedTlsHandshakesCounter;
        private PollingCounter _connectionQueueLengthCounter;
        private PollingCounter _httpRequestQueueLengthCounter;
        private PollingCounter _currrentUpgradedHttpRequestsCounter;

        private long _totalConnections;
        private long _currentConnections;
        private long _connectionQueueLength;
        private long _totalTlsHandshakes;
        private long _currentTlsHandshakes;
        private long _failedTlsHandshakes;
        private long _httpRequestQueueLength;
        private long _currentUpgradedHttpRequests;

        private KestrelEventSource()
        {
        }

        // NOTE
        // - The 'Start' and 'Stop' suffixes on the following event names have special meaning in EventSource. They
        //   enable creating 'activities'.
        //   For more information, take a look at the following blog post:
        //   https://blogs.msdn.microsoft.com/vancem/2015/09/14/exploring-eventsource-activity-correlation-and-causation-features/
        // - A stop event's event id must be next one after its start event.
        // - Avoid renaming methods or parameters marked with EventAttribute. EventSource uses these to form the event object.

        [NonEvent]
        public void ConnectionStart(BaseConnectionContext connection)
        {
            // avoid allocating strings unless this event source is enabled
            Interlocked.Increment(ref _totalConnections);
            Interlocked.Increment(ref _currentConnections);

            if (IsEnabled())
            {
                ConnectionStart(
                    connection.ConnectionId,
                    connection.LocalEndPoint?.ToString(),
                    connection.RemoteEndPoint?.ToString());
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Event(1, Level = EventLevel.Informational)]
        private void ConnectionStart(string connectionId,
            string localEndPoint,
            string remoteEndPoint)
        {
            WriteEvent(
                1,
                connectionId,
                localEndPoint,
                remoteEndPoint
            );
        }

        [NonEvent]
        public void ConnectionStop(BaseConnectionContext connection)
        {
            Interlocked.Decrement(ref _currentConnections);
            if (IsEnabled())
            {
                ConnectionStop(connection.ConnectionId);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Event(2, Level = EventLevel.Informational)]
        private void ConnectionStop(string connectionId)
        {
            WriteEvent(2, connectionId);
        }

        [NonEvent]
        public void RequestStart(HttpProtocol httpProtocol)
        {
            // avoid allocating the trace identifier unless logging is enabled
            if (IsEnabled())
            {
                RequestStart(httpProtocol.ConnectionIdFeature, httpProtocol.TraceIdentifier, httpProtocol.HttpVersion, httpProtocol.Path, httpProtocol.MethodText);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Event(3, Level = EventLevel.Informational)]
        private void RequestStart(string connectionId, string requestId, string httpVersion, string path, string method)
        {
            WriteEvent(3, connectionId, requestId, httpVersion, path, method);
        }

        [NonEvent]
        public void RequestStop(HttpProtocol httpProtocol)
        {
            // avoid allocating the trace identifier unless logging is enabled
            if (IsEnabled())
            {
                RequestStop(httpProtocol.ConnectionIdFeature, httpProtocol.TraceIdentifier, httpProtocol.HttpVersion, httpProtocol.Path, httpProtocol.MethodText);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Event(4, Level = EventLevel.Informational)]
        private void RequestStop(string connectionId, string requestId, string httpVersion, string path, string method)
        {
            WriteEvent(4, connectionId, requestId, httpVersion, path, method);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Event(5, Level = EventLevel.Informational)]
        public void ConnectionRejected(string connectionId)
        {
            if (IsEnabled())
            {
                WriteEvent(5, connectionId);
            }
        }

        [NonEvent]
        public void ConnectionQueuedStart(BaseConnectionContext connection)
        {
            Interlocked.Increment(ref _connectionQueueLength);
            if (IsEnabled())
            {
                ConnectionQueuedStart(
                    connection.ConnectionId,
                    connection.LocalEndPoint?.ToString(),
                    connection.RemoteEndPoint?.ToString());
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Event(6, Level = EventLevel.Informational)]
        private void ConnectionQueuedStart(string connectionId,
            string localEndPoint,
            string remoteEndPoint)
        {
            WriteEvent(
                6,
                connectionId,
                localEndPoint,
                remoteEndPoint
            );
        }

        [NonEvent]
        public void ConnectionQueuedStop(BaseConnectionContext connection)
        {
            Interlocked.Decrement(ref _connectionQueueLength);
            if (IsEnabled())
            {
                ConnectionQueuedStop(
                    connection.ConnectionId,
                    connection.LocalEndPoint?.ToString(),
                    connection.RemoteEndPoint?.ToString());
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Event(7, Level = EventLevel.Informational)]
        private void ConnectionQueuedStop(string connectionId,
            string localEndPoint,
            string remoteEndPoint)
        {
            WriteEvent(
                7,
                connectionId,
                localEndPoint,
                remoteEndPoint
            );
        }

        [NonEvent]
        public void TlsHandshakeStart(BaseConnectionContext connectionContext, SslServerAuthenticationOptions sslOptions)
        {
            Interlocked.Increment(ref _currentTlsHandshakes);
            Interlocked.Increment(ref _totalTlsHandshakes);
            if (IsEnabled())
            {
                TlsHandshakeStart(connectionContext.ConnectionId, sslOptions.EnabledSslProtocols.ToString());
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Event(8, Level = EventLevel.Informational)]
        private void TlsHandshakeStart(string connectionId, string sslProtocols)
        {
            WriteEvent(8, connectionId, sslProtocols);
        }

        [NonEvent]
        public void TlsHandshakeStop(BaseConnectionContext connectionContext, TlsConnectionFeature feature)
        {
            Interlocked.Decrement(ref _currentTlsHandshakes);
            if (IsEnabled())
            {
                // TODO: Write this without a string allocation using WriteEventData
                var applicationProtocol = feature == null ? string.Empty : Encoding.UTF8.GetString(feature.ApplicationProtocol.Span);
                var sslProtocols = feature?.Protocol.ToString() ?? string.Empty;
                var hostName = feature?.HostName ?? string.Empty;
                TlsHandshakeStop(connectionContext.ConnectionId, sslProtocols, applicationProtocol, hostName);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Event(9, Level = EventLevel.Informational)]
        private void TlsHandshakeStop(string connectionId, string sslProtocols, string applicationProtocol, string hostName)
        {
            WriteEvent(9, connectionId, sslProtocols, applicationProtocol, hostName);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Event(10, Level = EventLevel.Error)]
        public void TlsHandshakeFailed(string connectionId)
        {
            Interlocked.Increment(ref _failedTlsHandshakes);
            WriteEvent(10, connectionId);
        }

        [NonEvent]
        public void RequestQueuedStart(HttpProtocol httpProtocol, string httpVersion)
        {
            Interlocked.Increment(ref _httpRequestQueueLength);
            // avoid allocating the trace identifier unless logging is enabled
            if (IsEnabled())
            {
                RequestQueuedStart(httpProtocol.ConnectionIdFeature, httpProtocol.TraceIdentifier, httpVersion);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Event(11, Level = EventLevel.Informational)]
        private void RequestQueuedStart(string connectionId, string requestId, string httpVersion)
        {
            WriteEvent(11, connectionId, requestId, httpVersion);
        }

        [NonEvent]
        public void RequestQueuedStop(HttpProtocol httpProtocol, string httpVersion)
        {
            Interlocked.Decrement(ref _httpRequestQueueLength);
            // avoid allocating the trace identifier unless logging is enabled
            if (IsEnabled())
            {
                RequestQueuedStop(httpProtocol.ConnectionIdFeature, httpProtocol.TraceIdentifier, httpVersion);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Event(12, Level = EventLevel.Informational)]
        private void RequestQueuedStop(string connectionId, string requestId, string httpVersion)
        {
            WriteEvent(12, connectionId, requestId, httpVersion);
        }

        [NonEvent]
        public void RequestUpgradedStart(HttpProtocol httpProtocol)
        {
            Interlocked.Increment(ref _currentUpgradedHttpRequests);
            if (IsEnabled())
            {
                RequestUpgradedStart(httpProtocol.ConnectionIdFeature, httpProtocol.TraceIdentifier, httpProtocol.HttpVersion, httpProtocol.Path, httpProtocol.MethodText);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Event(13, Level = EventLevel.Informational)]
        private void RequestUpgradedStart(string connectionId, string requestId, string httpVersion, string path, string method)
        {
            WriteEvent(13, connectionId, requestId, httpVersion, path, method);
        }

        [NonEvent]
        public void RequestUpgradedStop(HttpProtocol httpProtocol)
        {
            Interlocked.Decrement(ref _currentUpgradedHttpRequests);
            if (IsEnabled())
            {
                RequestUpgradedStop(httpProtocol.ConnectionIdFeature, httpProtocol.TraceIdentifier, httpProtocol.HttpVersion, httpProtocol.Path, httpProtocol.MethodText);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Event(14, Level = EventLevel.Informational)]
        private void RequestUpgradedStop(string connectionId, string requestId, string httpVersion, string path, string method)
        {
            WriteEvent(14, connectionId, requestId, httpVersion, path, method);
        }

        protected override void OnEventCommand(EventCommandEventArgs command)
        {
            if (command.Command == EventCommand.Enable)
            {
                // This is the convention for initializing counters in the RuntimeEventSource (lazily on the first enable command).
                // They aren't disabled afterwards...

                _connectionsPerSecondCounter ??= new IncrementingPollingCounter("connections-per-second", this, () => _totalConnections)
                {
                    DisplayName = "Connection Rate",
                    DisplayRateTimeScale = TimeSpan.FromSeconds(1)
                };

                _totalConnectionsCounter ??= new PollingCounter("total-connections", this, () => _totalConnections)
                {
                    DisplayName = "Total Connections",
                };

                _tlsHandshakesPerSecondCounter ??= new IncrementingPollingCounter("tls-handshakes-per-second", this, () => _totalTlsHandshakes)
                {
                    DisplayName = "TLS Handshake Rate",
                    DisplayRateTimeScale = TimeSpan.FromSeconds(1)
                };

                _totalTlsHandshakesCounter ??= new PollingCounter("total-tls-handshakes", this, () => _totalTlsHandshakes)
                {
                    DisplayName = "Total TLS Handshakes",
                };

                _currentTlsHandshakesCounter ??= new PollingCounter("current-tls-handshakes", this, () => _currentTlsHandshakes)
                {
                    DisplayName = "Current TLS Handshakes"
                };

                _failedTlsHandshakesCounter ??= new PollingCounter("failed-tls-handshakes", this, () => _failedTlsHandshakes)
                {
                    DisplayName = "Failed TLS Handshakes"
                };

                _currentConnectionsCounter ??= new PollingCounter("current-connections", this, () => _currentConnections)
                {
                    DisplayName = "Current Connections"
                };

                _connectionQueueLengthCounter ??= new PollingCounter("connection-queue-length", this, () => _connectionQueueLength)
                {
                    DisplayName = "Connection Queue Length"
                };

                _httpRequestQueueLengthCounter ??= new PollingCounter("request-queue-length", this, () => _httpRequestQueueLength)
                {
                    DisplayName = "Request Queue Length"
                };

                _currrentUpgradedHttpRequestsCounter ??= new PollingCounter("current-upgraded-requests", this, () => _currentUpgradedHttpRequests)
                {
                    DisplayName = "Current Upgraded Requests (WebSockets)"
                };
            }
        }
    }
}
