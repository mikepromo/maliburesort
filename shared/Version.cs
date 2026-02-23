using System.Reflection;

public static class Version
{
	public static string VersionOf(Assembly assembly) =>
		assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0.0";
}