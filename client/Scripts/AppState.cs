using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

public partial class AppState(HttpClient http, NavigationManager nav, IJSRuntime jsRuntime)
{
	public string? ServerVersion { get; private set; }
	public string? ClientVersion { get; private set; }

	public event Action? OnChange;

	public async Task LaunchAsync()
	{
		await GetVersions();
		await RestoreSession();
	}

	public void ReconcileURL(bool force = false)
	{
		string target;

		if (!IsLoggedIn)
			target = "/";
		else if (IsInGame)
			target = $"/game/{Player!.CurrentTableId}";
		else
			target = "/lobby";

		if (force || nav.Uri != nav.BaseUri + target.TrimStart('/'))
		{
			nav.NavigateTo(target);
		}
	}

	void Dirty()
	{
		OnChange?.Invoke();
	}
}