// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Components.Server.Circuits
{
    /// <summary>
    /// Represents an active connection between an ASP.NET Core Component on the server and a client.
    /// </summary>
    public class Circuit
    {
        internal Circuit(string id)
        {
            Id = id;
        }

        /// <summary>
        /// Gets the identifier for the <see cref="Circuit"/>.
        /// </summary>
        public string Id { get; }
    }
}
