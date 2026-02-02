# Stage 2: UI Components and Plugin Manager Dialog

## Overview

This stage implements the visual interface for the plugin marketplace. Users will be able to browse available plugins, search/filter by category and tags, view plugin details, and install/uninstall plugins directly from the UI.

## Prerequisites

- Stage 1 completed (all interfaces, models, and services implemented)
- `LocalPluginMarketplaceSource` working with a test repository folder
- `PluginInstallationService` able to install/uninstall plugins

## Goals

- [x] Create the main Plugin Manager dialog component
- [x] Implement plugin cards for list and grid display
- [x] Add search bar with category/tag filtering
- [x] Create plugin details panel with install/uninstall actions
- [x] Add "Installed" tab showing currently installed plugins
- [x] Implement progress indicators for install/uninstall operations
- [x] Add CSS styles following the existing `ne-` convention

---

## 1. Component Architecture

```
PluginManagerDialog.razor (Main container)
├── PluginSearchBar.razor (Search input + filters)
├── PluginCategoryTabs.razor (Browse / Installed tabs)
├── PluginList.razor (Grid/list of plugins)
│   └── PluginCard.razor (Individual plugin preview)
└── PluginDetailsPanel.razor (Selected plugin details + actions)
```

---

## 2. Main Dialog Component

### 2.1 PluginManagerDialog.razor

**File:** `NodeEditor.Blazor/Components/Marketplace/PluginManagerDialog.razor`

```razor
@namespace NodeEditor.Blazor.Components.Marketplace
@using NodeEditor.Blazor.Services.Plugins.Marketplace
@using NodeEditor.Blazor.Services.Plugins.Marketplace.Models
@inject IPluginMarketplaceSource MarketplaceSource
@inject IPluginInstallationService InstallationService

<div class="ne-plugin-manager-overlay @(IsOpen ? "ne-plugin-manager-overlay--visible" : "")"
     @onclick="OnOverlayClick">
    <div class="ne-plugin-manager" @onclick:stopPropagation="true">
        @* Header *@
        <div class="ne-plugin-manager-header">
            <h2 class="ne-plugin-manager-title">Plugin Manager</h2>
            <button class="ne-plugin-manager-close" @onclick="Close" title="Close">
                <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor">
                    <path d="M4.646 4.646a.5.5 0 0 1 .708 0L8 7.293l2.646-2.647a.5.5 0 0 1 .708.708L8.707 8l2.647 2.646a.5.5 0 0 1-.708.708L8 8.707l-2.646 2.647a.5.5 0 0 1-.708-.708L7.293 8 4.646 5.354a.5.5 0 0 1 0-.708z"/>
                </svg>
            </button>
        </div>
        
        @* Tabs *@
        <div class="ne-plugin-manager-tabs">
            <button class="ne-plugin-manager-tab @(_activeTab == Tab.Browse ? "ne-plugin-manager-tab--active" : "")"
                    @onclick="() => SetActiveTab(Tab.Browse)">
                Browse
                @if (_availablePlugins.Count > 0)
                {
                    <span class="ne-plugin-manager-tab-badge">@_availablePlugins.Count</span>
                }
            </button>
            <button class="ne-plugin-manager-tab @(_activeTab == Tab.Installed ? "ne-plugin-manager-tab--active" : "")"
                    @onclick="() => SetActiveTab(Tab.Installed)">
                Installed
                @if (_installedPlugins.Count > 0)
                {
                    <span class="ne-plugin-manager-tab-badge">@_installedPlugins.Count</span>
                }
            </button>
            @if (_updatesAvailable.Count > 0)
            {
                <button class="ne-plugin-manager-tab @(_activeTab == Tab.Updates ? "ne-plugin-manager-tab--active" : "")"
                        @onclick="() => SetActiveTab(Tab.Updates)">
                    Updates
                    <span class="ne-plugin-manager-tab-badge ne-plugin-manager-tab-badge--highlight">
                        @_updatesAvailable.Count
                    </span>
                </button>
            }
        </div>
        
        @* Content Area *@
        <div class="ne-plugin-manager-content">
            @* Left Panel - Search and List *@
            <div class="ne-plugin-manager-list-panel">
                <PluginSearchBar @bind-SearchText="_searchText"
                                 @bind-SelectedCategory="_selectedCategory"
                                 Categories="_categories"
                                 OnSearch="OnSearch" />
                
                <div class="ne-plugin-manager-list">
                    @if (_isLoading)
                    {
                        <div class="ne-plugin-manager-loading">
                            <div class="ne-plugin-manager-spinner"></div>
                            <span>Loading plugins...</span>
                        </div>
                    }
                    else if (_displayedPlugins.Count == 0)
                    {
                        <div class="ne-plugin-manager-empty">
                            <svg width="48" height="48" viewBox="0 0 24 24" fill="currentColor" opacity="0.4">
                                <path d="M20 7h-4V4c0-1.1-.9-2-2-2h-4c-1.1 0-2 .9-2 2v3H4c-1.1 0-2 .9-2 2v11c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V9c0-1.1-.9-2-2-2zM10 4h4v3h-4V4z"/>
                            </svg>
                            <p>
                                @if (_activeTab == Tab.Browse)
                                {
                                    @("No plugins found. Try a different search.")
                                }
                                else if (_activeTab == Tab.Installed)
                                {
                                    @("No plugins installed yet.")
                                }
                                else
                                {
                                    @("All plugins are up to date!")
                                }
                            </p>
                        </div>
                    }
                    else
                    {
                        @foreach (var plugin in _displayedPlugins)
                        {
                            <PluginCard Plugin="plugin"
                                        IsSelected="_selectedPlugin?.Id == plugin.Id"
                                        IsInstalled="IsPluginInstalled(plugin.Id)"
                                        InstalledVersion="GetInstalledVersion(plugin.Id)"
                                        OnSelect="() => SelectPlugin(plugin)"
                                        OnInstall="() => InstallPlugin(plugin)"
                                        OnUninstall="() => UninstallPlugin(plugin.Id)" />
                        }
                    }
                </div>
            </div>
            
            @* Right Panel - Details *@
            <div class="ne-plugin-manager-details-panel">
                @if (_selectedPlugin is not null)
                {
                    <PluginDetailsPanel Plugin="_selectedPlugin"
                                        IsInstalled="IsPluginInstalled(_selectedPlugin.Id)"
                                        InstalledInfo="GetInstalledInfo(_selectedPlugin.Id)"
                                        IsInstalling="_installingPluginIds.Contains(_selectedPlugin.Id)"
                                        IsUninstalling="_uninstallingPluginIds.Contains(_selectedPlugin.Id)"
                                        OnInstall="() => InstallPlugin(_selectedPlugin)"
                                        OnUninstall="() => UninstallPlugin(_selectedPlugin.Id)"
                                        OnUpdate="() => UpdatePlugin(_selectedPlugin)" />
                }
                else
                {
                    <div class="ne-plugin-manager-no-selection">
                        <svg width="64" height="64" viewBox="0 0 24 24" fill="currentColor" opacity="0.3">
                            <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z"/>
                        </svg>
                        <p>Select a plugin to view details</p>
                    </div>
                }
            </div>
        </div>
        
        @* Status Bar *@
        @if (!string.IsNullOrEmpty(_statusMessage))
        {
            <div class="ne-plugin-manager-status @(_statusIsError ? "ne-plugin-manager-status--error" : "")">
                @_statusMessage
            </div>
        }
    </div>
</div>

@code {
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public EventCallback OnClosed { get; set; }
    
    private enum Tab { Browse, Installed, Updates }
    private Tab _activeTab = Tab.Browse;
    
    private List<MarketplacePluginInfo> _availablePlugins = [];
    private List<InstalledPluginInfo> _installedPlugins = [];
    private List<PluginUpdateInfo> _updatesAvailable = [];
    private List<MarketplacePluginInfo> _displayedPlugins = [];
    private List<string> _categories = [];
    
    private MarketplacePluginInfo? _selectedPlugin;
    private string _searchText = "";
    private string? _selectedCategory;
    
    private bool _isLoading;
    private string _statusMessage = "";
    private bool _statusIsError;
    
    private HashSet<string> _installingPluginIds = [];
    private HashSet<string> _uninstallingPluginIds = [];
    
    protected override async Task OnParametersSetAsync()
    {
        if (IsOpen && _availablePlugins.Count == 0)
        {
            await LoadDataAsync();
        }
    }
    
    private async Task LoadDataAsync()
    {
        _isLoading = true;
        StateHasChanged();
        
        try
        {
            var availableTask = MarketplaceSource.SearchAsync();
            var installedTask = InstallationService.GetInstalledPluginsAsync();
            var categoriesTask = MarketplaceSource.GetCategoriesAsync();
            
            await Task.WhenAll(availableTask, installedTask, categoriesTask);
            
            _availablePlugins = [.. await availableTask];
            _installedPlugins = [.. await installedTask];
            _categories = [.. await categoriesTask];
            
            _updatesAvailable = [.. await InstallationService.CheckForUpdatesAsync(MarketplaceSource)];
            
            UpdateDisplayedPlugins();
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to load plugins: {ex.Message}", isError: true);
        }
        finally
        {
            _isLoading = false;
        }
    }
    
    private void SetActiveTab(Tab tab)
    {
        _activeTab = tab;
        _selectedPlugin = null;
        UpdateDisplayedPlugins();
    }
    
    private void UpdateDisplayedPlugins()
    {
        IEnumerable<MarketplacePluginInfo> source = _activeTab switch
        {
            Tab.Browse => _availablePlugins,
            Tab.Installed => _availablePlugins.Where(p => IsPluginInstalled(p.Id)),
            Tab.Updates => _availablePlugins.Where(p => 
                _updatesAvailable.Any(u => u.PluginId == p.Id)),
            _ => _availablePlugins
        };
        
        // Apply search filter
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var q = _searchText.Trim().ToLowerInvariant();
            source = source.Where(p =>
                p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.Id.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (p.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.Author?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        
        // Apply category filter
        if (!string.IsNullOrWhiteSpace(_selectedCategory))
        {
            source = source.Where(p =>
                string.Equals(p.Category, _selectedCategory, StringComparison.OrdinalIgnoreCase));
        }
        
        _displayedPlugins = source.ToList();
    }
    
    private void OnSearch()
    {
        UpdateDisplayedPlugins();
    }
    
    private void SelectPlugin(MarketplacePluginInfo plugin)
    {
        _selectedPlugin = plugin;
    }
    
    private bool IsPluginInstalled(string pluginId)
    {
        return _installedPlugins.Any(p =>
            string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));
    }
    
    private string? GetInstalledVersion(string pluginId)
    {
        return _installedPlugins
            .FirstOrDefault(p => string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase))
            ?.Version;
    }
    
    private InstalledPluginInfo? GetInstalledInfo(string pluginId)
    {
        return _installedPlugins
            .FirstOrDefault(p => string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));
    }
    
    private async Task InstallPlugin(MarketplacePluginInfo plugin)
    {
        _installingPluginIds.Add(plugin.Id);
        ShowStatus($"Installing {plugin.Name}...");
        StateHasChanged();
        
        try
        {
            var result = await InstallationService.InstallAsync(MarketplaceSource, plugin.Id);
            
            if (result.Success)
            {
                ShowStatus($"Successfully installed {plugin.Name}");
                await LoadDataAsync();
            }
            else
            {
                ShowStatus($"Failed to install {plugin.Name}: {result.ErrorMessage}", isError: true);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to install {plugin.Name}: {ex.Message}", isError: true);
        }
        finally
        {
            _installingPluginIds.Remove(plugin.Id);
        }
    }
    
    private async Task UninstallPlugin(string pluginId)
    {
        var plugin = _availablePlugins.FirstOrDefault(p => p.Id == pluginId);
        var pluginName = plugin?.Name ?? pluginId;
        
        _uninstallingPluginIds.Add(pluginId);
        ShowStatus($"Uninstalling {pluginName}...");
        StateHasChanged();
        
        try
        {
            var result = await InstallationService.UninstallAsync(pluginId);
            
            if (result.Success)
            {
                ShowStatus($"Successfully uninstalled {pluginName}");
                await LoadDataAsync();
            }
            else
            {
                ShowStatus($"Failed to uninstall {pluginName}: {result.ErrorMessage}", isError: true);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to uninstall {pluginName}: {ex.Message}", isError: true);
        }
        finally
        {
            _uninstallingPluginIds.Remove(pluginId);
        }
    }
    
    private async Task UpdatePlugin(MarketplacePluginInfo plugin)
    {
        _installingPluginIds.Add(plugin.Id);
        ShowStatus($"Updating {plugin.Name}...");
        StateHasChanged();
        
        try
        {
            var result = await InstallationService.UpdateAsync(MarketplaceSource, plugin.Id);
            
            if (result.Success)
            {
                ShowStatus($"Successfully updated {plugin.Name} to v{result.Plugin?.Version}");
                await LoadDataAsync();
            }
            else
            {
                ShowStatus($"Failed to update {plugin.Name}: {result.ErrorMessage}", isError: true);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to update {plugin.Name}: {ex.Message}", isError: true);
        }
        finally
        {
            _installingPluginIds.Remove(plugin.Id);
        }
    }
    
    private void ShowStatus(string message, bool isError = false)
    {
        _statusMessage = message;
        _statusIsError = isError;
    }
    
    private void OnOverlayClick()
    {
        Close();
    }
    
    private async Task Close()
    {
        await IsOpenChanged.InvokeAsync(false);
        await OnClosed.InvokeAsync();
    }
}
```

---

## 3. Search Bar Component

### 3.1 PluginSearchBar.razor

**File:** `NodeEditor.Blazor/Components/Marketplace/PluginSearchBar.razor`

```razor
@namespace NodeEditor.Blazor.Components.Marketplace

<div class="ne-plugin-search">
    <div class="ne-plugin-search-input-wrapper">
        <svg class="ne-plugin-search-icon" width="16" height="16" viewBox="0 0 16 16" fill="currentColor">
            <path d="M11.742 10.344a6.5 6.5 0 1 0-1.397 1.398h-.001c.03.04.062.078.098.115l3.85 3.85a1 1 0 0 0 1.415-1.414l-3.85-3.85a1.007 1.007 0 0 0-.115-.1zM12 6.5a5.5 5.5 0 1 1-11 0 5.5 5.5 0 0 1 11 0z"/>
        </svg>
        <input type="text"
               class="ne-plugin-search-input"
               placeholder="Search plugins..."
               value="@SearchText"
               @oninput="OnSearchInput"
               @onkeydown="OnKeyDown" />
        @if (!string.IsNullOrEmpty(SearchText))
        {
            <button class="ne-plugin-search-clear" @onclick="ClearSearch" title="Clear search">
                <svg width="14" height="14" viewBox="0 0 16 16" fill="currentColor">
                    <path d="M4.646 4.646a.5.5 0 0 1 .708 0L8 7.293l2.646-2.647a.5.5 0 0 1 .708.708L8.707 8l2.647 2.646a.5.5 0 0 1-.708.708L8 8.707l-2.646 2.647a.5.5 0 0 1-.708-.708L7.293 8 4.646 5.354a.5.5 0 0 1 0-.708z"/>
                </svg>
            </button>
        }
    </div>
    
    @if (Categories.Count > 0)
    {
        <select class="ne-plugin-search-category"
                value="@SelectedCategory"
                @onchange="OnCategoryChange">
            <option value="">All Categories</option>
            @foreach (var category in Categories)
            {
                <option value="@category">@category</option>
            }
        </select>
    }
</div>

@code {
    [Parameter] public string SearchText { get; set; } = "";
    [Parameter] public EventCallback<string> SearchTextChanged { get; set; }
    
    [Parameter] public string? SelectedCategory { get; set; }
    [Parameter] public EventCallback<string?> SelectedCategoryChanged { get; set; }
    
    [Parameter] public List<string> Categories { get; set; } = [];
    [Parameter] public EventCallback OnSearch { get; set; }
    
    private System.Timers.Timer? _debounceTimer;
    
    private void OnSearchInput(ChangeEventArgs e)
    {
        var value = e.Value?.ToString() ?? "";
        SearchText = value;
        SearchTextChanged.InvokeAsync(value);
        
        // Debounce the search
        _debounceTimer?.Stop();
        _debounceTimer?.Dispose();
        _debounceTimer = new System.Timers.Timer(300);
        _debounceTimer.Elapsed += async (s, e) =>
        {
            _debounceTimer?.Stop();
            await InvokeAsync(async () =>
            {
                await OnSearch.InvokeAsync();
                StateHasChanged();
            });
        };
        _debounceTimer.Start();
    }
    
    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            _debounceTimer?.Stop();
            await OnSearch.InvokeAsync();
        }
    }
    
    private async Task ClearSearch()
    {
        SearchText = "";
        await SearchTextChanged.InvokeAsync("");
        await OnSearch.InvokeAsync();
    }
    
    private async Task OnCategoryChange(ChangeEventArgs e)
    {
        var value = e.Value?.ToString();
        SelectedCategory = string.IsNullOrEmpty(value) ? null : value;
        await SelectedCategoryChanged.InvokeAsync(SelectedCategory);
        await OnSearch.InvokeAsync();
    }
    
    public void Dispose()
    {
        _debounceTimer?.Dispose();
    }
}
```

---

## 4. Plugin Card Component

### 4.1 PluginCard.razor

**File:** `NodeEditor.Blazor/Components/Marketplace/PluginCard.razor`

```razor
@namespace NodeEditor.Blazor.Components.Marketplace
@using NodeEditor.Blazor.Services.Plugins.Marketplace.Models

<div class="ne-plugin-card @(IsSelected ? "ne-plugin-card--selected" : "") @(IsInstalled ? "ne-plugin-card--installed" : "")"
     @onclick="OnCardClick">
    
    @* Icon *@
    <div class="ne-plugin-card-icon">
        @if (!string.IsNullOrEmpty(Plugin.IconUrl))
        {
            <img src="@Plugin.IconUrl" alt="@Plugin.Name" />
        }
        else
        {
            <svg width="32" height="32" viewBox="0 0 24 24" fill="currentColor">
                <path d="M20.5 11H19V7c0-1.1-.9-2-2-2h-4V3.5C13 2.12 11.88 1 10.5 1S8 2.12 8 3.5V5H4c-1.1 0-1.99.9-1.99 2v3.8H3.5c1.49 0 2.7 1.21 2.7 2.7s-1.21 2.7-2.7 2.7H2V20c0 1.1.9 2 2 2h3.8v-1.5c0-1.49 1.21-2.7 2.7-2.7 1.49 0 2.7 1.21 2.7 2.7V22H17c1.1 0 2-.9 2-2v-4h1.5c1.38 0 2.5-1.12 2.5-2.5S21.88 11 20.5 11z"/>
            </svg>
        }
    </div>
    
    @* Info *@
    <div class="ne-plugin-card-info">
        <div class="ne-plugin-card-header">
            <span class="ne-plugin-card-name">@Plugin.Name</span>
            <span class="ne-plugin-card-version">v@Plugin.Version</span>
        </div>
        
        @if (!string.IsNullOrEmpty(Plugin.Author))
        {
            <div class="ne-plugin-card-author">by @Plugin.Author</div>
        }
        
        @if (!string.IsNullOrEmpty(Plugin.Description))
        {
            <div class="ne-plugin-card-description">@TruncateDescription(Plugin.Description)</div>
        }
        
        <div class="ne-plugin-card-meta">
            @if (!string.IsNullOrEmpty(Plugin.Category))
            {
                <span class="ne-plugin-card-category">@Plugin.Category</span>
            }
            @if (Plugin.DownloadCount > 0)
            {
                <span class="ne-plugin-card-downloads">
                    <svg width="12" height="12" viewBox="0 0 16 16" fill="currentColor">
                        <path d="M.5 9.9a.5.5 0 0 1 .5.5v2.5a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-2.5a.5.5 0 0 1 1 0v2.5a2 2 0 0 1-2 2H2a2 2 0 0 1-2-2v-2.5a.5.5 0 0 1 .5-.5z"/>
                        <path d="M7.646 11.854a.5.5 0 0 0 .708 0l3-3a.5.5 0 0 0-.708-.708L8.5 10.293V1.5a.5.5 0 0 0-1 0v8.793L5.354 8.146a.5.5 0 1 0-.708.708l3 3z"/>
                    </svg>
                    @FormatDownloadCount(Plugin.DownloadCount)
                </span>
            }
        </div>
    </div>
    
    @* Action Button *@
    <div class="ne-plugin-card-actions" @onclick:stopPropagation="true">
        @if (IsInstalled)
        {
            @if (HasUpdate)
            {
                <button class="ne-plugin-card-btn ne-plugin-card-btn--update"
                        @onclick="OnUpdateClick"
                        title="Update available">
                    Update
                </button>
            }
            else
            {
                <span class="ne-plugin-card-installed-badge">
                    <svg width="14" height="14" viewBox="0 0 16 16" fill="currentColor">
                        <path d="M12.736 3.97a.733.733 0 0 1 1.047 0c.286.289.29.756.01 1.05L7.88 12.01a.733.733 0 0 1-1.065.02L3.217 8.384a.757.757 0 0 1 0-1.06.733.733 0 0 1 1.047 0l3.052 3.093 5.4-6.425a.247.247 0 0 1 .02-.022z"/>
                    </svg>
                    Installed
                </span>
            }
        }
        else
        {
            <button class="ne-plugin-card-btn ne-plugin-card-btn--install"
                    @onclick="OnInstallClick">
                Install
            </button>
        }
    </div>
</div>

@code {
    [Parameter, EditorRequired]
    public required MarketplacePluginInfo Plugin { get; set; }
    
    [Parameter] public bool IsSelected { get; set; }
    [Parameter] public bool IsInstalled { get; set; }
    [Parameter] public string? InstalledVersion { get; set; }
    
    [Parameter] public EventCallback OnSelect { get; set; }
    [Parameter] public EventCallback OnInstall { get; set; }
    [Parameter] public EventCallback OnUninstall { get; set; }
    
    private bool HasUpdate => IsInstalled && !string.IsNullOrEmpty(InstalledVersion) &&
        Version.TryParse(InstalledVersion, out var installed) &&
        Version.TryParse(Plugin.Version, out var available) &&
        available > installed;
    
    private async Task OnCardClick()
    {
        await OnSelect.InvokeAsync();
    }
    
    private async Task OnInstallClick()
    {
        await OnInstall.InvokeAsync();
    }
    
    private async Task OnUpdateClick()
    {
        await OnInstall.InvokeAsync();
    }
    
    private static string TruncateDescription(string description, int maxLength = 100)
    {
        if (description.Length <= maxLength) return description;
        return description[..(maxLength - 3)] + "...";
    }
    
    private static string FormatDownloadCount(int count)
    {
        return count switch
        {
            >= 1_000_000 => $"{count / 1_000_000.0:0.#}M",
            >= 1_000 => $"{count / 1_000.0:0.#}K",
            _ => count.ToString()
        };
    }
}
```

---

## 5. Plugin Details Panel

### 5.1 PluginDetailsPanel.razor

**File:** `NodeEditor.Blazor/Components/Marketplace/PluginDetailsPanel.razor`

```razor
@namespace NodeEditor.Blazor.Components.Marketplace
@using NodeEditor.Blazor.Services.Plugins.Marketplace.Models

<div class="ne-plugin-details">
    @* Header *@
    <div class="ne-plugin-details-header">
        <div class="ne-plugin-details-icon">
            @if (!string.IsNullOrEmpty(Plugin.IconUrl))
            {
                <img src="@Plugin.IconUrl" alt="@Plugin.Name" />
            }
            else
            {
                <svg width="48" height="48" viewBox="0 0 24 24" fill="currentColor">
                    <path d="M20.5 11H19V7c0-1.1-.9-2-2-2h-4V3.5C13 2.12 11.88 1 10.5 1S8 2.12 8 3.5V5H4c-1.1 0-1.99.9-1.99 2v3.8H3.5c1.49 0 2.7 1.21 2.7 2.7s-1.21 2.7-2.7 2.7H2V20c0 1.1.9 2 2 2h3.8v-1.5c0-1.49 1.21-2.7 2.7-2.7 1.49 0 2.7 1.21 2.7 2.7V22H17c1.1 0 2-.9 2-2v-4h1.5c1.38 0 2.5-1.12 2.5-2.5S21.88 11 20.5 11z"/>
                </svg>
            }
        </div>
        
        <div class="ne-plugin-details-title-section">
            <h3 class="ne-plugin-details-name">@Plugin.Name</h3>
            <div class="ne-plugin-details-meta">
                <span class="ne-plugin-details-version">v@Plugin.Version</span>
                @if (!string.IsNullOrEmpty(Plugin.Author))
                {
                    <span class="ne-plugin-details-author">by @Plugin.Author</span>
                }
            </div>
        </div>
    </div>
    
    @* Action Buttons *@
    <div class="ne-plugin-details-actions">
        @if (IsInstalled)
        {
            @if (HasUpdate)
            {
                <button class="ne-plugin-details-btn ne-plugin-details-btn--primary"
                        disabled="@IsInstalling"
                        @onclick="OnUpdate">
                    @if (IsInstalling)
                    {
                        <span class="ne-plugin-details-spinner"></span>
                        <span>Updating...</span>
                    }
                    else
                    {
                        <span>Update to v@Plugin.Version</span>
                    }
                </button>
            }
            
            <button class="ne-plugin-details-btn ne-plugin-details-btn--danger"
                    disabled="@IsUninstalling"
                    @onclick="OnUninstall">
                @if (IsUninstalling)
                {
                    <span class="ne-plugin-details-spinner"></span>
                    <span>Uninstalling...</span>
                }
                else
                {
                    <span>Uninstall</span>
                }
            </button>
            
            @if (InstalledInfo is not null)
            {
                <div class="ne-plugin-details-installed-info">
                    <span>Installed: v@InstalledInfo.Version</span>
                    @if (InstalledInfo.InstalledAt != default)
                    {
                        <span>on @InstalledInfo.InstalledAt.ToString("MMM d, yyyy")</span>
                    }
                </div>
            }
        }
        else
        {
            <button class="ne-plugin-details-btn ne-plugin-details-btn--primary"
                    disabled="@IsInstalling"
                    @onclick="OnInstall">
                @if (IsInstalling)
                {
                    <span class="ne-plugin-details-spinner"></span>
                    <span>Installing...</span>
                }
                else
                {
                    <span>Install</span>
                }
            </button>
        }
    </div>
    
    @* Description *@
    @if (!string.IsNullOrEmpty(Plugin.Description))
    {
        <div class="ne-plugin-details-section">
            <h4>Description</h4>
            <p>@Plugin.Description</p>
        </div>
    }
    
    @* Long Description (Markdown) *@
    @if (!string.IsNullOrEmpty(Plugin.LongDescription))
    {
        <div class="ne-plugin-details-section ne-plugin-details-readme">
            <h4>README</h4>
            <div class="ne-plugin-details-markdown">
                @* TODO: Render markdown *@
                <pre>@Plugin.LongDescription</pre>
            </div>
        </div>
    }
    
    @* Tags *@
    @if (Plugin.Tags.Count > 0)
    {
        <div class="ne-plugin-details-section">
            <h4>Tags</h4>
            <div class="ne-plugin-details-tags">
                @foreach (var tag in Plugin.Tags)
                {
                    <span class="ne-plugin-details-tag">@tag</span>
                }
            </div>
        </div>
    }
    
    @* Info Grid *@
    <div class="ne-plugin-details-section">
        <h4>Information</h4>
        <div class="ne-plugin-details-info-grid">
            @if (!string.IsNullOrEmpty(Plugin.Category))
            {
                <div class="ne-plugin-details-info-item">
                    <span class="ne-plugin-details-info-label">Category</span>
                    <span class="ne-plugin-details-info-value">@Plugin.Category</span>
                </div>
            }
            
            @if (!string.IsNullOrEmpty(Plugin.License))
            {
                <div class="ne-plugin-details-info-item">
                    <span class="ne-plugin-details-info-label">License</span>
                    <span class="ne-plugin-details-info-value">@Plugin.License</span>
                </div>
            }
            
            @if (Plugin.PackageSizeBytes.HasValue)
            {
                <div class="ne-plugin-details-info-item">
                    <span class="ne-plugin-details-info-label">Size</span>
                    <span class="ne-plugin-details-info-value">@FormatSize(Plugin.PackageSizeBytes.Value)</span>
                </div>
            }
            
            @if (Plugin.LastUpdatedAt.HasValue)
            {
                <div class="ne-plugin-details-info-item">
                    <span class="ne-plugin-details-info-label">Last Updated</span>
                    <span class="ne-plugin-details-info-value">@Plugin.LastUpdatedAt.Value.ToString("MMM d, yyyy")</span>
                </div>
            }
            
            <div class="ne-plugin-details-info-item">
                <span class="ne-plugin-details-info-label">Min API Version</span>
                <span class="ne-plugin-details-info-value">@Plugin.MinApiVersion</span>
            </div>
            
            @if (Plugin.DownloadCount > 0)
            {
                <div class="ne-plugin-details-info-item">
                    <span class="ne-plugin-details-info-label">Downloads</span>
                    <span class="ne-plugin-details-info-value">@Plugin.DownloadCount.ToString("N0")</span>
                </div>
            }
        </div>
    </div>
    
    @* Links *@
    @if (!string.IsNullOrEmpty(Plugin.HomepageUrl) || !string.IsNullOrEmpty(Plugin.RepositoryUrl))
    {
        <div class="ne-plugin-details-section">
            <h4>Links</h4>
            <div class="ne-plugin-details-links">
                @if (!string.IsNullOrEmpty(Plugin.HomepageUrl))
                {
                    <a href="@Plugin.HomepageUrl" target="_blank" rel="noopener noreferrer"
                       class="ne-plugin-details-link">
                        <svg width="14" height="14" viewBox="0 0 16 16" fill="currentColor">
                            <path d="M0 8a8 8 0 1 1 16 0A8 8 0 0 1 0 8zm7.5-6.923c-.67.204-1.335.82-1.887 1.855A7.97 7.97 0 0 0 5.145 4H7.5V1.077zM4.09 4a9.267 9.267 0 0 1 .64-1.539 6.7 6.7 0 0 1 .597-.933A7.025 7.025 0 0 0 2.255 4H4.09zm-.582 3.5c.03-.877.138-1.718.312-2.5H1.674a6.958 6.958 0 0 0-.656 2.5h2.49z"/>
                        </svg>
                        Homepage
                    </a>
                }
                @if (!string.IsNullOrEmpty(Plugin.RepositoryUrl))
                {
                    <a href="@Plugin.RepositoryUrl" target="_blank" rel="noopener noreferrer"
                       class="ne-plugin-details-link">
                        <svg width="14" height="14" viewBox="0 0 16 16" fill="currentColor">
                            <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.012 8.012 0 0 0 16 8c0-4.42-3.58-8-8-8z"/>
                        </svg>
                        Repository
                    </a>
                }
            </div>
        </div>
    }
    
    @* Version History *@
    @if (Plugin.AvailableVersions.Count > 1)
    {
        <div class="ne-plugin-details-section">
            <h4>Version History</h4>
            <div class="ne-plugin-details-versions">
                @foreach (var version in Plugin.AvailableVersions.Take(5))
                {
                    <div class="ne-plugin-details-version-item">
                        <span class="ne-plugin-details-version-number">v@version.Version</span>
                        @if (version.ReleasedAt.HasValue)
                        {
                            <span class="ne-plugin-details-version-date">
                                @version.ReleasedAt.Value.ToString("MMM d, yyyy")
                            </span>
                        }
                        @if (!string.IsNullOrEmpty(version.ReleaseNotes))
                        {
                            <p class="ne-plugin-details-version-notes">@version.ReleaseNotes</p>
                        }
                    </div>
                }
            </div>
        </div>
    }
</div>

@code {
    [Parameter, EditorRequired]
    public required MarketplacePluginInfo Plugin { get; set; }
    
    [Parameter] public bool IsInstalled { get; set; }
    [Parameter] public InstalledPluginInfo? InstalledInfo { get; set; }
    [Parameter] public bool IsInstalling { get; set; }
    [Parameter] public bool IsUninstalling { get; set; }
    
    [Parameter] public EventCallback OnInstall { get; set; }
    [Parameter] public EventCallback OnUninstall { get; set; }
    [Parameter] public EventCallback OnUpdate { get; set; }
    
    private bool HasUpdate => IsInstalled && InstalledInfo is not null &&
        Version.TryParse(InstalledInfo.Version, out var installed) &&
        Version.TryParse(Plugin.Version, out var available) &&
        available > installed;
    
    private static string FormatSize(long bytes)
    {
        return bytes switch
        {
            >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:0.##} GB",
            >= 1_048_576 => $"{bytes / 1_048_576.0:0.##} MB",
            >= 1_024 => $"{bytes / 1_024.0:0.##} KB",
            _ => $"{bytes} B"
        };
    }
}
```

---

## 6. CSS Styles

### 6.1 plugin-manager.css

**File:** `NodeEditor.Blazor/wwwroot/css/plugin-manager.css`

```css
/* ============================================
   Plugin Manager Dialog Styles
   Following the ne- prefix convention
   ============================================ */

/* Overlay */
.ne-plugin-manager-overlay {
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.6);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 1000;
    opacity: 0;
    visibility: hidden;
    transition: opacity 0.2s ease, visibility 0.2s ease;
}

.ne-plugin-manager-overlay--visible {
    opacity: 1;
    visibility: visible;
}

/* Main Container */
.ne-plugin-manager {
    background: var(--ne-bg-primary, #1e1e1e);
    border: 1px solid var(--ne-border-color, #3c3c3c);
    border-radius: 8px;
    width: 90vw;
    max-width: 1200px;
    height: 80vh;
    max-height: 800px;
    display: flex;
    flex-direction: column;
    box-shadow: 0 8px 32px rgba(0, 0, 0, 0.4);
    overflow: hidden;
}

/* Header */
.ne-plugin-manager-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 16px 20px;
    border-bottom: 1px solid var(--ne-border-color, #3c3c3c);
    background: var(--ne-bg-secondary, #252526);
}

.ne-plugin-manager-title {
    margin: 0;
    font-size: 18px;
    font-weight: 600;
    color: var(--ne-text-primary, #cccccc);
}

.ne-plugin-manager-close {
    background: transparent;
    border: none;
    color: var(--ne-text-secondary, #808080);
    cursor: pointer;
    padding: 4px;
    border-radius: 4px;
    display: flex;
    align-items: center;
    justify-content: center;
    transition: background 0.15s ease, color 0.15s ease;
}

.ne-plugin-manager-close:hover {
    background: var(--ne-bg-hover, #2a2d2e);
    color: var(--ne-text-primary, #cccccc);
}

/* Tabs */
.ne-plugin-manager-tabs {
    display: flex;
    gap: 4px;
    padding: 8px 20px;
    background: var(--ne-bg-secondary, #252526);
    border-bottom: 1px solid var(--ne-border-color, #3c3c3c);
}

.ne-plugin-manager-tab {
    background: transparent;
    border: none;
    padding: 8px 16px;
    color: var(--ne-text-secondary, #808080);
    cursor: pointer;
    border-radius: 4px;
    font-size: 14px;
    display: flex;
    align-items: center;
    gap: 8px;
    transition: background 0.15s ease, color 0.15s ease;
}

.ne-plugin-manager-tab:hover {
    background: var(--ne-bg-hover, #2a2d2e);
    color: var(--ne-text-primary, #cccccc);
}

.ne-plugin-manager-tab--active {
    background: var(--ne-accent-color, #0e639c);
    color: white;
}

.ne-plugin-manager-tab--active:hover {
    background: var(--ne-accent-hover, #1177bb);
    color: white;
}

.ne-plugin-manager-tab-badge {
    background: var(--ne-bg-tertiary, #3c3c3c);
    color: var(--ne-text-secondary, #808080);
    padding: 2px 6px;
    border-radius: 10px;
    font-size: 11px;
    font-weight: 600;
}

.ne-plugin-manager-tab--active .ne-plugin-manager-tab-badge {
    background: rgba(255, 255, 255, 0.2);
    color: white;
}

.ne-plugin-manager-tab-badge--highlight {
    background: var(--ne-warning-color, #cca700) !important;
    color: var(--ne-bg-primary, #1e1e1e) !important;
}

/* Content Area */
.ne-plugin-manager-content {
    display: flex;
    flex: 1;
    overflow: hidden;
}

.ne-plugin-manager-list-panel {
    width: 400px;
    min-width: 300px;
    border-right: 1px solid var(--ne-border-color, #3c3c3c);
    display: flex;
    flex-direction: column;
    background: var(--ne-bg-primary, #1e1e1e);
}

.ne-plugin-manager-details-panel {
    flex: 1;
    overflow-y: auto;
    background: var(--ne-bg-secondary, #252526);
}

/* Search Bar */
.ne-plugin-search {
    padding: 12px 16px;
    border-bottom: 1px solid var(--ne-border-color, #3c3c3c);
    display: flex;
    flex-direction: column;
    gap: 8px;
}

.ne-plugin-search-input-wrapper {
    position: relative;
    display: flex;
    align-items: center;
}

.ne-plugin-search-icon {
    position: absolute;
    left: 10px;
    color: var(--ne-text-secondary, #808080);
    pointer-events: none;
}

.ne-plugin-search-input {
    width: 100%;
    padding: 8px 32px 8px 36px;
    background: var(--ne-bg-tertiary, #3c3c3c);
    border: 1px solid transparent;
    border-radius: 4px;
    color: var(--ne-text-primary, #cccccc);
    font-size: 14px;
    outline: none;
    transition: border-color 0.15s ease, background 0.15s ease;
}

.ne-plugin-search-input:focus {
    border-color: var(--ne-accent-color, #0e639c);
    background: var(--ne-bg-primary, #1e1e1e);
}

.ne-plugin-search-input::placeholder {
    color: var(--ne-text-secondary, #808080);
}

.ne-plugin-search-clear {
    position: absolute;
    right: 8px;
    background: transparent;
    border: none;
    color: var(--ne-text-secondary, #808080);
    cursor: pointer;
    padding: 4px;
    border-radius: 4px;
    display: flex;
    align-items: center;
    justify-content: center;
}

.ne-plugin-search-clear:hover {
    color: var(--ne-text-primary, #cccccc);
}

.ne-plugin-search-category {
    padding: 6px 10px;
    background: var(--ne-bg-tertiary, #3c3c3c);
    border: 1px solid transparent;
    border-radius: 4px;
    color: var(--ne-text-primary, #cccccc);
    font-size: 13px;
    cursor: pointer;
    outline: none;
}

.ne-plugin-search-category:focus {
    border-color: var(--ne-accent-color, #0e639c);
}

/* Plugin List */
.ne-plugin-manager-list {
    flex: 1;
    overflow-y: auto;
    padding: 8px;
}

.ne-plugin-manager-loading,
.ne-plugin-manager-empty {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 12px;
    padding: 40px 20px;
    color: var(--ne-text-secondary, #808080);
    text-align: center;
}

.ne-plugin-manager-spinner {
    width: 24px;
    height: 24px;
    border: 2px solid var(--ne-border-color, #3c3c3c);
    border-top-color: var(--ne-accent-color, #0e639c);
    border-radius: 50%;
    animation: ne-spin 0.8s linear infinite;
}

@keyframes ne-spin {
    to { transform: rotate(360deg); }
}

/* Plugin Card */
.ne-plugin-card {
    display: flex;
    align-items: flex-start;
    gap: 12px;
    padding: 12px;
    border-radius: 6px;
    cursor: pointer;
    transition: background 0.15s ease;
    margin-bottom: 4px;
}

.ne-plugin-card:hover {
    background: var(--ne-bg-hover, #2a2d2e);
}

.ne-plugin-card--selected {
    background: var(--ne-selection-bg, #094771);
}

.ne-plugin-card--selected:hover {
    background: var(--ne-selection-bg, #094771);
}

.ne-plugin-card-icon {
    width: 40px;
    height: 40px;
    flex-shrink: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    background: var(--ne-bg-tertiary, #3c3c3c);
    border-radius: 6px;
    color: var(--ne-text-secondary, #808080);
}

.ne-plugin-card-icon img {
    width: 100%;
    height: 100%;
    object-fit: cover;
    border-radius: 6px;
}

.ne-plugin-card-info {
    flex: 1;
    min-width: 0;
}

.ne-plugin-card-header {
    display: flex;
    align-items: baseline;
    gap: 8px;
    margin-bottom: 2px;
}

.ne-plugin-card-name {
    font-weight: 600;
    color: var(--ne-text-primary, #cccccc);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

.ne-plugin-card-version {
    font-size: 12px;
    color: var(--ne-text-secondary, #808080);
    flex-shrink: 0;
}

.ne-plugin-card-author {
    font-size: 12px;
    color: var(--ne-text-secondary, #808080);
    margin-bottom: 4px;
}

.ne-plugin-card-description {
    font-size: 13px;
    color: var(--ne-text-secondary, #9d9d9d);
    line-height: 1.4;
    margin-bottom: 6px;
}

.ne-plugin-card-meta {
    display: flex;
    align-items: center;
    gap: 10px;
}

.ne-plugin-card-category {
    font-size: 11px;
    padding: 2px 6px;
    background: var(--ne-bg-tertiary, #3c3c3c);
    border-radius: 3px;
    color: var(--ne-text-secondary, #808080);
}

.ne-plugin-card-downloads {
    display: flex;
    align-items: center;
    gap: 4px;
    font-size: 11px;
    color: var(--ne-text-secondary, #808080);
}

.ne-plugin-card-actions {
    flex-shrink: 0;
}

.ne-plugin-card-btn {
    padding: 6px 12px;
    border: none;
    border-radius: 4px;
    font-size: 12px;
    font-weight: 500;
    cursor: pointer;
    transition: background 0.15s ease;
}

.ne-plugin-card-btn--install {
    background: var(--ne-accent-color, #0e639c);
    color: white;
}

.ne-plugin-card-btn--install:hover {
    background: var(--ne-accent-hover, #1177bb);
}

.ne-plugin-card-btn--update {
    background: var(--ne-warning-color, #cca700);
    color: var(--ne-bg-primary, #1e1e1e);
}

.ne-plugin-card-btn--update:hover {
    background: #ddb800;
}

.ne-plugin-card-installed-badge {
    display: flex;
    align-items: center;
    gap: 4px;
    font-size: 12px;
    color: var(--ne-success-color, #89d185);
}

/* No Selection State */
.ne-plugin-manager-no-selection {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    height: 100%;
    gap: 16px;
    color: var(--ne-text-secondary, #808080);
}

/* Plugin Details Panel */
.ne-plugin-details {
    padding: 24px;
}

.ne-plugin-details-header {
    display: flex;
    gap: 16px;
    margin-bottom: 20px;
}

.ne-plugin-details-icon {
    width: 64px;
    height: 64px;
    flex-shrink: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    background: var(--ne-bg-tertiary, #3c3c3c);
    border-radius: 8px;
    color: var(--ne-text-secondary, #808080);
}

.ne-plugin-details-icon img {
    width: 100%;
    height: 100%;
    object-fit: cover;
    border-radius: 8px;
}

.ne-plugin-details-title-section {
    flex: 1;
}

.ne-plugin-details-name {
    margin: 0 0 4px 0;
    font-size: 22px;
    font-weight: 600;
    color: var(--ne-text-primary, #cccccc);
}

.ne-plugin-details-meta {
    display: flex;
    align-items: center;
    gap: 12px;
}

.ne-plugin-details-version {
    font-size: 14px;
    color: var(--ne-text-secondary, #808080);
}

.ne-plugin-details-author {
    font-size: 14px;
    color: var(--ne-text-secondary, #808080);
}

/* Action Buttons */
.ne-plugin-details-actions {
    display: flex;
    align-items: center;
    gap: 12px;
    margin-bottom: 24px;
    flex-wrap: wrap;
}

.ne-plugin-details-btn {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 10px 20px;
    border: none;
    border-radius: 4px;
    font-size: 14px;
    font-weight: 500;
    cursor: pointer;
    transition: background 0.15s ease, opacity 0.15s ease;
}

.ne-plugin-details-btn:disabled {
    opacity: 0.6;
    cursor: not-allowed;
}

.ne-plugin-details-btn--primary {
    background: var(--ne-accent-color, #0e639c);
    color: white;
}

.ne-plugin-details-btn--primary:hover:not(:disabled) {
    background: var(--ne-accent-hover, #1177bb);
}

.ne-plugin-details-btn--danger {
    background: transparent;
    border: 1px solid var(--ne-error-color, #f14c4c);
    color: var(--ne-error-color, #f14c4c);
}

.ne-plugin-details-btn--danger:hover:not(:disabled) {
    background: rgba(241, 76, 76, 0.1);
}

.ne-plugin-details-spinner {
    width: 14px;
    height: 14px;
    border: 2px solid rgba(255, 255, 255, 0.3);
    border-top-color: white;
    border-radius: 50%;
    animation: ne-spin 0.8s linear infinite;
}

.ne-plugin-details-btn--danger .ne-plugin-details-spinner {
    border-color: rgba(241, 76, 76, 0.3);
    border-top-color: var(--ne-error-color, #f14c4c);
}

.ne-plugin-details-installed-info {
    font-size: 13px;
    color: var(--ne-text-secondary, #808080);
    display: flex;
    gap: 8px;
}

/* Sections */
.ne-plugin-details-section {
    margin-bottom: 24px;
}

.ne-plugin-details-section h4 {
    margin: 0 0 12px 0;
    font-size: 14px;
    font-weight: 600;
    color: var(--ne-text-primary, #cccccc);
    text-transform: uppercase;
    letter-spacing: 0.5px;
}

.ne-plugin-details-section p {
    margin: 0;
    font-size: 14px;
    line-height: 1.6;
    color: var(--ne-text-secondary, #9d9d9d);
}

/* Tags */
.ne-plugin-details-tags {
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
}

.ne-plugin-details-tag {
    padding: 4px 10px;
    background: var(--ne-bg-tertiary, #3c3c3c);
    border-radius: 4px;
    font-size: 12px;
    color: var(--ne-text-secondary, #808080);
}

/* Info Grid */
.ne-plugin-details-info-grid {
    display: grid;
    grid-template-columns: repeat(2, 1fr);
    gap: 12px;
}

.ne-plugin-details-info-item {
    display: flex;
    flex-direction: column;
    gap: 2px;
}

.ne-plugin-details-info-label {
    font-size: 11px;
    color: var(--ne-text-secondary, #808080);
    text-transform: uppercase;
}

.ne-plugin-details-info-value {
    font-size: 14px;
    color: var(--ne-text-primary, #cccccc);
}

/* Links */
.ne-plugin-details-links {
    display: flex;
    flex-wrap: wrap;
    gap: 12px;
}

.ne-plugin-details-link {
    display: flex;
    align-items: center;
    gap: 6px;
    color: var(--ne-accent-color, #3794ff);
    text-decoration: none;
    font-size: 14px;
}

.ne-plugin-details-link:hover {
    text-decoration: underline;
}

/* Version History */
.ne-plugin-details-versions {
    display: flex;
    flex-direction: column;
    gap: 12px;
}

.ne-plugin-details-version-item {
    padding: 10px 12px;
    background: var(--ne-bg-tertiary, #3c3c3c);
    border-radius: 4px;
}

.ne-plugin-details-version-number {
    font-weight: 600;
    color: var(--ne-text-primary, #cccccc);
    margin-right: 8px;
}

.ne-plugin-details-version-date {
    font-size: 12px;
    color: var(--ne-text-secondary, #808080);
}

.ne-plugin-details-version-notes {
    margin: 8px 0 0 0;
    font-size: 13px;
    color: var(--ne-text-secondary, #9d9d9d);
    line-height: 1.4;
}

/* README/Markdown */
.ne-plugin-details-readme {
    max-height: 300px;
    overflow-y: auto;
}

.ne-plugin-details-markdown pre {
    margin: 0;
    font-size: 13px;
    line-height: 1.5;
    color: var(--ne-text-secondary, #9d9d9d);
    white-space: pre-wrap;
}

/* Status Bar */
.ne-plugin-manager-status {
    padding: 10px 20px;
    background: var(--ne-bg-secondary, #252526);
    border-top: 1px solid var(--ne-border-color, #3c3c3c);
    font-size: 13px;
    color: var(--ne-text-secondary, #808080);
}

.ne-plugin-manager-status--error {
    background: rgba(241, 76, 76, 0.1);
    color: var(--ne-error-color, #f14c4c);
}
```

---

## 7. Integration with Main Editor

### 7.1 Add Plugins Button to Toolbar

Update the main editor component to include a button that opens the Plugin Manager.

**Modification to:** `NodeEditor.Blazor/Components/NodeEditorCanvas.razor` (or relevant toolbar component)

```razor
@* Add this button to the toolbar area *@
<button class="ne-toolbar-btn" @onclick="OpenPluginManager" title="Plugin Manager">
    <svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor">
        <path d="M20.5 11H19V7c0-1.1-.9-2-2-2h-4V3.5C13 2.12 11.88 1 10.5 1S8 2.12 8 3.5V5H4c-1.1 0-1.99.9-1.99 2v3.8H3.5c1.49 0 2.7 1.21 2.7 2.7s-1.21 2.7-2.7 2.7H2V20c0 1.1.9 2 2 2h3.8v-1.5c0-1.49 1.21-2.7 2.7-2.7 1.49 0 2.7 1.21 2.7 2.7V22H17c1.1 0 2-.9 2-2v-4h1.5c1.38 0 2.5-1.12 2.5-2.5S21.88 11 20.5 11z"/>
    </svg>
</button>

@* Add the dialog component *@
<PluginManagerDialog @bind-IsOpen="_isPluginManagerOpen" />

@code {
    private bool _isPluginManagerOpen = false;
    
    private void OpenPluginManager()
    {
        _isPluginManagerOpen = true;
    }
}
```

### 7.2 Update _Imports.razor

**Add to:** `NodeEditor.Blazor/_Imports.razor`

```razor
@using NodeEditor.Blazor.Components.Marketplace
```

### 7.3 Include CSS

**Add to:** `NodeEditor.Blazor/wwwroot/css/bundle.css` or main stylesheet

```css
@import 'plugin-manager.css';
```

Or link directly in the HTML:

```html
<link href="_content/NodeEditor.Blazor/css/plugin-manager.css" rel="stylesheet" />
```

---

## 8. Deliverables Checklist

| File | Status |
|------|--------|
| `Components/Marketplace/PluginManagerDialog.razor` | To create |
| `Components/Marketplace/PluginSearchBar.razor` | To create |
| `Components/Marketplace/PluginCard.razor` | To create |
| `Components/Marketplace/PluginDetailsPanel.razor` | To create |
| `wwwroot/css/plugin-manager.css` | To create |
| `_Imports.razor` | To modify (add using) |
| Toolbar component | To modify (add button) |

---

## 9. Testing Strategy

1. **Manual Testing:**
   - Open Plugin Manager dialog
   - Verify search filters work
   - Test install/uninstall flow
   - Verify installed plugins appear correctly
   - Test update detection and flow

2. **UI Tests (bUnit):**
   - `PluginCardTests.cs` - Test card rendering and events
   - `PluginSearchBarTests.cs` - Test search input and filtering
   - `PluginManagerDialogTests.cs` - Test dialog open/close and tab switching

---

## Next Stage Preview

**Stage 3** will implement:
- Remote marketplace HTTP client for online repository
- Authentication integration
- Plugin publishing workflow
- Caching and offline support
- Settings UI for marketplace configuration
