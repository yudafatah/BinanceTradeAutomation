using Binance.Trade.Automation.Contracts;
using Binance.Trade.Automation.Entities;
using Binance.Trade.Automation.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using System.IO;

namespace Binance.Trade.Automation
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            services.AddCors(c => c.AddPolicy("defaultPolicy",
                b => b.AllowAnyOrigin()
                    .AllowAnyHeader()
                    // .WithHeaders("Authorization", "X-Moduit-Token", "content-type")
                    .AllowAnyMethod()
                    //.WithExposedHeaders("Authorization", "X-Moduit-Token")
            ));

            services.AddControllers(o => o.OutputFormatters.RemoveType<StringOutputFormatter>())
                .AddNewtonsoftJson(opt =>
                {
                    opt.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
                    opt.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                });

            services.Configure<GzipCompressionProviderOptions>(options =>
            {
                options.Level = System.IO.Compression.CompressionLevel.Optimal;
            });

            ConfigureDependencies(services);
            
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("specs", new OpenApiInfo { Title = "Binance Trade Automation Doc", Version = "v1",
                    Description = @"
<b>Welcome to Binance Trade Automation Doc!</b> <br /> <br />
", Contact = new OpenApiContact{Email = "yudafatah@gmail.com", Name = "For any API related inquiry, you can send us email at"}
                });

                // c.EnableAnnotations();
                c.CustomSchemaIds(type => type.ToString());
                c.OrderActionsBy(d => d.GroupName);

                var filePath = Path.Combine(System.AppContext.BaseDirectory, "Binance.Trade.Automation.xml");
                c.IncludeXmlComments(filePath, true);

            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            app.UseRouting();

            app.UseAuthorization();
            
            // let's exclude some path...
            //app.Use(async (context, next) =>
            //{
            //    if (env.IsProduction() && context.Request.Path.Value != null && context.Request.Path.Value.Contains("simulation"))
            //    {
            //        context.Response.StatusCode = 404;
            //        return;
            //    }

            //    await next.Invoke();
            //});

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
            
           

            //if (env.IsProduction()) return;

            app.UseSwagger();
//#else
//app.UseSwagger(c =>
//            {
//                c.PreSerializeFilters.Add((swagger, httpReq) =>
//                {
//                    swagger.Servers = new List<OpenApiServer>
//                    {
//                        new OpenApiServer { Url = $"https://api-ext-dev.moduit.id/v1" }
//                    };
//                });
//            });
//#endif

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("specs/swagger.json", "Binance Trade Automation API");
                c.DefaultModelExpandDepth(-1);
                //c.DocumentTitle = "Documentation";
                //c.DocExpansion(DocExpansion.None);
            });

            app.UseReDoc(c =>
            {
                c.RequiredPropsFirst();
                c.DocumentTitle = "Moduit External API";
//#if DEBUG
                c.SpecUrl = "/swagger/specs/swagger.json";
//#else
//c.SpecUrl = "https://api-ext-dev.moduit.id/v1/swagger/specs/swagger.json";
//#endif
                c.RoutePrefix = "docs";
                c.PathInMiddlePanel();
                c.ExpandResponses("200, 201");

            });

        }

        public void ConfigureDependencies(IServiceCollection services)
        {
            services.AddSingleton<IOrderService, OrderService>();
            
            var credConfig = Configuration.GetSection("BinanceCredential");
            services.Configure<BinanceCredential>(credConfig);
            
        }
    }
}