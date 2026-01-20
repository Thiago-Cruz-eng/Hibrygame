using System.Text;
using System.Text.Json.Serialization;
using AspNetCore.Identity.MongoDbCore.Extensions;
using AspNetCore.Identity.MongoDbCore.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Owin.Builder;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.Infra.Mongo;
using Orchestrator.Infra.Repositories;
using Orchestrator.Infra.Settings;
using Orchestrator.Infra.SignalR;
using Orchestrator.UseCases;
using Orchestrator.UseCases.Interfaces;
using Owin;


var builder = WebApplication.CreateBuilder(args);

BsonSerializer.RegisterSerializer(new GuidSerializer(MongoDB.Bson.BsonType.String));
BsonSerializer.RegisterSerializer(new DateTimeSerializer(MongoDB.Bson.BsonType.String));
BsonSerializer.RegisterSerializer(new DateTimeOffsetSerializer(MongoDB.Bson.BsonType.String));

var mongoDbIdentityConfig = new MongoDbIdentityConfiguration
{
    MongoDbSettings = new MongoDbSettings
    {
        ConnectionString = "mongodb://localhost:27017",
        DatabaseName = "Hibrygame"
    },
    IdentityOptionsAction = options =>
    {
        options.Password.RequireDigit = false;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireLowercase = false;
        
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
        options.Lockout.MaxFailedAccessAttempts = 5;

        options.User.RequireUniqueEmail = true;
    }
};

builder.Services.ConfigureMongoDbIdentity<User, Roles, Guid>(mongoDbIdentityConfig)
    .AddUserManager<UserManager<User>>()
    .AddSignInManager<SignInManager<User>>()
    .AddRoleManager<RoleManager<Roles>>()
    .AddDefaultTokenProviders();

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("Jwt settings are missing.");

builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = true;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateLifetime = true,
        ValidateAudience = true,
        ValidateIssuer = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddControllers().AddJsonOptions(x =>
    x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve);;
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactDevelopment",
        builder =>
        {
            builder.WithOrigins("http://localhost:3000")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
});

builder.Services.AddSignalR();

builder.Services.Configure<HibrygameDatabaseSettings>(
    builder.Configuration.GetSection("HibrygameDatabase"));

builder.Services.AddScoped<CreateUserUseCase>();
builder.Services.AddScoped<GetUserUseCase>();
builder.Services.AddScoped<LoginAsyncUseCase>();
builder.Services.AddScoped<CreateRoleUseCase>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IValidationService, ValidationService>();
builder.Services.AddScoped<IUserRepositoryNoSql, UserRepositoryNoNoSql>();
builder.Services.AddScoped<IValidationRepositoryNoSql, ValidationRepositoryNoSql>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowReactDevelopment");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller}/{action}/{id?}");
app.MapHub<ChessHub>("/chesshub");

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
