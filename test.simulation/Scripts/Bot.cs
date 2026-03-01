using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using shared;

public class Bot
{
    readonly HttpClient _http;
    readonly Random _rng = new();
    string? _currentTableId;
    decimal _lastBalance;

    const decimal TARGET_BALANCE = 1_000_000m;
    const decimal DEPOSIT_AMOUNT = 1_000_000m;
    const double CHECK_BALANCE_PROB = 0.05;
    const double WITHDRAW_PROB = 0.2;
    const double SWITCH_TABLE_PROB = 0.02;
    const decimal MIN_WITHDRAW = 1_000m;

    public Bot(string baseAddress) => _http = new HttpClient { BaseAddress = new Uri(baseAddress) };

    public async Task Boot(string username, string password)
    {
        // Register – 409 (already exists) is treated as success, no log
        HttpResponseMessage regRes = await _http.PostAsJsonAsync("/auth/register", new PlayerCredentials(username, password));
        if (regRes.StatusCode != HttpStatusCode.Conflict && !await Step(regRes)) return;

        HttpResponseMessage loginRes = await _http.PostAsJsonAsync("/auth/login", new PlayerCredentials(username, password));
        if (!await Step(loginRes)) return;

        JWTResponse? auth = await loginRes.Content.ReadFromJsonAsync<JWTResponse>();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.JWT);

        PlayerDto? me = await GetPlayerInfo();
        if (me == null) return;

        _currentTableId = me.CurrentTableId;
        await CheckAndDeposit();

        if (_currentTableId == null) { if (!await JoinRandomTable()) return; }
        else Console.WriteLine($"[OK] {username} already at {_currentTableId}");

        Console.WriteLine($"[OK] {username} active");

        while (true)
        {
            if (_rng.NextDouble() < CHECK_BALANCE_PROB) await CheckAndManageBalance();
            if (_currentTableId != null && _rng.NextDouble() < SWITCH_TABLE_PROB) await TrySwitchTable();
            if (_currentTableId != null && _rng.NextDouble() > 0.15) await Bet(_currentTableId);
            await Task.Delay(_rng.Next(5000, 12000));
        }
    }

    async Task<bool> Step(HttpResponseMessage res)
    {
        if (res.IsSuccessStatusCode) return true;
        string body = await res.Content.ReadAsStringAsync();
        string url = res.RequestMessage?.RequestUri?.ToString() ?? "unknown";
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[FAIL] {url} | {res.StatusCode} | {body}");
        Console.ForegroundColor = originalColor;
        return false;
    }

    async Task Bet(string tableId)
    {
        await Task.Delay(_rng.Next(1000, 5000));
        int number = _rng.Next(0, 37);
        decimal amount = _rng.Next(100, 500);
        await Step(await _http.PostAsJsonAsync($"/tables/{tableId}/bet", new PlaceBetRequest(number, amount)));

        if (_rng.NextDouble() > 0.8)
        {
            Console.WriteLine($"Betting on {number}!");
            await Step(await _http.PostAsJsonAsync($"/tables/{tableId}/chat",
                new SendChatRequest($"Betting USD {amount} on {number}!")));
        }
    }

    async Task<PlayerDto?> GetPlayerInfo()
    {
        HttpResponseMessage res = await _http.GetAsync("/players/me");
        return await Step(res) ? await res.Content.ReadFromJsonAsync<PlayerDto>() : null;
    }

    async Task<decimal> GetBalance()
    {
        HttpResponseMessage res = await _http.GetAsync("/players/balance");
        return await Step(res) ? (await res.Content.ReadFromJsonAsync<TxValue>())?.Value ?? 0 : 0;
    }

    async Task<bool> JoinRandomTable()
    {
        HttpResponseMessage tablesRes = await _http.GetAsync("/tables");
        if (!await Step(tablesRes)) return false;

        List<TableDto>? tables = await tablesRes.Content.ReadFromJsonAsync<List<TableDto>>();
        if (tables == null || tables.Count == 0) { Console.WriteLine("[ERR] No tables found."); return false; }

        foreach (TableDto table in tables.OrderBy(_ => _rng.Next()))
        {
            HttpResponseMessage res = await _http.PostAsJsonAsync($"/tables/{table.Id}/join", new { });
            if (res.IsSuccessStatusCode) { _currentTableId = table.Id; Console.WriteLine($"[OK] Joined {table.Name}");
            {
	            return true;
            } }
            Console.WriteLine($"[INFO] Could not join {table.Name}: {res.StatusCode}");
        }
        Console.WriteLine("[WARN] Failed to join any table.");
        return false;
    }

    async Task CheckAndDeposit()
    {
        _lastBalance = await GetBalance();
        if (_lastBalance >= TARGET_BALANCE) return;
        Console.WriteLine($"[INFO] Balance {_lastBalance} below {TARGET_BALANCE}, depositing...");
        await Step(await _http.PostAsJsonAsync("/players/deposit", new TxValue(DEPOSIT_AMOUNT)));
        _lastBalance = await GetBalance();
    }

    async Task CheckAndManageBalance()
    {
        decimal balance = await GetBalance();
        _lastBalance = balance;

        if (balance < TARGET_BALANCE)
        {
            Console.WriteLine($"[INFO] Balance {balance} below {TARGET_BALANCE}, depositing...");
            await Step(await _http.PostAsJsonAsync("/players/deposit", new TxValue(DEPOSIT_AMOUNT)));
        }
        else if (_rng.NextDouble() < WITHDRAW_PROB)
        {
            decimal maxWithdraw = Math.Min(balance * 0.2m, balance - MIN_WITHDRAW);
            if (maxWithdraw < MIN_WITHDRAW) return;
            decimal amount = _rng.Next((int)MIN_WITHDRAW, (int)maxWithdraw + 1);
            Console.WriteLine($"[INFO] Withdrawing {amount}...");
            await Step(await _http.PostAsJsonAsync("/players/withdraw", new TxValue(amount)));
        }
    }

    async Task TrySwitchTable()
    {
        if (_currentTableId == null) return;
        if (!await Step(await _http.PostAsJsonAsync($"/tables/{_currentTableId}/leave", new { }))) return;
        _currentTableId = null;
        Console.WriteLine("[INFO] Left table, looking for a new one...");
        await JoinRandomTable();
    }
}