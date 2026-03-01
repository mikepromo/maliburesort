
Console.WriteLine("Hello, World!");

// using Microsoft.AspNetCore.Hosting;
// using Microsoft.AspNetCore.Mvc.Testing;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.DependencyInjection;
// using Microsoft.Extensions.Logging;
//
// public class MalibuFactory : WebApplicationFactory<Program>
// {
// 	protected override void ConfigureWebHost(IWebHostBuilder builder)
// 	{
// 		builder.UseSetting("ConnectionStrings:DefaultConnection",
// 			"Host=localhost;Database=maliburesort_test_db;Username=postgres;");
//
// 		builder.ConfigureLogging(logging => { logging.ClearProviders(); });
//
// 		builder.ConfigureServices(services =>
// 		{
// 			//; flush test_db to test afresh
// 			ServiceProvider sp = services.BuildServiceProvider();
// 			using (IServiceScope scope = sp.CreateScope())
// 			{
// 				MainDbContext db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
// 				db.Database.EnsureDeleted();
// 				db.Database.Migrate();
// 			}
// 		
// 			//; remove SpinService to simulate manually
// 			ServiceDescriptor? spinDescriptor =
// 				services.FirstOrDefault(d => d.ImplementationType == typeof(SpinService));
// 			if (spinDescriptor != null)
// 				services.Remove(spinDescriptor);
// 		});
// 	}
// }