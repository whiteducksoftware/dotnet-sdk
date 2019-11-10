// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    /// <summary>
    /// Represents an error response from the state store.
    /// </summary>
    internal class ErrorResponse
    {
        public string ErrorCode { get; set; } = default!;

        public string Message { get; set; } = default!;
    }
}