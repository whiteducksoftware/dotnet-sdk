// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public class StateTestClient : StateClient
    {
        public Dictionary<string, (object value, ETag etag)> State { get; } = new Dictionary<string, (object, ETag)>();

        public override ValueTask<(TValue, ETag)> GetStateAndETagAsync<TValue>(string key, CancellationToken cancellationToken = default)
        {
            if (this.State.TryGetValue(key, out var tuple))
            {
                return new ValueTask<(TValue, ETag)>(((TValue)tuple.value, tuple.etag));
            }
            else
            {
                return new ValueTask<(TValue, ETag)>((default(TValue), default));
            }
        }

        public override ValueTask SaveStateAsync<TValue>(string key, TValue value, ETag etag, CancellationToken cancellationToken = default)
        {
            if (etag.HasValue && this.State.TryGetValue(key, out var tuple))
            {
                // already have a value stored, and we have an etag, so we simulate if-modified-since behaviour
                if (tuple.etag == etag)
                {
                    this.State[key] = (value, etag);
                }
                else
                {
                    // rejected!
                }
            }
            else
            {
                // no value stored, or no etag - overwrite what's there.
                etag = etag.HasValue ? etag : new ETag(Guid.NewGuid().ToString());
                this.State[key] = (value, etag);
            }

            return new ValueTask(Task.CompletedTask);
        }

        public override ValueTask DeleteStateAsync(string key, ETag etag, CancellationToken cancellationToken = default)
        {
            if (etag.HasValue && this.State.TryGetValue(key, out var tuple))
            {
                // already have a value stored, and we have an etag, so we simulate if-modified-since behaviour
                if (tuple.etag == etag)
                {
                    this.State.Remove(key);
                }
                else
                {
                    // rejected!
                }
            }
            else
            {
                // no value stored, or no etag - remove what's there.
                this.State.Remove(key);
            }

            return new ValueTask(Task.CompletedTask);
        }
    }
}