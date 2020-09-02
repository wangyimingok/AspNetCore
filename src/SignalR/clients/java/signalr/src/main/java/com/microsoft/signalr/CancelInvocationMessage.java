// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

package com.microsoft.signalr;

import java.util.Map;

final class CancelInvocationMessage extends HubMessage {
    private final int type = HubMessageType.CANCEL_INVOCATION.value;
    private Map<String, String> headers;
    private final String invocationId;
    
    public CancelInvocationMessage(Map<String, String> headers, String invocationId) {
        if (headers != null && !headers.isEmpty()) {
            this.headers = headers;
        }
        this.invocationId = invocationId;
    }

    public Map<String, String> getHeaders() {
        return headers;
    }
    
    public String getInvocationId() {
        return invocationId;
    }
    
    @Override
    public HubMessageType getMessageType() {
        return HubMessageType.CANCEL_INVOCATION;
    }
}
