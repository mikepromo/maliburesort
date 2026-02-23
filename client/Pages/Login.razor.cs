using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using shared;

namespace client.Pages;

public partial class Login
{
	protected bool isRegisterMode = false;
	protected string name = "";
	protected string pass = "";
	protected string passConfirm = "";

	protected ElementReference nameInput;
	protected ElementReference passInput;
	protected ElementReference confirmInput;

	protected void ToggleMode()
	{
		isRegisterMode = !isRegisterMode;
		name = "";
		pass = "";
		passConfirm = "";

		_ = FocusName();
	}

	protected async Task ExecuteConfirm()
	{
		if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(pass))
		{
			State.Cerr("INPUT REQUIRED");
			return;
		}

		string? nameError = Validation.IsValidName(name);
		if (nameError != null)
		{
			State.Cerr(nameError);
			return;
		}

		string? passError = Validation.IsValidPass(pass);
		if (passError != null)
		{
			State.Cerr(passError);
			return;
		}

		if (isRegisterMode)
		{
			if (pass != passConfirm)
			{
				State.Cerr("PASSWORD MISMATCH");
				return;
			}

			if (await State.Register(name, pass))
				ToggleMode();
		}
		else
		{
			await State.Login(name, pass);
		}
	}

	protected async Task HandleNameKey(KeyboardEventArgs e)
	{
		if (e.Key == "Enter") await passInput.FocusAsync();
	}

	protected async Task HandlePassKey(KeyboardEventArgs e)
	{
		if (e.Key == "Enter")
		{
			if (isRegisterMode) await confirmInput.FocusAsync();
			else await ExecuteConfirm();
		}
	}

	protected async Task HandleConfirmKey(KeyboardEventArgs e)
	{
		if (e.Key == "Enter") await ExecuteConfirm();
	}

	protected void HandleToggleKey(KeyboardEventArgs e)
	{
		if (e.Key == "Enter") ToggleMode();
	}

	protected async Task ResetFocus()
	{
		await FocusName();
	}

	async Task FocusName()
	{
		await nameInput.FocusAsync();
	}
}