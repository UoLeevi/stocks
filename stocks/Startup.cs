using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;
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
                        .SetIsOriginAllowed(origin 
                            => origin == "http://localhost"
                            || origin.StartsWith("http://localhost:"))
                        .AllowCredentials());

                options.AddPolicy(
                    "AllowLocalhostAndKpiApp",
                    builder => builder
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .SetIsOriginAllowed(origin
                            => origin == "http://localhost"
                            || origin.StartsWith("http://localhost:")
                            || origin == "https://kpi.app"
                            || origin.StartsWith("https://") && origin.EndsWith(".kpi.app"))
                        .AllowCredentials());
            });

            services.AddSignalR();
        }

        // This method gets called by the runtime.
        // Use this method to configure the HTTP request pipeline.
        public void Configure(
            IApplicationBuilder app,
            IHostingEnvironment env,
            IHubContext<StocksHub> context)
        {
            app.UseForwardedHeaders(
                new ForwardedHeadersOptions
                {
                    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                });
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseCors("AllowLocalhost");
            }
            else
            {
                app.UseCors("AllowLocalhostAndKpiApp");
                //app.UseCors("AllowKpiApp");
            }
            app.UseSignalR(routes => routes.MapHub<StocksHub>("/hubs/stocks"));

            StocksHub.ReceiveIntradayQuotesJson += 
                (tickerSymbol, intradayQuotesJson) => context.Clients.Group(tickerSymbol).SendAsync(
                    nameof(StocksHub.ReceiveIntradayQuotesJson),
                    tickerSymbol,
                    intradayQuotesJson);

            StocksHub.ReceiveQuotes +=
                (tickerSymbol, quotes) => context.Clients.Group(tickerSymbol).SendAsync(
                    nameof(StocksHub.ReceiveQuotes),
                    tickerSymbol,
                    quotes.ToArray());
        }
    }
}
