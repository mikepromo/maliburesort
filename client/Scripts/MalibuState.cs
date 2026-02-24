using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using shared;

public partial class MalibuState
{
	readonly HttpClient _http;
	readonly NavigationManager _nav;

	public string? Jwt { get; private set; }
	public PlayerDto? Player { get; private set; }
	public event Action? OnChange;

	public string? ServerVersion { get; private set; }
	public bool IsLoggedIn => !string.IsNullOrEmpty(Jwt);

	public MalibuState(HttpClient http, NavigationManager nav)
	{
		_http = http;
		_nav = nav;
	}

	public async Task LoadServerVersionAsync()
	{
		VersionResponse? response = await _http.GetFromJsonAsync<VersionResponse>("/version");
		ServerVersion = response?.Version;
	}

	public async Task Login(string name, string pass)
	{
		Cinf("VERIFYING CREDENTIALS...");

		try
		{
			HttpResponseMessage res = await _http.PostAsJsonAsync("/auth/login", new { Name = name, Pass = pass });
			if (res.IsSuccessStatusCode)
			{
				JWTResponse? data = await res.Content.ReadFromJsonAsync<JWTResponse>();
				Jwt = data!.JWT;
				Player = data.Player;
				_http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Jwt);

				await ConnectHub();
				Notify();
				
				if (!string.IsNullOrEmpty(Player.CurrentTableId))
				{
					Cinf($"SESSION RECOVERED. ROUTING TO TABLE {Player.CurrentTableId}...");
					_nav.NavigateTo($"/game/{Player.CurrentTableId}");
				}
				else
				{
					Cinf("SUCCESSFUL AUTHENTICATION.");
					_nav.NavigateTo("/lobby");
				}
			}
			else
			{
				await Chttp(res);
			}
		}
		catch (Exception ex)
		{
			Cex(ex);
		}
	}

	public async Task<bool> Register(string name, string pass)
	{
		Cinf("PROCESSING REGISTRATION...");

		try
		{
			PlayerCredentials payload = new(name, pass);
			HttpResponseMessage res = await _http.PostAsJsonAsync("/auth/register", payload);

			if (res.IsSuccessStatusCode)
			{
				Cinf("IDENTITY CREATED. PLEASE AUTHENTICATE");
				return true;
			}

			await Chttp(res);
			return false;
		}
		catch (Exception ex)
		{
			Cex(ex);
			return false;
		}
	}

	void Notify()
	{
		OnChange?.Invoke();
	}
}