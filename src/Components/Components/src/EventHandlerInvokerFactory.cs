// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Components
{
    public sealed class EventHandlerInvokerFactory
    {
        public Action CreateDelegate(object receiver, Action action)
        {
            if (receiver == null)
            {
                throw new ArgumentNullException(nameof(receiver));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            // Determine whether the action is already a bound-delegate attached to handler.
            //
            // This is important for a case like the following:
            //
            //  @* in Outer.cshtml *@
            //  <Inner OnClick="@OnClick" />
            //  @functions {
            //      void OnClick() { ... }
            //  }
            //
            //  @* in Inner.cshtml *@
            //  <button onclick="@OnClick">Click Me!</button>
            //  @functions {
            //      [Parameter] Action OnClick { get; set; }
            //  }
            //
            // We want to make sure to call StateHasChanged on the Outer component instead
            // of just the Inner component. We can't rely on action.Target to always point
            // to the outer component because in the case of a non-capturing lambda it won't.
            if (receiver is IHandleStateChange handler && !object.ReferenceEquals(receiver, action.Target))
            {
                return (Action)(() =>
                {
                    action();
                    _ = handler.HandleStateChangeAsync(Task.CompletedTask);
                });
            }

            return action;
        }

        public Action<TEventArgs> CreateDelegate<TEventArgs>(object receiver, Action action) where TEventArgs : UIEventArgs
        {
            if (receiver == null)
            {
                throw new ArgumentNullException(nameof(receiver));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (receiver is IHandleStateChange handler && !object.ReferenceEquals(receiver, action.Target))
            {
                return (Action<TEventArgs>)((TEventArgs args) =>
                {
                    action();
                    _ = handler.HandleStateChangeAsync(Task.CompletedTask);
                });
            }

            return (args) => action();
        }

        public Action<TEventArgs> CreateDelegate<TEventArgs>(object receiver, Action<TEventArgs> action) where TEventArgs : UIEventArgs
        {
            if (receiver == null)
            {
                throw new ArgumentNullException(nameof(receiver));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (receiver is IHandleStateChange handler && !object.ReferenceEquals(receiver, action.Target))
            {
                return (Action<TEventArgs>)((TEventArgs args) =>
                {
                    action(args);
                    _ = handler.HandleStateChangeAsync(Task.CompletedTask);
                });
            }

            return action;
        }
    }
}
