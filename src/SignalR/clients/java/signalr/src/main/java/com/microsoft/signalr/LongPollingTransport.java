// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

package com.microsoft.signalr;

import java.nio.ByteBuffer;
import java.nio.charset.StandardCharsets;
import java.util.Map;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.atomic.AtomicBoolean;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import io.reactivex.Completable;
import io.reactivex.Single;
import io.reactivex.subjects.CompletableSubject;

class LongPollingTransport implements Transport {
    private OnReceiveCallBack onReceiveCallBack;
    private TransportOnClosedCallback onClose;
    private String url;
    private final HttpClient client;
    private final HttpClient pollingClient;
    private final Map<String, String> headers;
    private static final int POLL_TIMEOUT = 100*1000;
    private final Single<String> accessTokenProvider;
    private volatile Boolean active = false;
    private String pollUrl;
    private String closeError;
    private CompletableSubject receiveLoop = CompletableSubject.create();
    private ExecutorService threadPool;
    private ExecutorService onReceiveThread;
    private AtomicBoolean stopCalled = new AtomicBoolean(false);

    private final Logger logger = LoggerFactory.getLogger(LongPollingTransport.class);

    public LongPollingTransport(Map<String, String> headers, HttpClient client, Single<String> accessTokenProvider) {
        this.headers = headers;
        this.client = client;
        this.pollingClient = client.cloneWithTimeOut(POLL_TIMEOUT);
        this.accessTokenProvider = accessTokenProvider;
    }

    //Package private active accessor for testing.
    boolean isActive() {
        return this.active;
    }

    private Completable updateHeaderToken() {
        return this.accessTokenProvider.doOnSuccess((token) -> {
            if (!token.isEmpty()) {
                this.headers.put("Authorization", "Bearer " + token);
            }
        }).ignoreElement();
    }

    @Override
    public Completable start(String url) {
        this.active = true;
        logger.debug("Starting LongPolling transport.");
        this.url = url;
        pollUrl = url + "&_=" + System.currentTimeMillis();
        logger.debug("Polling {}.", pollUrl);
        return this.updateHeaderToken().andThen(Completable.defer(() -> {
            HttpRequest request = new HttpRequest();
            request.addHeaders(headers);
            return this.pollingClient.get(pollUrl, request).flatMapCompletable(response -> {
                if (response.getStatusCode() != 200) {
                    logger.error("Unexpected response code {}.", response.getStatusCode());
                    this.active = false;
                    return Completable.error(new Exception("Failed to connect."));
                } else {
                    this.active = true;
                }
                this.threadPool = Executors.newCachedThreadPool();
                threadPool.execute(() -> {
                    this.onReceiveThread = Executors.newSingleThreadExecutor();
                    receiveLoop.subscribe(() -> {
                        this.stop().onErrorComplete().subscribe();
                    }, e -> {
                        this.stop().onErrorComplete().subscribe();
                    });
                    poll(url).subscribeWith(receiveLoop);
                });

                return Completable.complete();
            });
        }));
    }

    private Completable poll(String url) {
        if (this.active) {
            pollUrl = url + "&_=" + System.currentTimeMillis();
            logger.debug("Polling {}.", pollUrl);
            return this.updateHeaderToken().andThen(Completable.defer(() -> {
                HttpRequest request = new HttpRequest();
                request.addHeaders(headers);
                Completable pollingCompletable = this.pollingClient.get(pollUrl, request).flatMapCompletable(response -> {
                    if (response.getStatusCode() == 204) {
                        logger.info("LongPolling transport terminated by server.");
                        this.active = false;
                    } else if (response.getStatusCode() != 200) {
                        logger.error("Unexpected response code {}.", response.getStatusCode());
                        this.active = false;
                        this.closeError = "Unexpected response code " + response.getStatusCode() + ".";
                    } else {
                        if (response.getContent() != null) {
                            logger.debug("Message received.");
                            onReceiveThread.submit(() -> this.onReceive(response.getContent()));
                        } else {
                            logger.debug("Poll timed out, reissuing.");
                        }
                    }
                    return poll(url);
                });

                return pollingCompletable;
            }));
        } else {
            logger.debug("Long Polling transport polling complete.");
            receiveLoop.onComplete();
            return Completable.complete();
        }
    }

    @Override
    public Completable send(ByteBuffer message) {
        if (!this.active) {
            return Completable.error(new Exception("Cannot send unless the transport is active."));
        }
        return this.updateHeaderToken().andThen(Completable.defer(() -> {
            HttpRequest request = new HttpRequest();
            request.addHeaders(headers);
            return this.client.post(url, message, request).ignoreElement();
        }));
    }

    @Override
    public void setOnReceive(OnReceiveCallBack callback) {
        this.onReceiveCallBack = callback;
    }

    @Override
    public void onReceive(ByteBuffer message) {
        this.onReceiveCallBack.invoke(message);
        logger.debug("OnReceived callback has been invoked.");
    }

    @Override
    public void setOnClose(TransportOnClosedCallback onCloseCallback) {
        this.onClose = onCloseCallback;
    }

    @Override
    public Completable stop() {
        if (stopCalled.compareAndSet(false, true)) {
            this.active = false;
            return this.updateHeaderToken().andThen(Completable.defer(() -> {
                HttpRequest request = new HttpRequest();
                request.addHeaders(headers);
                return this.pollingClient.delete(this.url, request).ignoreElement()
                    .andThen(receiveLoop)
                    .doOnComplete(() -> {
                        cleanup(this.closeError);
                    });
            })).doOnError(e -> {
                cleanup(e.getMessage());
            });
        }
        return Completable.complete();
    }

    private void cleanup(String error) {
        logger.info("LongPolling transport stopped.");
        if (this.onReceiveThread != null) {
            this.onReceiveThread.shutdown();
        }
        if (this.threadPool != null) {
            this.threadPool.shutdown();
        }
        this.onClose.invoke(error);
    }
}
