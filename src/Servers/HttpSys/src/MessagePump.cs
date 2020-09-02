// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpSys.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Server.HttpSys
{
    internal class MessagePump : IServer
    {
        private readonly ILogger _logger;
        private readonly HttpSysOptions _options;

        private int _maxAccepts;
        private int _acceptorCounts;

        private volatile int _stopping;
        private int _outstandingRequests;
        private readonly TaskCompletionSource<object> _shutdownSignal = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _shutdownSignalCompleted;

        private readonly ServerAddressesFeature _serverAddresses;

        public MessagePump(IOptions<HttpSysOptions> options, ILoggerFactory loggerFactory, IAuthenticationSchemeProvider authentication)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }
            _options = options.Value;
            Listener = new HttpSysListener(_options, loggerFactory);
            _logger = loggerFactory.CreateLogger<MessagePump>();

            if (_options.Authentication.Schemes != AuthenticationSchemes.None)
            {
                authentication.AddScheme(new AuthenticationScheme(HttpSysDefaults.AuthenticationScheme, displayName: _options.Authentication.AuthenticationDisplayName, handlerType: typeof(AuthenticationHandler)));
            }

            Features = new FeatureCollection();
            _serverAddresses = new ServerAddressesFeature();
            Features.Set<IServerAddressesFeature>(_serverAddresses);

            if (HttpApi.IsFeatureSupported(HttpApiTypes.HTTP_FEATURE_ID.HttpFeatureDelegateEx))
            {
                var delegationProperty = new ServerDelegationPropertyFeature(Listener.RequestQueue, _logger);
                Features.Set<IServerDelegationFeature>(delegationProperty);
            }

            _maxAccepts = _options.MaxAccepts;
        }

        internal HttpSysListener Listener { get; }

        internal IHttpApplication<object> Application { get; set; }

        public IFeatureCollection Features { get; }

        internal bool Stopping => _stopping == 1;

        public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
        {
            if (application == null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            var hostingUrlsPresent = _serverAddresses.Addresses.Count > 0;
            var serverAddressCopy = _serverAddresses.Addresses.ToList();
            _serverAddresses.Addresses.Clear();

            if (_serverAddresses.PreferHostingUrls && hostingUrlsPresent)
            {
                if (_options.UrlPrefixes.Count > 0)
                {
                    _logger.LogWarning(LoggerEventIds.ClearedPrefixes, $"Overriding endpoints added to {nameof(HttpSysOptions.UrlPrefixes)} since {nameof(IServerAddressesFeature.PreferHostingUrls)} is set to true." +
                        $" Binding to address(es) '{string.Join(", ", _serverAddresses.Addresses)}' instead. ");

                    Listener.Options.UrlPrefixes.Clear();
                }

                UpdateUrlPrefixes(serverAddressCopy);
            }
            else if (_options.UrlPrefixes.Count > 0)
            {
                if (hostingUrlsPresent)
                {
                    _logger.LogWarning(LoggerEventIds.ClearedAddresses, $"Overriding address(es) '{string.Join(", ", _serverAddresses.Addresses)}'. " +
                        $"Binding to endpoints added to {nameof(HttpSysOptions.UrlPrefixes)} instead.");

                    _serverAddresses.Addresses.Clear();
                }

            }
            else if (hostingUrlsPresent)
            {
                UpdateUrlPrefixes(serverAddressCopy);
            }
            else if (Listener.RequestQueue.Created)
            {
                _logger.LogDebug(LoggerEventIds.BindingToDefault, $"No listening endpoints were configured. Binding to {Constants.DefaultServerAddress} by default.");

                Listener.Options.UrlPrefixes.Add(Constants.DefaultServerAddress);
            }
            // else // Attaching to an existing queue, don't add a default.

            // Can't call Start twice
            Contract.Assert(Application == null);

            Contract.Assert(application != null);

            Application = new ApplicationWrapper<TContext>(application);

            Listener.Start();

            // Update server addresses after we start listening as port 0
            // needs to be selected at the point of binding.
            foreach (var prefix in _options.UrlPrefixes)
            {
                _serverAddresses.Addresses.Add(prefix.FullPrefix);
            }

            ActivateRequestProcessingLimits();

            return Task.CompletedTask;
        }

        private void ActivateRequestProcessingLimits()
        {
            for (int i = _acceptorCounts; i < _maxAccepts; i++)
            {
                ProcessRequestsWorker();
            }
        }

        private void UpdateUrlPrefixes(IList<string> serverAddressCopy)
        {
            foreach (var value in serverAddressCopy)
            {
                Listener.Options.UrlPrefixes.Add(value);
            }
        }

        internal int IncrementOutstandingRequest()
        {
            return Interlocked.Increment(ref _outstandingRequests);
        }

        internal int DecrementOutstandingRequest()
        {
            return Interlocked.Decrement(ref _outstandingRequests);
        }

        internal void SetShutdownSignal()
        {
            _shutdownSignal.TrySetResult(null);
        }

        // The message pump.
        // When we start listening for the next request on one thread, we may need to be sure that the
        // completion continues on another thread as to not block the current request processing.
        // The awaits will manage stack depth for us.
        private async void ProcessRequestsWorker()
        {
            int workerIndex = Interlocked.Increment(ref _acceptorCounts);
            while (!Stopping && workerIndex <= _maxAccepts)
            {
                // Receive a request
                RequestContext requestContext;
                try
                {
                    requestContext = await Listener.AcceptAsync().SupressContext();
                    // Assign the message pump to this request context
                    requestContext.MessagePump = this;
                }
                catch (Exception exception)
                {
                    Contract.Assert(Stopping);
                    if (Stopping)
                    {
                        _logger.LogDebug(LoggerEventIds.AcceptErrorStopping, exception, "Failed to accept a request, the server is stopping.");
                    }
                    else
                    {
                        _logger.LogError(LoggerEventIds.AcceptError, exception, "Failed to accept a request.");
                    }
                    continue;
                }
                try
                {
                    ThreadPool.UnsafeQueueUserWorkItem(requestContext, preferLocal: false);
                }
                catch (Exception ex)
                {
                    // Request processing failed to be queued in threadpool
                    // Log the error message, release throttle and move on
                    _logger.LogError(LoggerEventIds.RequestListenerProcessError, ex, "ProcessRequestAsync");
                }
            }
            Interlocked.Decrement(ref _acceptorCounts);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            void RegisterCancelation()
            {
                cancellationToken.Register(() =>
                {
                    if (Interlocked.Exchange(ref _shutdownSignalCompleted, 1) == 0)
                    {
                        _logger.LogInformation(LoggerEventIds.StopCancelled, "Canceled, terminating " + _outstandingRequests + " request(s).");
                        _shutdownSignal.TrySetResult(null);
                    }
                });
            }

            if (Interlocked.Exchange(ref _stopping, 1) == 1)
            {
                RegisterCancelation();

                return _shutdownSignal.Task;
            }

            try
            {
                // Wait for active requests to drain
                if (_outstandingRequests > 0)
                {
                    _logger.LogInformation(LoggerEventIds.WaitingForRequestsToDrain, "Stopping, waiting for " + _outstandingRequests + " request(s) to drain.");
                    RegisterCancelation();
                }
                else
                {
                    _shutdownSignal.TrySetResult(null);
                }
            }
            catch (Exception ex)
            {
                _shutdownSignal.TrySetException(ex);
            }

            return _shutdownSignal.Task;
        }

        public void Dispose()
        {
            _stopping = 1;
            _shutdownSignal.TrySetResult(null);

            Listener.Dispose();
        }

        private class ApplicationWrapper<TContext> : IHttpApplication<object>
        {
            private readonly IHttpApplication<TContext> _application;

            public ApplicationWrapper(IHttpApplication<TContext> application)
            {
                _application = application;
            }

            public object CreateContext(IFeatureCollection contextFeatures)
            {
                return _application.CreateContext(contextFeatures);
            }

            public void DisposeContext(object context, Exception exception)
            {
                _application.DisposeContext((TContext)context, exception);
            }

            public Task ProcessRequestAsync(object context)
            {
                return _application.ProcessRequestAsync((TContext)context);
            }
        }
    }
}
