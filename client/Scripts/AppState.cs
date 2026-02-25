using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

public partial class AppState(HttpClient http, NavigationManager nav, IJSRuntime jsRuntime)
{
	public string? ServerVersion { get; private set; }
	public string? ClientVersion { get; private set; }

	public event Action? OnChange;

	public async Task LaunchAsync()
	{
		await GetServerVersion();
		await RestoreSession();
	}

	void Dirty()
	{
		OnChange?.Invoke();
	}
}