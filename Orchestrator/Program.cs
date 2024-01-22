using System.Text.Json.Serialization;
using Orchestrator.Infra.Interfaces;
using Orchestrator.Infra.Mongo;
using Orchestrator.Infra.Repositories;
using Orchestrator.UseCases;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(x =>
    x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve);;
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<HibrygameDatabaseSettings>(
    builder.Configuration.GetSection("HibrygameDatabase"));

builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
builder.Services.AddScoped<CreateUserService>();
builder.Services.AddScoped<IUserRepositoryNoSql, UserRepositoryNoNoSql>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();


