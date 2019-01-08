// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

package com.microsoft.signalr;

import io.reactivex.Completable;
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
    private Completable receiving;

    private final Logger logger = LoggerFactory.getLogger(WebSocketTransport.class);

    public LongPollingTransport(Map<String, String> headers, HttpClient client) {
        this.headers = headers;
        this.client = new DefaultHttpClient();
    }

    @Override
    public Completable start(String url) {
        this.active = true;
        logger.info("Starting LongPolling transport");

        pollUrl = url + "&_=" + System.currentTimeMillis();
        logger.info("Polling {}", pollUrl);

        HttpResponse response = this.client.get(pollUrl).blockingGet();
        if (response.getStatusCode() != 200){
            logger.error("Unexpected response code {}", response.getStatusCode());
            this.active = false;
            return Completable.error(new Exception("Failed to connect"));
        } else {
            this.active = true;
        }

        this.receiving = poll(url);

        return Completable.complete();
    }

    private Completable poll(String url){
        while(this.active){
            // Poll
            pollUrl = url + "&_=" + System.currentTimeMillis();
            logger.info("Polling {}", pollUrl);
            HttpResponse response = this.client.get(pollUrl).blockingGet();
            response.getStatusCode();

            if (response.getStatusCode() == 204) {
                logger.info("LongPolling transport terminated by server.");
                this.active = false;
            } else if (response.getStatusCode() != 200) {
                logger.error("Unexpected response code {}", response.getStatusCode());
            } else {
                logger.info("Message received");
                this.onReceive(response.getContent());
            }
        }
        return Completable.complete();
    }

    @Override
    public Completable send(String message) {
        //HttpResponse response = this.client.post(url, )
        return null;
    }

    @Override
    public void setOnReceive(OnReceiveCallBack callback) {
        this.onReceiveCallBack = callback;
        logger.debug("OnReceived callback has been set.");
    }

    @Override
    public void onReceive(String message) {
        this.onReceiveCallBack.invoke(message);
    }

    @Override
    public void setOnClose(TransportOnClosedCallback onCloseCallback) {
        this.onClose = onCloseCallback;
    }

    @Override
    public Completable stop() {
        return null;
    }
}
