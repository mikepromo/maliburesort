using client.Razor;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace client;

public class Program
{
	public static async Task Main(string[] args)
	{
		WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
		builder.RootComponents.Add<App>("#app");
		builder.RootComponents.Add<HeadOutlet>("head::after");

		if (builder.HostEnvironment.IsDevelopment())
		{
			builder.Services.AddScoped(sp =>
				new HttpClient
				{
					BaseAddress = new Uri("http://localhost:5078")
				});
		}
		else
		{
			builder.Services.AddScoped(sp =>
				new HttpClient
				{
					BaseAddress = new Uri("https://todo.trycloudflare.com")
				});
		}

		builder.Services.AddScoped<AppState>();

		WebAssemblyHost host = builder.Build();

		AppState appState = host.Services.GetRequiredService<AppState>();
		await appState.LaunchAsync();

		await host.RunAsync();
	}
}