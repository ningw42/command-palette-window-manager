// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using WindowManager.Windowing;

namespace WindowManager;

[Guid("23e8f4da-139b-41bf-84d5-b1af06856f3e")]
public sealed partial class WindowManager : IExtension, IDisposable
{
    private readonly ManualResetEvent _extensionDisposedEvent;

    private readonly WindowController _windows;
    private readonly WindowManagerCommandsProvider _provider;

    public WindowManager(ManualResetEvent extensionDisposedEvent)
    {
        this._extensionDisposedEvent = extensionDisposedEvent;
        _windows = WindowController.CreateForCurrentDesktop();
        _provider = new WindowManagerCommandsProvider(_windows);
    }

    public object? GetProvider(ProviderType providerType)
    {
        return providerType switch
        {
            ProviderType.Commands => _provider,
            _ => null,
        };
    }

    public void Dispose()
    {
        try
        {
            _windows.Dispose();
        }
        finally
        {
            _extensionDisposedEvent.Set();
        }
    }
}
