// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Routing.Tree
{
    internal readonly struct OutboundMatchResult
    {
        public OutboundMatchResult(OutboundMatch match, bool isFallbackMatch)
        {
            Match = match;
            IsFallbackMatch = isFallbackMatch;
        }

        public OutboundMatch Match { get; }

        public bool IsFallbackMatch { get; }
    }
}
