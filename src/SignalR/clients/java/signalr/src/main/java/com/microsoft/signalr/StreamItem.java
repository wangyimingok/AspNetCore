// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

package com.microsoft.signalr;

import java.util.Map;

final class StreamItem extends HubMessage {
    private final int type = HubMessageType.STREAM_ITEM.value;
    private Map<String, String> headers;
    private final String invocationId;
    private final Object item;
    
    public StreamItem(Map<String, String> headers, String invocationId, Object item) {
        if (headers != null && !headers.isEmpty()) {
            this.headers = headers;
        }
        this.invocationId = invocationId;
        this.item = item;
    }
    
    public Map<String, String> getHeaders() {
        return headers;
    }

    public String getInvocationId() {
        return invocationId;
    }

    public Object getItem() {
        return item;
    }

    @Override
    public HubMessageType getMessageType() {
        return HubMessageType.STREAM_ITEM;
    }
}
