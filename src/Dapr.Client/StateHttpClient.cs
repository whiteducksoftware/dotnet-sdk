// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using static Dapr.DaprUris;

    /// <summary>
    /// A client for interacting with the Dapr state store using <see cref="HttpClient" />.
    /// </summary>
    public sealed class StateHttpClient : StateClient
    {
        private readonly HttpClient client;
        private readonly JsonSerializerOptions? serializerOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="StateHttpClient"/> class.
        /// </summary>
        /// <param name="client">The <see cref="HttpClient" />.</param>
        /// <param name="serializerOptions">The <see cref="JsonSerializerOptions" />.</param>
        public StateHttpClient(HttpClient client, JsonSerializerOptions? serializerOptions = null)
        {
            if (client is null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            this.client = client;
            this.serializerOptions = serializerOptions;
        }

        /// <summary>
        /// Gets the current value associated with the <paramref name="key" /> from the Dapr state store.
        /// </summary>
        /// <param name="key">The state key.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TValue">The data type.</typeparam>
        /// <returns>A <see cref="ValueTask" /> that will return the value when the operation has completed.</returns>
        public async override ValueTask<TValue> GetStateAsync<TValue>(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("The value cannot be null or empty.", nameof(key));
            }

            // Docs: https://github.com/dapr/docs/blob/master/reference/api/state.md#get-state
            var url = this.client.BaseAddress == null ? $"http://localhost:{DefaultHttpPort}{StatePath}/{key}" : $"{StatePath}{key}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await this.client.SendAsync(request, cancellationToken).ConfigureAwait(false);

            // 200: found state
            if (response.StatusCode == HttpStatusCode.OK && response.Content != null)
            {
                using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                return await JsonSerializer.DeserializeAsync<TValue>(
                    stream,
                    this.serializerOptions,
                    cancellationToken).ConfigureAwait(false);
            }

            // 204: no entry for this key
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return default!;
            }

            throw await this.ReportErrorAsync(response, "get state", cancellationToken);
        }

        /// <summary>
        /// Saves the provided <paramref name="value" /> associated with the provided <paramref name="key" /> to the Dapr state
        /// store.
        /// </summary>
        /// <param name="key">The state key.</param>
        /// <param name="value">The value to save.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TValue">The data type.</typeparam>
        /// <returns>A <see cref="ValueTask" /> that will complete when the operation has completed.</returns>
        public async override ValueTask SaveStateAsync<TValue>(string key, [MaybeNull] TValue value, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("The value cannot be null or empty.", nameof(key));
            }

            // Docs: https://github.com/dapr/docs/blob/master/reference/api/state.md#save-state
            var url = this.client.BaseAddress == null ? $"http://localhost:{DefaultHttpPort}{StatePath}" : StatePath;
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            var obj = new object[] { new { key = key, value = value, } };
            request.Content = this.CreateContent(obj);

            var response = await this.client.SendAsync(request, cancellationToken).ConfigureAwait(false);

            // 201: success
            //
            // To avoid being overload coupled we handle a range of 2XX status codes in common use for POSTs.
            if ((int)response.StatusCode >= 200 && (int)response.StatusCode <= 204)
            {
                return;
            }

            throw await this.ReportErrorAsync(response, "save state", cancellationToken);
        }

        private async ValueTask<Exception> ReportErrorAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
        {
            // The state store will return 400 or 500 depending on whether its a configuration error
            // or unknown failure, we just want to surface all of them the same way. It's not really
            // something that application code would handle.
            if (response.Content == null)
            {
                return new HttpRequestException($"Failed to {operation} with status code '{response.StatusCode}'.");
            }
            else if (response.Content.Headers.ContentType.MediaType == "application/json")
            {
                using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                var error = await JsonSerializer.DeserializeAsync<ErrorResponse>(
                    stream,
                    this.serializerOptions,
                    cancellationToken).ConfigureAwait(false);
                return new HttpRequestException(
                    $"Failed to {operation} with status code '{response.StatusCode}': {error.ErrorCode}." +
                    Environment.NewLine +
                    error.Message);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpRequestException($"Failed to {operation} with status code '{response.StatusCode}': {error}.");
            }
        }

        private AsyncJsonContent<T> CreateContent<T>(T obj)
        {
            return new AsyncJsonContent<T>(obj, this.serializerOptions);
        }

        // Note: using push-streaming content here has a little higher cost for trivially-size payloads,
        // but avoids the significant allocation overhead in the cases where the content is really large.
        //
        // Similar to https://github.com/aspnet/AspNetWebStack/blob/master/src/System.Net.Http.Formatting/PushStreamContent.cs
        // but simplified because of async.
        private class AsyncJsonContent<T> : HttpContent
        {
            private readonly T obj;
            private readonly JsonSerializerOptions? serializerOptions;

            public AsyncJsonContent(T obj, JsonSerializerOptions? serializerOptions)
            {
                this.obj = obj;
                this.serializerOptions = serializerOptions;

                this.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "UTF-8", };
            }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                return JsonSerializer.SerializeAsync(stream, this.obj, this.serializerOptions);
            }

            protected override bool TryComputeLength(out long length)
            {
                // We can't know the length of the content being pushed to the output stream without doing
                // some writing.
                //
                // If we want to optimize this case, it could be done by implementing a custom stream
                // and then doing the first write to a fixed-size pooled byte array.
                //
                // HTTP is slightly more efficient when you can avoid using chunking (need to know Content-Length)
                // up front.
                length = -1;
                return false;
            }
        }
    }
}
