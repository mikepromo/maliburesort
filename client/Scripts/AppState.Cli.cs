using shared;

partial class AppState
{
	public bool IsHelpMenuOpen { get; private set; }

	public void CloseHelp()
	{
		IsHelpMenuOpen = false;
		Dirty();
	}

	public async Task DispatchCommand(string cliText)
	{
		if (string.IsNullOrWhiteSpace(cliText)) return;

		string[] parts = cliText.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

		string cmd = parts[0].ToUpperInvariant();
		string[] args = parts.Skip(1).ToArray();

		switch (cmd)
		{
			case "HELP":
			case "?":
				IsHelpMenuOpen = true;
				Dirty();
				break;


			case "LOGOUT":
			case "SHUTDOWN":
			case "SD":
				await Logout();
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




			case "JOIN":
			case "J":
				if (LobbyContext?.Tables != null && args.Length == 1)
				{
					TableDto? table = null;
					string target = args[0];

					if (int.TryParse(target, out int index) &&
					    index > 0 && index <= LobbyContext.Tables.Count)
					{
						table = LobbyContext.Tables[index - 1];
					}

					if (table != null)
					{
						await JoinTable(table.Id);
					}
				}
				break;
			case "LOBBY":
			case "HOME":
			case "EXIT":
			case "Q":
				await LeaveTable();
				break;




			case "CHAT":
			case "MSG":
			case "C":
				if (args.Length > 0)
				{
					string message = string.Join(" ", args);
					await SendInChat(message);
				}
				break;




			default:
				Cerr("COMMAND UNRECOGNIZED");
				break;
		}
	}
}