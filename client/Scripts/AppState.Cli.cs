using System.Text.Json;
using shared;

public enum OutputType
{
	ClientInfo,
	ClientError,
	ClientException,
	ServerError
}

partial class AppState
{
	public string LatestOutput { get; private set; } = "SYSTEM READY";
	public OutputType OutputType { get; private set; }

	public event Func<string, string[], Task>? OnCliInput;

	public async Task DispatchCommand(string cliText)
	{
		if (string.IsNullOrWhiteSpace(cliText)) return;

		string[] parts = cliText.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

		string cmd = parts[0].ToUpperInvariant();
		string[] args = parts.Skip(1).ToArray();

		switch (cmd)
		{
			case "LOGOUT":
			case "SD":
				await Logout();
				break;
			case "RELOAD":
			case "REFRESH":
				nav.NavigateTo(nav.Uri, true);
				break;
			case "BET":
			case "B":
				if (args.Length == 2)
				{
					if (int.TryParse(args[0], out int nmb) && decimal.TryParse(args[1], out decimal amt))
					{
						await PlaceBet(nmb, amt);
					}
				}
				else
				{
					Cerr("INVALID BET ARGS. USAGE: BET <0-36> <AMT>");
				}
				break;
			case "DEPOSIT":
			case "DP":
				if (args.Length == 1)
				{
					if (decimal.TryParse(args[0], out decimal amt))
					{
						await Deposit(amt);
					}
				}
				else
				{
					Cerr("INVALID DEPOSIT ARGS. USAGE: DEPOSIT <AMT>");
				}
				break;
			case "WITHDRAW":
			case "WD":
				if (args.Length == 1)
				{
					if (decimal.TryParse(args[0], out decimal amt))
					{
						await Withdraw(amt);
					}
				}
				else
				{
					Cerr("INVALID WITHDRAW ARGS. USAGE: WITHDRAW <AMT>");
				}
				break;
			case "BALANCE":
			case "BAL":
				await CheckBalance();
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

		string content = await response.Content.ReadAsStringAsync();

		try
		{
			ErrorResponse? err = JsonSerializer.Deserialize<ErrorResponse>(content,
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

			if (err?.Message != null)
			{
				Serr(err.Code != null ? $"[{err.Code}] :: {err.Message}" : err.Message);
				return;
			}
		}
		catch
		{
			//empty
		}

		string statusInfo = $"{(int)response.StatusCode} {response.StatusCode.ToString().ToUpper()}";

		string summary = content.Length > 150 ? content[..150].Replace("\n", " ") + "..." : content;

		Serr($"[HTTP {statusInfo}] :: {summary}");
	}

	void PrintLine(string message, OutputType type)
	{
		Console.WriteLine($"{type}: {message}");
		LatestOutput = message.ToUpper();
		OutputType = type;
		Dirty();
	}
}