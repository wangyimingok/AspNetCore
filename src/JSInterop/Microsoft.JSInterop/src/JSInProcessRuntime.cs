// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.JSInterop.Infrastructure;

namespace Microsoft.JSInterop
{
    /// <summary>
    /// Abstract base class for an in-process JavaScript runtime.
    /// </summary>
    public abstract class JSInProcessRuntime : JSRuntime, IJSInProcessRuntime
    {
        /// <summary>
        /// Initializes a new instance of <see cref="JSInProcessRuntime"/>.
        /// </summary>
        protected JSInProcessRuntime()
        {
            JsonSerializerOptions.Converters.Add(new JSObjectReferenceJsonConverter<JSInProcessObjectReference>(
                id => new JSInProcessObjectReference(this, id)));
        }

        [return: MaybeNull]
        internal TValue Invoke<TValue>(string identifier, long targetInstanceId, params object?[]? args)
        {
            var resultJson = InvokeJS(
                identifier,
                JsonSerializer.Serialize(args, JsonSerializerOptions),
                ResultTypeFromGeneric<TValue>(),
                targetInstanceId);

            if (resultJson is null)
            {
                return default;
            }

            return JsonSerializer.Deserialize<TValue>(resultJson, JsonSerializerOptions);
        }

        /// <summary>
        /// Invokes the specified JavaScript function synchronously.
        /// </summary>
        /// <typeparam name="TValue">The JSON-serializable return type.</typeparam>
        /// <param name="identifier">An identifier for the function to invoke. For example, the value <c>"someScope.someFunction"</c> will invoke the function <c>window.someScope.someFunction</c>.</param>
        /// <param name="args">JSON-serializable arguments.</param>
        /// <returns>An instance of <typeparamref name="TValue"/> obtained by JSON-deserializing the return value.</returns>
        [return: MaybeNull]
        public TValue Invoke<TValue>(string identifier, params object?[]? args)
            => Invoke<TValue>(identifier, 0, args);

        /// <summary>
        /// Performs a synchronous function invocation.
        /// </summary>
        /// <param name="identifier">The identifier for the function to invoke.</param>
        /// <param name="argsJson">A JSON representation of the arguments.</param>
        /// <returns>A JSON representation of the result.</returns>
        protected virtual string? InvokeJS(string identifier, string? argsJson)
            => InvokeJS(identifier, argsJson, JSCallResultType.Default, 0);

        /// <summary>
        /// Performs a synchronous function invocation.
        /// </summary>
        /// <param name="identifier">The identifier for the function to invoke.</param>
        /// <param name="argsJson">A JSON representation of the arguments.</param>
        /// <param name="resultType">The type of result expected from the invocation.</param>
        /// <param name="targetInstanceId">The instance ID of the target JS object.</param>
        /// <returns>A JSON representation of the result.</returns>
        protected abstract string? InvokeJS(string identifier, string? argsJson, JSCallResultType resultType, long targetInstanceId);
    }
}
