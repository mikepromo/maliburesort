using System.Net.Http.Headers;
using Microsoft.JSInterop;

public partial class AppState
{
	public string? Jwt { get; private set; }
	public bool IsLoggedIn => !string.IsNullOrEmpty(Jwt);

	public async Task RestoreSession()
	{
		string? savedJwt = await GetJwt();

		if (!string.IsNullOrEmpty(savedJwt))
		{
			Cinf("RESTORING SESSION...");

			await SetJWT(savedJwt);
			await SyncPlayer();

			if (Player != null)
			{
				await CheckBalance();
				await ConnectHub();
			}
		}
	}

	async Task<string?> GetJwt()
	{
		try
		{
			return await jsRuntime.InvokeAsync<string?>("storageGet", "malibu_jwt");
		}
		catch (Exception ex)
		{
			Cex(ex);
			return null;
		}
	}

	async Task SetJWT(string jwt)
	{
		Jwt = jwt;
		http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Jwt);

		try
		{
			await jsRuntime.InvokeVoidAsync("storageSet", "malibu_jwt", Jwt);
		}
		catch (Exception ex)
		{
			Cex(ex);
		}
	}

	public async Task ClearJWT()
	{
		Jwt = null;
		Player = null;
		http.DefaultRequestHeaders.Authorization = null;

		try
		{
			await jsRuntime.InvokeVoidAsync("storageRemove", "malibu_jwt");
		}
		catch (Exception ex)
		{
			Cex(ex);
		}
	}
}