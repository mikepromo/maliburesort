using System.Net.Http.Json;
using shared;

public enum OutputType
{
	ClientInfo,
	ClientError,
	ClientException,
	ServerError
}

partial class MalibuState
{
	public event Func<string, string[], Task>? OnCliInput;
	public string LatestOutput { get; private set; } = "SYSTEM READY";
	public OutputType OutputType { get; private set; }

	public event Action? OnFocusCliRequested;

	public void RequestCliFocus()
	{
		OnFocusCliRequested?.Invoke();
	}

	public string OutputCssClass => OutputType switch
	{
		OutputType.ClientInfo      => "text-green",
		OutputType.ClientError     => "text-amber",
		OutputType.ServerError     => "text-red",
		OutputType.ClientException => "text-blue",
		_                          => "text-amber"
	};

	public async Task DispatchCommand(string cliText)
	{
		if (string.IsNullOrWhiteSpace(cliText)) return;

		string[] parts = cliText.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

		string cmd = parts[0].ToUpperInvariant();
		string[] args = parts.Skip(1).ToArray();

		switch (cmd)
		{
			case "LOBBY":
			case "HOME":
			case "EXIT":
				_nav.NavigateTo("/lobby");
				break;
			case "LOGOUT":
				Jwt = null;
				Player = null;
				_nav.NavigateTo("/");
				break;
			case "RELOAD":
			case "REFRESH":
				_nav.NavigateTo(_nav.Uri, true);
				break;
			case "DEPOSIT":
				;
				break;
			case "WITHDRAW":
				;
				break;
			case "BALANCE":
				;
				break;
			default:
				if (OnCliInput != null)
					await OnCliInput.Invoke(cmd, args);
				break;
		}
	}

	public void Cinf(string message)
	{
		PrintLine(message, OutputType.ClientInfo);
	}

	public void Cerr(string message)
	{
		PrintLine(message, OutputType.ClientError);
	}

	public void Cex(Exception ex)
	{
		PrintLine(ex.Message, OutputType.ClientException);
	}

	public void Serr(string message)
	{
		PrintLine(message, OutputType.ServerError);
	}

	public async Task Chttp(HttpResponseMessage response)
	{
		if (response.IsSuccessStatusCode) return;

		try
		{
			ErrorResponse? err = await response.Content.ReadFromJsonAsync<ErrorResponse>();
			if (err == null) throw new Exception();
			Serr(err.Code != null
				? $"[{err.Code}] :: {err.Message}"
				: err.Message);
		}
		catch
		{
			//; fallback if server let us down and didn't return our DTO
			Serr($"[HTTP] :: {(int)response.StatusCode}");
		}
	}

	void PrintLine(string message, OutputType type)
	{
		LatestOutput = message.ToUpper();
		OutputType = type;
		Notify();
	}
}