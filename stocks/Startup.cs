using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using stocks.Hubs;

namespace stocks
{
    public class Startup
    {
        // This method gets called by the runtime.
        // Use this method to add services to the container.
        public void ConfigureServices(
            IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddPolicy(
                  "AllowKpiApp",
                  builder => builder
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .WithOrigins("https://kpi.app")
                      .SetIsOriginAllowedToAllowWildcardSubdomains()
                      .AllowCredentials());

                options.AddPolicy(
                    "AllowLocalhost",
                    builder => builder
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .WithOrigins("http://localhost")
                        .SetIsOriginAllowed(origin 
                            => origin == "http://localhost"
                            || origin.StartsWith("http://localhost:"))
                        .AllowCredentials());
            });

            services.AddSignalR();
        }

        // This method gets called by the runtime.
        // Use this method to configure the HTTP request pipeline.
        public void Configure(
            IApplicationBuilder app,
            IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseCors("AllowLocalhost");
            }
            else
            {
                app.UseCors("AllowKpiApp");
            }

            app.UseSignalR(routes => routes.MapHub<StocksHub>("/hubs/stocks"));
        }
    }
}
