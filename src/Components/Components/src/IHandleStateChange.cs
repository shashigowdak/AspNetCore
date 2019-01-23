// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Components
{
    /// <summary>
    /// Interface implemented by components that receive notification of state changes.
    /// </summary>
    public interface IHandleStateChange
    {
        /// <summary>
        /// Notifies the a state change has been triggered.
        /// </summary>
        /// <param name="task">
        /// The <see cref="Task"/> whose asynchronous completion represents the compeletion of the state
        /// change.
        /// <param>
        /// <returns>
        /// A <see cref="Task"/> that completes once the component has processed the state change.
        /// </returns>
        Task HandleStateChangeAsync(Task task);
    }
}
