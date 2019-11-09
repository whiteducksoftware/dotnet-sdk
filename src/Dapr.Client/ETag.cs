// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Represents an ETag value returned by the state store.
    /// </summary>
    public struct ETag : IEquatable<ETag>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ETag" /> struct.
        /// </summary>
        /// <param name="value">The ETag value.</param>
        public ETag(string? value)
        {
            this.Value = value;
        }

        /// <summary>
        /// Gets a value indicating whether this instance has an ETag header value.
        /// </summary>
        public bool HasValue => this.Value != null;

        /// <summary>
        /// Gets the ETag value.
        /// </summary>
        public string? Value { get; }

        /// <summary>
        /// Determines whether two specified ETags have the same value.
        /// </summary>
        /// <param name="a">The first ETag to compare.</param>
        /// <param name="b">The second ETag to compare.</param>
        /// <returns>true if the value of a is the same as the value of b; otherwise, false.</returns>
        public static bool operator ==(ETag a, ETag b)
        {
            return a.Equals(b);
        }

        /// <summary>
        /// Determines whether two specified ETags have the different values.
        /// </summary>
        /// <param name="a">The first ETag to compare.</param>
        /// <param name="b">The second ETag to compare.</param>
        /// <returns>true if the value of a is the different from the value of b; otherwise, false.</returns>
        public static bool operator !=(ETag a, ETag b)
        {
            return !a.Equals(b);
        }

        /// <summary>
        /// Returns the hash code for this string.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode()
        {
            return this.Value?.GetHashCode() ?? 0;
        }

        /// <summary>
        /// Indicates whether this instance and a specified object are equal.
        /// </summary>
        /// <param name="obj">The object to compare with the current instance.</param>
        /// <returns>true if obj and this instance are the same type and represent the same value; otherwise, false.</returns>
        public override bool Equals(object? obj)
        {
            var other = obj as ETag?;
            return other is null ? false : this.Equals(other.Value);
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>true if the current object is equal to the other parameter; otherwise, false.</returns>
        public bool Equals(ETag other)
        {
            // Dapr ETag values are opaque text.
            return string.Equals(this.Value, other.Value, StringComparison.Ordinal);
        }
    }
}
