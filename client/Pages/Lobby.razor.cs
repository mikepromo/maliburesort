using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using shared;

namespace client.Pages;

public partial class Lobby
{
	List<TableDto>? tables;
	protected ElementReference lobbyContainer;

	protected override async Task OnInitializedAsync()
	{
		State.OnConsoleCommand += HandleConsoleCommand;
		tables = await Http.GetFromJsonAsync<List<TableDto>>("/tables");
	}

	//; the input works only within the Lobby page, as it should
	void HandleConsoleCommand(string cmd, string[] args)
	{
		if (cmd == "JOIN" && args.Length > 0 && tables != null)
		{
			TableDto? table = null;
			string target = args[0];

			if (int.TryParse(target, out int index) &&
			    index > 0 && index <= tables.Count)
			{
				table = tables[index - 1];
			}

			if (table != null)
			{
				Join(table.Id);
			}
		}
	}

	void Join(string id)
	{
		Nav.NavigateTo($"/game/{id}");
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (firstRender)
		{
			await lobbyContainer.FocusAsync();
		}
	}

	protected void HandleContainerKey(KeyboardEventArgs e)
	{
		if (e.Key == "Enter")
		{
			State.RequestCmdFocus();
		}
	}

	public void Dispose()
	{
		State.OnConsoleCommand -= HandleConsoleCommand;
	}
}