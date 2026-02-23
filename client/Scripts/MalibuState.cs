using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using shared;

public partial class MalibuState
{
	readonly HttpClient _http;
	readonly NavigationManager _nav;
	HubConnection? _hub;

	public string? Jwt { get; private set; }
	public PlayerDTO? Player { get; private set; }
	public event Action? OnChange;

	public HubConnection Hub => _hub!;
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
				_nav.NavigateTo("/lobby");
				Cinf("SUCCESSFUL AUTHENTICATION");
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

	async Task ConnectHub()
	{
		_hub = new HubConnectionBuilder()
			.WithUrl($"{_http.BaseAddress}hubs/game?access_token={Jwt}")
			.WithAutomaticReconnect()
			.Build();

		_hub.On<decimal>(RPC.BalanceUpdate, bal =>
		{
			Player!.Balance = bal;
			Notify();
		});

		await _hub.StartAsync();
	}

	void Notify()
	{
		OnChange?.Invoke();
	}
}