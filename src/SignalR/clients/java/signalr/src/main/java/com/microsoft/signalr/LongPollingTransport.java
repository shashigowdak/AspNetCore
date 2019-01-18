// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

package com.microsoft.signalr;

import io.reactivex.Completable;
import okhttp3.Headers;
import okhttp3.Request;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.util.Map;

public class LongPollingTransport implements Transport {
    private OnReceiveCallBack onReceiveCallBack;
    private TransportOnClosedCallback onClose;
    private String url;
    private final HttpClient client;
    private final Map<String, String> headers;
    private Boolean active;
    private String pollUrl;
    private final Logger logger = LoggerFactory.getLogger(LongPollingTransport.class);

    public LongPollingTransport(Map<String, String> headers, HttpClient client) {
        this.headers = headers;
        this.client = client;
    }

    @Override
    public Completable start(String url) {
        this.active = true;
        logger.info("Starting LongPolling transport");
        this.url = url;
        pollUrl = url + "&_=" + System.currentTimeMillis();
        logger.info("Polling {}", pollUrl);
        HttpRequest request = new HttpRequest();
        request.addHeaders(headers);
        HttpResponse response = this.client.get(pollUrl, request).blockingGet();
        if (response.getStatusCode() != 200){
            logger.error("Unexpected response code {}", response.getStatusCode());
            this.active = false;
            return Completable.error(new Exception("Failed to connect"));
        } else {
            this.active = true;
        }

        new Thread(() -> poll(url)).start();

        return Completable.complete();
    }

    private Completable poll(String url){
        while(this.active){
            // Poll
            pollUrl = url + "&_=" + System.currentTimeMillis();
            logger.info("Polling {}", pollUrl);
            HttpRequest request = new HttpRequest();
            request.addHeaders(headers);
            HttpResponse response = this.client.get(pollUrl).blockingGet();
            response.getStatusCode();

            if (response.getStatusCode() == 204) {
                logger.info("LongPolling transport terminated by server.");
                this.active = false;
            } else if (response.getStatusCode() != 200) {
                logger.error("Unexpected response code {}", response.getStatusCode());
                this.active = false;
            } else {
                logger.info("Message received");
                this.onReceive(response.getContent());
            }
        }
        return Completable.complete();
    }

    @Override
    public Completable send(String message) {
        this.client.post(url, message).blockingGet();
        return Completable.complete();
    }

    @Override
    public void setOnReceive(OnReceiveCallBack callback) {
        this.onReceiveCallBack = callback;
    }

    @Override
    public void onReceive(String message) {
        this.onReceiveCallBack.invoke(message);
        logger.debug("OnReceived callback has been invoked.");
    }

    @Override
    public void setOnClose(TransportOnClosedCallback onCloseCallback) {
        this.onClose = onCloseCallback;
    }

    @Override
    public Completable stop() {
        logger.info("LongPolling transport stopped.");
        this.active = false;
        this.client.delete(this.url);
        this.onClose.invoke(null);
        return Completable.complete();
    }
}
