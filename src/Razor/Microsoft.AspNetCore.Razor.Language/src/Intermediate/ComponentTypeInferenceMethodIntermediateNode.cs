// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Razor.Language.Intermediate
{
    /// <summary>
    /// Represents a type-inference thunk that is used by the generated component code.
    /// </summary>
    public sealed class ComponentTypeInferenceMethodIntermediateNode : IntermediateNode
    {
        public override IntermediateNodeCollection Children => IntermediateNodeCollection.ReadOnly;
        
        /// <summary>
        /// Gets the component usage linked to this type inference method.
        /// </summary>
        public ComponentIntermediateNode Component { get; set; }
        
        /// <summary>
        /// Gets the full type name of the generated class containing this method.
        /// </summary>
        public string FullTypeName { get; internal set; }

        /// <summary>
        /// Gets the name of the generated method.
        /// </summary>
        public string MethodName { get; set; }

        public override void Accept(IntermediateNodeVisitor visitor)
        {
            if (visitor == null)
            {
                throw new ArgumentNullException(nameof(visitor));
            }

            visitor.VisitComponentTypeInferenceMethod(this);
        }

        public override void FormatNode(IntermediateNodeFormatter formatter)
        {
            if (formatter == null)
            {
                throw new ArgumentNullException(nameof(formatter));
            }

            formatter.WriteContent(Component?.TagName);        
            
            formatter.WriteProperty(nameof(Component), Component?.Component?.DisplayName);
            formatter.WriteProperty(nameof(FullTypeName), FullTypeName);
            formatter.WriteProperty(nameof(MethodName), MethodName);
        }
    }
}
