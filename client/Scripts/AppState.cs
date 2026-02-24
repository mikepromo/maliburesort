using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using shared;

public partial class AppState(HttpClient http, NavigationManager nav, IJSRuntime jsRuntime)
{
	public string? Jwt { get; private set; }
	public PlayerDto? Player { get; private set; }
	public string? ServerVersion { get; private set; }
	
	public event Action? OnChange;
	
	public bool IsLoggedIn => !string.IsNullOrEmpty(Jwt);

	public async Task LaunchAsync()
	{
		string? savedJwt = await GetJwt();

		return;

		if (!string.IsNullOrEmpty(savedJwt))
		{
			Cinf("RESTORING SESSION...");

			await SetJWT(savedJwt);

			await OnLoginSuccessful();
		}
	}

	async Task OnLoginSuccessful()
	{
		await CheckBalance();

		if (IsLoggedIn && Player != null)
		{
			await ConnectHub();
				
			if (!string.IsNullOrEmpty(Player.CurrentTableId))
				nav.NavigateTo($"/game/{Player.CurrentTableId}");
			else
				nav.NavigateTo("/lobby");
		}
	}

	async Task<string?> GetJwt()
	{
		return await jsRuntime.InvokeAsync<string?>("storageGet", "malibu_jwt");
	}

	async Task SetJWT(string jwt)
	{
		Jwt = jwt;
		http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Jwt);

		await jsRuntime.InvokeVoidAsync("storageSet", "malibu_jwt", Jwt);
	}

	public async Task ClearJWT()
	{
		Jwt = null;
		Player = null;
		http.DefaultRequestHeaders.Authorization = null;
		await jsRuntime.InvokeVoidAsync("storageRemove", "malibu_jwt");
	}

	void Dirty()
	{
		OnChange?.Invoke();
	}
}