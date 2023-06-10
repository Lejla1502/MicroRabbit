using MicroRabbit.Banking.Data.Context;
using MicroRabbit.Infra.IoC;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace MicroRabbit.Banking.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddDbContext<BankingDbContext>(option =>
        {
            option.UseSqlServer(builder.Configuration.GetConnectionString("BankingDbConnection"));
        });

        //builder.Services.AddMvcCore();
        //builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddMvc(c=>c.EnableEndpointRouting = false);

        builder.Services.AddSwaggerGen();

        builder.Services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<Program>());
        RegisterServices(builder.Services);
        //builder.Services.AddControllers();

        // Add services to the container.
        builder.Services.AddRazorPages();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        app.UseSwagger();

        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("../swagger/v1/swagger.json", "Banking Microservice v1");
        });

        app.UseMvc();
        

        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthorization();

        //app.MapRazorPages();

        app.Run();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        DependencyContainer.ResgisterService(services);
    }
}