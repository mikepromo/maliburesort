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

		if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
		{
			Serr("[SYS] SESSION EXPIRED. PLEASE RE-AUTHENTICATE.");
			await ClearJWT();
			ReconcileURL(true);
			return;
		}
		
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