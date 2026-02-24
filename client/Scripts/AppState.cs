using Microsoft.AspNetCore.Components;
using shared;

public partial class AppState(HttpClient http, NavigationManager nav)
{
	public string? Jwt { get; private set; }
	public PlayerDto? Player { get; private set; }
	public event Action? OnChange;

	public string? ServerVersion { get; private set; }
	public bool IsLoggedIn => !string.IsNullOrEmpty(Jwt);

	void Dirty()
	{
		OnChange?.Invoke();
	}
}